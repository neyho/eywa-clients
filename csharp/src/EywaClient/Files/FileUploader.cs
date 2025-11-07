using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using EywaClient.Exceptions;
using EywaClient.GraphQL;
using EywaClient.Models;
using EywaClient.Utilities;

namespace EywaClient.Files;

/// <summary>
/// Handles file upload operations to EYWA file service.
/// 
/// IMPORTANT DESIGN NOTES:
/// - EYWA uses a 3-step protocol: request URL → upload to S3 → confirm
/// - EYWA does NOT return file info after upload (just confirms success)
/// - Upload methods return void/null on success, throw on failure
/// - User is responsible for providing complete file metadata via dictionary
/// - Users query folders first using FolderManager.GetFolderByPathAsync() to get folder UUIDs
/// - Root folder always exists at path "/" with UUID in FileConstants.RootFolderUuid
/// 
/// Required fields in fileData:
///   - "name": string (filename)
///   - "size": long (file size in bytes)
///   - "content_type": string (MIME type)
/// 
/// Optional fields:
///   - "euuid": Guid or string (for deduplication - if not provided, EYWA creates new file)
///   - "folder": object with { "euuid": Guid } (parent folder reference)
/// 
/// Example fileData:
/// new Dictionary&lt;string, object&gt; {
///     { "name", "report.pdf" },
///     { "size", 1024000 },
///     { "content_type", "application/pdf" },
///     { "euuid", Guid.NewGuid() },
///     { "folder", new { euuid = folderGuid } }
/// }
/// </summary>
public class FileUploader
{
    private readonly GraphQLClient _graphqlClient;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploader"/> class.
    /// </summary>
    /// <param name="graphqlClient">The GraphQL client for EYWA API calls</param>
    /// <param name="httpClient">The HTTP client for S3 uploads (optional, creates new if null)</param>
    public FileUploader(GraphQLClient graphqlClient, HttpClient? httpClient = null)
    {
        _graphqlClient = graphqlClient ?? throw new ArgumentNullException(nameof(graphqlClient));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Core upload method - uploads a stream to EYWA.
    /// All other upload methods delegate to this method.
    /// 
    /// Returns void on success, throws FileUploadException on failure.
    /// User is responsible for tracking uploaded file by euuid they provided.
    /// </summary>
    /// <param name="stream">Stream to upload (will not be disposed by this method)</param>
    /// <param name="fileData">
    /// Complete file metadata dictionary matching EYWA's FileInput GraphQL type.
    /// Required: "name", "size", "content_type"
    /// Optional: "euuid", "folder"
    /// </param>
    /// <param name="progressCallback">Optional progress callback (bytesUploaded, totalBytes)</param>
    /// <exception cref="ArgumentNullException">Thrown when stream or fileData is null</exception>
    /// <exception cref="ArgumentException">Thrown when required fields are missing</exception>
    /// <exception cref="FileUploadException">Thrown when upload fails</exception>
    /// <example>
    /// <code>
    /// // Upload with all metadata
    /// var fileData = new Dictionary&lt;string, object&gt; 
    /// {
    ///     { "name", "report.pdf" },
    ///     { "size", stream.Length },
    ///     { "content_type", "application/pdf" },
    ///     { "euuid", Guid.NewGuid() },
    ///     { "folder", new { euuid = folderGuid } }
    /// };
    /// 
    /// using var stream = File.OpenRead("report.pdf");
    /// await uploader.UploadStreamAsync(stream, fileData);
    /// // Success! File uploaded with provided euuid
    /// 
    /// // Upload without folder (orphan file)
    /// var fileData2 = new Dictionary&lt;string, object&gt; 
    /// {
    ///     { "name", "data.txt" },
    ///     { "size", 1024 },
    ///     { "content_type", "text/plain" },
    ///     { "euuid", Guid.NewGuid() }
    /// };
    /// await uploader.UploadStreamAsync(stream, fileData2);
    /// 
    /// // Upload without euuid (EYWA will create new file each time)
    /// var fileData3 = new Dictionary&lt;string, object&gt; 
    /// {
    ///     { "name", "temp.bin" },
    ///     { "size", 512 },
    ///     { "content_type", "application/octet-stream" }
    /// };
    /// await uploader.UploadStreamAsync(stream, fileData3);
    /// </code>
    /// </example>
    public async Task UploadStreamAsync(
        Stream stream,
        Dictionary<string, object> fileData,
        Action<long, long>? progressCallback = null)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (fileData == null)
            throw new ArgumentNullException(nameof(fileData));

        // Validate required fields
        ValidateFileData(fileData);

        var name = fileData["name"].ToString()!;
        var size = Convert.ToInt64(fileData["size"]);
        var contentType = fileData["content_type"].ToString()!;

        // Step 1: Request upload URL from EYWA
        var uploadUrl = await RequestUploadUrlAsync(fileData)
            .ConfigureAwait(false);

        // Step 2: Upload stream to S3
        await UploadToS3Async(stream, uploadUrl, size, contentType, progressCallback)
            .ConfigureAwait(false);

        // Step 3: Confirm upload with EYWA
        await ConfirmUploadAsync(uploadUrl)
            .ConfigureAwait(false);

        // Success! EYWA doesn't return file info, user tracks by euuid
    }

    /// <summary>
    /// Upload a file from disk to EYWA.
    /// Helper method that reads file and delegates to UploadStreamAsync.
    /// </summary>
    /// <param name="filePath">Local file path to upload</param>
    /// <param name="fileData">File metadata dictionary (size and content_type can be auto-filled)</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <exception cref="FileNotFoundException">Thrown when local file doesn't exist</exception>
    /// <example>
    /// <code>
    /// var fileData = new Dictionary&lt;string, object&gt; 
    /// {
    ///     { "name", "report.pdf" },
    ///     { "euuid", Guid.NewGuid() },
    ///     { "folder", new { euuid = folderGuid } }
    ///     // size and content_type auto-detected
    /// };
    /// await uploader.UploadFileAsync("C:\\reports\\report.pdf", fileData);
    /// </code>
    /// </example>
    public async Task UploadFileAsync(
        string filePath,
        Dictionary<string, object> fileData,
        Action<long, long>? progressCallback = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        // Auto-fill size if not provided
        if (!fileData.ContainsKey("size"))
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            fileData["size"] = fileInfo.Length;
        }

        // Auto-detect content_type if not provided
        if (!fileData.ContainsKey("content_type"))
        {
            fileData["content_type"] = MimeTypeHelper.GetMimeType(filePath);
        }

        using var fileStream = File.OpenRead(filePath);
        await UploadStreamAsync(fileStream, fileData, progressCallback)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Upload byte array content to EYWA.
    /// Helper method that creates MemoryStream and delegates to UploadStreamAsync.
    /// </summary>
    /// <param name="bytes">Byte array to upload</param>
    /// <param name="fileData">File metadata dictionary (size auto-filled)</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <example>
    /// <code>
    /// var bytes = Encoding.UTF8.GetBytes("Hello, EYWA!");
    /// var fileData = new Dictionary&lt;string, object&gt; 
    /// {
    ///     { "name", "greeting.txt" },
    ///     { "content_type", "text/plain" },
    ///     { "euuid", Guid.NewGuid() }
    ///     // size auto-filled from bytes.Length
    /// };
    /// await uploader.UploadBytesAsync(bytes, fileData);
    /// </code>
    /// </example>
    public async Task UploadBytesAsync(
        byte[] bytes,
        Dictionary<string, object> fileData,
        Action<long, long>? progressCallback = null)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        // Auto-fill size
        fileData["size"] = bytes.Length;

        using var memStream = new MemoryStream(bytes, false);
        await UploadStreamAsync(memStream, fileData, progressCallback)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Upload string content to EYWA.
    /// Helper method that converts to bytes and delegates to UploadBytesAsync.
    /// </summary>
    /// <param name="content">String content to upload</param>
    /// <param name="fileData">File metadata dictionary (size and content_type auto-filled)</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <example>
    /// <code>
    /// var fileData = new Dictionary&lt;string, object&gt; 
    /// {
    ///     { "name", "data.json" },
    ///     { "euuid", Guid.NewGuid() }
    ///     // size and content_type auto-filled
    /// };
    /// await uploader.UploadTextAsync("{\"hello\": \"world\"}", fileData);
    /// </code>
    /// </example>
    public async Task UploadTextAsync(
        string content,
        Dictionary<string, object> fileData,
        Action<long, long>? progressCallback = null)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        // Auto-fill content_type if not provided
        if (!fileData.ContainsKey("content_type"))
        {
            fileData["content_type"] = "text/plain";
        }

        await UploadBytesAsync(bytes, fileData, progressCallback)
            .ConfigureAwait(false);
    }

    // Private helper methods

    private void ValidateFileData(Dictionary<string, object> fileData)
    {
        if (!fileData.ContainsKey("name"))
            throw new ArgumentException("fileData must contain 'name' field", nameof(fileData));
        
        if (!fileData.ContainsKey("size"))
            throw new ArgumentException("fileData must contain 'size' field", nameof(fileData));
        
        if (!fileData.ContainsKey("content_type"))
            throw new ArgumentException("fileData must contain 'content_type' field", nameof(fileData));
    }

    private async Task<string> RequestUploadUrlAsync(Dictionary<string, object> fileData)
    {
        const string query = @"
            mutation RequestUpload($file: FileInput!) {
                requestUploadURL(file: $file)
            }";

        var response = await _graphqlClient.ExecuteAsync<GraphQL.CommonTypes.RequestUploadResponse>(
            query,
            new { file = fileData }
        ).ConfigureAwait(false);

        // Extract the upload URL from the strongly-typed response
        if (response.Data == null || string.IsNullOrEmpty(response.Data.RequestUploadURL))
            throw new FileUploadException("Failed to get upload URL from EYWA");

        return response.Data.RequestUploadURL;
    }

    private async Task UploadToS3Async(
        Stream stream,
        string uploadUrl,
        long totalBytes,
        string contentType,
        Action<long, long>? progressCallback)
    {
        // Report initial progress
        progressCallback?.Invoke(0, totalBytes);

        // Create progress-tracking stream wrapper if callback provided
        Stream uploadStream = stream;
        if (progressCallback != null)
        {
            uploadStream = new ProgressStream(stream, totalBytes, progressCallback);
        }

        try
        {
            var content = new StreamContent(uploadStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Headers.ContentLength = totalBytes;

            var response = await _httpClient.PutAsync(uploadUrl, content)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);
                throw new FileUploadException(
                    $"S3 upload failed with status {response.StatusCode}: {errorBody}");
            }

            // Report completion
            progressCallback?.Invoke(totalBytes, totalBytes);
        }
        catch (HttpRequestException ex)
        {
            throw new FileUploadException($"S3 upload failed: {ex.Message}", ex);
        }
        finally
        {
            if (uploadStream != stream)
            {
                uploadStream.Dispose();
            }
        }
    }

    private async Task ConfirmUploadAsync(string uploadUrl)
    {
        const string query = @"
            mutation ConfirmUpload($url: String!) {
                confirmFileUpload(url: $url)
            }";

        var response = await _graphqlClient.ExecuteAsync<GraphQL.CommonTypes.ConfirmUploadResponse>(
            query,
            new { url = uploadUrl }
        ).ConfigureAwait(false);

        if (response.Data == null || !response.Data.ConfirmFileUpload)
            throw new FileUploadException("Upload confirmation failed");
    }

    // Helper class for progress tracking
    private class ProgressStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _totalBytes;
        private readonly Action<long, long> _progressCallback;
        private long _bytesProcessed;

        public ProgressStream(Stream baseStream, long totalBytes, Action<long, long> progressCallback)
        {
            _baseStream = baseStream;
            _totalBytes = totalBytes;
            _progressCallback = progressCallback;
            _bytesProcessed = 0;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _totalBytes;
        public override long Position
        {
            get => _bytesProcessed;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _baseStream.Read(buffer, offset, count);
            if (bytesRead > 0)
            {
                _bytesProcessed += bytesRead;
                _progressCallback?.Invoke(_bytesProcessed, _totalBytes);
            }
            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, 
            System.Threading.CancellationToken cancellationToken)
        {
            var bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead > 0)
            {
                _bytesProcessed += bytesRead;
                _progressCallback?.Invoke(_bytesProcessed, _totalBytes);
            }
            return bytesRead;
        }

        public override void Flush() => _baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            // Don't dispose the base stream - let the caller handle that
            base.Dispose(disposing);
        }
    }
}