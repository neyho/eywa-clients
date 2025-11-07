using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EywaClient.Files;
using EywaClient.GraphQL;

namespace EywaClient.Examples;

/// <summary>
/// Comprehensive examples demonstrating file and folder operations in EYWA.
/// 
/// This example follows the "building blocks" approach where users have full control
/// over metadata, folder relationships, and UUIDs, similar to the Babashka client pattern.
/// </summary>
public class FileOperationsExample
{
    private readonly GraphQLClient _client;
    private readonly FileUploader _uploader;
    private readonly FolderManager _folderManager;

    public FileOperationsExample(GraphQLClient client)
    {
        _client = client;
        _uploader = new FileUploader(client);
        _folderManager = new FolderManager(client);
    }

    /// <summary>
    /// Example 1: Upload file to root folder
    /// </summary>
    public async Task Example1_UploadToRoot()
    {
        Console.WriteLine("\n=== Example 1: Upload to Root ===\n");

        var fileData = new Dictionary<string, object>
        {
            { "name", "document.pdf" },
            // size and content_type auto-detected from file
        };

        await _uploader.UploadFileAsync("path/to/document.pdf", fileData);
        Console.WriteLine("✅ Uploaded document.pdf to root");
    }

    /// <summary>
    /// Example 2: Query folder by path and upload to it
    /// </summary>
    public async Task Example2_QueryFolderAndUpload()
    {
        Console.WriteLine("\n=== Example 2: Query Folder and Upload ===\n");

        // First, query the folder by path
        var reportsFolder = await _folderManager.GetFolderByPathAsync("/reports");
        
        if (reportsFolder == null)
        {
            Console.WriteLine("❌ Folder /reports not found");
            return;
        }

        var folderUuid = (Guid)reportsFolder["euuid"];
        Console.WriteLine($"Found folder: {reportsFolder["name"]} (UUID: {folderUuid})");

        // Upload file to that folder
        var fileData = new Dictionary<string, object>
        {
            { "name", "monthly-report.pdf" },
            { "folder", new Dictionary<string, object> { { "euuid", folderUuid } } }
        };

        await _uploader.UploadFileAsync("path/to/report.pdf", fileData);
        Console.WriteLine("✅ Uploaded to /reports folder");
    }

    /// <summary>
    /// Example 3: Create folder structure and upload files
    /// </summary>
    public async Task Example3_CreateFoldersAndUpload()
    {
        Console.WriteLine("\n=== Example 3: Create Folders and Upload ===\n");

        // Create parent folder in root
        var reportsFolderData = new Dictionary<string, object>
        {
            { "name", "reports" },
            { "euuid", Guid.NewGuid() }, // Optional: control the UUID
            { "parent", FileConstants.RootFolder }
        };

        var reportsFolder = await _folderManager.CreateFolderAsync(reportsFolderData);
        var reportsUuid = (Guid)reportsFolder["euuid"];
        Console.WriteLine($"✅ Created folder: {reportsFolder["path"]}");

        // Create subfolder
        var archiveFolderData = new Dictionary<string, object>
        {
            { "name", "archive" },
            { "parent", new Dictionary<string, object> { { "euuid", reportsUuid } } }
        };

        var archiveFolder = await _folderManager.CreateFolderAsync(archiveFolderData);
        var archiveUuid = (Guid)archiveFolder["euuid"];
        Console.WriteLine($"✅ Created subfolder: {archiveFolder["path"]}");

        // Upload file to subfolder
        var fileData = new Dictionary<string, object>
        {
            { "name", "2023-report.pdf" },
            { "folder", new Dictionary<string, object> { { "euuid", archiveUuid } } }
        };

        await _uploader.UploadFileAsync("path/to/old-report.pdf", fileData);
        Console.WriteLine($"✅ Uploaded to {archiveFolder["path"]}");
    }

    /// <summary>
    /// Example 4: Upload with custom UUID (for deduplication/replacement)
    /// </summary>
    public async Task Example4_UploadWithCustomUuid()
    {
        Console.WriteLine("\n=== Example 4: Upload with Custom UUID ===\n");

        // First upload with specific UUID
        var fileUuid = Guid.NewGuid();
        var fileData = new Dictionary<string, object>
        {
            { "name", "config.json" },
            { "euuid", fileUuid },
            { "folder", FileConstants.RootFolder }
        };

        await _uploader.UploadFileAsync("path/to/config.json", fileData);
        Console.WriteLine($"✅ Initial upload (UUID: {fileUuid})");

        // Later, replace/update the same file by using same UUID
        await Task.Delay(2000); // Simulate time passing

        var updatedFileData = new Dictionary<string, object>
        {
            { "name", "config.json" },
            { "euuid", fileUuid }, // Same UUID = replaces existing file
        };

        await _uploader.UploadFileAsync("path/to/config-updated.json", updatedFileData);
        Console.WriteLine($"✅ Updated file (same UUID: {fileUuid})");
    }

    /// <summary>
    /// Example 5: Upload generated content directly
    /// </summary>
    public async Task Example5_UploadGeneratedContent()
    {
        Console.WriteLine("\n=== Example 5: Upload Generated Content ===\n");

        // Generate JSON content
        var data = new { timestamp = DateTime.UtcNow, value = 42 };
        var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        var fileData = new Dictionary<string, object>
        {
            { "name", "export.json" },
            { "content_type", "application/json" }
            // size will be auto-calculated
        };

        await _uploader.UploadTextAsync(json, fileData);
        Console.WriteLine("✅ Uploaded generated JSON content");
    }

    /// <summary>
    /// Example 6: Upload with progress tracking
    /// </summary>
    public async Task Example6_UploadWithProgress()
    {
        Console.WriteLine("\n=== Example 6: Upload with Progress ===\n");

        var fileData = new Dictionary<string, object>
        {
            { "name", "large-file.zip" }
        };

        await _uploader.UploadFileAsync(
            "path/to/large-file.zip",
            fileData,
            progressCallback: (bytesUploaded, totalBytes) =>
            {
                var percent = (bytesUploaded * 100.0) / totalBytes;
                Console.WriteLine($"Upload progress: {percent:F1}% ({bytesUploaded:N0} / {totalBytes:N0} bytes)");
            }
        );

        Console.WriteLine("✅ Upload complete!");
    }

    /// <summary>
    /// Example 7: Upload bytes directly
    /// </summary>
    public async Task Example7_UploadBytes()
    {
        Console.WriteLine("\n=== Example 7: Upload Bytes ===\n");

        var bytes = Encoding.UTF8.GetBytes("Hello, EYWA!");

        var fileData = new Dictionary<string, object>
        {
            { "name", "greeting.txt" },
            { "content_type", "text/plain" },
            { "euuid", Guid.NewGuid() }
        };

        await _uploader.UploadBytesAsync(bytes, fileData);
        Console.WriteLine("✅ Uploaded byte array");
    }

    /// <summary>
    /// Example 8: List folders and files
    /// </summary>
    public async Task Example8_ListFoldersAndFiles()
    {
        Console.WriteLine("\n=== Example 8: List Folders ===\n");

        // List all folders
        var allFolders = await _folderManager.ListFoldersAsync();
        Console.WriteLine($"Total folders: {allFolders.Count}");

        // List folders with pattern
        var reportFolders = await _folderManager.ListFoldersAsync(namePattern: "report");
        Console.WriteLine($"Folders matching 'report': {reportFolders.Count}");
        foreach (var folder in reportFolders)
        {
            Console.WriteLine($"  - {folder["path"]}");
        }

        // List root-level folders only
        var rootFolders = await _folderManager.ListFoldersAsync(parentFolderUuid: Guid.Empty);
        Console.WriteLine($"\nRoot-level folders: {rootFolders.Count}");
        foreach (var folder in rootFolders)
        {
            Console.WriteLine($"  - {folder["name"]} ({folder["path"]})");
        }
    }

    /// <summary>
    /// Example 9: Working with root folder constant
    /// </summary>
    public async Task Example9_RootFolderConstant()
    {
        Console.WriteLine("\n=== Example 9: Root Folder Constant ===\n");

        Console.WriteLine($"Root folder UUID: {FileConstants.RootFolderUuid}");
        Console.WriteLine($"Root folder path: {FileConstants.RootFolderPath}");

        // Get root folder info
        var rootFolder = await _folderManager.GetFolderByPathAsync(FileConstants.RootFolderPath);
        Console.WriteLine($"Root folder: {rootFolder["name"]} at {rootFolder["path"]}");

        // Create folder in root explicitly
        var folderData = new Dictionary<string, object>
        {
            { "name", "new-folder" },
            { "parent", FileConstants.RootFolder }
        };

        var newFolder = await _folderManager.CreateFolderAsync(folderData);
        Console.WriteLine($"✅ Created folder in root: {newFolder["path"]}");
    }

    /// <summary>
    /// Example 10: Complex folder hierarchy
    /// </summary>
    public async Task Example10_ComplexHierarchy()
    {
        Console.WriteLine("\n=== Example 10: Complex Folder Hierarchy ===\n");

        // Pre-generate UUIDs for the entire structure
        var projectUuid = Guid.NewGuid();
        var srcUuid = Guid.NewGuid();
        var testUuid = Guid.NewGuid();
        var docsUuid = Guid.NewGuid();

        var structure = new Dictionary<string, Guid>
        {
            { "project", projectUuid },
            { "project/src", srcUuid },
            { "project/test", testUuid },
            { "project/docs", docsUuid }
        };

        Console.WriteLine("Creating folder structure with pre-generated UUIDs:");
        foreach (var kvp in structure)
        {
            Console.WriteLine($"  {kvp.Key} -> {kvp.Value}");
        }

        // Create project folder
        var projectFolder = await _folderManager.CreateFolderAsync(new Dictionary<string, object>
        {
            { "name", "project" },
            { "euuid", projectUuid },
            { "parent", FileConstants.RootFolder }
        });

        // Create subfolders
        await _folderManager.CreateFolderAsync(new Dictionary<string, object>
        {
            { "name", "src" },
            { "euuid", srcUuid },
            { "parent", new Dictionary<string, object> { { "euuid", projectUuid } } }
        });

        await _folderManager.CreateFolderAsync(new Dictionary<string, object>
        {
            { "name", "test" },
            { "euuid", testUuid },
            { "parent", new Dictionary<string, object> { { "euuid", projectUuid } } }
        });

        await _folderManager.CreateFolderAsync(new Dictionary<string, object>
        {
            { "name", "docs" },
            { "euuid", docsUuid },
            { "parent", new Dictionary<string, object> { { "euuid", projectUuid } } }
        });

        Console.WriteLine("\n✅ Folder structure created!");

        // Upload files to different folders
        await _uploader.UploadTextAsync(
            "// Main application code",
            new Dictionary<string, object>
            {
                { "name", "main.cs" },
                { "folder", new Dictionary<string, object> { { "euuid", srcUuid } } }
            }
        );

        await _uploader.UploadTextAsync(
            "// Test cases",
            new Dictionary<string, object>
            {
                { "name", "tests.cs" },
                { "folder", new Dictionary<string, object> { { "euuid", testUuid } } }
            }
        );

        await _uploader.UploadTextAsync(
            "# Project Documentation",
            new Dictionary<string, object>
            {
                { "name", "README.md" },
                { "folder", new Dictionary<string, object> { { "euuid", docsUuid } } }
            }
        );

        Console.WriteLine("✅ Files uploaded to folder structure!");

        // List the structure
        var subfolders = await _folderManager.ListFoldersAsync(parentFolderUuid: projectUuid);
        Console.WriteLine($"\nSubfolders of /project: {subfolders.Count}");
        foreach (var folder in subfolders)
        {
            Console.WriteLine($"  - {folder["path"]}");
        }
    }

    /// <summary>
    /// Example 11: Upload without folder (orphan file)
    /// </summary>
    public async Task Example11_UploadWithoutFolder()
    {
        Console.WriteLine("\n=== Example 11: Upload Without Folder (Orphan) ===\n");

        // File without folder reference - not linked to any folder
        var fileData = new Dictionary<string, object>
        {
            { "name", "orphan-file.txt" },
            { "content_type", "text/plain" },
            { "euuid", Guid.NewGuid() }
            // No folder specified - file exists but not in any folder hierarchy
        };

        await _uploader.UploadTextAsync("This file is not in any folder", fileData);
        Console.WriteLine("✅ Uploaded orphan file (not linked to any folder)");
        Console.WriteLine("Note: File can still be accessed by its UUID, just not browsable via folder hierarchy");
    }

    /// <summary>
    /// Run all examples
    /// </summary>
    public async Task RunAllExamples()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════╗");
        Console.WriteLine("║  EYWA File Operations Examples            ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");

        try
        {
            await Example1_UploadToRoot();
            await Example2_QueryFolderAndUpload();
            await Example3_CreateFoldersAndUpload();
            await Example4_UploadWithCustomUuid();
            await Example5_UploadGeneratedContent();
            await Example6_UploadWithProgress();
            await Example7_UploadBytes();
            await Example8_ListFoldersAndFiles();
            await Example9_RootFolderConstant();
            await Example10_ComplexHierarchy();
            await Example11_UploadWithoutFolder();

            Console.WriteLine("\n✅ All examples completed successfully!");
            Console.WriteLine("\n💡 Key Takeaways:");
            Console.WriteLine("  • Users control all metadata via dictionaries");
            Console.WriteLine("  • Users decide folder relationships explicitly");
            Console.WriteLine("  • Pre-generate UUIDs for deduplication/replacement");
            Console.WriteLine("  • Query folders first using GetFolderByPathAsync()");
            Console.WriteLine("  • Root folder always exists at '/' with known UUID");
            Console.WriteLine("  • No hidden logic - full transparency and control");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            throw;
        }
    }
}
