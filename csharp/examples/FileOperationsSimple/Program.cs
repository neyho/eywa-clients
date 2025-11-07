using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EywaClient.Core;
using EywaClient.Files;
using EywaClient.GraphQL;

namespace EywaClient.Examples;

/// <summary>
/// Simple file operations example - minimal functionality demonstration.
/// Based on the Babashka file_operations_simple.clj pattern.
/// 
/// Run with: eywa run -c "dotnet run --project examples/FileOperationsSimple.cs"
/// </summary>
public class FileOperationsSimple
{
    private readonly GraphQLClient _client;
    private readonly FolderManager _folderManager;
    private readonly FileManager _fileManager;
    private readonly FileUploader _uploader;
    private readonly FileDownloader _downloader;

    public FileOperationsSimple(GraphQLClient client)
    {
        _client = client;
        _folderManager = new FolderManager(client);
        _fileManager = new FileManager(client);
        _uploader = new FileUploader(client);
        _downloader = new FileDownloader(client);
    }

    public static async Task Main(string[] args)
    {
        Console.WriteLine("\n=== EYWA File Operations - Simple Examples ===\n");

        try
        {
            // Initialize client (assumes EYWA connection is configured via environment)
            var rpcClient = new JsonRpcClient();
            
            // CRITICAL: Open the pipe to start reading from stdin/stdout
            rpcClient.OpenPipe();
            
            // Give the client time to initialize (like the Babashka 500ms sleep)
            await Task.Delay(500);
            
            var client = new GraphQLClient(rpcClient);
            var example = new FileOperationsSimple(client);

            await example.RunExamples();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            throw;
        }
    }

    private async Task RunExamples()
    {
        // Pre-generate UUIDs for predictable structure
        var demoFolderUuid = Guid.Parse("9bd6fe99-7540-4a54-9998-138405ea8d2c");
        var reportsFolderUuid = Guid.Parse("4e2dfc2f-d46e-499a-b008-2104b9214aa1");
        var sampleFileUuid = Guid.Parse("3f0f4173-4ef7-4499-857e-37568adeab48");
        var generatedFileUuid = Guid.Parse("b986f49c-b91b-48fb-b4df-e749f6ca735a");

        Console.WriteLine("📋 Test Data Generated:");
        Console.WriteLine($"  demo-files folder: {demoFolderUuid}");
        Console.WriteLine($"  reports folder:    {reportsFolderUuid}");
        Console.WriteLine($"  sample file:       {sampleFileUuid}");
        Console.WriteLine($"  generated file:    {generatedFileUuid}");

        // ============================================
        // EXAMPLE 1: Create Folder Structure
        // ============================================
        Console.WriteLine("\n\n📁 EXAMPLE 1: Create Folder Structure");

        // Create demo-files folder under system root
        Console.WriteLine("Creating folder: demo-files");
        await _folderManager.CreateFolderAsync(new Dictionary<string, object>
        {
            { "name", "demo-files" },
            { "euuid", demoFolderUuid },
            { "parent", FileConstants.RootFolder }
        });
        Console.WriteLine("✅ SUCCESS");

        // Verify folder by querying it by UUID (like Babashka example)
        var demoFolder = await _folderManager.GetFolderAsync(demoFolderUuid);
        if (demoFolder == null)
            throw new Exception("Failed to create demo-files folder");
        Console.WriteLine($"  UUID: {demoFolder["euuid"]}");
        Console.WriteLine($"  Name: {demoFolder["name"]}");
        Console.WriteLine($"  Path: {demoFolder["path"]}");

        // Create reports subfolder
        Console.WriteLine("\nCreating folder: reports");
        await _folderManager.CreateFolderAsync(new Dictionary<string, object>
        {
            { "name", "reports" },
            { "euuid", reportsFolderUuid },
            { "parent", new Dictionary<string, object> { { "euuid", demoFolderUuid } } }
        });
        Console.WriteLine("✅ SUCCESS");

        var reportsFolder = await _folderManager.GetFolderAsync(reportsFolderUuid);
        if (reportsFolder == null)
            throw new Exception("Failed to create reports folder");
        Console.WriteLine($"  UUID: {reportsFolder["euuid"]}");
        Console.WriteLine($"  Path: {reportsFolder["path"]}");
        var parent = reportsFolder["parent"] as Dictionary<string, object>;
        Console.WriteLine($"  Parent: {parent?["name"]}");

        // ============================================
        // EXAMPLE 2: Upload File to Folder
        // ============================================
        Console.WriteLine("\n\n📤 EXAMPLE 2: Upload File to Folder");

        var sampleText = "Hello from EYWA C# Client!\nThis is a sample file.\n";
        Console.WriteLine($"Uploading sample.txt to demo-files folder");
        await _uploader.UploadTextAsync(sampleText, new Dictionary<string, object>
        {
            { "name", "sample.txt" },
            { "euuid", sampleFileUuid },
            { "content_type", "text/plain" },
            { "folder", new Dictionary<string, object> { { "euuid", demoFolderUuid } } }
        });
        Console.WriteLine("✅ SUCCESS");

        // Verify file
        var fileInfo = await _fileManager.GetFileInfoAsync(sampleFileUuid);
        if (fileInfo == null)
            throw new Exception("Failed to upload sample.txt");
        Console.WriteLine($"  UUID: {fileInfo["euuid"]}");
        Console.WriteLine($"  Name: {fileInfo["name"]}");
        Console.WriteLine($"  Size: {fileInfo["size"]} bytes");
        if (fileInfo.ContainsKey("folder") && fileInfo["folder"] is Dictionary<string, object> folder)
        {
            Console.WriteLine($"  Folder Path: {folder["path"]}");
        }

        // ============================================
        // EXAMPLE 3: Upload Generated Content
        // ============================================
        Console.WriteLine("\n\n📤 EXAMPLE 3: Upload Generated Content");

        var generatedContent = $"Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                               $"Random value: {new Random().Next(1000)}\n";

        Console.WriteLine("Uploading generated.txt");
        await _uploader.UploadTextAsync(generatedContent, new Dictionary<string, object>
        {
            { "name", "generated.txt" },
            { "euuid", generatedFileUuid },
            { "content_type", "text/plain" },
            { "folder", new Dictionary<string, object> { { "euuid", demoFolderUuid } } }
        });
        Console.WriteLine("✅ SUCCESS");

        var generatedInfo = await _fileManager.GetFileInfoAsync(generatedFileUuid);
        if (generatedInfo == null)
            throw new Exception("Failed to upload generated.txt");
        Console.WriteLine($"  UUID: {generatedInfo["euuid"]}");
        Console.WriteLine($"  Size: {generatedInfo["size"]} bytes");
        if (generatedInfo.ContainsKey("folder") && generatedInfo["folder"] is Dictionary<string, object> genFolder)
        {
            Console.WriteLine($"  Folder Path: {genFolder["path"]}");
        }

        // ============================================
        // EXAMPLE 4: List Files in Folder
        // ============================================
        Console.WriteLine("\n\n📋 EXAMPLE 4: List Files in Folder");

        var files = await _fileManager.ListFilesAsync(folderUuid: demoFolderUuid);
        Console.WriteLine($"Listing files in demo-files folder");
        Console.WriteLine("✅ SUCCESS");
        Console.WriteLine($"  Found {files.Count} file(s):");
        foreach (var file in files)
        {
            var fileName = file["name"];
            var fileSize = file["size"];
            Console.WriteLine($"    - {fileName} ({fileSize} bytes)");
        }

        // ============================================
        // EXAMPLE 5: Download and Verify Content
        // ============================================
        Console.WriteLine("\n\n📥 EXAMPLE 5: Download File");

        Console.WriteLine("Downloading generated.txt");
        var fileRef = new Dictionary<string, object> { { "euuid", generatedFileUuid } };
        var downloadedBytes = await _downloader.DownloadBytesAsync(fileRef);
        Console.WriteLine("✅ SUCCESS");
        
        var downloadedText = System.Text.Encoding.UTF8.GetString(downloadedBytes);
        Console.WriteLine($"  Downloaded {downloadedBytes.Length} bytes");
        Console.WriteLine($"  Content preview: {downloadedText.Substring(0, Math.Min(50, downloadedText.Length))}...");

        // ============================================
        // EXAMPLE 6: Replace File Content
        // ============================================
        Console.WriteLine("\n\n📤 EXAMPLE 6: Replace File Content");

        var updatedContent = $"UPDATED at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                            $"This file has been replaced!\n";

        Console.WriteLine("Replacing generated.txt with new content");
        await _uploader.UploadTextAsync(updatedContent, new Dictionary<string, object>
        {
            { "name", "generated.txt" },
            { "euuid", generatedFileUuid }, // Same UUID = replacement
            { "content_type", "text/plain" }
        });
        Console.WriteLine("✅ SUCCESS");

        // Verify update
        var updatedInfo = await _fileManager.GetFileInfoAsync(generatedFileUuid);
        if (updatedInfo == null)
            throw new Exception("Failed to get updated file info");
        Console.WriteLine($"  Updated size: {updatedInfo["size"]} bytes");
        Console.WriteLine($"  Same UUID: {updatedInfo["euuid"]}");

        // ============================================
        // VERIFICATION SUMMARY
        // ============================================
        Console.WriteLine("\n\n✅ VERIFICATION SUMMARY");
        Console.WriteLine("======================================");

        Console.WriteLine("\n📁 Folders Created:");
        Console.WriteLine($"  demo-files: {demoFolder["path"]}");
        Console.WriteLine($"  reports:    {reportsFolder["path"]}");

        Console.WriteLine("\n📄 Files Uploaded:");
        
        var samplePath = fileInfo.ContainsKey("folder") && fileInfo["folder"] is Dictionary<string, object> sf 
            ? sf["path"] + "/" + fileInfo["name"] 
            : "/" + fileInfo["name"];
        Console.WriteLine($"  sample.txt:    {samplePath} ({fileInfo["size"]} bytes)");
        
        var generatedPath = generatedInfo.ContainsKey("folder") && generatedInfo["folder"] is Dictionary<string, object> gf 
            ? gf["path"] + "/" + generatedInfo["name"] 
            : "/" + generatedInfo["name"];
        Console.WriteLine($"  generated.txt: {generatedPath} ({generatedInfo["size"]} bytes)");

        Console.WriteLine("\n💡 Key Takeaways:");
        Console.WriteLine("  • Pre-generate UUIDs and pass complete folder/file definitions");
        Console.WriteLine("  • CreateFolderAsync accepts { name, euuid, parent }");
        Console.WriteLine("  • Folder can be specified as { euuid: ... } in file metadata");
        Console.WriteLine("  • EYWA automatically computes file paths based on folder hierarchy");
        Console.WriteLine("  • Same UUID can be used to replace/update file content");
        Console.WriteLine("  • Upload returns void on success - verify with GetFileInfoAsync");

        Console.WriteLine("\n👋 Demo finished!");
    }
}