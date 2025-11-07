using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EywaClient.Exceptions;
using EywaClient.GraphQL;

namespace EywaClient.Files;

/// <summary>
/// High-level convenience helper for downloading files by path.
/// 
/// This helper provides a simple path-based API for downloads, matching
/// the FilePathUploader pattern. It automatically:
/// - Parses target paths (e.g., "/tmp/reports/file.pdf")
/// - Queries folders to find folder UUIDs
/// - Searches for files by name in the folder
/// - Downloads the file
/// 
/// Use this when you want simple path-based downloads.
/// For direct UUID-based downloads, use FileDownloader.
/// </summary>
/// <example>
/// <code>
/// var pathDownloader = new FilePathDownloader(graphqlClient);
/// 
/// // Download by path
/// await pathDownloader.DownloadFromPathAsync("/tmp/report.pdf", @"C:\downloads\report.pdf");
/// 
/// // Download as bytes
/// var bytes = await pathDownloader.DownloadBytesFromPathAsync("/tmp/data.bin");
/// </code>
/// </example>
public class FilePathDownloader
{
    private readonly FileDownloader _downloader;
    private readonly FileManager _fileManager;
    private readonly FolderManager _folderManager;

    /// <summary>
    /// Initializes a new instance of the FilePathDownloader class.
    /// </summary>
    /// <param name="graphqlClient">GraphQL client for EYWA API</param>
    public FilePathDownloader(GraphQLClient graphqlClient)
    {
        _downloader = new FileDownloader(graphqlClient);
        _fileManager = new FileManager(graphqlClient);
        _folderManager = new FolderManager(graphqlClient);
    }

    /// <summary>
    /// Initializes with existing instances.
    /// </summary>
    public FilePathDownloader(
        FileDownloader downloader,
        FileManager fileManager,
        FolderManager folderManager)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
        _folderManager = folderManager ?? throw new ArgumentNullException(nameof(folderManager));
    }

    /// <summary>
    /// Downloads a file by its path in EYWA to a local file.
    /// 
    /// The path should be the full path including filename (e.g., "/tmp/reports/file.pdf").
    /// The helper will parse the path, find the folder, search for the file, and download it.
    /// </summary>
    /// <param name="sourcePath">Source path in EYWA (e.g., "/tmp/reports/file.pdf")</param>
    /// <param name="savePath">Local path to save the file</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <returns>Download result with file information</returns>
    /// <exception cref="ArgumentException">Thrown when paths are invalid</exception>
    /// <exception cref="FileNotFoundException">Thrown when file not found in EYWA</exception>
    /// <example>
    /// <code>
    /// // Download from root
    /// await pathDownloader.DownloadFromPathAsync("/config.json", @"C:\config.json");
    /// 
    /// // Download from folder
    /// await pathDownloader.DownloadFromPathAsync(
    ///     "/reports/2024/january/report.pdf",
    ///     @"C:\downloads\report.pdf"
    /// );
    /// 
    /// // With progress tracking
    /// await pathDownloader.DownloadFromPathAsync(
    ///     "/large-file.zip",
    ///     @"C:\downloads\large-file.zip",
    ///     (downloaded, total) => Console.WriteLine($"{downloaded}/{total}")
    /// );
    /// </code>
    /// </example>
    public async Task<DownloadResult> DownloadFromPathAsync(
        string sourcePath,
        string savePath,
        Action<long, long>? progressCallback = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (string.IsNullOrWhiteSpace(savePath))
            throw new ArgumentException("Save path cannot be empty", nameof(savePath));

        if (!sourcePath.StartsWith("/"))
            throw new ArgumentException("Source path must start with '/'", nameof(sourcePath));

        // Parse the source path
        var (folderPath, fileName) = ParsePath(sourcePath);

        // Find the file
        var fileInfo = await FindFileByPathAsync(folderPath, fileName);

        if (fileInfo == null)
            throw new FileNotFoundException($"File not found: {sourcePath}");

        // Download the file
        var fileUuid = (Guid)fileInfo["euuid"];
        var fileRef = new Dictionary<string, object> { { "euuid", fileUuid } };

        var downloadedPath = await _downloader.DownloadToFileAsync(fileRef, savePath, progressCallback);

        return new DownloadResult
        {
            FileUuid = fileUuid,
            FileName = fileName,
            FolderPath = folderPath,
            FullPath = sourcePath,
            LocalPath = downloadedPath,
            Size = Convert.ToInt64(fileInfo["size"])
        };
    }

    /// <summary>
    /// Downloads a file by its path in EYWA and returns it as a byte array.
    /// Warning: Loads entire file into memory. Use DownloadStreamFromPathAsync for large files.
    /// </summary>
    /// <param name="sourcePath">Source path in EYWA (e.g., "/tmp/data.bin")</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <returns>Byte array containing file content</returns>
    /// <example>
    /// <code>
    /// var bytes = await pathDownloader.DownloadBytesFromPathAsync("/tmp/image.png");
    /// File.WriteAllBytes(@"C:\image.png", bytes);
    /// </code>
    /// </example>
    public async Task<byte[]> DownloadBytesFromPathAsync(
        string sourcePath,
        Action<long, long>? progressCallback = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (!sourcePath.StartsWith("/"))
            throw new ArgumentException("Source path must start with '/'", nameof(sourcePath));

        // Parse the source path
        var (folderPath, fileName) = ParsePath(sourcePath);

        // Find the file
        var fileInfo = await FindFileByPathAsync(folderPath, fileName);

        if (fileInfo == null)
            throw new FileNotFoundException($"File not found: {sourcePath}");

        // Download as bytes
        var fileUuid = (Guid)fileInfo["euuid"];
        var fileRef = new Dictionary<string, object> { { "euuid", fileUuid } };

        return await _downloader.DownloadBytesAsync(fileRef, progressCallback);
    }

    /// <summary>
    /// Downloads a file by its path in EYWA and returns it as text.
    /// Warning: Loads entire file into memory. Use DownloadStreamFromPathAsync for large files.
    /// </summary>
    /// <param name="sourcePath">Source path in EYWA (e.g., "/tmp/config.txt")</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <returns>String containing file content</returns>
    /// <example>
    /// <code>
    /// var text = await pathDownloader.DownloadTextFromPathAsync("/config.json");
    /// var config = JsonSerializer.Deserialize&lt;Config&gt;(text);
    /// </code>
    /// </example>
    public async Task<string> DownloadTextFromPathAsync(
        string sourcePath,
        Action<long, long>? progressCallback = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (!sourcePath.StartsWith("/"))
            throw new ArgumentException("Source path must start with '/'", nameof(sourcePath));

        // Parse the source path
        var (folderPath, fileName) = ParsePath(sourcePath);

        // Find the file
        var fileInfo = await FindFileByPathAsync(folderPath, fileName);

        if (fileInfo == null)
            throw new FileNotFoundException($"File not found: {sourcePath}");

        // Download as text
        var fileUuid = (Guid)fileInfo["euuid"];
        var fileRef = new Dictionary<string, object> { { "euuid", fileUuid } };

        return await _downloader.DownloadTextAsync(fileRef, progressCallback);
    }

    /// <summary>
    /// Downloads a file by its path in EYWA and returns it as a stream (memory-efficient).
    /// Caller is responsible for disposing the returned stream.
    /// </summary>
    /// <param name="sourcePath">Source path in EYWA (e.g., "/tmp/large-file.zip")</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <returns>Stream containing file content (caller must dispose)</returns>
    /// <example>
    /// <code>
    /// using var stream = await pathDownloader.DownloadStreamFromPathAsync("/large-file.zip");
    /// using var fileStream = File.Create(@"C:\large-file.zip");
    /// await stream.CopyToAsync(fileStream);
    /// </code>
    /// </example>
    public async Task<Stream> DownloadStreamFromPathAsync(
        string sourcePath,
        Action<long, long>? progressCallback = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (!sourcePath.StartsWith("/"))
            throw new ArgumentException("Source path must start with '/'", nameof(sourcePath));

        // Parse the source path
        var (folderPath, fileName) = ParsePath(sourcePath);

        // Find the file
        var fileInfo = await FindFileByPathAsync(folderPath, fileName);

        if (fileInfo == null)
            throw new FileNotFoundException($"File not found: {sourcePath}");

        // Download as stream
        var fileUuid = (Guid)fileInfo["euuid"];
        var fileRef = new Dictionary<string, object> { { "euuid", fileUuid } };

        return await _downloader.DownloadStreamAsync(fileRef, progressCallback);
    }

    /// <summary>
    /// Gets file information by path without downloading.
    /// </summary>
    /// <param name="sourcePath">Source path in EYWA</param>
    /// <returns>File information or null if not found</returns>
    /// <example>
    /// <code>
    /// var fileInfo = await pathDownloader.GetFileInfoByPathAsync("/tmp/file.txt");
    /// if (fileInfo != null)
    /// {
    ///     Console.WriteLine($"Size: {fileInfo["size"]} bytes");
    ///     Console.WriteLine($"UUID: {fileInfo["euuid"]}");
    /// }
    /// </code>
    /// </example>
    public async Task<Dictionary<string, object>?> GetFileInfoByPathAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (!sourcePath.StartsWith("/"))
            throw new ArgumentException("Source path must start with '/'", nameof(sourcePath));

        var (folderPath, fileName) = ParsePath(sourcePath);
        return await FindFileByPathAsync(folderPath, fileName);
    }

    // Private helper methods

    /// <summary>
    /// Parses a full path into folder path and filename.
    /// </summary>
    private (string folderPath, string fileName) ParsePath(string fullPath)
    {
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
    /// Finds a file by its folder path and filename.
    /// </summary>
    private async Task<Dictionary<string, object>?> FindFileByPathAsync(
        string folderPath,
        string fileName)
    {
        // Special case: root folder
        if (folderPath == "/")
        {
            var rootFiles = await _fileManager.FindFileInFolderAsync(
                fileName,
                FileConstants.RootFolderUuid
            );
            return rootFiles;
        }

        // Get folder by path
        var folder = await _folderManager.GetFolderByPathAsync(folderPath);
        
        if (folder == null)
            return null;

        var folderUuid = (Guid)folder["euuid"];

        // Find file in folder
        return await _fileManager.FindFileInFolderAsync(fileName, folderUuid);
    }
}

/// <summary>
/// Result of a download operation via FilePathDownloader.
/// </summary>
public class DownloadResult
{
    /// <summary>
    /// UUID of the downloaded file
    /// </summary>
    public Guid FileUuid { get; set; }
    
    /// <summary>
    /// Name of the downloaded file
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Folder path in EYWA where file was downloaded from (e.g., "/tmp/reports")
    /// </summary>
    public string FolderPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Full path in EYWA including filename (e.g., "/tmp/reports/file.pdf")
    /// </summary>
    public string FullPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Local file path where file was saved (for DownloadFromPathAsync)
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long Size { get; set; }

    public override string ToString()
    {
        return $"Downloaded '{FileName}' ({Size:N0} bytes) from {FolderPath} to {LocalPath}";
    }
}
