/**
 * HTTP S3 Client Utilities
 * 
 * HTTP utilities for S3 operations with proper Content-Length handling
 * following S3 protocol requirements from FILES_SPEC.md
 */

using System.Text;

namespace EywaClient.Utils;

/// <summary>
/// HTTP utilities for S3 operations
/// </summary>
public static class HttpS3Client
{
    private static readonly HttpClient _httpClient = new();
    
    /// <summary>
    /// Upload progress callback
    /// </summary>
    public delegate void ProgressCallback(long bytesTransferred, long totalBytes);
    
    /// <summary>
    /// Result of HTTP operation
    /// </summary>
    public class HttpResult
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public long ContentLength { get; set; }
        public Stream? Stream { get; set; }
    }
    
    /// <summary>
    /// Perform HTTP PUT to S3 with buffer content
    /// </summary>
    public static async Task<HttpResult> PutAsync(string url, byte[] data, string contentType, ProgressCallback? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke(0, data.Length);
            
            using var content = new ByteArrayContent(data);
            content.Headers.Add("Content-Type", contentType);
            content.Headers.Add("Content-Length", data.Length.ToString());
            
            var response = await _httpClient.PutAsync(url, content);
            
            progressCallback?.Invoke(data.Length, data.Length);
            
            return new HttpResult
            {
                IsSuccess = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Message = response.IsSuccessStatusCode ? null : await response.Content.ReadAsStringAsync()
            };
        }
        catch (Exception ex)
        {
            return new HttpResult
            {
                IsSuccess = false,
                StatusCode = 0,
                Message = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Perform HTTP PUT to S3 with stream content
    /// Note: Reads entire stream into memory to avoid chunked transfer encoding
    /// which is incompatible with S3 PUT operations
    /// </summary>
    public static async Task<HttpResult> PutStreamAsync(string url, Stream inputStream, long contentLength, string contentType, ProgressCallback? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke(0, contentLength);
            
            // Read entire stream into buffer to avoid chunked encoding
            var buffer = new byte[contentLength];
            var totalRead = 0;
            int bytesRead;
            
            while (totalRead < contentLength && (bytesRead = await inputStream.ReadAsync(buffer, totalRead, (int)(contentLength - totalRead))) > 0)
            {
                totalRead += bytesRead;
                progressCallback?.Invoke(totalRead, contentLength);
            }
            
            using var content = new ByteArrayContent(buffer, 0, totalRead);
            content.Headers.Add("Content-Type", contentType);
            content.Headers.Add("Content-Length", totalRead.ToString());
            
            var response = await _httpClient.PutAsync(url, content);
            
            return new HttpResult
            {
                IsSuccess = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Message = response.IsSuccessStatusCode ? null : await response.Content.ReadAsStringAsync()
            };
        }
        catch (Exception ex)
        {
            return new HttpResult
            {
                IsSuccess = false,
                StatusCode = 0,
                Message = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Perform HTTP GET and return stream
    /// </summary>
    public static async Task<HttpResult> GetStreamAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            
            if (response.IsSuccessStatusCode)
            {
                var contentLength = response.Content.Headers.ContentLength ?? 0;
                var stream = await response.Content.ReadAsStreamAsync();
                
                return new HttpResult
                {
                    IsSuccess = true,
                    StatusCode = (int)response.StatusCode,
                    ContentLength = contentLength,
                    Stream = stream
                };
            }
            else
            {
                return new HttpResult
                {
                    IsSuccess = false,
                    StatusCode = (int)response.StatusCode,
                    Message = await response.Content.ReadAsStringAsync()
                };
            }
        }
        catch (Exception ex)
        {
            return new HttpResult
            {
                IsSuccess = false,
                StatusCode = 0,
                Message = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Perform HTTP GET and return content as byte array
    /// </summary>
    public static async Task<HttpResult> GetBytesAsync(string url)
    {
        var streamResult = await GetStreamAsync(url);
        
        if (!streamResult.IsSuccess || streamResult.Stream == null)
            return streamResult;
            
        try
        {
            using var memoryStream = new MemoryStream();
            await streamResult.Stream.CopyToAsync(memoryStream);
            streamResult.Stream.Dispose();
            
            return new HttpResult
            {
                IsSuccess = true,
                StatusCode = streamResult.StatusCode,
                ContentLength = memoryStream.Length,
                Stream = new MemoryStream(memoryStream.ToArray())
            };
        }
        catch (Exception ex)
        {
            streamResult.Stream?.Dispose();
            return new HttpResult
            {
                IsSuccess = false,
                StatusCode = 0,
                Message = ex.Message
            };
        }
    }
}
