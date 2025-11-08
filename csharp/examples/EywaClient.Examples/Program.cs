using System;
using System.Threading.Tasks;
using EywaClient.GraphQL;

namespace EywaClient.Examples;

/// <summary>
/// Interactive example runner for EYWA C# Client file operations.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║  EYWA C# Client - File Operations Examples║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        // Check for command line arguments
        if (args.Length > 0)
        {
            await RunCommandLine(args);
            return;
        }

        // Interactive mode
        await RunInteractive();
    }

    static async Task RunCommandLine(string[] args)
    {
        var command = args[0].ToLower();
        
        // TODO: Get actual GraphQL client from your setup
        // For now, this is a placeholder
        var client = await SetupClient();

        switch (command)
        {
            case "simple":
                Console.WriteLine("Running Simple File Example...\n");
                var simple = new SimpleFileExample(client);
                await simple.Main(new string[0]);
                break;

            case "operations":
                Console.WriteLine("Running File Operations Example...\n");
                var operations = new FileOperationsExample(client);
                await operations.RunAllExamples();
                break;

            case "upload":
                Console.WriteLine("Running FilePathUploader Example...\n");
                var upload = new FilePathUploaderExample(client);
                await upload.RunAllExamples();
                break;

            case "manager":
                Console.WriteLine("Running FileManager Example...\n");
                var manager = new FileManagerExample(client);
                await manager.RunAllExamples();
                break;

            case "download":
                Console.WriteLine("Running FilePathDownloader Example...\n");
                var download = new FilePathDownloaderExample(client);
                await download.RunAllExamples();
                break;

            case "all":
                Console.WriteLine("Running ALL Examples...\n");
                await RunAllExamples(client);
                break;

            case "help":
            case "--help":
            case "-h":
                ShowHelp();
                break;

            default:
                Console.WriteLine($"❌ Unknown command: {command}");
                Console.WriteLine("Run with --help to see available commands");
                break;
        }
    }

    static async Task RunInteractive()
    {
        var client = await SetupClient();

        while (true)
        {
            Console.WriteLine("\nChoose an example to run:\n");
            Console.WriteLine("1. Simple File Example (5 quick patterns)");
            Console.WriteLine("2. File Operations Example (11 building blocks examples)");
            Console.WriteLine("3. FilePathUploader Example (11 convenience examples)");
            Console.WriteLine("4. FileManager Example (10 file management examples)");
            Console.WriteLine("5. FilePathDownloader Example (11 download examples)");
            Console.WriteLine("6. Run All Examples");
            Console.WriteLine();
            Console.WriteLine("🚀 NEW: JSON Hell Escape Examples!");
            Console.WriteLine("7. Quick Start (5-minute JSON hell relief)");
            Console.WriteLine("8. No-Refactor Migration Guide (keep existing code)");
            Console.WriteLine("9. Dynamic JSON Examples (hashmap-style access)");
            Console.WriteLine();
            Console.WriteLine("0. Exit\n");
            Console.Write("Enter choice: ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        var simple = new SimpleFileExample(client);
                        await simple.Main(new string[0]);
                        break;

                    case "2":
                        var operations = new FileOperationsExample(client);
                        await operations.RunAllExamples();
                        break;

                    case "3":
                        var upload = new FilePathUploaderExample(client);
                        await upload.RunAllExamples();
                        break;

                    case "4":
                        var manager = new FileManagerExample(client);
                        await manager.RunAllExamples();
                        break;

                    case "5":
                        var download = new FilePathDownloaderExample(client);
                        await download.RunAllExamples();
                        break;

                    case "6":
                        await RunAllExamples(client);
                        break;

                    case "7":
                        Console.WriteLine("\n🚀 Running Quick Start - 5-Minute JSON Hell Relief!");
                        await QuickStart.FiveMinuteIntegration();
                        break;

                    case "8":
                        Console.WriteLine("\n🛠️ Running No-Refactor Migration Guide!");
                        await NoRefactorMigrationGuide.RunExample();
                        break;

                    case "9":
                        Console.WriteLine("\n⚡ Running Dynamic JSON Examples!");
                        await DynamicJsonExample.RunExample();
                        break;

                    case "0":
                        Console.WriteLine("\n👋 Goodbye!");
                        return;

                    default:
                        Console.WriteLine("\n❌ Invalid choice. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error running example: {ex.Message}");
                Console.WriteLine($"   {ex.GetType().Name}");
            }

            Console.WriteLine("\n" + new string('─', 48));
        }
    }

    static async Task RunAllExamples(GraphQLClient client)
    {
        Console.WriteLine("\n🚀 Running ALL Examples...\n");
        Console.WriteLine(new string('═', 48) + "\n");

        try
        {
            // Simple examples
            Console.WriteLine("📦 1/5 Simple File Example");
            var simple = new SimpleFileExample(client);
            await simple.Main(new string[0]);
            Console.WriteLine("\n" + new string('─', 48) + "\n");

            // Building blocks
            Console.WriteLine("📦 2/5 File Operations Example");
            var operations = new FileOperationsExample(client);
            await operations.RunAllExamples();
            Console.WriteLine("\n" + new string('─', 48) + "\n");

            // Upload convenience
            Console.WriteLine("📦 3/5 FilePathUploader Example");
            var upload = new FilePathUploaderExample(client);
            await upload.RunAllExamples();
            Console.WriteLine("\n" + new string('─', 48) + "\n");

            // File management
            Console.WriteLine("📦 4/5 FileManager Example");
            var manager = new FileManagerExample(client);
            await manager.RunAllExamples();
            Console.WriteLine("\n" + new string('─', 48) + "\n");

            // Download convenience
            Console.WriteLine("📦 5/5 FilePathDownloader Example");
            var download = new FilePathDownloaderExample(client);
            await download.RunAllExamples();
            Console.WriteLine("\n" + new string('═', 48) + "\n");

            Console.WriteLine("🎉 ALL EXAMPLES COMPLETED SUCCESSFULLY!\n");
            Console.WriteLine("Total: 47 examples across 5 categories");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Failed to complete all examples: {ex.Message}");
        }
    }

    static async Task<GraphQLClient> SetupClient()
    {
        Console.WriteLine("Setting up EYWA client...\n");

        // TODO: Replace with actual client setup
        // This should:
        // 1. Check for environment variables (EYWA_SERVER_URL, etc.)
        // 2. Read from configuration file
        // 3. Or prompt user for connection details
        // 4. Establish connection
        // 5. Authenticate (device flow, client credentials, etc.)

        // For now, returning a mock placeholder
        // Replace this with actual GraphQLClient initialization
        Console.WriteLine("⚠️  Client setup not implemented yet");
        Console.WriteLine("    TODO: Add actual GraphQL client initialization");
        Console.WriteLine("    See ManualIntegrationTest.cs for reference\n");

        throw new NotImplementedException(
            "Client setup not implemented. " +
            "Please implement SetupClient() method with your EYWA connection details."
        );

        // Example implementation:
        // var serverUrl = Environment.GetEnvironmentVariable("EYWA_SERVER_URL") ?? "http://localhost:8080";
        // var client = new GraphQLClient(serverUrl);
        // await client.AuthenticateAsync();
        // return client;
    }

    static void ShowHelp()
    {
        Console.WriteLine("EYWA C# Client - File Operations Examples");
        Console.WriteLine("\nUsage:");
        Console.WriteLine("  dotnet run                    Run in interactive mode");
        Console.WriteLine("  dotnet run -- <command>       Run specific example\n");
        Console.WriteLine("Available commands:");
        Console.WriteLine("  simple        Run Simple File Example (5 examples)");
        Console.WriteLine("  operations    Run File Operations Example (11 examples)");
        Console.WriteLine("  upload        Run FilePathUploader Example (11 examples)");
        Console.WriteLine("  manager       Run FileManager Example (10 examples)");
        Console.WriteLine("  download      Run FilePathDownloader Example (11 examples)");
        Console.WriteLine("  all           Run all examples (47 total)");
        Console.WriteLine("  help          Show this help message\n");
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run");
        Console.WriteLine("  dotnet run -- upload");
        Console.WriteLine("  dotnet run -- all\n");
        Console.WriteLine("Documentation:");
        Console.WriteLine("  See RUNNING_EXAMPLES.md for detailed instructions");
        Console.WriteLine("  See README_FILE_OPERATIONS.md for API documentation\n");
    }
}