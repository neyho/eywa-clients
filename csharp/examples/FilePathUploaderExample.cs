using System;
using System.Text;
using System.Threading.Tasks;
using EywaClient.Files;
using EywaClient.GraphQL;

namespace EywaClient.Examples;

/// <summary>
/// Examples demonstrating the FilePathUploader convenience helper.
/// 
/// This helper provides a simple path-based API that automatically:
/// - Creates missing folders in the path hierarchy
/// - Links folders with proper parent references
/// - Uploads files to the correct location
/// </summary>
public class FilePathUploaderExample
{
    private readonly GraphQLClient _client;
    private readonly FilePathUploader _pathUploader;

    public FilePathUploaderExample(GraphQLClient client)
    {
        _client = client;
        _pathUploader = new FilePathUploader(client);
    }

    /// <summary>
    /// Example 1: Simple upload to root
    /// </summary>
    public async Task Example1_UploadToRoot()
    {
        Console.WriteLine("\n=== Example 1: Upload to Root ===\n");

        // Upload file to root directory
        var result = await _pathUploader.UploadToPathAsync(
            @"C:\data\config.json",
            "/config.json"
        );

        Console.WriteLine($"✅ {result}");
        Console.WriteLine($"   File UUID: {result.FileUuid}");
    }

    /// <summary>
    /// Example 2: Upload with automatic folder creation
    /// </summary>
    public async Task Example2_AutoCreateFolders()
    {
        Console.WriteLine("\n=== Example 2: Auto-Create Folders ===\n");

        // This will automatically create /tmp if it doesn't exist
        var result = await _pathUploader.UploadToPathAsync(
            @"C:\data\test.txt",
            "/tmp/test.txt"
        );

        Console.WriteLine($"✅ {result}");
        Console.WriteLine($"   Folder created/used: {result.FolderPath}");
        Console.WriteLine($"   Folder UUID: {result.FolderUuid}");
    }

    /// <summary>
    /// Example 3: Upload with deep folder hierarchy
    /// </summary>
    public async Task Example3_DeepHierarchy()
    {
        Console.WriteLine("\n=== Example 3: Deep Folder Hierarchy ===\n");

        // This will create entire path: /reports/2024/january
        var result = await _pathUploader.UploadToPathAsync(
            @"C:\reports\monthly-report.pdf",
            "/reports/2024/january/monthly-report.pdf"
        );

        Console.WriteLine($"✅ {result}");
        Console.WriteLine($"   Created hierarchy: {result.FolderPath}");
        Console.WriteLine("   Folders created (if missing): /reports → /reports/2024 → /reports/2024/january");
    }

    /// <summary>
    /// Example 4: Upload with custom UUID for replacement
    /// </summary>
    public async Task Example4_ReplaceFile()
    {
        Console.WriteLine("\n=== Example 4: Replace File with Custom UUID ===\n");

        // First upload with specific UUID
        var fileUuid = Guid.NewGuid();
        
        var result1 = await _pathUploader.UploadToPathAsync(
            @"C:\data\config-v1.json",
            "/tmp/config.json",
            fileUuid: fileUuid
        );

        Console.WriteLine($"✅ Initial upload: {result1}");

        // Simulate some time passing
        await Task.Delay(1000);

        // Replace with new version (same UUID)
        var result2 = await _pathUploader.UploadToPathAsync(
            @"C:\data\config-v2.json",
            "/tmp/config.json",
            fileUuid: fileUuid // Same UUID = replacement
        );

        Console.WriteLine($"✅ Replaced file: {result2}");
        Console.WriteLine($"   Same UUID: {result1.FileUuid == result2.FileUuid}");
    }

    /// <summary>
    /// Example 5: Upload text content directly
    /// </summary>
    public async Task Example5_UploadTextContent()
    {
        Console.WriteLine("\n=== Example 5: Upload Text Content ===\n");

        var jsonData = @"{
  ""timestamp"": ""2025-11-07T12:00:00Z"",
  ""status"": ""active"",
  ""value"": 42
}";

        var result = await _pathUploader.UploadTextToPathAsync(
            jsonData,
            "/tmp/data/export.json"
        );

        Console.WriteLine($"✅ {result}");
        Console.WriteLine($"   Created folders: /tmp → /tmp/data");
    }

    /// <summary>
    /// Example 6: Upload bytes directly
    /// </summary>
    public async Task Example6_UploadBytes()
    {
        Console.WriteLine("\n=== Example 6: Upload Bytes ===\n");

        var bytes = Encoding.UTF8.GetBytes("Hello, EYWA!");

        var result = await _pathUploader.UploadBytesToPathAsync(
            bytes,
            "/tmp/greeting.txt",
            contentType: "text/plain"
        );

        Console.WriteLine($"✅ {result}");
        Console.WriteLine($"   Uploaded {bytes.Length} bytes");
    }

    /// <summary>
    /// Example 7: Batch upload to same folder
    /// </summary>
    public async Task Example7_BatchUpload()
    {
        Console.WriteLine("\n=== Example 7: Batch Upload ===\n");

        var files = new[]
        {
            (@"C:\reports\file1.pdf", "/reports/2024/file1.pdf"),
            (@"C:\reports\file2.pdf", "/reports/2024/file2.pdf"),
            (@"C:\reports\file3.pdf", "/reports/2024/file3.pdf")
        };

        Console.WriteLine("Uploading multiple files to /reports/2024...");

        foreach (var (localPath, targetPath) in files)
        {
            var result = await _pathUploader.UploadToPathAsync(localPath, targetPath);
            Console.WriteLine($"  ✅ {result.FileName}");
        }

        Console.WriteLine("\n💡 Note: FilePathUploader caches folder UUIDs internally,");
        Console.WriteLine("   so /reports/2024 is only queried/created once!");
    }

    /// <summary>
    /// Example 8: Upload with progress tracking
    /// </summary>
    public async Task Example8_UploadWithProgress()
    {
        Console.WriteLine("\n=== Example 8: Upload with Progress ===\n");

        var result = await _pathUploader.UploadToPathAsync(
            @"C:\large-file.zip",
            "/uploads/large-file.zip",
            progressCallback: (bytesUploaded, totalBytes) =>
            {
                var percent = (bytesUploaded * 100.0) / totalBytes;
                Console.Write($"\rUploading: {percent:F1}% ({bytesUploaded:N0} / {totalBytes:N0} bytes)");
            }
        );

        Console.WriteLine($"\n✅ {result}");
    }

    /// <summary>
    /// Example 9: Organize files by date
    /// </summary>
    public async Task Example9_OrganizeByDate()
    {
        Console.WriteLine("\n=== Example 9: Organize by Date ===\n");

        var now = DateTime.UtcNow;
        var datePath = $"/backups/{now:yyyy}/{now:MM}/{now:dd}";

        Console.WriteLine($"Organizing backup in: {datePath}");

        var result = await _pathUploader.UploadToPathAsync(
            @"C:\backup\database.bak",
            $"{datePath}/database.bak"
        );

        Console.WriteLine($"✅ {result}");
        Console.WriteLine($"   Full hierarchy created: /backups/{now:yyyy}/{now:MM}/{now:dd}");
    }

    /// <summary>
    /// Example 10: Project structure upload
    /// </summary>
    public async Task Example10_ProjectStructure()
    {
        Console.WriteLine("\n=== Example 10: Upload Project Structure ===\n");

        var projectFiles = new[]
        {
            ("README.md", "/project/README.md"),
            ("main.cs", "/project/src/main.cs"),
            ("helper.cs", "/project/src/helper.cs"),
            ("test.cs", "/project/test/test.cs"),
            ("config.json", "/project/config/config.json")
        };

        Console.WriteLine("Creating project structure...\n");

        foreach (var (fileName, targetPath) in projectFiles)
        {
            // For demo, upload text content instead of actual files
            var content = $"// Content of {fileName}";
            
            var result = await _pathUploader.UploadTextToPathAsync(content, targetPath);
            Console.WriteLine($"  ✅ {targetPath}");
        }

        Console.WriteLine("\n✅ Project structure created!");
        Console.WriteLine("   Folders: /project, /project/src, /project/test, /project/config");
    }

    /// <summary>
    /// Example 11: Clear cache when needed
    /// </summary>
    public async Task Example11_CacheManagement()
    {
        Console.WriteLine("\n=== Example 11: Cache Management ===\n");

        // First upload - queries folder
        Console.WriteLine("First upload (queries folder)...");
        await _pathUploader.UploadTextToPathAsync("v1", "/tmp/test.txt");

        // Second upload - uses cached folder UUID
        Console.WriteLine("Second upload (uses cache)...");
        await _pathUploader.UploadTextToPathAsync("v2", "/tmp/test.txt");

        // If folders were modified externally, clear cache
        Console.WriteLine("\nClearing cache...");
        _pathUploader.ClearCache();

        // Next upload will query folder again
        Console.WriteLine("Third upload (queries folder again)...");
        await _pathUploader.UploadTextToPathAsync("v3", "/tmp/test.txt");

        Console.WriteLine("\n✅ Cache cleared successfully");
    }

    /// <summary>
    /// Run all examples
    /// </summary>
    public async Task RunAllExamples()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════╗");
        Console.WriteLine("║  FilePathUploader Examples                ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");

        try
        {
            await Example1_UploadToRoot();
            await Example2_AutoCreateFolders();
            await Example3_DeepHierarchy();
            await Example4_ReplaceFile();
            await Example5_UploadTextContent();
            await Example6_UploadBytes();
            await Example7_BatchUpload();
            await Example8_UploadWithProgress();
            await Example9_OrganizeByDate();
            await Example10_ProjectStructure();
            await Example11_CacheManagement();

            Console.WriteLine("\n✅ All examples completed successfully!");
            Console.WriteLine("\n💡 Key Takeaways:");
            Console.WriteLine("  • Simple path-based API: UploadToPathAsync(localPath, \"/target/path.txt\")");
            Console.WriteLine("  • Automatic folder creation with proper parent linkage");
            Console.WriteLine("  • Internal caching for efficient batch operations");
            Console.WriteLine("  • Supports files, text, and bytes");
            Console.WriteLine("  • Returns file and folder UUIDs for tracking");
            Console.WriteLine("  • Built on top of FileUploader + FolderManager primitives");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            throw;
        }
    }
}
