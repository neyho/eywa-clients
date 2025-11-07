using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EywaClient.Files;
using EywaClient.GraphQL;

namespace EywaClient.Examples;

/// <summary>
/// Examples demonstrating file query, list, and delete operations.
/// </summary>
public class FileManagerExample
{
    private readonly GraphQLClient _client;
    private readonly FileManager _fileManager;

    public FileManagerExample(GraphQLClient client)
    {
        _client = client;
        _fileManager = new FileManager(client);
    }

    /// <summary>
    /// Example 1: Get file information
    /// </summary>
    public async Task Example1_GetFileInfo()
    {
        Console.WriteLine("\n=== Example 1: Get File Info ===\n");

        var fileUuid = Guid.Parse("..."); // Your file UUID

        var fileInfo = await _fileManager.GetFileInfoAsync(fileUuid);

        if (fileInfo != null)
        {
            Console.WriteLine($"Name: {fileInfo["name"]}");
            Console.WriteLine($"Size: {fileInfo["size"]:N0} bytes");
            Console.WriteLine($"Status: {fileInfo["status"]}");
            Console.WriteLine($"Content Type: {fileInfo["content_type"]}");
            Console.WriteLine($"Path: {fileInfo["path"]}");
            Console.WriteLine($"Uploaded: {fileInfo["uploaded_at"]}");

            if (fileInfo.ContainsKey("folder"))
            {
                var folder = (Dictionary<string, object>)fileInfo["folder"];
                Console.WriteLine($"Folder: {folder["path"]}");
            }
        }
        else
        {
            Console.WriteLine("File not found");
        }
    }

    /// <summary>
    /// Example 2: List all files
    /// </summary>
    public async Task Example2_ListAllFiles()
    {
        Console.WriteLine("\n=== Example 2: List All Files ===\n");

        var files = await _fileManager.ListFilesAsync();

        Console.WriteLine($"Total files: {files.Count}\n");

        foreach (var file in files.Take(10)) // Show first 10
        {
            Console.WriteLine($"- {file["path"]} ({file["size"]:N0} bytes)");
        }
    }

    /// <summary>
    /// Example 3: List files in folder
    /// </summary>
    public async Task Example3_ListFilesInFolder()
    {
        Console.WriteLine("\n=== Example 3: List Files in Folder ===\n");

        var folderUuid = Guid.Parse("..."); // Your folder UUID

        var files = await _fileManager.ListFilesAsync(folderUuid: folderUuid);

        Console.WriteLine($"Files in folder: {files.Count}\n");

        foreach (var file in files)
        {
            Console.WriteLine($"- {file["name"]} ({file["size"]:N0} bytes)");
        }
    }

    /// <summary>
    /// Example 4: List files by pattern
    /// </summary>
    public async Task Example4_ListFilesByPattern()
    {
        Console.WriteLine("\n=== Example 4: List Files by Pattern ===\n");

        // Find all PDF files
        var pdfFiles = await _fileManager.ListFilesAsync(namePattern: ".pdf");

        Console.WriteLine($"PDF files found: {pdfFiles.Count}\n");

        foreach (var file in pdfFiles.Take(5))
        {
            Console.WriteLine($"- {file["path"]}");
        }

        // Find all files with "report" in name
        var reportFiles = await _fileManager.ListFilesAsync(namePattern: "report");

        Console.WriteLine($"\nFiles with 'report': {reportFiles.Count}");
    }

    /// <summary>
    /// Example 5: List files by status
    /// </summary>
    public async Task Example5_ListFilesByStatus()
    {
        Console.WriteLine("\n=== Example 5: List Files by Status ===\n");

        var uploadedFiles = await _fileManager.ListFilesAsync(
            status: "UPLOADED",
            limit: 10
        );

        Console.WriteLine($"Uploaded files: {uploadedFiles.Count}\n");

        foreach (var file in uploadedFiles)
        {
            Console.WriteLine($"- {file["path"]} (uploaded: {file["uploaded_at"]})");
        }
    }

    /// <summary>
    /// Example 6: Find file in folder
    /// </summary>
    public async Task Example6_FindFileInFolder()
    {
        Console.WriteLine("\n=== Example 6: Find File in Folder ===\n");

        var folderUuid = Guid.Parse("..."); // Your folder UUID
        var fileName = "report.pdf";

        var file = await _fileManager.FindFileInFolderAsync(fileName, folderUuid);

        if (file != null)
        {
            Console.WriteLine($"✅ Found: {file["path"]}");
            Console.WriteLine($"   UUID: {file["euuid"]}");
            Console.WriteLine($"   Size: {file["size"]:N0} bytes");
        }
        else
        {
            Console.WriteLine($"❌ File '{fileName}' not found in folder");
        }
    }

    /// <summary>
    /// Example 7: Search files by path pattern
    /// </summary>
    public async Task Example7_SearchByPath()
    {
        Console.WriteLine("\n=== Example 7: Search Files by Path ===\n");

        // Find all files in /tmp
        var tmpFiles = await _fileManager.SearchFilesByPathAsync("/tmp/%");
        Console.WriteLine($"Files in /tmp: {tmpFiles.Count}");

        // Find all files with "reports" in path
        var reportFiles = await _fileManager.SearchFilesByPathAsync("%/reports/%");
        Console.WriteLine($"Files with '/reports/' in path: {reportFiles.Count}");

        // Find all files in 2024 folders
        var files2024 = await _fileManager.SearchFilesByPathAsync("%/2024/%");
        Console.WriteLine($"Files in 2024 folders: {files2024.Count}");

        Console.WriteLine("\nSample results:");
        foreach (var file in tmpFiles.Take(5))
        {
            Console.WriteLine($"  - {file["path"]}");
        }
    }

    /// <summary>
    /// Example 8: Delete file
    /// </summary>
    public async Task Example8_DeleteFile()
    {
        Console.WriteLine("\n=== Example 8: Delete File ===\n");

        var fileUuid = Guid.Parse("..."); // Your file UUID

        // Get file info first
        var fileInfo = await _fileManager.GetFileInfoAsync(fileUuid);
        if (fileInfo != null)
        {
            Console.WriteLine($"Deleting: {fileInfo["path"]}");

            var success = await _fileManager.DeleteFileAsync(fileUuid);

            if (success)
            {
                Console.WriteLine("✅ File deleted successfully");
            }
            else
            {
                Console.WriteLine("❌ File deletion failed");
            }
        }
        else
        {
            Console.WriteLine("File not found");
        }
    }

    /// <summary>
    /// Example 9: Clean up old files
    /// </summary>
    public async Task Example9_CleanupOldFiles()
    {
        Console.WriteLine("\n=== Example 9: Cleanup Old Files ===\n");

        // List all files
        var files = await _fileManager.ListFilesAsync();

        // Find files older than 30 days
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        var oldFiles = files.Where(f =>
        {
            var uploadedAt = (DateTime)f["uploaded_at"];
            return uploadedAt < cutoffDate;
        }).ToList();

        Console.WriteLine($"Found {oldFiles.Count} files older than 30 days");

        if (oldFiles.Count > 0)
        {
            Console.WriteLine("\nDeleting old files...");
            var deletedCount = 0;

            foreach (var file in oldFiles)
            {
                var fileUuid = (Guid)file["euuid"];
                var success = await _fileManager.DeleteFileAsync(fileUuid);

                if (success)
                {
                    deletedCount++;
                    Console.WriteLine($"  ✅ Deleted: {file["path"]}");
                }
            }

            Console.WriteLine($"\n✅ Deleted {deletedCount} of {oldFiles.Count} old files");
        }
    }

    /// <summary>
    /// Example 10: Generate file report
    /// </summary>
    public async Task Example10_GenerateReport()
    {
        Console.WriteLine("\n=== Example 10: File Report ===\n");

        var files = await _fileManager.ListFilesAsync();

        // Calculate statistics
        var totalSize = files.Sum(f => Convert.ToInt64(f["size"]));
        var avgSize = files.Any() ? totalSize / files.Count : 0;

        // Group by folder
        var byFolder = files
            .GroupBy(f => f.ContainsKey("folder") 
                ? ((Dictionary<string, object>)f["folder"])["path"].ToString()
                : "Root")
            .OrderByDescending(g => g.Count());

        Console.WriteLine("File Statistics:");
        Console.WriteLine($"  Total Files: {files.Count:N0}");
        Console.WriteLine($"  Total Size: {FormatSize(totalSize)}");
        Console.WriteLine($"  Average Size: {FormatSize(avgSize)}");

        Console.WriteLine("\nFiles by Folder:");
        foreach (var group in byFolder.Take(10))
        {
            var groupSize = group.Sum(f => Convert.ToInt64(f["size"]));
            Console.WriteLine($"  {group.Key}: {group.Count()} files ({FormatSize(groupSize)})");
        }

        // Group by extension
        var byExtension = files
            .GroupBy(f =>
            {
                var name = f["name"].ToString() ?? "";
                var lastDot = name.LastIndexOf('.');
                return lastDot >= 0 ? name.Substring(lastDot) : "no extension";
            })
            .OrderByDescending(g => g.Count());

        Console.WriteLine("\nFiles by Extension:");
        foreach (var group in byExtension.Take(10))
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} files");
        }
    }

    private string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int order = 0;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:F2} {sizes[order]}";
    }

    /// <summary>
    /// Run all examples
    /// </summary>
    public async Task RunAllExamples()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════╗");
        Console.WriteLine("║  FileManager Examples                      ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");

        try
        {
            await Example1_GetFileInfo();
            await Example2_ListAllFiles();
            await Example3_ListFilesInFolder();
            await Example4_ListFilesByPattern();
            await Example5_ListFilesByStatus();
            await Example6_FindFileInFolder();
            await Example7_SearchByPath();
            await Example8_DeleteFile();
            await Example9_CleanupOldFiles();
            await Example10_GenerateReport();

            Console.WriteLine("\n✅ All examples completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            throw;
        }
    }
}
