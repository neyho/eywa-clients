using EywaClient.Core;
using EywaClient.GraphQL;

namespace EywaClient.Examples;

/// <summary>
/// Manual integration test - Run via: eywa run -c "dotnet run --project examples/EywaClient.Examples"
/// </summary>
public class ManualIntegrationTest
{
    public static async Task Run(string[] args)
    {
        Console.WriteLine("=== EYWA C# Client - Manual Integration Test ===\n");

        try
        {
            // Initialize client
            var rpcClient = new JsonRpcClient();
            rpcClient.OpenPipe();
            Console.WriteLine("✓ JSON-RPC client initialized");

            // Initialize components
            var taskManager = new TaskManager(rpcClient);
            var logger = new Logger(rpcClient);
            var graphqlClient = new GraphQLClient(rpcClient);
            Console.WriteLine("✓ All components initialized\n");

            // Test 1: Get Task
            Console.WriteLine("Test 1: Getting current task...");
            var task = await taskManager.GetTaskAsync();
            logger.Info("Task retrieved", new { taskId = task.Euuid, message = task.Message });
            Console.WriteLine($"  Task ID: {task.Euuid}");
            Console.WriteLine($"  Message: {task.Message}");
            Console.WriteLine($"  Status: {task.Status}\n");

            // Test 2: Logging
            Console.WriteLine("Test 2: Testing all log levels...");
            logger.Info("This is an info message");
            logger.Debug("This is a debug message");
            logger.Warn("This is a warning");
            logger.Error("This is an error (test)");
            logger.Trace("This is a trace");
            Console.WriteLine("  ✓ All log levels tested\n");

            // Test 3: Update Task Status
            Console.WriteLine("Test 3: Updating task status...");
            taskManager.UpdateTask(EywaClient.Models.TaskStatus.Processing);
            Console.WriteLine("  ✓ Status updated to PROCESSING\n");

            // Test 4: GraphQL Query
            Console.WriteLine("Test 4: Executing GraphQL query...");
            try
            {
                var result = await graphqlClient.ExecuteAsync(@"
                    query {
                        searchUser(_limit: 3) {
                            euuid
                            name
                        }
                    }
                ");
                
                Console.WriteLine("  ✓ GraphQL query executed successfully");
                Console.WriteLine($"  Response: {result.Data}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ GraphQL query failed (this might be expected if no users exist)");
                Console.WriteLine($"    Error: {ex.Message}");
            }

            // Test 5: Report
            Console.WriteLine("\nTest 5: Sending report...");
            logger.Report("Manual integration test completed", new
            {
                timestamp = DateTime.Now,
                testsRun = 5,
                status = "success"
            });
            Console.WriteLine("  ✓ Report sent\n");

            // Success
            Console.WriteLine("=== All Tests Completed Successfully ===\n");
            logger.Info("Manual integration test completed successfully");
            
            taskManager.CloseTask(EywaClient.Models.TaskStatus.Success);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n❌ Test failed: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            
            Environment.Exit(1);
        }
    }
}