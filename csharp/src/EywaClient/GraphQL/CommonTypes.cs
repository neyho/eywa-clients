using System.Text.Json.Serialization;

namespace EywaClient.GraphQL;

/// <summary>
/// Common GraphQL response types for EYWA file operations.
/// </summary>
public static class CommonTypes
{
    /// <summary>
    /// Response for requestUploadURL mutation.
    /// </summary>
    public class RequestUploadResponse
    {
        /// <summary>
        /// The presigned S3 upload URL.
        /// </summary>
        [JsonPropertyName("requestUploadURL")]
        public string RequestUploadURL { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response for confirmFileUpload mutation.
    /// </summary>
    public class ConfirmUploadResponse
    {
        /// <summary>
        /// True if upload was confirmed successfully.
        /// </summary>
        [JsonPropertyName("confirmFileUpload")]
        public bool ConfirmFileUpload { get; set; }
    }

    /// <summary>
    /// Response for requestDownloadURL query.
    /// </summary>
    public class RequestDownloadResponse
    {
        /// <summary>
        /// The presigned S3 download URL.
        /// </summary>
        [JsonPropertyName("requestDownloadURL")]
        public string RequestDownloadURL { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response for searchFile query.
    /// </summary>
    public class SearchFileResponse
    {
        /// <summary>
        /// List of files matching the search criteria.
        /// </summary>
        [JsonPropertyName("searchFile")]
        public List<Models.FileInfo> SearchFile { get; set; } = new();
    }

    /// <summary>
    /// Response for getFile query.
    /// </summary>
    public class GetFileResponse
    {
        /// <summary>
        /// The requested file.
        /// </summary>
        [JsonPropertyName("getFile")]
        public Models.FileInfo? GetFile { get; set; }
    }

    /// <summary>
    /// Response for stackFolder mutation.
    /// </summary>
    public class StackFolderResponse
    {
        /// <summary>
        /// The created or updated folder.
        /// </summary>
        [JsonPropertyName("stackFolder")]
        public Models.FolderInfo? StackFolder { get; set; }
    }

    /// <summary>
    /// Response for getFolder query.
    /// </summary>
    public class GetFolderResponse
    {
        /// <summary>
        /// The requested folder.
        /// </summary>
        [JsonPropertyName("getFolder")]
        public Models.FolderInfo? GetFolder { get; set; }
    }

    /// <summary>
    /// Response for deleteFile mutation.
    /// </summary>
    public class DeleteFileResponse
    {
        /// <summary>
        /// True if file was deleted successfully.
        /// </summary>
        [JsonPropertyName("deleteFile")]
        public bool DeleteFile { get; set; }
    }

    /// <summary>
    /// Response for deleteFolder mutation.
    /// </summary>
    public class DeleteFolderResponse
    {
        /// <summary>
        /// True if folder was deleted successfully.
        /// </summary>
        [JsonPropertyName("deleteFolder")]
        public bool DeleteFolder { get; set; }
    }
}

/// <summary>
/// Common GraphQL filter operators for EYWA queries.
/// </summary>
/// <remarks>
/// Use these in your GraphQL queries to filter results.
/// Example: _where: { name: { _eq: "example" } }
/// </remarks>
public static class FilterOperators
{
    /// <summary>
    /// Equal to (_eq)
    /// </summary>
    public const string Equal = "_eq";

    /// <summary>
    /// Not equal to (_neq)
    /// </summary>
    public const string NotEqual = "_neq";

    /// <summary>
    /// Greater than (_gt)
    /// </summary>
    public const string GreaterThan = "_gt";

    /// <summary>
    /// Greater than or equal to (_gte)
    /// </summary>
    public const string GreaterThanOrEqual = "_gte";

    /// <summary>
    /// Less than (_lt)
    /// </summary>
    public const string LessThan = "_lt";

    /// <summary>
    /// Less than or equal to (_lte)
    /// </summary>
    public const string LessThanOrEqual = "_lte";

    /// <summary>
    /// Like pattern matching (_like)
    /// </summary>
    public const string Like = "_like";

    /// <summary>
    /// Case-insensitive like (_ilike)
    /// </summary>
    public const string ILike = "_ilike";

    /// <summary>
    /// In array (_in)
    /// </summary>
    public const string In = "_in";

    /// <summary>
    /// Not in array (_nin)
    /// </summary>
    public const string NotIn = "_nin";

    /// <summary>
    /// Is null (_is_null)
    /// </summary>
    public const string IsNull = "_is_null";

    /// <summary>
    /// Logical AND (_and)
    /// </summary>
    public const string And = "_and";

    /// <summary>
    /// Logical OR (_or)
    /// </summary>
    public const string Or = "_or";

    /// <summary>
    /// Logical NOT (_not)
    /// </summary>
    public const string Not = "_not";
}

/// <summary>
/// Common GraphQL query modifiers for EYWA queries.
/// </summary>
public static class QueryModifiers
{
    /// <summary>
    /// Where clause (_where)
    /// </summary>
    public const string Where = "_where";

    /// <summary>
    /// Order by clause (_order_by)
    /// </summary>
    public const string OrderBy = "_order_by";

    /// <summary>
    /// Limit (_limit)
    /// </summary>
    public const string Limit = "_limit";

    /// <summary>
    /// Offset (_offset)
    /// </summary>
    public const string Offset = "_offset";

    /// <summary>
    /// Distinct on (_distinct_on)
    /// </summary>
    public const string DistinctOn = "_distinct_on";
}

/// <summary>
/// Example GraphQL query templates for common EYWA operations.
/// </summary>
/// <remarks>
/// These are reference examples. In practice, write queries directly
/// as strings for maximum flexibility.
/// </remarks>
public static class QueryExamples
{
    /// <summary>
    /// Example: Search files with filters
    /// </summary>
    public const string SearchFiles = @"
        query SearchFiles($where: FileFilter) {
            searchFile(_where: $where) {
                euuid
                name
                content_type
                size
                status
                uploaded_at
                folder {
                    path
                }
            }
        }
    ";

    /// <summary>
    /// Example: Get file by UUID
    /// </summary>
    public const string GetFile = @"
        query GetFile($id: UUID!) {
            getFile(euuid: $id) {
                euuid
                name
                content_type
                size
                status
                uploaded_at
                folder {
                    euuid
                    name
                    path
                }
            }
        }
    ";

    /// <summary>
    /// Example: Request upload URL
    /// </summary>
    public const string RequestUploadURL = @"
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
    ";

    /// <summary>
    /// Example: Confirm file upload
    /// </summary>
    public const string ConfirmUpload = @"
        mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }
    ";

    /// <summary>
    /// Example: Request download URL
    /// </summary>
    public const string RequestDownloadURL = @"
        query RequestDownload($file: FileInput!) {
            requestDownloadURL(file: $file)
        }
    ";

    /// <summary>
    /// Example: Create/update folder
    /// </summary>
    public const string StackFolder = @"
        mutation CreateFolder($folder: FolderInput!) {
            stackFolder(data: $folder) {
                euuid
                name
                path
                parent {
                    euuid
                }
            }
        }
    ";

    /// <summary>
    /// Example: Get folder by path
    /// </summary>
    public const string GetFolder = @"
        query GetFolder($path: String!) {
            getFolder(path: $path) {
                euuid
                name
                path
                parent {
                    euuid
                    path
                }
            }
        }
    ";

    /// <summary>
    /// Example: Delete file
    /// </summary>
    public const string DeleteFile = @"
        mutation DeleteFile($id: UUID!) {
            deleteFile(euuid: $id)
        }
    ";

    /// <summary>
    /// Example: Delete folder
    /// </summary>
    public const string DeleteFolder = @"
        mutation DeleteFolder($path: String!) {
            deleteFolder(path: $path)
        }
    ";
}
