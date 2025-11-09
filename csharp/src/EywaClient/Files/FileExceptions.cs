/**
 * EYWA File Exceptions
 * 
 * Exception types for file operations following FILES_SPEC.md
 */

namespace EywaClient.Files;

/// <summary>
/// Exception thrown during file upload operations
/// </summary>
public class FileUploadError : Exception
{
    public string Type { get; }
    public int? Code { get; }
    
    public FileUploadError(string message, string type = "upload-error", int? code = null) 
        : base(message)
    {
        Type = type;
        Code = code;
    }
}

/// <summary>
/// Exception thrown during file download operations
/// </summary>
public class FileDownloadError : Exception
{
    public string Type { get; }
    public int? Code { get; }
    
    public FileDownloadError(string message, string type = "download-error", int? code = null) 
        : base(message)
    {
        Type = type;
        Code = code;
    }
}
