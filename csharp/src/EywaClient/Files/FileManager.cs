using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EywaClient.Exceptions;
using EywaClient.GraphQL;

namespace EywaClient.Files;

/// <summary>
/// Manages file query and deletion operations in EYWA file service.
/// Provides methods to list, search, query, and delete files.
/// 
/// For downloads, use FileDownloader.
/// For uploads, use FileUploader or FilePathUploader.
/// </summary>
public class FileManager
{
    private readonly GraphQLClient _graphqlClient;

    /// <summary>
    /// Initializes a new instance of the FileManager class.
    /// </summary>
    /// <param name="graphqlClient">GraphQL client for EYWA API</param>
    public FileManager(GraphQLClient graphqlClient)
    {
        _graphqlClient = graphqlClient ?? throw new ArgumentNullException(nameof(graphqlClient));
    }

    /// <summary>
    /// Gets information about a specific file by UUID.
    /// </summary>
    /// <param name="fileUuid">UUID of the file</param>
    /// <returns>File information dictionary or null if not found</returns>
    /// <example>
    /// <code>
    /// var fileInfo = await fileManager.GetFileInfoAsync(fileUuid);
    /// if (fileInfo != null)
    /// {
    ///     Console.WriteLine($"Name: {fileInfo["name"]}");
    ///     Console.WriteLine($"Size: {fileInfo["size"]} bytes");
    ///     Console.WriteLine($"Path: {fileInfo["path"]}");
    /// }
    /// </code>
    /// </example>
    public async Task<Dictionary<string, object>?> GetFileInfoAsync(Guid fileUuid)
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
                    folder {
                        euuid
                        name
                        path
                    }
                }
            }";

        try
        {
            var response = await _graphqlClient.ExecuteAsync<GraphQL.CommonTypes.GetFileResponse>(
                query,
                new { uuid = fileUuid }
            ).ConfigureAwait(false);

            if (response.Data?.GetFile == null)
                return null;

            var file = response.Data.GetFile;

            var result = new Dictionary<string, object>
            {
                { "euuid", Guid.Parse(file.Euuid) },
                { "name", file.Name },
                { "status", file.Status ?? "UNKNOWN" },
                { "content_type", file.ContentType },
                { "size", file.Size }
            };

            if (file.UploadedAt.HasValue)
            {
                result["uploaded_at"] = file.UploadedAt.Value;
            }

            if (file.Folder != null)
            {
                result["folder"] = new Dictionary<string, object>
                {
                    { "euuid", Guid.Parse(file.Folder.Euuid) },
                    { "name", file.Folder.Name },
                    { "path", file.Folder.Path }
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new FileOperationException($"Failed to get file info for {fileUuid}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists files with optional filtering.
    /// </summary>
    /// <param name="limit">Maximum number of files to return (optional)</param>
    /// <param name="status">Filter by status: PENDING, UPLOADED, etc. (optional)</param>
    /// <param name="namePattern">Filter by name pattern using SQL LIKE syntax (optional)</param>
    /// <param name="folderUuid">Filter by folder UUID (optional)</param>
    /// <returns>List of file information dictionaries</returns>
    /// <example>
    /// <code>
    /// // List all files
    /// var allFiles = await fileManager.ListFilesAsync();
    /// 
    /// // List files in specific folder
    /// var folderFiles = await fileManager.ListFilesAsync(folderUuid: folderUuid);
    /// 
    /// // List files matching pattern
    /// var pdfFiles = await fileManager.ListFilesAsync(namePattern: "%.pdf");
    /// 
    /// // List only uploaded files
    /// var uploadedFiles = await fileManager.ListFilesAsync(status: "UPLOADED");
    /// </code>
    /// </example>
    public async Task<List<Dictionary<string, object>>> ListFilesAsync(
        int? limit = null,
        string? status = null,
        string? namePattern = null,
        Guid? folderUuid = null)
    {
        const string query = @"
            query ListFiles($limit: Int, $where: JSON) {
                searchFile(_limit: $limit, _where: $where, _order_by: {uploaded_at: desc}) {
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

        var whereConditions = new List<object>();

        if (!string.IsNullOrEmpty(status))
        {
            whereConditions.Add(new Dictionary<string, object>
            {
                { "status", new Dictionary<string, object> { { "_eq", status } } }
            });
        }

        if (!string.IsNullOrEmpty(namePattern))
        {
            whereConditions.Add(new Dictionary<string, object>
            {
                { "name", new Dictionary<string, object> { { "_ilike", $"%{namePattern}%" } } }
            });
        }

        if (folderUuid.HasValue)
        {
            whereConditions.Add(new Dictionary<string, object>
            {
                { "folder", new Dictionary<string, object> 
                    { 
                        { "euuid", new Dictionary<string, object> { { "_eq", folderUuid.Value } } } 
                    } 
                }
            });
        }

        var variables = new Dictionary<string, object?>();
        if (limit.HasValue)
            variables["limit"] = limit.Value;

        if (whereConditions.Any())
        {
            variables["where"] = whereConditions.Count == 1
                ? whereConditions[0]
                : new Dictionary<string, object> { { "_and", whereConditions } };
        }

        try
        {
            var response = await _graphqlClient.ExecuteAsync<GraphQL.CommonTypes.SearchFileResponse>(
                query,
                variables
            ).ConfigureAwait(false);

            var files = new List<Dictionary<string, object>>();

            if (response.Data?.SearchFile != null)
            {
                foreach (var file in response.Data.SearchFile)
                {
                    var fileDict = new Dictionary<string, object>
                    {
                        { "euuid", Guid.Parse(file.Euuid) },
                        { "name", file.Name },
                        { "status", file.Status ?? "UNKNOWN" },
                        { "content_type", file.ContentType },
                        { "size", file.Size }
                    };

                    if (file.UploadedAt.HasValue)
                    {
                        fileDict["uploaded_at"] = file.UploadedAt.Value;
                    }

                    if (file.Folder != null)
                    {
                        fileDict["folder"] = new Dictionary<string, object>
                        {
                            { "euuid", Guid.Parse(file.Folder.Euuid) },
                            { "name", file.Folder.Name },
                            { "path", file.Folder.Path }
                        };
                    }

                    files.Add(fileDict);
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            throw new FileOperationException($"Failed to list files: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Searches for a file by name in a specific folder.
    /// Returns the first matching file or null if not found.
    /// </summary>
    /// <param name="fileName">Name of the file to find</param>
    /// <param name="folderUuid">UUID of the folder to search in</param>
    /// <returns>File information or null if not found</returns>
    /// <example>
    /// <code>
    /// var file = await fileManager.FindFileInFolderAsync("report.pdf", folderUuid);
    /// if (file != null)
    /// {
    ///     var fileUuid = (Guid)file["euuid"];
    ///     // Use for download, etc.
    /// }
    /// </code>
    /// </example>
    public async Task<Dictionary<string, object>?> FindFileInFolderAsync(
        string fileName,
        Guid folderUuid)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        var files = await ListFilesAsync(
            limit: 1,
            namePattern: fileName,
            folderUuid: folderUuid
        ).ConfigureAwait(false);

        return files.FirstOrDefault(f => 
            string.Equals((string)f["name"], fileName, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Deletes a file from EYWA file service.
    /// </summary>
    /// <param name="fileUuid">UUID of the file to delete</param>
    /// <returns>True if deletion successful, false otherwise</returns>
    /// <exception cref="FileOperationException">Thrown when deletion fails</exception>
    /// <example>
    /// <code>
    /// var success = await fileManager.DeleteFileAsync(fileUuid);
    /// if (success)
    /// {
    ///     Console.WriteLine("File deleted successfully");
    /// }
    /// </code>
    /// </example>
    public async Task<bool> DeleteFileAsync(Guid fileUuid)
    {
        const string mutation = @"
            mutation DeleteFile($uuid: UUID!) {
                deleteFile(euuid: $uuid)
            }";

        try
        {
            var response = await _graphqlClient.ExecuteAsync<GraphQL.CommonTypes.DeleteFileResponse>(
                mutation,
                new { uuid = fileUuid }
            ).ConfigureAwait(false);

            return response.Data?.DeleteFile ?? false;
        }
        catch (Exception ex)
        {
            throw new FileOperationException($"Failed to delete file {fileUuid}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Searches for files by path pattern.
    /// Useful for finding files in a specific path hierarchy.
    /// </summary>
    /// <param name="pathPattern">Path pattern to match (e.g., "/tmp/%", "%/reports/%")</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of matching files</returns>
    /// <example>
    /// <code>
    /// // Find all files in /tmp
    /// var tmpFiles = await fileManager.SearchFilesByPathAsync("/tmp/%");
    /// 
    /// // Find all files with 'reports' in path
    /// var reportFiles = await fileManager.SearchFilesByPathAsync("%/reports/%");
    /// </code>
    /// </example>
    public async Task<List<Dictionary<string, object>>> SearchFilesByPathAsync(
        string pathPattern,
        int? limit = null)
    {
        const string query = @"
            query SearchFiles($limit: Int, $where: JSON) {
                searchFile(_limit: $limit, _where: $where, _order_by: {path: asc}) {
                    euuid
                    name
                    status
                    content_type
                    size
                    path
                    uploaded_at
                    folder {
                        euuid
                        name
                        path
                    }
                }
            }";

        var whereCondition = new Dictionary<string, object>
        {
            { "path", new Dictionary<string, object> { { "_ilike", pathPattern } } }
        };

        var variables = new Dictionary<string, object?>
        {
            { "where", whereCondition }
        };

        if (limit.HasValue)
            variables["limit"] = limit.Value;

        try
        {
            var response = await _graphqlClient.ExecuteAsync<GraphQL.CommonTypes.SearchFileResponse>(
                query,
                variables
            ).ConfigureAwait(false);

            var files = new List<Dictionary<string, object>>();

            if (response.Data?.SearchFile != null)
            {
                foreach (var file in response.Data.SearchFile)
                {
                    var fileDict = new Dictionary<string, object>
                    {
                        { "euuid", Guid.Parse(file.Euuid) },
                        { "name", file.Name },
                        { "status", file.Status ?? "UNKNOWN" },
                        { "content_type", file.ContentType },
                        { "size", file.Size }
                    };

                    if (file.UploadedAt.HasValue)
                    {
                        fileDict["uploaded_at"] = file.UploadedAt.Value;
                    }

                    if (file.Folder != null)
                    {
                        fileDict["folder"] = new Dictionary<string, object>
                        {
                            { "euuid", Guid.Parse(file.Folder.Euuid) },
                            { "name", file.Folder.Name },
                            { "path", file.Folder.Path }
                        };
                    }

                    files.Add(fileDict);
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            throw new FileOperationException($"Failed to search files by path: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Exception thrown when file operations fail.
/// </summary>
public class FileOperationException : Exception
{
    public FileOperationException(string message) : base(message) { }
    public FileOperationException(string message, Exception innerException) 
        : base(message, innerException) { }
}