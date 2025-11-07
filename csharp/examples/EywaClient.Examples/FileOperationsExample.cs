using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EywaClient.Core;
using EywaClient.Files;
using EywaClient.GraphQL;
using EywaClient.Models;

namespace EywaClient.Examples;

/// <summary>
/// Demonstrates file upload and download operations.
/// 
/// This example shows:
/// - Uploading files from different sources (file, bytes, text, stream)
/// - Downloading files to different destinations (file, bytes, text, stream)
/// - Progress tracking during uploads and downloads
/// - Proper resource management
/// </summary>
class FileOperationsExample
{
    static async Task Main(string[] args)
    {
        // Initialize EYWA client
        var rpcClient = new JsonRpcClient();
        rpcClient.OpenPipe();

        var graphqlClient = new GraphQLClient(rpcClient);
        var uploader = new FileUploader(graphqlClient);
        var downloader = new FileDownloader(graphqlClient);

        try
        {
            Console.WriteLine("=== EYWA File Operations Example ===\n");

            // Example 1: Upload text content
            await UploadTextExample(uploader);

            // Example 2: Upload from file with progress tracking
            await UploadFileExample(uploader);

            // Example 3: Upload from byte array
            await UploadBytesExample(uploader);

            // Example 4: Upload from stream (memory-efficient)
            await UploadStreamExample(uploader);

            // Example 5: Download to file
            //await DownloadToFileExample(downloader);

            // Example 6: Download as text
            //await DownloadAsTextExample(downloader);

            // Example 7: Download as stream (memory-efficient)
            //await DownloadAsStreamExample(downloader);

            Console.WriteLine("\n=== All examples completed successfully! ===");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
        finally
        {
            rpcClient.Dispose();
        }
    }

    static async Task UploadTextExample(FileUploader uploader)
    {
        Console.WriteLine("--- Example 1: Upload Text Content ---");

        var content = $"Hello from EYWA C# client!\nTimestamp: {DateTime.Now:O}";
        
        var fileInfo = await uploader.UploadTextAsync(
            content,
            "/examples/greeting.txt"
        );

        Console.WriteLine($"✓ Uploaded text file: {fileInfo.Name}");
        Console.WriteLine($"  UUID: {fileInfo.Euuid}");
        Console.WriteLine($"  Size: {fileInfo.Size} bytes");
        Console.WriteLine($"  Type: {fileInfo.ContentType}\n");
    }

    static async Task UploadFileExample(FileUploader uploader)
    {
        Console.WriteLine("--- Example 2: Upload File with Progress ---");

        // Create a temporary test file
        var tempFile = Path.GetTempFileName();
        var content = new StringBuilder();
        for (int i = 0; i < 10000; i++)
        {
            content.AppendLine($"Line {i}: The quick brown fox jumps over the lazy dog.");
        }
        await File.WriteAllTextAsync(tempFile, content.ToString());

        try
        {
            var fileInfo = await uploader.UploadFileAsync(
                tempFile,
                "/examples/test-file.txt",
                new UploadOptions
                {
                    ProgressCallback = (uploaded, total) =>
                    {
                        var percent = (int)((uploaded * 100) / total);
                        Console.Write($"\r  Progress: {percent}% ({uploaded}/{total} bytes)");
                    }
                }
            );

            Console.WriteLine($"\n✓ Uploaded file: {fileInfo.Name}");
            Console.WriteLine($"  UUID: {fileInfo.Euuid}");
            Console.WriteLine($"  Size: {fileInfo.Size} bytes\n");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    static async Task UploadBytesExample(FileUploader uploader)
    {
        Console.WriteLine("--- Example 3: Upload Byte Array ---");

        // Create some binary data
        var data = new byte[1024];
        new Random().NextBytes(data);

        var fileInfo = await uploader.UploadBytesAsync(
            data,
            "/examples/binary-data.bin",
            new UploadOptions
            {
                ContentType = "application/octet-stream"
            }
        );

        Console.WriteLine($"✓ Uploaded binary file: {fileInfo.Name}");
        Console.WriteLine($"  UUID: {fileInfo.Euuid}");
        Console.WriteLine($"  Size: {fileInfo.Size} bytes\n");
    }

    static async Task UploadStreamExample(FileUploader uploader)
    {
        Console.WriteLine("--- Example 4: Upload from Stream ---");

        // Create data in memory
        var jsonData = System.Text.Json.JsonSerializer.Serialize(new
        {
            timestamp = DateTime.Now,
            message = "Generated from C# stream",
            values = new[] { 1, 2, 3, 4, 5 }
        });

        var bytes = Encoding.UTF8.GetBytes(jsonData);

        // Upload from memory stream (could be any stream - network, file, etc.)
        using var memStream = new MemoryStream(bytes);
        
        var fileInfo = await uploader.UploadStreamAsync(
            memStream,
            "/examples/data.json",
            new UploadOptions
            {
                ContentType = "application/json"
            }
        );

        Console.WriteLine($"✓ Uploaded JSON file: {fileInfo.Name}");
        Console.WriteLine($"  UUID: {fileInfo.Euuid}");
        Console.WriteLine($"  Size: {fileInfo.Size} bytes\n");
    }

    static async Task DownloadToFileExample(FileDownloader downloader)
    {
        Console.WriteLine("--- Example 5: Download to File ---");

        var fileUuid = Guid.Parse("your-file-uuid-here");
        var savePath = Path.Combine(Path.GetTempPath(), "downloaded-file.txt");

        var savedPath = await downloader.DownloadToFileAsync(
            fileUuid,
            savePath,
            new DownloadOptions
            {
                ProgressCallback = (downloaded, total) =>
                {
                    var percent = total > 0 ? (int)((downloaded * 100) / total) : 0;
                    Console.Write($"\r  Progress: {percent}% ({downloaded}/{total} bytes)");
                }
            }
        );

        Console.WriteLine($"\n✓ Downloaded to: {savedPath}\n");
    }

    static async Task DownloadAsTextExample(FileDownloader downloader)
    {
        Console.WriteLine("--- Example 6: Download as Text ---");

        var fileUuid = Guid.Parse("your-file-uuid-here");

        var content = await downloader.DownloadTextAsync(fileUuid);

        Console.WriteLine($"✓ Downloaded text content:");
        Console.WriteLine($"  {content.Substring(0, Math.Min(100, content.Length))}...\n");
    }

    static async Task DownloadAsStreamExample(FileDownloader downloader)
    {
        Console.WriteLine("--- Example 7: Download as Stream (Memory-Efficient) ---");

        var fileUuid = Guid.Parse("your-file-uuid-here");

        using var stream = await downloader.DownloadStreamAsync(fileUuid);
        using var reader = new StreamReader(stream);

        // Process stream line by line (memory-efficient for large files)
        var lineCount = 0;
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            lineCount++;
        }

        Console.WriteLine($"✓ Processed {lineCount} lines from stream\n");
    }
}
