using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EywaClient.Exceptions;
using EywaClient.GraphQL;

namespace EywaClient.Files;

/// <summary>
/// High-level convenience helper for uploading files to specific paths.
/// 
/// This helper automatically:
/// - Parses target paths (e.g., "/tmp/reports/file.txt")
/// - Checks which folders exist
/// - Creates missing folders with proper parent linkage
/// - Uploads file to the final folder
/// 
/// Use this when you want simple path-based uploads.
/// For full control, use FileUploader + FolderManager directly.
/// </summary>
/// <example>
/// <code>
/// var helper = new FilePathUploader(graphqlClient);
/// 
/// // Simple upload - creates /tmp folder if needed
/// await helper.UploadToPathAsync("C:\\data\\file.txt", "/tmp/file.txt");
/// 
/// // Creates entire hierarchy if needed: /reports/2024/january
/// await helper.UploadToPathAsync("report.pdf", "/reports/2024/january/report.pdf");
/// </code>
/// </example>
public class FilePathUploader
{
    private readonly FileUploader _uploader;
    private readonly FolderManager _folderManager;
    
    // Cache folder UUIDs to avoid repeated queries
    private readonly Dictionary<string, Guid> _folderCache = new Dictionary<string, Guid>();

    /// <summary>
    /// Initializes a new instance of the FilePathUploader class.
    /// </summary>
    /// <param name="graphqlClient">GraphQL client for EYWA API</param>
    public FilePathUploader(GraphQLClient graphqlClient)
    {
        _uploader = new FileUploader(graphqlClient);
        _folderManager = new FolderManager(graphqlClient);
        
        // Pre-cache root folder
        _folderCache["/"] = FileConstants.RootFolderUuid;
    }

    /// <summary>
    /// Initializes with existing uploader and folder manager instances.
    /// Useful when you want to share instances across multiple helpers.
    /// </summary>
    public FilePathUploader(FileUploader uploader, FolderManager folderManager)
    {
        _uploader = uploader ?? throw new ArgumentNullException(nameof(uploader));
        _folderManager = folderManager ?? throw new ArgumentNullException(nameof(folderManager));
        
        // Pre-cache root folder
        _folderCache["/"] = FileConstants.RootFolderUuid;
    }

    /// <summary>
    /// Uploads a local file to a specific path in EYWA, creating folders as needed.
    /// 
    /// The target path should be a full path including filename (e.g., "/tmp/reports/file.txt").
    /// Folders will be created automatically if they don't exist.
    /// </summary>
    /// <param name="localFilePath">Local file path to upload (e.g., "C:\\data\\file.txt")</param>
    /// <param name="targetPath">Target path in EYWA (e.g., "/tmp/reports/file.txt")</param>
    /// <param name="fileUuid">Optional custom UUID for the file (for deduplication/replacement)</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <returns>Dictionary with file UUID and folder path</returns>
    /// <exception cref="ArgumentException">Thrown when paths are invalid</exception>
    /// <exception cref="FileNotFoundException">Thrown when local file doesn't exist</exception>
    /// <example>
    /// <code>
    /// // Upload to root
    /// await helper.UploadToPathAsync("data.txt", "/data.txt");
    /// 
    /// // Upload with folder creation
    /// await helper.UploadToPathAsync("report.pdf", "/reports/2024/january/report.pdf");
    /// 
    /// // Upload with custom UUID for replacement
    /// var fileUuid = Guid.NewGuid();
    /// await helper.UploadToPathAsync("config.json", "/config.json", fileUuid);
    /// </code>
    /// </example>
    public async Task<UploadResult> UploadToPathAsync(
        string localFilePath,
        string targetPath,
        Guid? fileUuid = null,
        Action<long, long>? progressCallback = null)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(localFilePath))
            throw new ArgumentException("Local file path cannot be empty", nameof(localFilePath));
        
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("Target path cannot be empty", nameof(targetPath));
        
        if (!File.Exists(localFilePath))
            throw new FileNotFoundException($"Local file not found: {localFilePath}", localFilePath);
        
        if (!targetPath.StartsWith("/"))
            throw new ArgumentException("Target path must start with '/'", nameof(targetPath));

        // Parse the target path
        var (folderPath, fileName) = ParsePath(targetPath);
        
        // Ensure folder exists (create if needed)
        var folderUuid = await EnsureFolderExistsAsync(folderPath);
        
        // Prepare file metadata
        var actualFileUuid = fileUuid ?? Guid.NewGuid();
        var fileData = new Dictionary<string, object>
        {
            { "name", fileName },
            { "euuid", actualFileUuid },
            { "folder", new Dictionary<string, object> { { "euuid", folderUuid } } }
        };
        
        // Upload the file
        await _uploader.UploadFileAsync(localFilePath, fileData, progressCallback);
        
        return new UploadResult
        {
            FileUuid = actualFileUuid,
            FileName = fileName,
            FolderPath = folderPath,
            FolderUuid = folderUuid,
            FullPath = targetPath
        };
    }

    /// <summary>
    /// Uploads text content to a specific path in EYWA, creating folders as needed.
    /// </summary>
    /// <param name="content">Text content to upload</param>
    /// <param name="targetPath">Target path in EYWA (e.g., "/tmp/data.txt")</param>
    /// <param name="fileUuid">Optional custom UUID for the file</param>
    /// <returns>Upload result with file and folder information</returns>
    public async Task<UploadResult> UploadTextToPathAsync(
        string content,
        string targetPath,
        Guid? fileUuid = null)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("Target path cannot be empty", nameof(targetPath));
        
        if (!targetPath.StartsWith("/"))
            throw new ArgumentException("Target path must start with '/'", nameof(targetPath));

        // Parse the target path
        var (folderPath, fileName) = ParsePath(targetPath);
        
        // Ensure folder exists
        var folderUuid = await EnsureFolderExistsAsync(folderPath);
        
        // Prepare file metadata
        var actualFileUuid = fileUuid ?? Guid.NewGuid();
        var fileData = new Dictionary<string, object>
        {
            { "name", fileName },
            { "euuid", actualFileUuid },
            { "folder", new Dictionary<string, object> { { "euuid", folderUuid } } }
        };
        
        // Upload the content
        await _uploader.UploadTextAsync(content, fileData);
        
        return new UploadResult
        {
            FileUuid = actualFileUuid,
            FileName = fileName,
            FolderPath = folderPath,
            FolderUuid = folderUuid,
            FullPath = targetPath
        };
    }

    /// <summary>
    /// Uploads bytes to a specific path in EYWA, creating folders as needed.
    /// </summary>
    /// <param name="bytes">Byte array to upload</param>
    /// <param name="targetPath">Target path in EYWA (e.g., "/tmp/data.bin")</param>
    /// <param name="fileUuid">Optional custom UUID for the file</param>
    /// <param name="contentType">Optional content type (auto-detected if not provided)</param>
    /// <returns>Upload result with file and folder information</returns>
    public async Task<UploadResult> UploadBytesToPathAsync(
        byte[] bytes,
        string targetPath,
        Guid? fileUuid = null,
        string? contentType = null)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));
        
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("Target path cannot be empty", nameof(targetPath));
        
        if (!targetPath.StartsWith("/"))
            throw new ArgumentException("Target path must start with '/'", nameof(targetPath));

        // Parse the target path
        var (folderPath, fileName) = ParsePath(targetPath);
        
        // Ensure folder exists
        var folderUuid = await EnsureFolderExistsAsync(folderPath);
        
        // Prepare file metadata
        var actualFileUuid = fileUuid ?? Guid.NewGuid();
        var fileData = new Dictionary<string, object>
        {
            { "name", fileName },
            { "euuid", actualFileUuid },
            { "folder", new Dictionary<string, object> { { "euuid", folderUuid } } }
        };
        
        if (!string.IsNullOrEmpty(contentType))
            fileData["content_type"] = contentType;
        
        // Upload the bytes
        await _uploader.UploadBytesAsync(bytes, fileData);
        
        return new UploadResult
        {
            FileUuid = actualFileUuid,
            FileName = fileName,
            FolderPath = folderPath,
            FolderUuid = folderUuid,
            FullPath = targetPath
        };
    }

    /// <summary>
    /// Clears the internal folder path cache.
    /// Useful if folders were deleted or modified outside this helper.
    /// </summary>
    public void ClearCache()
    {
        _folderCache.Clear();
        _folderCache["/"] = FileConstants.RootFolderUuid;
    }

    // Private helper methods

    /// <summary>
    /// Parses a full path into folder path and filename.
    /// Examples:
    /// - "/file.txt" -> ("/", "file.txt")
    /// - "/tmp/file.txt" -> ("/tmp", "file.txt")
    /// - "/tmp/reports/2024/file.txt" -> ("/tmp/reports/2024", "file.txt")
    /// </summary>
    private (string folderPath, string fileName) ParsePath(string fullPath)
    {
        // Remove trailing slash if present
        fullPath = fullPath.TrimEnd('/');
        
        var lastSlashIndex = fullPath.LastIndexOf('/');
        
        if (lastSlashIndex == 0)
        {
            // File in root: "/file.txt"
            return ("/", fullPath.Substring(1));
        }
        
        var folderPath = fullPath.Substring(0, lastSlashIndex);
        var fileName = fullPath.Substring(lastSlashIndex + 1);
        
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException($"Invalid path: no filename found in '{fullPath}'");
        
        return (folderPath, fileName);
    }

    /// <summary>
    /// Ensures a folder path exists, creating missing folders as needed.
    /// Returns the UUID of the final folder.
    /// 
    /// Examples:
    /// - "/" -> returns root UUID
    /// - "/tmp" -> ensures /tmp exists, returns its UUID
    /// - "/tmp/reports/2024" -> ensures full hierarchy exists, returns 2024's UUID
    /// </summary>
    private async Task<Guid> EnsureFolderExistsAsync(string folderPath)
    {
        // Root folder always exists
        if (folderPath == "/")
            return FileConstants.RootFolderUuid;
        
        // Check cache first
        if (_folderCache.TryGetValue(folderPath, out var cachedUuid))
            return cachedUuid;
        
        // Try to query the folder
        var folder = await _folderManager.GetFolderByPathAsync(folderPath);
        
        if (folder != null)
        {
            var uuid = (Guid)folder["euuid"];
            _folderCache[folderPath] = uuid;
            return uuid;
        }
        
        // Folder doesn't exist - need to create it
        // First, ensure parent exists
        var parts = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        Guid parentUuid = FileConstants.RootFolderUuid;
        string currentPath = "";
        
        // Build path incrementally, creating folders as needed
        for (int i = 0; i < parts.Length; i++)
        {
            currentPath += "/" + parts[i];
            
            // Check if this level exists
            if (_folderCache.TryGetValue(currentPath, out var existingUuid))
            {
                parentUuid = existingUuid;
                continue;
            }
            
            // Try to query it
            var existingFolder = await _folderManager.GetFolderByPathAsync(currentPath);
            
            if (existingFolder != null)
            {
                var uuid = (Guid)existingFolder["euuid"];
                _folderCache[currentPath] = uuid;
                parentUuid = uuid;
            }
            else
            {
                // Create this folder
                var folderData = new Dictionary<string, object>
                {
                    { "name", parts[i] },
                    { "euuid", Guid.NewGuid() },
                    { "parent", new Dictionary<string, object> { { "euuid", parentUuid } } }
                };
                
                var createdFolder = await _folderManager.CreateFolderAsync(folderData);
                var createdUuid = (Guid)createdFolder["euuid"];
                
                _folderCache[currentPath] = createdUuid;
                parentUuid = createdUuid;
            }
        }
        
        return parentUuid;
    }
}

/// <summary>
/// Result of an upload operation via FilePathUploader.
/// Contains information about the uploaded file and its location.
/// </summary>
public class UploadResult
{
    /// <summary>
    /// UUID of the uploaded file
    /// </summary>
    public Guid FileUuid { get; set; }
    
    /// <summary>
    /// Name of the uploaded file
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Folder path where file was uploaded (e.g., "/tmp/reports")
    /// </summary>
    public string FolderPath { get; set; } = string.Empty;
    
    /// <summary>
    /// UUID of the folder where file was uploaded
    /// </summary>
    public Guid FolderUuid { get; set; }
    
    /// <summary>
    /// Full path including filename (e.g., "/tmp/reports/file.txt")
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"File '{FileName}' (UUID: {FileUuid}) uploaded to {FolderPath}";
    }
}
