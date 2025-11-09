/**
 * EYWA C# Files Example
 * 
 * Dead simple file upload test
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EywaClient;
using EywaClient.Core;

var eywa = new Eywa();

try
{
    eywa.OpenPipe();
    
    var task = await eywa.Tasks.GetTaskAsync();
    await eywa.Logger.InfoAsync("Simple File Test Started");
    await eywa.Tasks.UpdateTaskAsync(Status.Processing);
    
    // Simple file upload test
    Console.WriteLine("=== Simple File Upload Test ===");
    await TestSimpleFileUpload(eywa);
    
    Console.WriteLine("=== Simple File Test Completed ===");
    await eywa.Logger.InfoAsync("Simple File Test Completed");
    await eywa.Tasks.CloseTaskAsync(Status.Success);
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    await eywa.Logger.ErrorAsync("Simple File Test Failed", new { error = ex.Message });
    await eywa.Tasks.CloseTaskAsync(Status.Error);
    throw;
}
finally
{
    eywa.Dispose();
}

async Task TestSimpleFileUpload(Eywa eywa)
{
    try
    {
        // Create a test folder first
        Console.WriteLine("📁 Creating test folder...");
        var folderUuid = Guid.NewGuid().ToString();
        var folderData = new Dictionary<string, object>
        {
            ["euuid"] = folderUuid,
            ["name"] = "CSharp Test Folder",
            ["parent"] = eywa.Files.RootFolder  // Create in root folder
        };
        
        try
        {
            await eywa.Files.CreateFolderAsync(folderData);
            Console.WriteLine($"✅ Folder created! UUID: {folderUuid}");
        }
        catch (Exception folderEx)
        {
            Console.WriteLine($"❌ Folder creation failed: {folderEx.Message}");
            return; // Exit if folder creation fails
        }
        
        // Create a simple test file content
        var testContent = "Hello from C# EYWA client!\nThis is a simple test file.";
        var fileName = "csharp-test.txt";
        
        Console.WriteLine($"📁 Uploading: {fileName}");
        
        // Create file metadata - upload to the test folder
        var fileUuid = Guid.NewGuid().ToString();
        var fileData = new Dictionary<string, object>
        {
            ["euuid"] = fileUuid,
            ["name"] = fileName,
            ["content_type"] = "text/plain",
            ["folder"] = new Dictionary<string, object> { ["euuid"] = folderUuid }
        };
        
        // Upload the file
        await eywa.Files.UploadContentAsync(testContent, fileData);
        Console.WriteLine($"✅ Upload successful! File UUID: {fileUuid}");
        
        // Download the file back to verify
        Console.WriteLine($"📝 Downloading: {fileName}");
        
        try 
        {
            var downloadedBytes = await eywa.Files.DownloadAsync(fileUuid);
            var downloadedContent = System.Text.Encoding.UTF8.GetString(downloadedBytes);
            
            Console.WriteLine($"🔍 Downloaded {downloadedBytes.Length} bytes");
            
            if (downloadedContent == testContent)
            {
                Console.WriteLine("✅ Download successful! Content matches perfectly.");
            }
            else
            {
                Console.WriteLine($"⚠️ Content differs. Expected: '{testContent}' Got: '{downloadedContent}'");
            }
        }
        catch (Exception downloadEx)
        {
            Console.WriteLine($"❌ Download failed: {downloadEx.Message}");
        }
        
        // Delete the file to clean up
        Console.WriteLine($"🗑️ Deleting: {fileName}");
        
        try
        {
            await eywa.Files.DeleteFileAsync(fileUuid);
            Console.WriteLine("✅ File deleted successfully!");
        }
        catch (Exception deleteEx)
        {
            Console.WriteLine($"❌ Delete failed: {deleteEx.Message}");
        }
        
        // Note: File listing methods are not yet implemented in C# client
        // This would be a good next feature to add!
        Console.WriteLine("📋 File listing not yet implemented in C# client");
        
        // Clean up - delete the test folder
        Console.WriteLine($"🗑️ Deleting test folder...");
        
        try
        {
            await eywa.Files.DeleteFolderAsync(folderUuid);
            Console.WriteLine("✅ Folder deleted successfully!");
        }
        catch (Exception deleteFolderEx)
        {
            Console.WriteLine($"❌ Folder deletion failed: {deleteFolderEx.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ File upload failed: {ex.Message}");
    }
}