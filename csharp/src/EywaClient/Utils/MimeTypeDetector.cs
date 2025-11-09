/**
 * MIME Type Detection Utility
 * 
 * Simple MIME type detection for file operations
 */

namespace EywaClient.Utils;

/// <summary>
/// Simple MIME type detector
/// </summary>
public static class MimeTypeDetector
{
    private static readonly Dictionary<string, string> MimeTypes = new()
    {
        // Text files
        { ".txt", "text/plain" },
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".json", "application/json" },
        { ".xml", "application/xml" },
        { ".csv", "text/csv" },
        
        // Images
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".bmp", "image/bmp" },
        { ".ico", "image/x-icon" },
        { ".webp", "image/webp" },
        
        // Documents
        { ".pdf", "application/pdf" },
        { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt", "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        
        // Archives
        { ".zip", "application/zip" },
        { ".rar", "application/vnd.rar" },
        { ".7z", "application/x-7z-compressed" },
        { ".tar", "application/x-tar" },
        { ".gz", "application/gzip" },
        
        // Media
        { ".mp3", "audio/mpeg" },
        { ".wav", "audio/wav" },
        { ".mp4", "video/mp4" },
        { ".avi", "video/x-msvideo" },
        { ".mov", "video/quicktime" },
        
        // Programming
        { ".cs", "text/plain" },
        { ".py", "text/plain" },
        { ".java", "text/plain" },
        { ".cpp", "text/plain" },
        { ".c", "text/plain" },
        { ".h", "text/plain" },
        { ".php", "text/plain" },
        { ".rb", "text/plain" },
        { ".go", "text/plain" },
        { ".rs", "text/plain" },
        { ".swift", "text/plain" },
        { ".kt", "text/plain" },
        { ".scala", "text/plain" },
        { ".clj", "text/plain" },
        { ".sh", "text/plain" },
        { ".bat", "text/plain" },
        { ".ps1", "text/plain" },
        { ".sql", "text/plain" },
        { ".md", "text/markdown" },
        { ".yml", "application/yaml" },
        { ".yaml", "application/yaml" },
        { ".toml", "application/toml" },
    };

    /// <summary>
    /// Detect MIME type from filename
    /// </summary>
    public static string DetectMimeType(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return "application/octet-stream";
            
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        
        if (MimeTypes.TryGetValue(extension, out var mimeType))
            return mimeType;
            
        return "application/octet-stream";
    }
}
