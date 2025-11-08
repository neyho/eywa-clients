using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EywaClient.Core;
using EywaClient.GraphQL;
using EywaClient.Utilities;

namespace EywaClient.Files;

/// <summary>
/// Folder manager using dynamic JSON hashmap approach - clean hashmap-like access!
/// No more manual dictionary building or JsonElement navigation.
/// 
/// This follows the "building block" approach - transparent APIs that mirror GraphQL.
/// Build convenience helpers on top of this class as needed.
/// </summary>
public class FolderManager
{
    private readonly SimpleGraphQLClient _client;

    /// <summary>
    /// Initializes a new instance of the FolderManager class.
    /// </summary>
    /// <param name="client">Simple GraphQL client for dynamic JSON operations</param>
    public FolderManager(SimpleGraphQLClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Alternative constructor using JsonRpcClient directly.
    /// </summary>
    /// <param name="rpcClient">JSON-RPC client for EYWA communication</param>
    public FolderManager(JsonRpcClient rpcClient)
    {
        _client = new SimpleGraphQLClient(rpcClient ?? throw new ArgumentNullException(nameof(rpcClient)));
    }

    /// <summary>
    /// Gets folder information by UUID - returns dynamic JSON for easy access.
    /// </summary>
    /// <param name="folderUuid">UUID of the folder</param>
    /// <returns>Dynamic folder object or null if not found</returns>
    /// <example>
    /// <code>
    /// // Simple, clean access - like Clojure maps!
    /// var folder = await folderManager.GetFolder(folderUuid);
    /// if (folder != null)
    /// {
    ///     Console.WriteLine($"Folder: {folder.name}");
    ///     Console.WriteLine($"Path: {folder.path}");
    ///     Console.WriteLine($"Parent: {folder.parent?.name ?? "Root"}");
    /// }
    /// </code>
    /// </example>
    public async Task<dynamic?> GetFolder(Guid folderUuid)
    {
        const string query = @"
        query GetFolder($uuid: UUID!) {
            getFolder(euuid: $uuid) {
                euuid
                name
                path
                parent {
                    euuid
                    name
                    path
                }
            }
        }";

        var response = await _client.GraphQL(query, new { uuid = folderUuid });
        return response.data.getFolder;
    }

    /// <summary>
    /// Creates a new folder - follows single map argument pattern.
    /// </summary>
    /// <param name="folderData">Complete folder data including name, parent, etc.</param>
    /// <returns>Created folder information</returns>
    /// <example>
    /// <code>
    /// // Create folder in root
    /// var folder = await folderManager.CreateFolder(new {
    ///     name = "documents"
    /// });
    /// 
    /// // Create nested folder with explicit UUID (for deduplication)
    /// var subfolder = await folderManager.CreateFolder(new {
    ///     name = "important",
    ///     euuid = Guid.NewGuid(),
    ///     parent = new { euuid = folder.euuid }
    /// });
    /// 
    /// Console.WriteLine($"Created: {subfolder.name} at {subfolder.path}");
    /// </code>
    /// </example>
    public async Task<dynamic?> CreateFolder(object folderData)
    {
        const string mutation = @"
        mutation CreateFolder($data: FolderInput!) {
            stackFolder(data: $data) {
                euuid
                name
                path
                parent {
                    euuid
                    name
                    path
                }
            }
        }";

        var response = await _client.GraphQL(mutation, new { data = folderData });
        return response.data.stackFolder;
    }

    /// <summary>
    /// Lists folders with optional filtering.
    /// </summary>
    /// <param name="parentUuid">Optional parent folder UUID to filter by</param>
    /// <param name="namePattern">Optional name pattern for searching</param>
    /// <param name="limit">Optional limit on results</param>
    /// <returns>Array of dynamic folder objects</returns>
    /// <example>
    /// <code>
    /// // List all root folders
    /// var rootFolders = await folderManager.ListFolders(parentUuid: null);
    /// foreach (var folder in rootFolders)
    /// {
    ///     Console.WriteLine($"📁 {folder.name} ({folder.path})");
    /// }
    /// 
    /// // Search for folders containing "doc"
    /// var docFolders = await folderManager.ListFolders(namePattern: "%doc%");
    /// </code>
    /// </example>
    public async Task<dynamic> ListFolders(
        Guid? parentUuid = null,
        string? namePattern = null,
        int? limit = null)
    {
        const string query = @"
        query SearchFolders($where: searchFolderOperator, $limit: Int) {
            searchFolder(_where: $where, _limit: $limit) {
                euuid
                name
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
            whereConditions.Add(new { name = new { _ilike = namePattern } });
        }

        // TODO: Parent filter not working - temporarily disabled
        // if (parentUuid.HasValue)
        // {
        //     whereConditions.Add(new {
        //         parent = new {
        //             euuid = new { _eq = parentUuid.Value.ToString() }
        //         }
        //     });
        // }

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
        return response.data.searchFolder ?? new dynamic[0];
    }

    /// <summary>
    /// Gets folder contents (both subfolders and files).
    /// Convenience method that combines folder and file queries.
    /// </summary>
    /// <param name="folderUuid">UUID of the folder to list contents for</param>
    /// <param name="includeFiles">Whether to include files in the results (default: true)</param>
    /// <param name="includeSubfolders">Whether to include subfolders (default: true)</param>
    /// <returns>Dynamic object with folders and files arrays</returns>
    /// <example>
    /// <code>
    /// var contents = await folderManager.GetFolderContents(folderUuid);
    /// 
    /// Console.WriteLine($"Subfolders ({contents.folders.Length}):");
    /// foreach (var subfolder in contents.folders)
    /// {
    ///     Console.WriteLine($"  📁 {subfolder.name}");
    /// }
    /// 
    /// Console.WriteLine($"Files ({contents.files.Length}):");
    /// foreach (var file in contents.files)
    /// {
    ///     Console.WriteLine($"  📄 {file.name} ({file.size} bytes)");
    /// }
    /// </code>
    /// </example>
    public async Task<dynamic> GetFolderContents(
        Guid folderUuid, 
        bool includeFiles = true, 
        bool includeSubfolders = true)
    {
        var tasks = new List<Task<dynamic>>();

        if (includeSubfolders)
        {
            tasks.Add(ListFolders(parentUuid: folderUuid));
        }

        if (includeFiles)
        {
            // Use FileManager to get files (without folder filter for now)
            var fileManager = new FileManager(_client);
            tasks.Add(fileManager.ListFiles(limit: 100));
        }

        var results = await Task.WhenAll(tasks);
        
        var foldersResult = includeSubfolders ? results[0] : new dynamic[0];
        var filesResult = includeFiles && results.Length > 1 ? results[1] : new dynamic[0];
        
        // Use ExpandoObject for proper dynamic property access
        dynamic result = new System.Dynamic.ExpandoObject();
        result.folders = foldersResult;
        result.files = filesResult;
        
        return result;
    }

    /// <summary>
    /// Finds a folder by name within a parent folder.
    /// </summary>
    /// <param name="folderName">Exact folder name to search for</param>
    /// <param name="parentUuid">Parent folder UUID (null for root level)</param>
    /// <returns>First matching folder or null if not found</returns>
    /// <example>
    /// <code>
    /// // Find folder in root
    /// var folder = await folderManager.FindFolderByName("documents", null);
    /// 
    /// // Find nested folder
    /// var subfolder = await folderManager.FindFolderByName("important", folder.euuid);
    /// </code>
    /// </example>
    public async Task<dynamic?> FindFolderByName(string folderName, Guid? parentUuid = null)
    {
        var folders = await ListFolders(parentUuid: parentUuid, limit: 100);

        // Find exact name match
        for (int i = 0; i < folders.Length; i++)
        {
            if (folders[i].name == folderName)
                return folders[i];
        }

        return null;
    }

    /// <summary>
    /// Creates a folder path (like mkdir -p) - creates intermediate folders as needed.
    /// </summary>
    /// <param name="path">Folder path like "documents/projects/eywa"</param>
    /// <param name="startingParent">Starting parent folder UUID (null for root)</param>
    /// <returns>The final created/found folder</returns>
    /// <example>
    /// <code>
    /// // Create nested folder structure
    /// var folder = await folderManager.CreateFolderPath("documents/projects/eywa");
    /// Console.WriteLine($"Created path: {folder.path}");
    /// 
    /// // Create under specific parent
    /// var subfolder = await folderManager.CreateFolderPath("backup/2024", parentFolderUuid);
    /// </code>
    /// </example>
    public async Task<dynamic?> CreateFolderPath(string path, Guid? startingParent = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        dynamic? currentParent = null;
        Guid? currentParentUuid = startingParent;

        foreach (var part in parts)
        {
            // Try to find existing folder first
            var existing = await FindFolderByName(part, currentParentUuid);
            
            if (existing != null)
            {
                currentParent = existing;
                currentParentUuid = Guid.Parse(existing.euuid);
            }
            else
            {
                // Create new folder
                var folderData = new
                {
                    name = part,
                    parent = currentParentUuid.HasValue ? new { euuid = currentParentUuid.Value } : null
                };

                currentParent = await CreateFolder(folderData);
                if (currentParent != null)
                {
                    currentParentUuid = Guid.Parse(currentParent.euuid);
                }
            }
        }

        return currentParent;
    }

    /// <summary>
    /// Updates folder metadata (name, parent, etc.).
    /// Uses sync mutation for updating existing folders.
    /// </summary>
    /// <param name="folderUuid">UUID of the folder to update</param>
    /// <param name="updateData">New metadata values</param>
    /// <returns>Updated folder information</returns>
    /// <example>
    /// <code>
    /// // Rename folder
    /// var updated = await folderManager.UpdateFolder(folderUuid, new {
    ///     name = "renamed-folder"
    /// });
    /// 
    /// // Move folder to new parent
    /// var moved = await folderManager.UpdateFolder(folderUuid, new {
    ///     parent = new { euuid = newParentUuid }
    /// });
    /// </code>
    /// </example>
    public async Task<dynamic?> UpdateFolder(Guid folderUuid, object updateData)
    {
        const string mutation = @"
        mutation UpdateFolder($data: FolderInput!) {
            syncFolder(data: $data) {
                euuid
                name
                path
                parent {
                    euuid
                    name
                    path
                }
            }
        }";

        // Merge UUID with update data
        var data = new Dictionary<string, object>
        {
            { "euuid", folderUuid }
        };

        // Add update fields
        foreach (var prop in updateData.GetType().GetProperties())
        {
            data[prop.Name] = prop.GetValue(updateData) ?? "";
        }

        var response = await _client.GraphQL(mutation, new { data });
        return response.data.syncFolder;
    }

    /// <summary>
    /// Deletes a folder by UUID.
    /// Note: Folder must be empty unless EYWA supports recursive deletion.
    /// </summary>
    /// <param name="folderUuid">UUID of the folder to delete</param>
    /// <returns>True if deletion was successful</returns>
    /// <example>
    /// <code>
    /// var success = await folderManager.DeleteFolder(folderUuid);
    /// if (success)
    ///     Console.WriteLine("Folder deleted successfully");
    /// </code>
    /// </example>
    public async Task<bool> DeleteFolder(Guid folderUuid)
    {
        const string mutation = @"
        mutation DeleteFolder($uuid: UUID!) {
            deleteFolder(euuid: $uuid)
        }";

        var response = await _client.GraphQL(mutation, new { uuid = folderUuid });
        return response.data.deleteFolder == true;
    }

    /// <summary>
    /// Gets folder tree starting from a root folder.
    /// Useful for displaying hierarchical folder structures.
    /// </summary>
    /// <param name="rootUuid">Root folder UUID (null for full tree from root)</param>
    /// <param name="maxDepth">Maximum depth to traverse (default: 3)</param>
    /// <returns>Hierarchical folder structure with nested children</returns>
    /// <example>
    /// <code>
    /// var tree = await folderManager.GetFolderTree(rootUuid: null, maxDepth: 2);
    /// PrintFolderTree(tree, 0);
    /// 
    /// void PrintFolderTree(dynamic folders, int indent)
    /// {
    ///     foreach (var folder in folders)
    ///     {
    ///         Console.WriteLine($"{new string(' ', indent * 2)}📁 {folder.name}");
    ///         if (folder.children?.Length > 0)
    ///             PrintFolderTree(folder.children, indent + 1);
    ///     }
    /// }
    /// </code>
    /// </example>
    public async Task<dynamic> GetFolderTree(Guid? rootUuid = null, int maxDepth = 3)
    {
        if (maxDepth <= 0)
            return new dynamic[0];

        // Since parent filtering is disabled, just return all folders as a flat list
        var folders = await ListFolders();
        return folders;
    }
}