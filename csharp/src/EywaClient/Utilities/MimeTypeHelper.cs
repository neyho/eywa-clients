using MimeMapping;

namespace EywaClient.Utilities;

/// <summary>
/// Provides MIME type detection for files.
/// </summary>
public static class MimeTypeHelper
{
    /// <summary>
    /// Get MIME type from file path or extension.
    /// Falls back to "application/octet-stream" if type cannot be determined.
    /// </summary>
    /// <param name="filePath">File path or name</param>
    /// <returns>MIME type string</returns>
    /// <example>
    /// <code>
    /// var mimeType = MimeTypeHelper.GetMimeType("document.pdf");
    /// // mimeType = "application/pdf"
    /// 
    /// var mimeType2 = MimeTypeHelper.GetMimeType("data.bin");
    /// // mimeType2 = "application/octet-stream"
    /// </code>
    /// </example>
    public static string GetMimeType(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return "application/octet-stream";

        return MimeUtility.GetMimeMapping(filePath) 
               ?? "application/octet-stream";
    }

    /// <summary>
    /// Check if a MIME type represents a text format.
    /// </summary>
    /// <param name="mimeType">MIME type string</param>
    /// <returns>True if text format, false otherwise</returns>
    public static bool IsTextType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return false;

        return mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               mimeType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
               mimeType.Contains("xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if a MIME type represents an image format.
    /// </summary>
    /// <param name="mimeType">MIME type string</param>
    /// <returns>True if image format, false otherwise</returns>
    public static bool IsImageType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return false;

        return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}
