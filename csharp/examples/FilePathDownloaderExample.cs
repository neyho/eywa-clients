using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EywaClient.Files;
using EywaClient.GraphQL;

namespace EywaClient.Examples;

/// <summary>
/// Examples demonstrating FilePathDownloader convenience helper.
/// </summary>
public class FilePathDownloaderExample
{
    private readonly GraphQLClient _client;
    private readonly FilePathDownloader _pathDownloader;

    public FilePathDownloaderExample(GraphQLClient client)
    {
        _client = client;
        _pathDownloader = new FilePathDownloader(client);
    }

    /// <summary>
    /// Example 1: Download file by path
    /// </summary>
    public async Task Example1_DownloadByPath()
    {
        Console.WriteLine("\n=== Example 1: Download by Path ===\n");

        var result = await _pathDownloader.DownloadFromPathAsync(
            "/tmp/report.pdf",
            @"C:\downloads\report.pdf"
        );

        Console.WriteLine($"✅ {result}");
        Console.WriteLine($"   File UUID: {result.FileUuid}");
        Console.WriteLine($"   Size: {result.Size:N0} bytes");
    }

    /// <summary>
    /// Example 2: Download from deep hierarchy
    /// </summary>
    public async Task Example2_DownloadFromDeepPath()
    {
        Console.WriteLine("\n=== Example 2: Download from Deep Path ===\n");

        var result = await _pathDownloader.DownloadFromPathAsync(
            "/reports/2024/january/monthly-report.pdf",
            @"C:\downloads\monthly-report.pdf"
        );

        Console.WriteLine($"✅ Downloaded from: {result.FolderPath}");
        Console.WriteLine($"   File: {result.FileName}");
        Console.WriteLine($"   Saved to: {result.LocalPath}");
    }

    /// <summary>
    /// Example 3: Download with progress tracking
    /// </summary>
    public async Task Example3_DownloadWithProgress()
    {
        Console.WriteLine("\n=== Example 3: Download with Progress ===\n");

        var result = await _pathDownloader.DownloadFromPathAsync(
            "/large-file.zip",
            @"C:\downloads\large-file.zip",
            progressCallback: (downloaded, total) =>
            {
                var percent = (downloaded * 100.0) / total;
                Console.Write($"\rDownloading: {percent:F1}% ({downloaded:N0} / {total:N0} bytes)");
            }
        );

        Console.WriteLine($"\n✅ Download complete: {result.LocalPath}");
    }

    /// <summary>
    /// Example 4: Download as bytes
    /// </summary>
    public async Task Example4_DownloadAsBytes()
    {
        Console.WriteLine("\n=== Example 4: Download as Bytes ===\n");

        var bytes = await _pathDownloader.DownloadBytesFromPathAsync("/tmp/image.png");

        Console.WriteLine($"✅ Downloaded {bytes.Length:N0} bytes");

        // Save manually if needed
        File.WriteAllBytes(@"C:\downloads\image.png", bytes);
        Console.WriteLine($"   Saved to: C:\\downloads\\image.png");
    }

    /// <summary>
    /// Example 5: Download as text
    /// </summary>
    public async Task Example5_DownloadAsText()
    {
        Console.WriteLine("\n=== Example 5: Download as Text ===\n");

        var text = await _pathDownloader.DownloadTextFromPathAsync("/config.json");

        Console.WriteLine($"✅ Downloaded text ({text.Length} characters)");
        Console.WriteLine($"\nContent preview:");
        Console.WriteLine(text.Substring(0, Math.Min(200, text.Length)));
        if (text.Length > 200)
            Console.WriteLine("...");
    }

    /// <summary>
    /// Example 6: Download as stream (memory-efficient)
    /// </summary>
    public async Task Example6_DownloadAsStream()
    {
        Console.WriteLine("\n=== Example 6: Download as Stream ===\n");

        using var stream = await _pathDownloader.DownloadStreamFromPathAsync("/large-file.zip");
        using var fileStream = File.Create(@"C:\downloads\large-file.zip");

        await stream.CopyToAsync(fileStream);

        Console.WriteLine($"✅ File downloaded via stream");
        Console.WriteLine($"   Saved to: C:\\downloads\\large-file.zip");
    }

    /// <summary>
    /// Example 7: Get file info without downloading
    /// </summary>
    public async Task Example7_GetFileInfo()
    {
        Console.WriteLine("\n=== Example 7: Get File Info ===\n");

        var fileInfo = await _pathDownloader.GetFileInfoByPathAsync("/tmp/report.pdf");

        if (fileInfo != null)
        {
            Console.WriteLine($"File found:");
            Console.WriteLine($"  UUID: {fileInfo["euuid"]}");
            Console.WriteLine($"  Name: {fileInfo["name"]}");
            Console.WriteLine($"  Size: {fileInfo["size"]:N0} bytes");
            Console.WriteLine($"  Path: {fileInfo["path"]}");
            Console.WriteLine($"  Status: {fileInfo["status"]}");
        }
        else
        {
            Console.WriteLine("File not found");
        }
    }

    /// <summary>
    /// Example 8: Batch download files
    /// </summary>
    public async Task Example8_BatchDownload()
    {
        Console.WriteLine("\n=== Example 8: Batch Download ===\n");

        var filesToDownload = new[]
        {
            ("/reports/2024/january/report1.pdf", @"C:\downloads\report1.pdf"),
            ("/reports/2024/january/report2.pdf", @"C:\downloads\report2.pdf"),
            ("/reports/2024/january/report3.pdf", @"C:\downloads\report3.pdf")
        };

        Console.WriteLine($"Downloading {filesToDownload.Length} files...\n");

        foreach (var (sourcePath, savePath) in filesToDownload)
        {
            try
            {
                var result = await _pathDownloader.DownloadFromPathAsync(sourcePath, savePath);
                Console.WriteLine($"  ✅ {result.FileName} ({result.Size:N0} bytes)");
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"  ❌ {sourcePath} (not found)");
            }
        }

        Console.WriteLine("\n✅ Batch download complete");
    }

    /// <summary>
    /// Example 9: Download and process
    /// </summary>
    public async Task Example9_DownloadAndProcess()
    {
        Console.WriteLine("\n=== Example 9: Download and Process ===\n");

        // Download JSON config
        var json = await _pathDownloader.DownloadTextFromPathAsync("/config.json");

        // Process it
        var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        Console.WriteLine("Config loaded:");
        foreach (var kvp in config!)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
    }

    /// <summary>
    /// Example 10: Download with error handling
    /// </summary>
    public async Task Example10_ErrorHandling()
    {
        Console.WriteLine("\n=== Example 10: Error Handling ===\n");

        try
        {
            var result = await _pathDownloader.DownloadFromPathAsync(
                "/nonexistent/file.txt",
                @"C:\downloads\file.txt"
            );

            Console.WriteLine($"✅ Downloaded: {result.FileName}");
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"❌ File not found: {ex.Message}");
            Console.WriteLine("   Check if the path is correct");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Download failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Example 11: Round-trip (upload then download)
    /// </summary>
    public async Task Example11_RoundTrip()
    {
        Console.WriteLine("\n=== Example 11: Round-Trip Test ===\n");

        var pathUploader = new FilePathUploader(_client);

        // Upload
        Console.WriteLine("Uploading file...");
        var uploadResult = await pathUploader.UploadToPathAsync(
            @"C:\data\test.txt",
            "/tmp/roundtrip/test.txt"
        );

        Console.WriteLine($"✅ Uploaded: {uploadResult.FullPath}");
        Console.WriteLine($"   UUID: {uploadResult.FileUuid}");

        // Download
        Console.WriteLine("\nDownloading file...");
        var downloadResult = await _pathDownloader.DownloadFromPathAsync(
            "/tmp/roundtrip/test.txt",
            @"C:\downloads\test.txt"
        );

        Console.WriteLine($"✅ Downloaded: {downloadResult.LocalPath}");
        Console.WriteLine($"   Size: {downloadResult.Size:N0} bytes");

        // Verify
        var originalHash = ComputeHash(@"C:\data\test.txt");
        var downloadedHash = ComputeHash(@"C:\downloads\test.txt");

        if (originalHash == downloadedHash)
        {
            Console.WriteLine("\n✅ Round-trip successful - files match!");
        }
        else
        {
            Console.WriteLine("\n❌ Files don't match");
        }
    }

    private string ComputeHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Run all examples
    /// </summary>
    public async Task RunAllExamples()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════╗");
        Console.WriteLine("║  FilePathDownloader Examples               ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");

        try
        {
            await Example1_DownloadByPath();
            await Example2_DownloadFromDeepPath();
            await Example3_DownloadWithProgress();
            await Example4_DownloadAsBytes();
            await Example5_DownloadAsText();
            await Example6_DownloadAsStream();
            await Example7_GetFileInfo();
            await Example8_BatchDownload();
            await Example9_DownloadAndProcess();
            await Example10_ErrorHandling();
            await Example11_RoundTrip();

            Console.WriteLine("\n✅ All examples completed!");
            Console.WriteLine("\n💡 Key Takeaways:");
            Console.WriteLine("  • Simple path-based API: DownloadFromPathAsync(\"/path\", \"local\")");
            Console.WriteLine("  • Automatic folder and file lookup");
            Console.WriteLine("  • Multiple download methods: file, bytes, text, stream");
            Console.WriteLine("  • Progress tracking support");
            Console.WriteLine("  • Symmetric with FilePathUploader");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            throw;
        }
    }
}
