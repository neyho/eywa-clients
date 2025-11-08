using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EywaClient.Core;
using EywaClient.GraphQL;
using EywaClient.Utilities;

namespace EywaClient.Files;

/// <summary>
/// File manager using dynamic JSON hashmap approach - no more JsonElement hell!
/// Provides hashmap-like access to file operations just like Clojure maps.
/// 
/// This is the "building block" approach - transparent APIs that mirror GraphQL directly.
/// For convenience helpers, build on top of this class.
/// </summary>
public class FileManager
{
    private readonly SimpleGraphQLClient _client;

    /// <summary>
    /// Initializes a new instance of the FileManager class.
    /// </summary>
    /// <param name="client">Simple GraphQL client for dynamic JSON operations</param>
    public FileManager(SimpleGraphQLClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Alternative constructor using JsonRpcClient directly.
    /// </summary>
    /// <param name="rpcClient">JSON-RPC client for EYWA communication</param>
    public FileManager(JsonRpcClient rpcClient)
    {
        _client = new SimpleGraphQLClient(rpcClient ?? throw new ArgumentNullException(nameof(rpcClient)));
    }

    /// <summary>
    /// Gets file information by UUID - returns dynamic JSON for easy access.
    /// </summary>
    /// <param name="fileUuid">UUID of the file</param>
    /// <returns>Dynamic file object or null if not found</returns>
    /// <example>
    /// <code>
    /// // Clean, simple access - just like Clojure!
    /// var file = await fileManager.GetFile(fileUuid);
    /// if (file != null)
    /// {
    ///     Console.WriteLine($"Name: {file.name}");
    ///     Console.WriteLine($"Size: {file.size} bytes");
    ///     Console.WriteLine($"Folder: {file.folder?.path ?? "Root"}");
    ///     Console.WriteLine($"Status: {file.status}");
    /// }
    /// </code>
    /// </example>
    public async Task<dynamic?> GetFile(Guid fileUuid)
    {
        const string query = @"
        query GetFile($uuid: UUID!) {
            getFile(euuid: $uuid) {
                euuid
                name
                status
                content_type
                size
                uploaded_at
                uploaded_by {
                  name
                }
                folder {
                    euuid
                    name
                    path
                }
            }
        }";

        var response = await _client.GraphQL(query, new { uuid = fileUuid });
        return response.data.getFile;
    }

    /// <summary>
    /// Lists files with optional filtering - returns array of dynamic objects.
    /// </summary>
    /// <param name="folderUuid">Optional folder UUID to filter by</param>
    /// <param name="status">Optional status filter (e.g., "READY", "PENDING")</param>
    /// <param name="namePattern">Optional name pattern for searching</param>
    /// <param name="limit">Optional limit on results</param>
    /// <returns>Array of dynamic file objects</returns>
    /// <example>
    /// <code>
    /// // List all files in a folder
    /// var files = await fileManager.ListFiles(folderUuid: folderId, limit: 10);
    /// foreach (var file in files)
    /// {
    ///     Console.WriteLine($"{file.name}: {file.status} ({file.size} bytes)");
    ///     if (file.folder != null)
    ///         Console.WriteLine($"  In folder: {file.folder.path}");
    /// }
    /// 
    /// // Search for PDF files
    /// var pdfs = await fileManager.ListFiles(namePattern: "%.pdf", status: "READY");
    /// </code>
    /// </example>
    public async Task<dynamic> ListFiles(
        Guid? folderUuid = null, 
        string? status = null,
        string? namePattern = null,
        int? limit = null)
    {
        const string query = @"
        query SearchFiles($where: searchFileOperator, $limit: Int) {
            searchFile(_where: $where, _limit: $limit) {
                euuid
                name
                status
                content_type
                size
                uploaded_at
                uploaded_by {
                  name
                }
                folder {
                    euuid
                    name
                    path
                }
            }
        }";

        // Build where clause dynamically - just like the existing approach
        var whereConditions = new List<object>();

        if (!string.IsNullOrEmpty(status))
        {
            whereConditions.Add(new { status = new { _eq = status } });
        }

        if (!string.IsNullOrEmpty(namePattern))
        {
            whereConditions.Add(new { name = new { _ilike = namePattern } });
        }

        if (folderUuid.HasValue)
        {
            whereConditions.Add(new { 
                folder = new { 
                    euuid = new { _eq = folderUuid.Value.ToString() } 
                } 
            });
        }

        // Build variables object - only include _where if we have conditions
        object? variables = null;
        if (whereConditions.Count > 0 && limit.HasValue)
        {
            variables = new
            {
                where = whereConditions.Count == 1 
                    ? whereConditions[0] 
                    : new { _and = whereConditions },
                limit = limit
            };
        }
        else if (whereConditions.Count > 0)
        {
            variables = new
            {
                where = whereConditions.Count == 1 
                    ? whereConditions[0] 
                    : new { _and = whereConditions }
            };
        }
        else if (limit.HasValue)
        {
            variables = new { limit = limit };
        }

        var response = await _client.GraphQL(query, variables);
        return response.data.searchFile ?? new dynamic[0];
    }

    /// <summary>
    /// Finds a file by name in a specific folder.
    /// Convenience method that combines search and filtering.
    /// </summary>
    /// <param name="fileName">Exact file name to search for</param>
    /// <param name="folderUuid">Folder to search in (optional, searches all if null)</param>
    /// <returns>First matching file or null if not found</returns>
    /// <example>
    /// <code>
    /// // Find specific file
    /// var file = await fileManager.FindFileByName("document.pdf", folderId);
    /// if (file != null)
    /// {
    ///     Console.WriteLine($"Found: {file.name} (UUID: {file.euuid})");
    /// }
    /// </code>
    /// </example>
    public async Task<dynamic?> FindFileByName(string fileName, Guid? folderUuid = null)
    {
        var files = await ListFiles(
            folderUuid: folderUuid, 
            namePattern: null, // Use exact name matching in the query
            status: null,
            limit: 1
        );

        // Find exact name match since GraphQL pattern matching might be different
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].name == fileName)
                return files[i];
        }

        return null;
    }

    /// <summary>
    /// Deletes a file by UUID.
    /// </summary>
    /// <param name="fileUuid">UUID of the file to delete</param>
    /// <returns>True if deletion was successful</returns>
    /// <example>
    /// <code>
    /// var success = await fileManager.DeleteFile(fileUuid);
    /// if (success)
    ///     Console.WriteLine("File deleted successfully");
    /// </code>
    /// </example>
    public async Task<bool> DeleteFile(Guid fileUuid)
    {
        const string mutation = @"
        mutation DeleteFile($uuid: UUID!) {
            deleteFile(euuid: $uuid)
        }";

        var response = await _client.GraphQL(mutation, new { uuid = fileUuid });
        return response.data.deleteFile == true;
    }

    /// <summary>
    /// Gets file download URL for direct download access.
    /// Use this for streaming downloads or when you need the URL directly.
    /// </summary>
    /// <param name="fileUuid">UUID of the file</param>
    /// <returns>Dynamic object containing download URL and metadata</returns>
    /// <example>
    /// <code>
    /// var downloadInfo = await fileManager.GetDownloadUrl(fileUuid);
    /// Console.WriteLine($"Download URL: {downloadInfo.url}");
    /// Console.WriteLine($"Expires: {downloadInfo.expires_at}");
    /// </code>
    /// </example>
    public async Task<dynamic?> GetDownloadUrl(Guid fileUuid)
    {
        const string query = @"
        query GetFileDownloadUrl($file: FileInput!) {
            requestDownloadURL(file: $file)
        }";

        var response = await _client.GraphQL(query, new { file = new { euuid = fileUuid } });
        
        // requestDownloadURL returns a simple string URL like requestUploadURL
        var downloadUrl = response.data.requestDownloadURL;
        
        // Return it in the expected format for compatibility using ExpandoObject
        dynamic result = new System.Dynamic.ExpandoObject();
        result.url = downloadUrl;
        result.expires_at = (string?)null; // Not provided by this endpoint
        return result;
    }

    /// <summary>
    /// Requests upload URL for creating new files.
    /// This is step 1 of the 3-step EYWA upload process.
    /// </summary>
    /// <param name="fileData">File metadata including name, content_type, etc.</param>
    /// <returns>Upload URL information for S3 upload</returns>
    /// <example>
    /// <code>
    /// var uploadRequest = await fileManager.RequestUploadUrl(new {
    ///     name = "document.pdf",
    ///     content_type = "application/pdf",
    ///     size = fileSize,
    ///     folder = new { euuid = folderId }, // Optional
    ///     euuid = Guid.NewGuid() // Optional - for deduplication
    /// });
    /// 
    /// // Use uploadRequest.url for S3 upload
    /// Console.WriteLine($"Upload to: {uploadRequest.url}");
    /// </code>
    /// </example>
    public async Task<dynamic?> RequestUploadUrl(object fileData)
    {
        const string mutation = @"
        mutation RequestFileUpload($file: FileInput!) {
            requestUploadURL(file: $file) {
                url
                euuid
                fields
                expires_at
            }
        }";

        var response = await _client.GraphQL(mutation, new { file = fileData });
        return response.data.requestUploadURL;
    }

    /// <summary>
    /// Confirms file upload completion - step 3 of upload process.
    /// Call this after successfully uploading to S3.
    /// </summary>
    /// <param name="fileUuid">UUID of the uploaded file</param>
    /// <returns>Confirmed file information</returns>
    /// <example>
    /// <code>
    /// // After S3 upload completes
    /// var file = await fileManager.ConfirmUpload(fileUuid);
    /// Console.WriteLine($"Upload confirmed: {file.name} (Status: {file.status})");
    /// </code>
    /// </example>
    public async Task<dynamic?> ConfirmUpload(Guid fileUuid)
    {
        const string mutation = @"
        mutation ConfirmFileUpload($uuid: UUID!) {
            confirmFileUpload(euuid: $uuid) {
                euuid
                name
                status
                content_type
                size
                uploaded_at
                folder {
                    euuid
                    name
                    path
                }
            }
        }";

        var response = await _client.GraphQL(mutation, new { uuid = fileUuid });
        return response.data.confirmFileUpload;
    }

    /// <summary>
    /// Updates file metadata (name, folder, etc.).
    /// Uses sync mutation for updating existing files.
    /// </summary>
    /// <param name="fileUuid">UUID of the file to update</param>
    /// <param name="updateData">New metadata values</param>
    /// <returns>Updated file information</returns>
    /// <example>
    /// <code>
    /// var updated = await fileManager.UpdateFile(fileUuid, new {
    ///     name = "new-document-name.pdf",
    ///     folder = new { euuid = newFolderId }
    /// });
    /// Console.WriteLine($"Updated: {updated.name}");
    /// </code>
    /// </example>
    public async Task<dynamic?> UpdateFile(Guid fileUuid, object updateData)
    {
        const string mutation = @"
        mutation UpdateFile($data: FileInput!) {
            syncFile(data: $data) {
                euuid
                name
                status
                content_type
                size
                uploaded_at
                folder {
                    euuid
                    name
                    path
                }
            }
        }";

        // Merge UUID with update data
        var data = new Dictionary<string, object>
        {
            { "euuid", fileUuid }
        };

        // Add update fields (using reflection to convert anonymous object)
        foreach (var prop in updateData.GetType().GetProperties())
        {
            data[prop.Name] = prop.GetValue(updateData) ?? "";
        }

        var response = await _client.GraphQL(mutation, new { data });
        return response.data.syncFile;
    }
}