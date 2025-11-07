using System;
using System.Collections.Generic;

namespace EywaClient.Files;

/// <summary>
/// Constants for EYWA file system operations.
/// </summary>
public static class FileConstants
{
    /// <summary>
    /// UUID of the root folder in EYWA file system.
    /// This folder always exists at path "/" and can be used as a parent for top-level folders/files.
    /// </summary>
    public static readonly Guid RootFolderUuid = Guid.Parse("87ce50d8-5dfa-4008-a265-053e727ab793");

    /// <summary>
    /// Root folder reference object for use in folder/file parent relationships.
    /// Use this when creating folders or files at the root level.
    /// </summary>
    /// <example>
    /// <code>
    /// // Create folder in root
    /// var folderData = new Dictionary&lt;string, object&gt;
    /// {
    ///     { "name", "my-folder" },
    ///     { "parent", FileConstants.RootFolder }
    /// };
    /// 
    /// // Upload file to root
    /// var fileData = new Dictionary&lt;string, object&gt;
    /// {
    ///     { "name", "file.txt" },
    ///     { "size", 1024 },
    ///     { "content_type", "text/plain" },
    ///     { "folder", FileConstants.RootFolder }
    /// };
    /// </code>
    /// </example>
    public static readonly Dictionary<string, object> RootFolder = new Dictionary<string, object>
    {
        { "euuid", RootFolderUuid }
    };

    /// <summary>
    /// Root folder path in EYWA file system.
    /// </summary>
    public const string RootFolderPath = "/";
}
