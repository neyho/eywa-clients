using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using EywaClient.Core;
using EywaClient.GraphQL;
using EywaClient.Utilities;

namespace EywaClient.Files;

/// <summary>
/// File downloader using dynamic JSON hashmap approach - complete download workflow!
/// Handles file downloads with clean, Clojure-like APIs and progress reporting.
/// 
/// This combines all download operations into a single, easy-to-use class.
/// For lower-level control, use FileManager directly.
/// </summary>
public class FileDownloader
{
    private readonly SimpleGraphQLClient _client;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the FileDownloader class.
    /// </summary>
    /// <param name="client">Simple GraphQL client for EYWA communication</param>
    /// <param name="httpClient">HTTP client for file downloads (optional)</param>
    public FileDownloader(SimpleGraphQLClient client, HttpClient? httpClient = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Alternative constructor using JsonRpcClient directly.
    /// </summary>
    /// <param name="rpcClient">JSON-RPC client for EYWA communication</param>
    /// <param name="httpClient">HTTP client for file downloads (optional)</param>
    public FileDownloader(JsonRpcClient rpcClient, HttpClient? httpClient = null)
    {
        _client = new SimpleGraphQLClient(rpcClient ?? throw new ArgumentNullException(nameof(rpcClient)));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Downloads a file to local path - complete workflow in one call!
    /// Gets download URL from EYWA and downloads file to specified path.
    /// </summary>
    /// <param name="fileUuid">UUID of the file to download</param>
    /// <param name="localPath">Local path where file should be saved</param>
    /// <param name="createDirectories">Whether to create directories if they don't exist (default: true)</param>
    /// <returns>Dynamic file info object</returns>
    /// <example>
    /// <code>
    /// // Simple download
    /// var file = await fileDownloader.DownloadToPath(fileUuid, "./downloads/document.pdf");
    /// Console.WriteLine($"Downloaded: {file.name} ({file.size} bytes)");
    /// 
    /// // Download with custom path structure
    /// var fileName = $"{file.name}_{DateTime.Now:yyyyMMdd}.{Path.GetExtension(file.name)}";
    /// await fileDownloader.DownloadToPath(fileUuid, $"./archive/{fileName}");
    /// </code>
    /// </example>
    public async Task<dynamic?> DownloadToPath(Guid fileUuid, string localPath, bool createDirectories = true)
    {
        if (string.IsNullOrWhiteSpace(localPath))
            throw new ArgumentException("Local path cannot be empty", nameof(localPath));

        // Create directory if needed
        if (createDirectories)
        {
            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        // Get file info and download URL
        var fileInfo = await GetFileInfo(fileUuid);
        if (fileInfo == null)
            throw new FileNotFoundException($"File with UUID {fileUuid} not found in EYWA");

        var downloadUrl = await GetDownloadUrl(fileUuid);
        if (downloadUrl?.url == null)
            throw new InvalidOperationException($"Could not get download URL for file {fileUuid}");

        // Download file
        using var response = await _httpClient.GetAsync(downloadUrl.url);
        response.EnsureSuccessStatusCode();

        using var fileStream = File.Create(localPath);
        await response.Content.CopyToAsync(fileStream);

        return fileInfo;
    }

    /// <summary>
    /// Downloads a file to stream - useful for in-memory processing.
    /// </summary>
    /// <param name="fileUuid">UUID of the file to download</param>
    /// <returns>Stream containing file data</returns>
    /// <example>
    /// <code>
    /// using var stream = await fileDownloader.DownloadToStream(fileUuid);
    /// 
    /// // Process stream directly
    /// var content = await new StreamReader(stream).ReadToEndAsync();
    /// Console.WriteLine($"File content: {content}");
    /// 
    /// // Or copy to memory stream for multiple uses
    /// var memoryStream = new MemoryStream();
    /// await stream.CopyToAsync(memoryStream);
    /// memoryStream.Position = 0;
    /// </code>
    /// </example>
    public async Task<Stream> DownloadToStream(Guid fileUuid)
    {
        var downloadUrl = await GetDownloadUrl(fileUuid);
        if (downloadUrl?.url == null)
            throw new InvalidOperationException($"Could not get download URL for file {fileUuid}");

        var response = await _httpClient.GetAsync(downloadUrl.url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync();
    }

    /// <summary>
    /// Downloads a file to byte array - convenient for small files.
    /// </summary>
    /// <param name="fileUuid">UUID of the file to download</param>
    /// <returns>File data as byte array</returns>
    /// <example>
    /// <code>
    /// byte[] fileData = await fileDownloader.DownloadToBytes(fileUuid);
    /// 
    /// // Save to different location
    /// await File.WriteAllBytesAsync("./backup/file-copy.pdf", fileData);
    /// 
    /// // Process in memory
    /// var base64 = Convert.ToBase64String(fileData);
    /// </code>
    /// </example>
    public async Task<byte[]> DownloadToBytes(Guid fileUuid)
    {
        var downloadUrl = await GetDownloadUrl(fileUuid);
        if (downloadUrl?.url == null)
            throw new InvalidOperationException($"Could not get download URL for file {fileUuid}");

        return await _httpClient.GetByteArrayAsync(downloadUrl.url);
    }

    /// <summary>
    /// Downloads multiple files to a target directory - efficient batch operation.
    /// </summary>
    /// <param name="fileUuids">Collection of file UUIDs to download</param>
    /// <param name="targetDirectory">Directory where files should be saved</param>
    /// <param name="preserveNames">Whether to use original file names (default: true)</param>
    /// <param name="nameSelector">Optional function to customize file names</param>
    /// <returns>Array of downloaded file info objects</returns>
    /// <example>
    /// <code>
    /// // Simple batch download
    /// var files = await fileDownloader.DownloadMultipleFiles(
    ///     fileUuids, 
    ///     "./downloads/"
    /// );
    /// 
    /// // Custom naming
    /// var files = await fileDownloader.DownloadMultipleFiles(
    ///     fileUuids,
    ///     "./archive/",
    ///     nameSelector: file => $"{DateTime.Now:yyyyMMdd}_{file.name}"
    /// );
    /// 
    /// foreach (var file in files)
    /// {
    ///     Console.WriteLine($"Downloaded: {file.name}");
    /// }
    /// </code>
    /// </example>
    public async Task<dynamic[]> DownloadMultipleFiles(
        IEnumerable<Guid> fileUuids,
        string targetDirectory,
        bool preserveNames = true,
        Func<dynamic, string>? nameSelector = null)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Target directory cannot be empty", nameof(targetDirectory));

        Directory.CreateDirectory(targetDirectory);

        var tasks = new List<Task<dynamic?>>();

        foreach (var fileUuid in fileUuids)
        {
            tasks.Add(DownloadFileWithCustomName(fileUuid, targetDirectory, preserveNames, nameSelector));
        }

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).ToArray()!;
    }

    /// <summary>
    /// Downloads file with progress reporting - useful for large files.
    /// </summary>
    /// <param name="fileUuid">UUID of the file to download</param>
    /// <param name="localPath">Local path where file should be saved</param>
    /// <param name="progress">Progress reporter for download status</param>
    /// <returns>Dynamic file info object</returns>
    /// <example>
    /// <code>
    /// var progress = new Progress&lt;DownloadProgress&gt;(p => {
    ///     Console.WriteLine($"Download progress: {p.Percentage:F1}% ({p.BytesDownloaded}/{p.TotalBytes} bytes)");
    /// });
    /// 
    /// var file = await fileDownloader.DownloadWithProgress(fileUuid, "./large-file.zip", progress);
    /// </code>
    /// </example>
    public async Task<dynamic?> DownloadWithProgress(
        Guid fileUuid,
        string localPath,
        IProgress<DownloadProgress> progress)
    {
        if (string.IsNullOrWhiteSpace(localPath))
            throw new ArgumentException("Local path cannot be empty", nameof(localPath));

        // Create directory if needed
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Get file info and download URL
        var fileInfo = await GetFileInfo(fileUuid);
        if (fileInfo == null)
            throw new FileNotFoundException($"File with UUID {fileUuid} not found in EYWA");

        var downloadUrl = await GetDownloadUrl(fileUuid);
        if (downloadUrl?.url == null)
            throw new InvalidOperationException($"Could not get download URL for file {fileUuid}");

        var totalBytes = (long)(fileInfo.size ?? 0);

        // Download with progress tracking
        using var response = await _httpClient.GetAsync(downloadUrl.url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = File.Create(localPath);

        var buffer = new byte[8192];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;

            progress.Report(new DownloadProgress
            {
                BytesDownloaded = totalBytesRead,
                TotalBytes = totalBytes,
                Stage = "Downloading"
            });
        }

        progress.Report(new DownloadProgress
        {
            BytesDownloaded = totalBytesRead,
            TotalBytes = totalBytes,
            Stage = "Complete"
        });

        return fileInfo;
    }

    /// <summary>
    /// Downloads file by name from a specific folder - convenience method.
    /// </summary>
    /// <param name="fileName">Name of the file to download</param>
    /// <param name="folderUuid">UUID of the folder to search in (null for root)</param>
    /// <param name="localPath">Local path where file should be saved</param>
    /// <returns>Dynamic file info object or null if file not found</returns>
    /// <example>
    /// <code>
    /// // Download by name from specific folder
    /// var file = await fileDownloader.DownloadByName("report.pdf", folderId, "./downloads/report.pdf");
    /// 
    /// // Download from root folder
    /// var file = await fileDownloader.DownloadByName("README.md", null, "./README.md");
    /// </code>
    /// </example>
    public async Task<dynamic?> DownloadByName(string fileName, Guid? folderUuid, string localPath)
    {
        // Find file by name
        var fileManager = new FileManager(_client);
        var file = await fileManager.FindFileByName(fileName, folderUuid);
        
        if (file == null)
            return null;

        var fileUuid = Guid.Parse(file.euuid);
        return await DownloadToPath(fileUuid, localPath);
    }

    /// <summary>
    /// Gets download URL for a file - useful for external download handling.
    /// </summary>
    /// <param name="fileUuid">UUID of the file</param>
    /// <returns>Dynamic object containing download URL and metadata</returns>
    /// <example>
    /// <code>
    /// var downloadInfo = await fileDownloader.GetDownloadUrl(fileUuid);
    /// Console.WriteLine($"Download URL: {downloadInfo.url}");
    /// Console.WriteLine($"Expires: {downloadInfo.expires_at}");
    /// 
    /// // Use URL with external HTTP client
    /// var customResponse = await customHttpClient.GetAsync(downloadInfo.url);
    /// </code>
    /// </example>
    public async Task<dynamic?> GetDownloadUrl(Guid fileUuid)
    {
        const string query = @"
        query GetFileDownloadUrl($file: FileInput!) {
            requestDownloadURL(file: $file)
        }";

        var response = await _client.GraphQL(query, new { file = new { euuid = fileUuid } });
        
        // requestDownloadURL returns a simple string URL
        var downloadUrl = response.data.requestDownloadURL;
        
        // Return it in the expected format for compatibility
        return new {
            url = downloadUrl,
            expires_at = (string?)null
        };
    }

    // Internal helper methods

    private async Task<dynamic?> GetFileInfo(Guid fileUuid)
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

        var response = await _client.GraphQL(query, new { uuid = fileUuid });
        return response.data.getFile;
    }

    private async Task<dynamic?> DownloadFileWithCustomName(
        Guid fileUuid,
        string targetDirectory,
        bool preserveNames,
        Func<dynamic, string>? nameSelector)
    {
        var fileInfo = await GetFileInfo(fileUuid);
        if (fileInfo == null)
            return null;

        string fileName;
        if (nameSelector != null)
        {
            fileName = nameSelector(fileInfo);
        }
        else if (preserveNames)
        {
            fileName = fileInfo.name ?? $"file_{fileUuid}";
        }
        else
        {
            fileName = $"file_{fileUuid}";
        }

        var localPath = Path.Combine(targetDirectory, fileName);
        return await DownloadToPath(fileUuid, localPath, false); // Don't create dirs again
    }

    /// <summary>
    /// Dispose pattern for proper cleanup
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Download progress information for progress reporting.
/// </summary>
public class DownloadProgress
{
    /// <summary>
    /// Number of bytes downloaded so far
    /// </summary>
    public long BytesDownloaded { get; set; }
    
    /// <summary>
    /// Total number of bytes to download
    /// </summary>
    public long TotalBytes { get; set; }
    
    /// <summary>
    /// Download percentage (0-100)
    /// </summary>
    public double Percentage => TotalBytes > 0 ? (double)BytesDownloaded / TotalBytes * 100 : 0;
    
    /// <summary>
    /// Current download stage
    /// </summary>
    public string Stage { get; set; } = "Downloading";
}