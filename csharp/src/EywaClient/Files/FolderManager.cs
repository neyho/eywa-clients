using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EywaClient.Exceptions;
using EywaClient.GraphQL;

namespace EywaClient.Files;

/// <summary>
/// Manages folder operations in EYWA file service.
/// Provides methods to create, query, list, and delete folders.
/// </summary>
public class FolderManager
{
    private readonly GraphQLClient _graphqlClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderManager"/> class.
    /// </summary>
    /// <param name="graphqlClient">The GraphQL client for EYWA API calls</param>
    public FolderManager(GraphQLClient graphqlClient)
    {
        _graphqlClient = graphqlClient ?? throw new ArgumentNullException(nameof(graphqlClient));
    }

    /// <summary>
    /// Creates a new folder in EYWA file service using the stack mutation.
    /// 
    /// The folder data dictionary should contain:
    /// - "name": string (required) - folder name
    /// - "euuid": Guid or string (optional) - custom UUID for the folder
    /// - "parent": object with { "euuid": Guid } (optional) - parent folder reference
    /// 
    /// If parent is not provided, folder is created in root.
    /// If euuid is not provided, EYWA generates a new one.
    /// </summary>
    /// <param name="folderData">Folder metadata dictionary</param>
    /// <returns>Created folder information</returns>
    /// <exception cref="ArgumentNullException">Thrown when folderData is null</exception>
    /// <exception cref="ArgumentException">Thrown when name is missing</exception>
    /// <exception cref="FolderOperationException">Thrown when folder creation fails</exception>
    /// <example>
    /// <code>
    /// // Create folder in root
    /// var folderData = new Dictionary&lt;string, object&gt;
    /// {
    ///     { "name", "my-folder" }
    /// };
    /// var folder = await folderManager.CreateFolderAsync(folderData);
    /// 
    /// // Create subfolder with custom UUID
    /// var subfolderData = new Dictionary&lt;string, object&gt;
    /// {
    ///     { "name", "subfolder" },
    ///     { "euuid", Guid.NewGuid() },
    ///     { "parent", new Dictionary&lt;string, object&gt; { { "euuid", parentFolderUuid } } }
    /// };
    /// var subfolder = await folderManager.CreateFolderAsync(subfolderData);
    /// 
    /// // Create folder in root using constant
    /// var rootFolderData = new Dictionary&lt;string, object&gt;
    /// {
    ///     { "name", "reports" },
    ///     { "parent", FileConstants.RootFolder }
    /// };
    /// var reports = await folderManager.CreateFolderAsync(rootFolderData);
    /// </code>
    /// </example>
    public async Task<Dictionary<string, object>> CreateFolderAsync(Dictionary<string, object> folderData)
    {
        if (folderData == null)
            throw new ArgumentNullException(nameof(folderData));

        if (!folderData.ContainsKey("name"))
            throw new ArgumentException("folderData must contain 'name' field", nameof(folderData));

        const string mutation = @"
            mutation CreateFolder($folder: FolderInput!) {
                stackFolder(data: $folder) {
                    euuid
                    name
                    path
                    modified_on
                    parent {
                        euuid
                        name
                    }
                }
            }";

        try
        {
            var response = await _graphqlClient.ExecuteAsync(
                mutation,
                new { folder = folderData }
            ).ConfigureAwait(false);

            // Extract folder data from JsonElement response
            var data = response.Data;
            var folderResult = data.GetProperty("stackFolder");
            
            // Convert to dictionary
            return new Dictionary<string, object>
            {
                { "euuid", Guid.Parse(folderResult.GetProperty("euuid").GetString()!) },
                { "name", folderResult.GetProperty("name").GetString()! },
                { "path", folderResult.GetProperty("path").GetString()! },
                { "modified_on", folderResult.GetProperty("modified_on").GetString()! }
            };
        }
        catch (Exception ex)
        {
            throw new FolderOperationException($"Failed to create folder: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets folder information by path.
    /// Use this to find folder UUIDs before uploading files to specific folders.
    /// 
    /// The root folder always exists at path "/" with UUID 87ce50d8-5dfa-4008-a265-053e727ab793
    /// </summary>
    /// <param name="path">Folder path (e.g., "/", "/reports", "/reports/archive")</param>
    /// <returns>Folder information dictionary or null if not found</returns>
    /// <example>
    /// <code>
    /// // Get root folder
    /// var root = await folderManager.GetFolderByPathAsync("/");
    /// 
    /// // Get specific folder
    /// var reports = await folderManager.GetFolderByPathAsync("/reports");
    /// if (reports != null)
    /// {
    ///     var folderUuid = (Guid)reports["euuid"];
    ///     // Use folderUuid for file uploads
    /// }
    /// </code>
    /// </example>
    public async Task<Dictionary<string, object>?> GetFolderByPathAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        const string query = @"
            query GetFolder($path: String!) {
                getFolder(path: $path) {
                    euuid
                    name
                    path
                    modified_on
                    parent {
                        euuid
                        name
                    }
                }
            }";

        try
        {
            var response = await _graphqlClient.ExecuteAsync(
                query,
                new { path }
            ).ConfigureAwait(false);

            var data = response.Data;
            if (!data.TryGetProperty("getFolder", out var folderElement) || 
                folderElement.ValueKind == System.Text.Json.JsonValueKind.Null)
                return null;

            var result = new Dictionary<string, object>
            {
                { "euuid", Guid.Parse(folderElement.GetProperty("euuid").GetString()!) },
                { "name", folderElement.GetProperty("name").GetString()! },
                { "path", folderElement.GetProperty("path").GetString()! },
                { "modified_on", folderElement.GetProperty("modified_on").GetString()! }
            };

            if (folderElement.TryGetProperty("parent", out var parentElement) && 
                parentElement.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                result["parent"] = new Dictionary<string, object>
                {
                    { "euuid", Guid.Parse(parentElement.GetProperty("euuid").GetString()!) },
                    { "name", parentElement.GetProperty("name").GetString()! }
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new FolderOperationException($"Failed to get folder by path '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets folder information by UUID.
    /// </summary>
    /// <param name="folderUuid">UUID of the folder</param>
    /// <returns>Folder information dictionary or null if not found</returns>
    /// <example>
    /// <code>
    /// var folder = await folderManager.GetFolderAsync(folderUuid);
    /// if (folder != null)
    /// {
    ///     Console.WriteLine($"Folder: {folder["name"]} at {folder["path"]}");
    /// }
    /// </code>
    /// </example>
    public async Task<Dictionary<string, object>?> GetFolderAsync(Guid folderUuid)
    {
        const string query = @"
            query GetFolder($uuid: UUID!) {
                getFolder(euuid: $uuid) {
                    euuid
                    name
                    path
                    modified_on
                    parent {
                        euuid
                        name
                    }
                }
            }";

        try
        {
            var response = await _graphqlClient.ExecuteAsync(
                query,
                new { uuid = folderUuid }
            ).ConfigureAwait(false);

            var data = response.Data;
            if (!data.TryGetProperty("getFolder", out var folderElement) || 
                folderElement.ValueKind == System.Text.Json.JsonValueKind.Null)
                return null;

            var result = new Dictionary<string, object>
            {
                { "euuid", Guid.Parse(folderElement.GetProperty("euuid").GetString()!) },
                { "name", folderElement.GetProperty("name").GetString()! },
                { "path", folderElement.GetProperty("path").GetString()! },
                { "modified_on", folderElement.GetProperty("modified_on").GetString()! }
            };

            if (folderElement.TryGetProperty("parent", out var parentElement) && 
                parentElement.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                result["parent"] = new Dictionary<string, object>
                {
                    { "euuid", Guid.Parse(parentElement.GetProperty("euuid").GetString()!) },
                    { "name", parentElement.GetProperty("name").GetString()! }
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new FolderOperationException($"Failed to get folder {folderUuid}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists folders with optional filtering.
    /// </summary>
    /// <param name="limit">Maximum number of folders to return (optional)</param>
    /// <param name="namePattern">Filter by name pattern using SQL LIKE syntax (optional)</param>
    /// <param name="parentFolderUuid">Filter by parent folder UUID (optional, null for root-level folders)</param>
    /// <returns>List of folder information dictionaries</returns>
    /// <example>
    /// <code>
    /// // List all folders
    /// var allFolders = await folderManager.ListFoldersAsync();
    /// 
    /// // List folders with name containing "report"
    /// var reportFolders = await folderManager.ListFoldersAsync(namePattern: "report");
    /// 
    /// // List subfolders of a specific folder
    /// var subfolders = await folderManager.ListFoldersAsync(parentFolderUuid: parentUuid);
    /// 
    /// // List root-level folders only
    /// var rootFolders = await folderManager.ListFoldersAsync(parentFolderUuid: Guid.Empty);
    /// </code>
    /// </example>
    public async Task<List<Dictionary<string, object>>> ListFoldersAsync(
        int? limit = null,
        string? namePattern = null,
        Guid? parentFolderUuid = null)
    {
        const string query = @"
            query ListFolders($limit: Int, $where: FolderWhereInput) {
                searchFolder(_limit: $limit, _where: $where, _order_by: {name: asc}) {
                    euuid
                    name
                    path
                    modified_on
                    parent {
                        euuid
                        name
                    }
                }
            }";

        var whereConditions = new List<object>();

        if (!string.IsNullOrEmpty(namePattern))
        {
            whereConditions.Add(new Dictionary<string, object>
            {
                { "name", new Dictionary<string, object> { { "_ilike", $"%{namePattern}%" } } }
            });
        }

        if (parentFolderUuid.HasValue)
        {
            if (parentFolderUuid.Value == Guid.Empty)
            {
                // Filter for root-level folders (parent is null)
                whereConditions.Add(new Dictionary<string, object>
                {
                    { "parent", new Dictionary<string, object> { { "_is_null", true } } }
                });
            }
            else
            {
                whereConditions.Add(new Dictionary<string, object>
                {
                    { "parent", new Dictionary<string, object> 
                        { 
                            { "euuid", new Dictionary<string, object> { { "_eq", parentFolderUuid.Value } } } 
                        } 
                    }
                });
            }
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
            var response = await _graphqlClient.ExecuteAsync(
                query,
                variables
            ).ConfigureAwait(false);

            var folders = new List<Dictionary<string, object>>();
            var data = response.Data;
            var searchFolderArray = data.GetProperty("searchFolder");

            foreach (var folderElement in searchFolderArray.EnumerateArray())
            {
                var folderDict = new Dictionary<string, object>
                {
                    { "euuid", Guid.Parse(folderElement.GetProperty("euuid").GetString()!) },
                    { "name", folderElement.GetProperty("name").GetString()! },
                    { "path", folderElement.GetProperty("path").GetString()! },
                    { "modified_on", folderElement.GetProperty("modified_on").GetString()! }
                };

                if (folderElement.TryGetProperty("parent", out var parentElement) && 
                    parentElement.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    folderDict["parent"] = new Dictionary<string, object>
                    {
                        { "euuid", Guid.Parse(parentElement.GetProperty("euuid").GetString()!) },
                        { "name", parentElement.GetProperty("name").GetString()! }
                    };
                }

                folders.Add(folderDict);
            }

            return folders;
        }
        catch (Exception ex)
        {
            throw new FolderOperationException($"Failed to list folders: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a folder from EYWA file service.
    /// Note: Folder must be empty (no files or subfolders) to be deleted.
    /// </summary>
    /// <param name="folderUuid">UUID of the folder to delete</param>
    /// <returns>True if deletion successful, false otherwise</returns>
    /// <exception cref="FolderOperationException">Thrown when deletion fails</exception>
    /// <example>
    /// <code>
    /// var success = await folderManager.DeleteFolderAsync(folderUuid);
    /// if (success)
    /// {
    ///     Console.WriteLine("Folder deleted successfully");
    /// }
    /// </code>
    /// </example>
    public async Task<bool> DeleteFolderAsync(Guid folderUuid)
    {
        const string mutation = @"
            mutation DeleteFolder($uuid: UUID!) {
                deleteFolder(euuid: $uuid)
            }";

        try
        {
            var response = await _graphqlClient.ExecuteAsync(
                mutation,
                new { uuid = folderUuid }
            ).ConfigureAwait(false);

            var data = response.Data;
            return data.GetProperty("deleteFolder").GetBoolean();
        }
        catch (Exception ex)
        {
            throw new FolderOperationException($"Failed to delete folder {folderUuid}: {ex.Message}", ex);
        }
    }
}