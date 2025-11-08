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
/// Input for file upload operations matching EYWA's FileInput GraphQL type.
/// IMPORTANT: You must provide euuid for deduplication.
/// If you upload the same file twice with the same euuid, EYWA will update it.
/// If you don't provide euuid, EYWA will create a new file each time.
/// </summary>
public class FileInput
{
    /// <summary>
    /// The file name (required).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The file UUID (required for proper deduplication).
    /// Generate with Guid.NewGuid() for new files.
    /// Use existing UUID to update a file.
    /// </summary>
    [JsonPropertyName("euuid")]
    public Guid Euuid { get; set; }

    /// <summary>
    /// The file size in bytes (required).
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// The MIME content type (optional, will be auto-detected if not provided).
    /// </summary>
    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    /// <summary>
    /// The parent folder reference (optional).
    /// Must provide folder's euuid if specified.
    /// </summary>
    [JsonPropertyName("folder")]
    public FolderReference? Folder { get; set; }
}

/// <summary>
/// Reference to a folder by UUID.
/// </summary>
public class FolderReference
{
    /// <summary>
    /// The folder UUID.
    /// </summary>
    [JsonPropertyName("euuid")]
    public Guid Euuid { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderReference"/> class.
    /// </summary>
    /// <param name="euuid">The unique identifier for the folder.</param>
    public FolderReference(Guid euuid)
    {
        Euuid = euuid;
    }
}

/// <summary>
/// Options for file upload operations.
/// </summary>
public class UploadOptions
{
    /// <summary>
    /// Optional callback for progress tracking during upload.
    /// Parameters: (bytesUploaded, totalBytes)
    /// </summary>
    public Action<long, long>? ProgressCallback { get; set; }
}

/// <summary>
/// Options for file download operations.
/// </summary>
public class DownloadOptions
{
    /// <summary>
    /// Optional callback for progress tracking during download.
    /// Parameters: (bytesDownloaded, totalBytes)
    /// </summary>
    public Action<long, long>? ProgressCallback { get; set; }
}