/// <summary>
/// EYWA File Constants
/// 
/// Constants and utilities for file operations following FILES_SPEC.md
/// </summary>

namespace EywaClient.Files;

/// <summary>
/// File operation constants
/// </summary>
public static class FileConstants
{
    /// <summary>
    /// Root folder UUID - matches all other EYWA clients
    /// </summary>
    public const string RootUuid = "87ce50d8-5dfa-4008-a265-053e727ab793";
    
    /// <summary>
    /// Root folder reference object for GraphQL operations
    /// </summary>
    public static readonly Dictionary<string, object> RootFolder = new()
    {
        ["euuid"] = RootUuid
    };
}