using System.Text.Json.Serialization;

namespace EywaClient.Models;

/// <summary>
/// Represents information about a file in EYWA.
/// </summary>
public class FileInfo
{
    /// <summary>
    /// The file UUID.
    /// </summary>
    [JsonPropertyName("euuid")]
    public string Euuid { get; set; } = string.Empty;

    /// <summary>
    /// The file name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The MIME content type.
    /// </summary>
    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// The file size in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// The file status (e.g., "UPLOADED").
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// The upload timestamp.
    /// </summary>
    [JsonPropertyName("uploaded_at")]
    public DateTime? UploadedAt { get; set; }

    /// <summary>
    /// The folder this file belongs to.
    /// </summary>
    [JsonPropertyName("folder")]
    public FolderInfo? Folder { get; set; }
}

/// <summary>
/// Represents information about a folder in EYWA.
/// </summary>
public class FolderInfo
{
    /// <summary>
    /// The folder UUID.
    /// </summary>
    [JsonPropertyName("euuid")]
    public string Euuid { get; set; } = string.Empty;

    /// <summary>
    /// The folder name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The full folder path (e.g., "/documents/2024/").
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The parent folder.
    /// </summary>
    [JsonPropertyName("parent")]
    public FolderInfo? Parent { get; set; }
}

/// <summary>
/// Options for uploading files.
/// </summary>
public class UploadOptions
{
    /// <summary>
    /// The file name. Required if eywaPath is null (orphan file).
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// The stream size in bytes. Required for non-seekable streams.
    /// Auto-detected for seekable streams (e.g., FileStream).
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// The MIME content type. Auto-detected from path/filename if not provided.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Whether to automatically create missing folders in the path.
    /// Default is true.
    /// </summary>
    public bool CreateFolders { get; set; } = true;

    /// <summary>
    /// Optional callback for progress tracking during upload.
    /// Parameters: (bytesUploaded, totalBytes)
    /// </summary>
    public Action<long, long>? ProgressCallback { get; set; }
}

/// <summary>
/// Options for downloading files.
/// </summary>
public class DownloadOptions
{
    /// <summary>
    /// Optional callback for progress tracking during download.
    /// Parameters: (bytesDownloaded, totalBytes)
    /// </summary>
    public Action<long, long>? ProgressCallback { get; set; }
}
