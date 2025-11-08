using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using EywaClient.Core;
using EywaClient.GraphQL;
using EywaClient.Utilities;

namespace EywaClient.Files;

/// <summary>
/// File uploader using dynamic JSON hashmap approach - complete upload workflow!
/// Handles the full 3-step EYWA upload process with clean, Clojure-like APIs.
/// 
/// This combines all upload operations into a single, easy-to-use class.
/// For lower-level control, use FileManager directly.
/// </summary>
public class FileUploader
{
    private readonly SimpleGraphQLClient _client;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the FileUploader class.
    /// </summary>
    /// <param name="client">Simple GraphQL client for EYWA communication</param>
    /// <param name="httpClient">HTTP client for S3 uploads (optional)</param>
    public FileUploader(SimpleGraphQLClient client, HttpClient? httpClient = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Alternative constructor using JsonRpcClient directly.
    /// </summary>
    /// <param name="rpcClient">JSON-RPC client for EYWA communication</param>
    /// <param name="httpClient">HTTP client for S3 uploads (optional)</param>
    public FileUploader(JsonRpcClient rpcClient, HttpClient? httpClient = null)
    {
        _client = new SimpleGraphQLClient(rpcClient ?? throw new ArgumentNullException(nameof(rpcClient)));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Uploads a file from local path - complete workflow in one call!
    /// Handles the full EYWA 3-step process: request URL → S3 upload → confirm
    /// </summary>
    /// <param name="filePath">Local file path to upload</param>
    /// <param name="metadata">File metadata including name, folder, etc.</param>
    /// <returns>Dynamic file object with upload results</returns>
    /// <example>
    /// <code>
    /// // Simple upload to root
    /// var file = await fileUploader.UploadFromPath("./document.pdf", new {
    ///     name = "important-document.pdf"
    /// });
    /// 
    /// // Upload to specific folder with custom UUID
    /// var file = await fileUploader.UploadFromPath("./report.xlsx", new {
    ///     name = "monthly-report.xlsx",
    ///     euuid = Guid.NewGuid(), // For deduplication
    ///     folder = new { euuid = folderId }
    /// });
    /// 
    /// Console.WriteLine($"Uploaded: {file.name} (UUID: {file.euuid})");
    /// Console.WriteLine($"Status: {file.status}");
    /// Console.WriteLine($"Size: {file.size} bytes");
    /// </code>
    /// </example>
    public async Task<dynamic?> UploadFromPath(string filePath, object metadata)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var fileInfo = new FileInfo(filePath);
        using var stream = File.OpenRead(filePath);
        
        // Merge file info with provided metadata
        var completeMetadata = MergeMetadata(metadata, new {
            name = Path.GetFileName(filePath),
            content_type = MimeTypeHelper.GetMimeType(filePath),
            size = fileInfo.Length
        });

        return await UploadFromStream(stream, completeMetadata);
    }

    /// <summary>
    /// Uploads a file from stream - complete workflow in one call!
    /// </summary>
    /// <param name="stream">Stream containing file data</param>
    /// <param name="metadata">Complete file metadata including name, content_type, size</param>
    /// <returns>Dynamic file object with upload results</returns>
    /// <example>
    /// <code>
    /// using var stream = new MemoryStream(fileBytes);
    /// var file = await fileUploader.UploadFromStream(stream, new {
    ///     name = "generated-report.pdf",
    ///     content_type = "application/pdf",
    ///     size = fileBytes.Length,
    ///     folder = new { euuid = folderId }
    /// });
    /// </code>
    /// </example>
    public async Task<dynamic?> UploadFromStream(Stream stream, object metadata)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable", nameof(stream));

        // Step 1: Request upload URL
        var uploadRequest = await RequestUploadUrl(metadata);
        if (uploadRequest == null)
            throw new InvalidOperationException("Failed to get upload URL from EYWA");

        try
        {
            // Step 2: Upload to S3 with content type
            var contentType = metadata.GetType().GetProperty("content_type")?.GetValue(metadata)?.ToString() ?? "application/octet-stream";
            await UploadToS3(uploadRequest, stream, contentType);

            // Step 3: Confirm upload with EYWA using URL
            var uploadUrl = (string)uploadRequest.url;
            var confirmed = await ConfirmUpload(uploadUrl);
            
            if (confirmed)
            {
                // Get the file UUID from metadata and fetch file info
                var fileUuidStr = uploadRequest.euuid?.ToString();
                if (!string.IsNullOrEmpty(fileUuidStr))
                {
                    if (Guid.TryParse(fileUuidStr, out Guid fileUuid))
                    {
                        // Use FileManager to get complete file info
                        var fileManager = new FileManager(_client);
                        return await fileManager.GetFile(fileUuid);
                    }
                }
            }
            
            return null;
        }
        catch (Exception)
        {
            // If upload fails, we might want to clean up the pending file
            // For now, let EYWA handle cleanup of failed uploads
            throw;
        }
    }

    /// <summary>
    /// Uploads a file from byte array - convenience method.
    /// </summary>
    /// <param name="data">File data as byte array</param>
    /// <param name="metadata">Complete file metadata</param>
    /// <returns>Dynamic file object with upload results</returns>
    /// <example>
    /// <code>
    /// byte[] pdfBytes = GeneratePdfReport();
    /// var file = await fileUploader.UploadFromBytes(pdfBytes, new {
    ///     name = "report.pdf",
    ///     content_type = "application/pdf",
    ///     size = pdfBytes.Length
    /// });
    /// </code>
    /// </example>
    public async Task<dynamic?> UploadFromBytes(byte[] data, object metadata)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        using var stream = new MemoryStream(data);
        return await UploadFromStream(stream, metadata);
    }

    /// <summary>
    /// Uploads multiple files in batch - efficient for multiple file operations.
    /// </summary>
    /// <param name="filePaths">Collection of file paths to upload</param>
    /// <param name="commonMetadata">Common metadata applied to all files</param>
    /// <param name="nameSelector">Optional function to customize file names</param>
    /// <returns>Array of upload results</returns>
    /// <example>
    /// <code>
    /// var files = await fileUploader.UploadMultipleFiles(
    ///     new[] { "./doc1.pdf", "./doc2.xlsx", "./doc3.txt" },
    ///     new { folder = new { euuid = folderId } }
    /// );
    /// 
    /// foreach (var file in files)
    /// {
    ///     Console.WriteLine($"Uploaded: {file.name} ({file.size} bytes)");
    /// }
    /// </code>
    /// </example>
    public async Task<dynamic[]> UploadMultipleFiles(
        IEnumerable<string> filePaths, 
        object commonMetadata,
        Func<string, string>? nameSelector = null)
    {
        var tasks = new List<Task<dynamic?>>();
        
        foreach (var filePath in filePaths)
        {
            var fileName = nameSelector?.Invoke(filePath) ?? Path.GetFileName(filePath);
            var fileMetadata = MergeMetadata(commonMetadata, new { name = fileName });
            
            tasks.Add(UploadFromPath(filePath, fileMetadata));
        }

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).ToArray()!;
    }

    /// <summary>
    /// Uploads file with progress reporting - useful for large files.
    /// </summary>
    /// <param name="filePath">Local file path to upload</param>
    /// <param name="metadata">File metadata</param>
    /// <param name="progress">Progress reporter for upload status</param>
    /// <returns>Dynamic file object with upload results</returns>
    /// <example>
    /// <code>
    /// var progress = new Progress&lt;UploadProgress&gt;(p => {
    ///     Console.WriteLine($"Upload progress: {p.Percentage:F1}% ({p.BytesUploaded}/{p.TotalBytes} bytes)");
    /// });
    /// 
    /// var file = await fileUploader.UploadWithProgress("./large-file.zip", metadata, progress);
    /// </code>
    /// </example>
    public async Task<dynamic?> UploadWithProgress(
        string filePath, 
        object metadata, 
        IProgress<UploadProgress> progress)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var fileInfo = new FileInfo(filePath);
        var totalBytes = fileInfo.Length;

        // Create progress-tracking stream
        using var fileStream = File.OpenRead(filePath);
        using var progressStream = new ProgressStream(fileStream, totalBytes, progress);
        
        var completeMetadata = MergeMetadata(metadata, new {
            name = Path.GetFileName(filePath),
            content_type = MimeTypeHelper.GetMimeType(filePath),
            size = totalBytes
        });

        return await UploadFromStream(progressStream, completeMetadata);
    }

    // Internal helper methods

    private async Task<dynamic?> RequestUploadUrl(object metadata)
    {
        const string mutation = @"
        mutation RequestFileUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }";

        var response = await _client.GraphQL(mutation, new { file = metadata });
        
        // requestUploadURL returns a simple string URL
        var uploadUrl = response.data.requestUploadURL;
        
        // Return it in the expected format for compatibility
        // The UUID should come from the metadata we sent, not the response
        string fileUuid;
        try {
            // Handle Dictionary objects (from MergeMetadata) differently than anonymous objects
            if (metadata is Dictionary<string, object> dict)
            {
                dict.TryGetValue("euuid", out var uuidObj);
                fileUuid = uuidObj?.ToString() ?? Guid.NewGuid().ToString();
            }
            else
            {
                var euuidProp = metadata.GetType().GetProperty("euuid");
                var euuidValue = euuidProp?.GetValue(metadata, null);
                fileUuid = euuidValue?.ToString() ?? Guid.NewGuid().ToString();
            }
        } catch {
            fileUuid = Guid.NewGuid().ToString();
            Console.WriteLine($"DEBUG: Generated new UUID: {fileUuid}");
        }
        
        return new {
            url = uploadUrl,
            euuid = fileUuid
        };
    }

    private async Task UploadToS3(dynamic uploadRequest, Stream stream, string contentType)
    {
        var uploadUrl = (string)uploadRequest.url;

        // Simple PUT request to S3 (matching Babashka implementation)
        using var streamContent = new StreamContent(stream);
        
        // Set content type that matches what was signed
        if (!string.IsNullOrEmpty(contentType))
        {
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }
        
        // S3 requires Content-Length header to be set
        if (stream.CanSeek)
        {
            streamContent.Headers.ContentLength = stream.Length;
        }
        
        // Upload to S3 with PUT
        var response = await _httpClient.PutAsync(uploadUrl, streamContent);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"S3 upload failed with status {response.StatusCode}: {errorContent}");
        }
    }

    private async Task<bool> ConfirmUpload(string uploadUrl)
    {
        const string mutation = @"
        mutation ConfirmFileUpload($url: String!) {
            confirmFileUpload(url: $url)
        }";

        var response = await _client.GraphQL(mutation, new { url = uploadUrl });
        return response.data.confirmFileUpload == true;
    }

    private static Dictionary<string, object> MergeMetadata(object metadata, object defaults)
    {
        var result = new Dictionary<string, object>();
        // Console.WriteLine($"DEBUG MergeMetadata: metadata type = {metadata.GetType().Name}");
        // Console.WriteLine($"DEBUG MergeMetadata: defaults type = {defaults.GetType().Name}");

        // Add defaults first
        foreach (var prop in defaults.GetType().GetProperties())
        {
            var value = prop.GetValue(defaults, null);
            // Console.WriteLine($"DEBUG: defaults prop = {prop.Name}, value = {value}");
            if (value != null)
                result[prop.Name] = value;
        }

        // Override with provided metadata
        foreach (var prop in metadata.GetType().GetProperties())
        {
            var value = prop.GetValue(metadata, null);
            // Console.WriteLine($"DEBUG: metadata prop = {prop.Name}, value = {value}");
            if (value != null)
                result[prop.Name] = value;
        }
        
        // Console.WriteLine($"DEBUG: Final merged keys = [{string.Join(", ", result.Keys)}]");

        return result;
    }

    private static IEnumerable<KeyValuePair<string, string>> GetFieldsFromDynamic(dynamic fields)
    {
        // This is a simplified implementation - you might need to enhance based on actual S3 field format
        var result = new List<KeyValuePair<string, string>>();
        
        try
        {
            // If fields is a dictionary-like object, iterate through it
            if (fields is DynamicJson dynJson)
            {
                var dict = dynJson.ToDictionary();
                foreach (var kvp in dict)
                {
                    if (kvp.Value != null)
                        result.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString()!));
                }
            }
        }
        catch
        {
            // If we can't parse fields, continue without them
            // S3 upload might still work without form fields
        }
        
        return result;
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
/// Upload progress information for progress reporting.
/// </summary>
public class UploadProgress
{
    /// <summary>
    /// Number of bytes uploaded so far
    /// </summary>
    public long BytesUploaded { get; set; }
    
    /// <summary>
    /// Total number of bytes to upload
    /// </summary>
    public long TotalBytes { get; set; }
    
    /// <summary>
    /// Upload percentage (0-100)
    /// </summary>
    public double Percentage => TotalBytes > 0 ? (double)BytesUploaded / TotalBytes * 100 : 0;
    
    /// <summary>
    /// Current upload stage
    /// </summary>
    public string Stage { get; set; } = "Uploading";
}

/// <summary>
/// Stream wrapper that reports progress during read operations.
/// </summary>
internal class ProgressStream : Stream
{
    private readonly Stream _baseStream;
    private readonly IProgress<UploadProgress> _progress;
    private readonly long _totalBytes;
    private long _bytesRead;

    public ProgressStream(Stream baseStream, long totalBytes, IProgress<UploadProgress> progress)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _totalBytes = totalBytes;
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => _baseStream.Length;
    public override long Position 
    { 
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _baseStream.Read(buffer, offset, count);
        _bytesRead += bytesRead;
        
        _progress.Report(new UploadProgress
        {
            BytesUploaded = _bytesRead,
            TotalBytes = _totalBytes,
            Stage = "Uploading"
        });
        
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        _bytesRead += bytesRead;
        
        _progress.Report(new UploadProgress
        {
            BytesUploaded = _bytesRead,
            TotalBytes = _totalBytes,
            Stage = "Uploading"
        });
        
        return bytesRead;
    }

    public override void Flush() => _baseStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
    public override void SetLength(long value) => _baseStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _baseStream?.Dispose();
        }
        base.Dispose(disposing);
    }
}