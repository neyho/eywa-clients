using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using EywaClient.Exceptions;
using EywaClient.GraphQL;

namespace EywaClient.Files;

/// <summary>
/// Handles file download operations from EYWA file service.
/// Uses a 2-step protocol: request URL → download from S3.
/// 
/// User provides file reference as dictionary with "euuid" field.
/// </summary>
public class FileDownloader
{
    private readonly GraphQLClient _graphqlClient;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDownloader"/> class.
    /// </summary>
    /// <param name="graphqlClient">The GraphQL client for EYWA API calls</param>
    /// <param name="httpClient">The HTTP client for S3 downloads (optional, creates new if null)</param>
    public FileDownloader(GraphQLClient graphqlClient, HttpClient? httpClient = null)
    {
        _graphqlClient = graphqlClient ?? throw new ArgumentNullException(nameof(graphqlClient));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Core download method - downloads a file as a stream (memory-efficient).
    /// The caller is responsible for disposing the returned stream.
    /// All other download methods delegate to this method.
    /// </summary>
    /// <param name="fileReference">Dictionary with "euuid" field identifying the file</param>
    /// <param name="progressCallback">Optional progress callback (bytesDownloaded, totalBytes)</param>
    /// <returns>Stream containing the file data (caller must dispose)</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileReference is null</exception>
    /// <exception cref="ArgumentException">Thrown when euuid is missing</exception>
    /// <exception cref="FileDownloadException">Thrown when download fails</exception>
    /// <example>
    /// <code>
    /// // Download by euuid
    /// var fileRef = new Dictionary&lt;string, object&gt; 
    /// {
    ///     { "euuid", fileGuid }
    /// };
    /// 
    /// using var stream = await downloader.DownloadStreamAsync(fileRef);
    /// using var reader = new StreamReader(stream);
    /// var content = await reader.ReadToEndAsync();
    /// 
    /// // With progress tracking
    /// using var stream = await downloader.DownloadStreamAsync(
    ///     fileRef,
    ///     (downloaded, total) => Console.WriteLine($"{downloaded}/{total}")
    /// );
    /// </code>
    /// </example>
    public async Task<Stream> DownloadStreamAsync(
        Dictionary<string, object> fileReference,
        Action<long, long>? progressCallback = null)
    {
        if (fileReference == null)
            throw new ArgumentNullException(nameof(fileReference));
        
        if (!fileReference.ContainsKey("euuid"))
            throw new ArgumentException("fileReference must contain 'euuid' field", nameof(fileReference));

        // Step 1: Request download URL from EYWA
        var downloadUrl = await RequestDownloadUrlAsync(fileReference)
            .ConfigureAwait(false);

        // Step 2: Download from S3 and return stream
        var stream = await DownloadFromS3Async(downloadUrl, progressCallback)
            .ConfigureAwait(false);

        return stream;
    }

    /// <summary>
    /// Download a file and save it to disk.
    /// </summary>
    /// <param name="fileReference">Dictionary with "euuid" field</param>
    /// <param name="savePath">Local path to save the file</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <returns>The local file path</returns>
    /// <example>
    /// <code>
    /// var fileRef = new Dictionary&lt;string, object&gt; { { "euuid", fileGuid } };
    /// var savedPath = await downloader.DownloadToFileAsync(
    ///     fileRef,
    ///     "C:\\downloads\\report.pdf"
    /// );
    /// </code>
    /// </example>
    public async Task<string> DownloadToFileAsync(
        Dictionary<string, object> fileReference,
        string savePath,
        Action<long, long>? progressCallback = null)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            throw new ArgumentNullException(nameof(savePath));

        // Ensure parent directory exists
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = await DownloadStreamAsync(fileReference, progressCallback)
            .ConfigureAwait(false);
        
        using var fileStream = File.Create(savePath);
        await stream.CopyToAsync(fileStream)
            .ConfigureAwait(false);

        return savePath;
    }

    /// <summary>
    /// Download a file as a byte array (for small files).
    /// Warning: This loads the entire file into memory. Use DownloadStreamAsync for large files.
    /// </summary>
    /// <param name="fileReference">Dictionary with "euuid" field</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <returns>Byte array containing the file data</returns>
    /// <example>
    /// <code>
    /// var fileRef = new Dictionary&lt;string, object&gt; { { "euuid", fileGuid } };
    /// var bytes = await downloader.DownloadBytesAsync(fileRef);
    /// </code>
    /// </example>
    public async Task<byte[]> DownloadBytesAsync(
        Dictionary<string, object> fileReference,
        Action<long, long>? progressCallback = null)
    {
        using var stream = await DownloadStreamAsync(fileReference, progressCallback)
            .ConfigureAwait(false);
        
        using var memStream = new MemoryStream();
        await stream.CopyToAsync(memStream)
            .ConfigureAwait(false);

        return memStream.ToArray();
    }

    /// <summary>
    /// Download a file as text (for text files).
    /// Warning: This loads the entire file into memory. Use DownloadStreamAsync for large files.
    /// </summary>
    /// <param name="fileReference">Dictionary with "euuid" field</param>
    /// <param name="progressCallback">Optional progress callback</param>
    /// <returns>String containing the file content</returns>
    /// <example>
    /// <code>
    /// var fileRef = new Dictionary&lt;string, object&gt; { { "euuid", fileGuid } };
    /// var text = await downloader.DownloadTextAsync(fileRef);
    /// Console.WriteLine(text);
    /// </code>
    /// </example>
    public async Task<string> DownloadTextAsync(
        Dictionary<string, object> fileReference,
        Action<long, long>? progressCallback = null)
    {
        using var stream = await DownloadStreamAsync(fileReference, progressCallback)
            .ConfigureAwait(false);
        
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync()
            .ConfigureAwait(false);
    }

    // Private helper methods

    private async Task<string> RequestDownloadUrlAsync(Dictionary<string, object> fileReference)
    {
        const string query = @"
            query RequestDownload($file: FileInput!) {
                requestDownloadURL(file: $file)
            }";

        var response = await _graphqlClient.ExecuteAsync<GraphQL.CommonTypes.RequestDownloadResponse>(
            query,
            new { file = fileReference }
        ).ConfigureAwait(false);

        if (response.Data == null || string.IsNullOrEmpty(response.Data.RequestDownloadURL))
            throw new FileDownloadException("Failed to get download URL from EYWA");

        return response.Data.RequestDownloadURL;
    }

    private async Task<Stream> DownloadFromS3Async(
        string downloadUrl,
        Action<long, long>? progressCallback)
    {
        try
        {
            // Use ResponseHeadersRead to start streaming immediately without buffering
            var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new FileDownloadException(
                    $"S3 download failed with status {response.StatusCode}");
            }

            var contentLength = response.Content.Headers.ContentLength ?? 0;
            var stream = await response.Content.ReadAsStreamAsync()
                .ConfigureAwait(false);

            // Wrap with progress tracking if callback provided
            if (progressCallback != null)
            {
                stream = new ProgressStream(stream, contentLength, progressCallback);
            }

            return stream;
        }
        catch (HttpRequestException ex)
        {
            throw new FileDownloadException($"S3 download failed: {ex.Message}", ex);
        }
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
            
            // Report initial progress
            _progressCallback?.Invoke(0, _totalBytes);
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _totalBytes > 0 ? _totalBytes : _baseStream.Length;
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
        
        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        
        public override void SetLength(long value) => throw new NotSupportedException();
        
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}