using System;

namespace EywaClient.Utilities;

/// <summary>
/// Parses EYWA file paths into folder and file components.
/// EYWA paths follow the format: /folder1/folder2/filename.ext
/// </summary>
public static class PathParser
{
    /// <summary>
    /// Parse an EYWA path into folder path and filename.
    /// </summary>
    /// <param name="eywaPath">EYWA path (e.g., "/documents/2024/report.pdf")</param>
    /// <returns>Tuple of (folderPath, fileName)</returns>
    /// <exception cref="ArgumentNullException">Thrown when eywaPath is null or empty</exception>
    /// <example>
    /// <code>
    /// var (folder, file) = PathParser.Parse("/documents/2024/report.pdf");
    /// // folder = "/documents/2024/"
    /// // file = "report.pdf"
    /// 
    /// var (folder2, file2) = PathParser.Parse("file.txt");
    /// // folder2 = ""
    /// // file2 = "file.txt"
    /// </code>
    /// </example>
    public static (string folderPath, string fileName) Parse(string eywaPath)
    {
        if (string.IsNullOrWhiteSpace(eywaPath))
            throw new ArgumentNullException(nameof(eywaPath), "EYWA path cannot be null or empty");

        // Normalize path separators to forward slashes
        eywaPath = eywaPath.Replace('\\', '/');

        // Handle root-level files (no folder path)
        if (!eywaPath.Contains('/'))
        {
            return ("", eywaPath);
        }

        // Handle paths ending with / (folder only, no file)
        if (eywaPath.EndsWith('/'))
        {
            return (eywaPath, "");
        }

        // Split into folder and file
        var lastSlashIndex = eywaPath.LastIndexOf('/');
        var folderPath = eywaPath.Substring(0, lastSlashIndex + 1); // Include trailing /
        var fileName = eywaPath.Substring(lastSlashIndex + 1);

        return (folderPath, fileName);
    }

    /// <summary>
    /// Split a folder path into individual folder segments.
    /// </summary>
    /// <param name="folderPath">Folder path (e.g., "/documents/2024/")</param>
    /// <returns>Array of folder names (e.g., ["documents", "2024"])</returns>
    /// <example>
    /// <code>
    /// var segments = PathParser.SplitFolderPath("/documents/2024/");
    /// // segments = ["documents", "2024"]
    /// </code>
    /// </example>
    public static string[] SplitFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return Array.Empty<string>();

        return folderPath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Join folder segments into a complete folder path.
    /// </summary>
    /// <param name="segments">Folder segments</param>
    /// <returns>Complete folder path with leading and trailing slashes</returns>
    /// <example>
    /// <code>
    /// var path = PathParser.JoinFolderPath("documents", "2024");
    /// // path = "/documents/2024/"
    /// </code>
    /// </example>
    public static string JoinFolderPath(params string[] segments)
    {
        if (segments == null || segments.Length == 0)
            return "/";

        return "/" + string.Join("/", segments) + "/";
    }
}
