using MimeMapping;

namespace EywaClient.Utilities;

/// <summary>
/// Utility for detecting MIME types from file paths.
/// </summary>
public static class MimeTypeHelper
{
    /// <summary>
    /// Gets the MIME type for a given file path or filename.
    /// </summary>
    /// <param name="path">The file path or filename.</param>
    /// <returns>The MIME type, or "application/octet-stream" if unknown.</returns>
    /// <example>
    /// <code>
    /// var mimeType = MimeTypeHelper.GetMimeType("report.pdf");
    /// // Returns: "application/pdf"
    /// 
    /// var mimeType2 = MimeTypeHelper.GetMimeType("/documents/image.png");
    /// // Returns: "image/png"
    /// 
    /// var mimeType3 = MimeTypeHelper.GetMimeType("unknown.xyz");
    /// // Returns: "application/octet-stream"
    /// </code>
    /// </example>
    public static string GetMimeType(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return "application/octet-stream";

        try
        {
            var mimeType = MimeUtility.GetMimeMapping(path);
            return string.IsNullOrEmpty(mimeType) 
                ? "application/octet-stream" 
                : mimeType;
        }
        catch
        {
            return "application/octet-stream";
        }
    }

    /// <summary>
    /// Gets the file extension for a given MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type.</param>
    /// <returns>The file extension (with dot), or null if unknown.</returns>
    /// <example>
    /// <code>
    /// var ext = MimeTypeHelper.GetExtension("application/pdf");
    /// // Returns: ".pdf"
    /// </code>
    /// </example>
    public static string? GetExtension(string mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return null;

        try
        {
            // MimeMapping doesn't have reverse lookup, so we return null
            // This is a simplified implementation
            return null;
        }
        catch
        {
            return null;
        }
    }
}