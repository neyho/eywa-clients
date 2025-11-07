using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EywaClient.Files;
using EywaClient.GraphQL;

/// <summary>
/// Simple, concise examples of common file operations in EYWA.
/// For comprehensive examples, see FileOperationsExample.cs
/// </summary>
public class SimpleFileExample
{
    public static async Task Main(string[] args)
    {
        // Initialize client (assuming connection is established)
        var client = new GraphQLClient(/* ... */);
        var uploader = new FileUploader(client);
        var folderManager = new FolderManager(client);

        // Example 1: Upload file to root
        Console.WriteLine("Example 1: Upload to root");
        await uploader.UploadFileAsync("document.pdf", new Dictionary<string, object>
        {
            { "name", "document.pdf" }
        });

        // Example 2: Upload to specific folder
        Console.WriteLine("\nExample 2: Upload to /reports folder");
        var reportsFolder = await folderManager.GetFolderByPathAsync("/reports");
        if (reportsFolder != null)
        {
            var folderUuid = (Guid)reportsFolder["euuid"];
            await uploader.UploadFileAsync("report.pdf", new Dictionary<string, object>
            {
                { "name", "monthly-report.pdf" },
                { "folder", new Dictionary<string, object> { { "euuid", folderUuid } } }
            });
        }

        // Example 3: Create folder and upload
        Console.WriteLine("\nExample 3: Create folder and upload to it");
        var newFolder = await folderManager.CreateFolderAsync(new Dictionary<string, object>
        {
            { "name", "archive" },
            { "parent", FileConstants.RootFolder }
        });
        
        var newFolderUuid = (Guid)newFolder["euuid"];
        await uploader.UploadFileAsync("old-report.pdf", new Dictionary<string, object>
        {
            { "name", "2023-report.pdf" },
            { "folder", new Dictionary<string, object> { { "euuid", newFolderUuid } } }
        });

        // Example 4: Upload with custom UUID (for replacement)
        Console.WriteLine("\nExample 4: Upload with custom UUID");
        var fileUuid = Guid.NewGuid();
        
        // Initial upload
        await uploader.UploadFileAsync("config.json", new Dictionary<string, object>
        {
            { "name", "config.json" },
            { "euuid", fileUuid }
        });
        
        // Replace the same file later
        await Task.Delay(1000);
        await uploader.UploadFileAsync("config-updated.json", new Dictionary<string, object>
        {
            { "name", "config.json" },
            { "euuid", fileUuid } // Same UUID = replacement
        });

        // Example 5: Upload generated content
        Console.WriteLine("\nExample 5: Upload generated JSON");
        var json = System.Text.Json.JsonSerializer.Serialize(new { 
            timestamp = DateTime.UtcNow, 
            data = "example" 
        });
        
        await uploader.UploadTextAsync(json, new Dictionary<string, object>
        {
            { "name", "export.json" },
            { "content_type", "application/json" }
        });

        Console.WriteLine("\n✅ All examples completed!");
    }
}
