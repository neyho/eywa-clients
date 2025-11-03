namespace EywaClient.Utilities;

/// <summary>
/// Utility for parsing EYWA file paths.
/// </summary>
public static class PathParser
{
    /// <summary>
    /// Parses an EYWA path into folder path and file name components.
    /// </summary>
    /// <param name="eywaPath">The EYWA path to parse (e.g., "/documents/2024/report.pdf").</param>
    /// <returns>A tuple containing (folderPath, fileName).</returns>
    /// <example>
    /// <code>
    /// var (folder, file) = PathParser.Parse("/documents/2024/report.pdf");
    /// // folder = "/documents/2024/", file = "report.pdf"
    /// 
    /// var (folder2, file2) = PathParser.Parse("/file.txt");
    /// // folder2 = "/", file2 = "file.txt"
    /// 
    /// var (folder3, file3) = PathParser.Parse("file.txt");
    /// // folder3 = null, file3 = "file.txt"
    /// </code>
    /// </example>
    public static (string? folderPath, string fileName) Parse(string? eywaPath)
    {
        // Handle null or paths without '/'
        if (string.IsNullOrEmpty(eywaPath) || !eywaPath.Contains('/'))
        {
            return (null, eywaPath ?? string.Empty);
        }

        // Normalize path to start with '/'
        var normalized = eywaPath.StartsWith('/') ? eywaPath : '/' + eywaPath;

        // Find last slash
        var lastSlash = normalized.LastIndexOf('/');

        if (lastSlash == 0)
        {
            // ROOT level: '/file.txt'
            return ("/", normalized.Substring(1));
        }

        // Extract folder and file
        var folderPath = normalized.Substring(0, lastSlash + 1);
        var fileName = normalized.Substring(lastSlash + 1);

        return (folderPath, fileName);
    }

    /// <summary>
    /// Splits a folder path into individual path segments.
    /// </summary>
    /// <param name="folderPath">The folder path (e.g., "/documents/2024/Q1/").</param>
    /// <returns>List of path segments in order.</returns>
    /// <example>
    /// <code>
    /// var segments = PathParser.GetSegments("/documents/2024/Q1/");
    /// // Returns: ["documents", "2024", "Q1"]
    /// </code>
    /// </example>
    public static List<string> GetSegments(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || folderPath == "/")
            return new List<string>();

        return folderPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    /// <summary>
    /// Builds a list of all parent paths for a given folder path.
    /// </summary>
    /// <param name="folderPath">The target folder path.</param>
    /// <returns>List of paths from root to target.</returns>
    /// <example>
    /// <code>
    /// var paths = PathParser.GetHierarchy("/documents/2024/Q1/");
    /// // Returns: ["/documents/", "/documents/2024/", "/documents/2024/Q1/"]
    /// </code>
    /// </example>
    public static List<string> GetHierarchy(string folderPath)
    {
        var segments = GetSegments(folderPath);
        var paths = new List<string>();
        var current = "/";

        foreach (var segment in segments)
        {
            current += segment + "/";
            paths.Add(current);
        }

        return paths;
    }

    /// <summary>
    /// Validates that a path is a valid folder path (ends with '/').
    /// </summary>
    /// <param name="folderPath">The path to validate.</param>
    /// <returns>True if valid folder path.</returns>
    public static bool IsValidFolderPath(string? folderPath)
    {
        return !string.IsNullOrEmpty(folderPath) && folderPath.EndsWith('/');
    }

    /// <summary>
    /// Normalizes a folder path to ensure it starts and ends with '/'.
    /// </summary>
    /// <param name="folderPath">The path to normalize.</param>
    /// <returns>Normalized folder path.</returns>
    public static string NormalizeFolderPath(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return "/";

        var normalized = folderPath;

        if (!normalized.StartsWith('/'))
            normalized = '/' + normalized;

        if (!normalized.EndsWith('/'))
            normalized += '/';

        return normalized;
    }
}
