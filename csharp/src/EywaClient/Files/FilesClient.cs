/// <summary>
/// EYWA Files Client - GraphQL-aligned file operations
/// 
/// Implements the 8 core functions from FILES_SPEC.md:
/// - upload, uploadStream, uploadContent
/// - download, downloadStream  
/// - createFolder, deleteFile, deleteFolder
/// 
/// Uses single map arguments that mirror GraphQL schema exactly.
/// All data interchange uses Dictionary&lt;string, object&gt; to stay as close 
/// to GraphQL as possible.
/// </summary>

using System.Text;
using System.Text.Json;
using EywaClient.Core;
using EywaClient.Utils;

namespace EywaClient.Files;

/// <summary>
/// Stream result for downloads
/// </summary>
public class StreamResult
{
    public Stream Stream { get; set; } = null!;
    public long ContentLength { get; set; }
}

/// <summary>
/// GraphQL-aligned files client
/// </summary>
public class FilesClient
{
    private readonly JsonRpcClient _jsonRpcClient;
    
    public FilesClient(JsonRpcClient jsonRpcClient)
    {
        _jsonRpcClient = jsonRpcClient;
    }
    
    /// <summary>
    /// Root folder UUID constant
    /// </summary>
    public string RootUuid => FileConstants.RootUuid;
    
    /// <summary>
    /// Root folder object for GraphQL operations
    /// </summary>
    public Dictionary<string, object> RootFolder => FileConstants.RootFolder;
    
    // ========================================================================
    // Upload Operations (Protocol Abstraction)
    // ========================================================================
    
    /// <summary>
    /// Upload a file from local filesystem to EYWA using the 3-step protocol
    /// </summary>
    public async Task UploadAsync(string filepath, Dictionary<string, object> fileData)
    {
        try
        {
            // Detect file information
            var fileInfo = new FileInfo(filepath);
            if (!fileInfo.Exists)
                throw new FileUploadError($"File not found: {filepath}");
                
            var fileName = fileData.GetValueOrDefault("name")?.ToString() ?? fileInfo.Name;
            var fileSize = fileInfo.Length;
            var contentType = fileData.GetValueOrDefault("content_type")?.ToString() ?? 
                             MimeTypeDetector.DetectMimeType(fileName);
            
            // Extract progress callback if provided
            var progressFn = fileData.GetValueOrDefault("progressFn") as HttpS3Client.ProgressCallback;
            
            // Step 1: Request upload URL
            var fileInput = new Dictionary<string, object>(fileData);
            fileInput["name"] = fileName;
            fileInput["content_type"] = contentType;
            fileInput["size"] = fileSize;
            fileInput.Remove("progressFn"); // Remove non-GraphQL field
            
            var uploadUrl = await RequestUploadUrl(fileInput);
            
            // Step 2: Stream file to S3
            using var fileStream = fileInfo.OpenRead();
            var uploadResult = await HttpS3Client.PutStreamAsync(uploadUrl, fileStream, fileSize, contentType, progressFn);
            
            if (!uploadResult.IsSuccess)
            {
                throw new FileUploadError(
                    $"S3 upload failed ({uploadResult.StatusCode}): {uploadResult.Message}",
                    code: uploadResult.StatusCode);
            }
            
            // Step 3: Confirm upload
            await ConfirmUpload(uploadUrl);
        }
        catch (FileUploadError)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileUploadError(ex.Message);
        }
    }
    
    /// <summary>
    /// Upload from a stream to EYWA
    /// </summary>
    public async Task UploadStreamAsync(Stream inputStream, Dictionary<string, object> fileData)
    {
        try
        {
            var name = fileData.GetValueOrDefault("name")?.ToString() ?? 
                      throw new FileUploadError("name is required for stream uploads");
            var size = Convert.ToInt64(fileData.GetValueOrDefault("size") ?? 
                      throw new FileUploadError("size is required for stream uploads"));
            var contentType = fileData.GetValueOrDefault("content_type")?.ToString() ?? 
                             MimeTypeDetector.DetectMimeType(name);
            
            // Extract progress callback if provided
            var progressFn = fileData.GetValueOrDefault("progressFn") as HttpS3Client.ProgressCallback;
            
            // Step 1: Request upload URL
            var fileInput = new Dictionary<string, object>(fileData);
            fileInput["name"] = name;
            fileInput["content_type"] = contentType;
            fileInput["size"] = size;
            fileInput.Remove("progressFn"); // Remove non-GraphQL field
            
            var uploadUrl = await RequestUploadUrl(fileInput);
            
            // Step 2: Stream to S3
            var uploadResult = await HttpS3Client.PutStreamAsync(uploadUrl, inputStream, size, contentType, progressFn);
            
            if (!uploadResult.IsSuccess)
            {
                throw new FileUploadError(
                    $"S3 upload failed ({uploadResult.StatusCode}): {uploadResult.Message}",
                    code: uploadResult.StatusCode);
            }
            
            // Step 3: Confirm upload
            await ConfirmUpload(uploadUrl);
        }
        catch (FileUploadError)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileUploadError(ex.Message);
        }
    }
    
    /// <summary>
    /// Upload string or binary content directly
    /// </summary>
    public async Task UploadContentAsync(object content, Dictionary<string, object> fileData)
    {
        try
        {
            var name = fileData.GetValueOrDefault("name")?.ToString() ?? 
                      throw new FileUploadError("name is required for content uploads");
            var contentType = fileData.GetValueOrDefault("content_type")?.ToString() ?? "text/plain";
            
            // Convert content to byte array
            byte[] contentBytes;
            if (content is byte[] bytes)
            {
                contentBytes = bytes;
            }
            else if (content is string str)
            {
                contentBytes = Encoding.UTF8.GetBytes(str);
            }
            else
            {
                throw new FileUploadError("Content must be string or byte array");
            }
            
            // Extract progress callback if provided
            var progressFn = fileData.GetValueOrDefault("progressFn") as HttpS3Client.ProgressCallback;
            
            // Step 1: Request upload URL
            var fileInput = new Dictionary<string, object>(fileData);
            fileInput["name"] = name;
            fileInput["content_type"] = contentType;
            fileInput["size"] = contentBytes.Length;
            fileInput.Remove("progressFn"); // Remove non-GraphQL field
            
            var uploadUrl = await RequestUploadUrl(fileInput);
            
            // Step 2: Upload content to S3
            var uploadResult = await HttpS3Client.PutAsync(uploadUrl, contentBytes, contentType, progressFn);
            
            if (!uploadResult.IsSuccess)
            {
                throw new FileUploadError(
                    $"S3 upload failed ({uploadResult.StatusCode}): {uploadResult.Message}",
                    code: uploadResult.StatusCode);
            }
            
            // Step 3: Confirm upload
            await ConfirmUpload(uploadUrl);
        }
        catch (FileUploadError)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileUploadError(ex.Message);
        }
    }
    
    // ========================================================================
    // Download Operations (Protocol Abstraction)
    // ========================================================================
    
    /// <summary>
    /// Download file as a stream
    /// </summary>
    public async Task<StreamResult> DownloadStreamAsync(string fileUuid)
    {
        try
        {
            // Step 1: Request download URL
            var downloadUrl = await RequestDownloadUrl(fileUuid);
            
            // Step 2: Get stream from S3
            var result = await HttpS3Client.GetStreamAsync(downloadUrl);
            
            if (!result.IsSuccess)
            {
                throw new FileDownloadError(
                    $"Download failed ({result.StatusCode}): {result.Message}",
                    code: result.StatusCode);
            }
            
            return new StreamResult
            {
                Stream = result.Stream!,
                ContentLength = result.ContentLength
            };
        }
        catch (FileDownloadError)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileDownloadError(ex.Message);
        }
    }
    
    /// <summary>
    /// Download file as complete byte array
    /// </summary>
    public async Task<byte[]> DownloadAsync(string fileUuid)
    {
        try
        {
            var streamResult = await DownloadStreamAsync(fileUuid);
            
            using var memoryStream = new MemoryStream();
            await streamResult.Stream.CopyToAsync(memoryStream);
            streamResult.Stream.Dispose();
            
            return memoryStream.ToArray();
        }
        catch (FileDownloadError)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileDownloadError(ex.Message);
        }
    }
    
    // ========================================================================
    // Simple CRUD Operations
    // ========================================================================
    
    /// <summary>
    /// Create a new folder
    /// </summary>
    public async Task<Dictionary<string, object>> CreateFolderAsync(Dictionary<string, object> folderData)
    {
        try
        {
            var response = await _jsonRpcClient.SendRequestAsync("eywa.datasets.graphql", new Dictionary<string, object>
            {
                ["query"] = @"
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
                    }",
                ["variables"] = new Dictionary<string, object>
                {
                    ["folder"] = folderData
                }
            });
            
            var result = ExtractGraphQLResult(response);
            return (result["stackFolder"] as Dictionary<string, object>) ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create folder: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Delete a file from EYWA
    /// </summary>
    public async Task<bool> DeleteFileAsync(string fileUuid)
    {
        try
        {
            var response = await _jsonRpcClient.SendRequestAsync("eywa.datasets.graphql", new Dictionary<string, object>
            {
                ["query"] = @"
                    mutation DeleteFile($uuid: UUID!) {
                        deleteFile(euuid: $uuid)
                    }",
                ["variables"] = new Dictionary<string, object>
                {
                    ["uuid"] = fileUuid
                }
            });
            
            var result = ExtractGraphQLResult(response);
            return Convert.ToBoolean(result.GetValueOrDefault("deleteFile"));
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete file: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Delete an empty folder
    /// </summary>
    public async Task<bool> DeleteFolderAsync(string folderUuid)
    {
        try
        {
            var response = await _jsonRpcClient.SendRequestAsync("eywa.datasets.graphql", new Dictionary<string, object>
            {
                ["query"] = @"
                    mutation DeleteFolder($uuid: UUID!) {
                        deleteFolder(euuid: $uuid)
                    }",
                ["variables"] = new Dictionary<string, object>
                {
                    ["uuid"] = folderUuid
                }
            });
            
            var result = ExtractGraphQLResult(response);
            return Convert.ToBoolean(result.GetValueOrDefault("deleteFolder"));
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete folder: {ex.Message}");
        }
    }
    
    // ========================================================================
    // Internal Protocol Helpers
    // ========================================================================
    
    /// <summary>
    /// Request upload URL from EYWA GraphQL API
    /// </summary>
    private async Task<string> RequestUploadUrl(Dictionary<string, object> fileInput)
    {
        var response = await _jsonRpcClient.SendRequestAsync("eywa.datasets.graphql", new Dictionary<string, object>
        {
            ["query"] = @"
                mutation RequestUpload($file: FileInput!) { 
                    requestUploadURL(file: $file)
                }",
            ["variables"] = new Dictionary<string, object>
            {
                ["file"] = fileInput
            }
        });
        
        var result = ExtractGraphQLResult(response);
        
        // Handle JsonElement for the URL value
        var urlValue = result.GetValueOrDefault("requestUploadURL");
        if (urlValue is JsonElement urlElement)
        {
            return urlElement.GetString() ?? throw new FileUploadError("No upload URL in response");
        }
        return urlValue?.ToString() ?? throw new FileUploadError("No upload URL in response");
    }
    
    /// <summary>
    /// Confirm upload completion to EYWA GraphQL API
    /// </summary>
    private async Task ConfirmUpload(string uploadUrl)
    {
        var response = await _jsonRpcClient.SendRequestAsync("eywa.datasets.graphql", new Dictionary<string, object>
        {
            ["query"] = @"
                mutation ConfirmUpload($url: String!) {
                    confirmFileUpload(url: $url)
                }",
            ["variables"] = new Dictionary<string, object>
            {
                ["url"] = uploadUrl
            }
        });
        
        var result = ExtractGraphQLResult(response);
        var confirmed = Convert.ToBoolean(result.GetValueOrDefault("confirmFileUpload"));
        
        if (!confirmed)
            throw new FileUploadError("Upload confirmation returned false");
    }
    
    /// <summary>
    /// Request download URL from EYWA GraphQL API
    /// </summary>
    private async Task<string> RequestDownloadUrl(string fileUuid)
    {
        var response = await _jsonRpcClient.SendRequestAsync("eywa.datasets.graphql", new Dictionary<string, object>
        {
            ["query"] = @"
                query RequestDownload($file: FileInput!) {
                    requestDownloadURL(file: $file)
                }",
            ["variables"] = new Dictionary<string, object>
            {
                ["file"] = new Dictionary<string, object>
                {
                    ["euuid"] = fileUuid
                }
            }
        });
        
        var result = ExtractGraphQLResult(response);
        return result.GetValueOrDefault("requestDownloadURL")?.ToString() ?? 
               throw new FileDownloadError("No download URL in response");
    }
    
    /// <summary>
    /// Extract GraphQL result and handle errors
    /// </summary>
    private Dictionary<string, object> ExtractGraphQLResult(Dictionary<string, object> response)
    {
        // Extract result from JSON-RPC response - handle JsonElement conversion
        var resultObj = response.GetValueOrDefault("result");
        Dictionary<string, object> result;
        
        if (resultObj is JsonElement jsonElement)
        {
            // Use JsonDocumentOptions to properly convert to native .NET types
            var resultJson = jsonElement.GetRawText();
            var jsonDoc = JsonDocument.Parse(resultJson);
            result = ConvertJsonElementToDictionary(jsonDoc.RootElement);
        }
        else if (resultObj is Dictionary<string, object> dict)
        {
            result = dict;
        }
        else
        {
            return new Dictionary<string, object>();
        }
        
        if (result.ContainsKey("error"))
        {
            throw new Exception($"GraphQL error: {JsonSerializer.Serialize(result["error"])}");
        }
        
        // Extract the data field - also handle JsonElement
        var dataObj = result.GetValueOrDefault("data");
        if (dataObj is JsonElement dataElement)
        {
            return ConvertJsonElementToDictionary(dataElement);
        }
        else if (dataObj is Dictionary<string, object> dataDict)
        {
            return dataDict;
        }
        
        return new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Convert JsonElement to Dictionary with native .NET types
    /// </summary>
    private static Dictionary<string, object> ConvertJsonElementToDictionary(JsonElement element)
    {
        var dictionary = new Dictionary<string, object>();
        
        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = ConvertJsonValue(property.Value);
        }
        
        return dictionary;
    }
    
    /// <summary>
    /// Convert JsonElement to appropriate .NET type
    /// </summary>
    private static object ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Object => ConvertJsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToArray(),
            _ => element.ToString()
        };
    }
}