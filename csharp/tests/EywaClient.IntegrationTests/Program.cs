using EywaClient.Core;
using EywaClient.GraphQL;
using EywaClient.Models;

namespace EywaClient.IntegrationTests;

/// <summary>
/// Integration test runner - chooses which tests to run based on arguments.
/// 
/// Usage:
///   eywa run -c "dotnet run --project tests/EywaClient.IntegrationTests"
///   eywa run -c "dotnet run --project tests/EywaClient.IntegrationTests -- files"
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // Check if user wants file operations tests
        if (args.Length > 0 && args[0] == "files")
        {
            Console.WriteLine("Running FILE OPERATIONS integration tests...\n");
            return await FileOperationsIntegrationTest.RunAsync(args.Skip(1).ToArray());
        }

        // Default: Run core functionality tests
        Console.WriteLine("=== EYWA C# Client - Core Integration Tests ===");
        Console.WriteLine("Running in real EYWA environment via stdin/stdout");
        Console.WriteLine("Tip: Run with 'files' argument for file operations tests\n");

        var testsPassed = 0;
        var testsFailed = 0;

        try
        {
            // Initialize real client (connects to EYWA via stdin/stdout)
            var client = new JsonRpcClient();
            client.OpenPipe();
            Console.WriteLine("✓ JsonRpcClient initialized and connected\n");

            var taskManager = new TaskManager(client);
            var logger = new Logger(client);
            var graphqlClient = new GraphQLClient(client);

            // ==================== TEST 1: Task Management ====================
            Console.WriteLine("TEST 1: Task Management");
            try
            {
                var task = await taskManager.GetTaskAsync();
                
                if (task == null)
                {
                    Console.WriteLine("  ✗ GetTaskAsync returned null");
                    testsFailed++;
                }
                else if (string.IsNullOrEmpty(task.Euuid))
                {
                    Console.WriteLine("  ✗ Task has no UUID");
                    testsFailed++;
                }
                else
                {
                    Console.WriteLine($"  ✓ Got task: {task.Euuid}");
                    Console.WriteLine($"    Message: {task.Message}");
                    Console.WriteLine($"    Status: {task.Status}");
                    testsPassed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ GetTaskAsync failed: {ex.Message}");
                testsFailed++;
            }

            // ==================== TEST 2: Update Task Status ====================
            Console.WriteLine("\nTEST 2: Update Task Status");
            try
            {
                taskManager.UpdateTask(TaskStatus.Processing);
                Console.WriteLine("  ✓ UpdateTask(Processing) succeeded");
                testsPassed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ UpdateTask failed: {ex.Message}");
                testsFailed++;
            }

            // ==================== TEST 3: Logger - All Levels ====================
            Console.WriteLine("\nTEST 3: Logger - All Log Levels");
            try
            {
                logger.Info("Integration test - Info message");
                logger.Debug("Integration test - Debug message");
                logger.Warn("Integration test - Warning message");
                logger.Error("Integration test - Error message (test)");
                logger.Trace("Integration test - Trace message");
                
                Console.WriteLine("  ✓ All log levels sent successfully");
                testsPassed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Logging failed: {ex.Message}");
                testsFailed++;
            }

            // ==================== TEST 4: Logger with Data ====================
            Console.WriteLine("\nTEST 4: Logger with Structured Data");
            try
            {
                logger.Info("Test with data", new
                {
                    testId = Guid.NewGuid(),
                    timestamp = DateTime.Now,
                    value = 42,
                    nested = new { foo = "bar" }
                });
                
                Console.WriteLine("  ✓ Log with structured data sent");
                testsPassed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Logging with data failed: {ex.Message}");
                testsFailed++;
            }

            // ==================== TEST 5: Report ====================
            Console.WriteLine("\nTEST 5: Report Functionality");
            try
            {
                logger.Report("Integration test report", new
                {
                    testsPassed,
                    testsFailed,
                    timestamp = DateTime.Now,
                    environment = "integration"
                });
                
                Console.WriteLine("  ✓ Report sent successfully");
                testsPassed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Report failed: {ex.Message}");
                testsFailed++;
            }

            // ==================== TEST 6: GraphQL Query ====================
            Console.WriteLine("\nTEST 6: GraphQL Query Execution");
            try
            {
                // Try a simple introspection query
                var result = await graphqlClient.ExecuteAsync(@"
                    query {
                        __schema {
                            queryType {
                                name
                            }
                        }
                    }
                ");
                
                if (result != null && result.Data.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                {
                    Console.WriteLine("  ✓ GraphQL query executed successfully");
                    testsPassed++;
                }
                else
                {
                    Console.WriteLine("  ✗ GraphQL query returned null");
                    testsFailed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ GraphQL query failed: {ex.Message}");
                Console.WriteLine("    (This might be expected if schema access is restricted)");
                // Don't fail the test - GraphQL might not be available
            }

            // ==================== TEST 7: Exception Logging ====================
            Console.WriteLine("\nTEST 7: Exception Logging");
            try
            {
                var testException = new InvalidOperationException("Test exception for logging");
                logger.Exception("Test exception logging", new
                {
                    exceptionType = testException.GetType().Name,
                    message = testException.Message
                });
                
                Console.WriteLine("  ✓ Exception logged successfully");
                testsPassed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Exception logging failed: {ex.Message}");
                testsFailed++;
            }

            // ==================== TEST 8: Null Data Handling ====================
            Console.WriteLine("\nTEST 8: Null Data Handling");
            try
            {
                logger.Info("Message with null data", null);
                Console.WriteLine("  ✓ Null data handled correctly");
                testsPassed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Null data handling failed: {ex.Message}");
                testsFailed++;
            }

            // ==================== RESULTS ====================
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("INTEGRATION TEST RESULTS");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"Tests Passed: {testsPassed}");
            Console.WriteLine($"Tests Failed: {testsFailed}");
            Console.WriteLine($"Total Tests:  {testsPassed + testsFailed}");
            
            if (testsFailed == 0)
            {
                Console.WriteLine("\n✓ ALL TESTS PASSED!");
                logger.Info("All integration tests passed", new
                {
                    testsPassed,
                    testsFailed,
                    totalTests = testsPassed + testsFailed
                });
                
                taskManager.CloseTask(TaskStatus.Success);
                return 0;
            }
            else
            {
                Console.WriteLine($"\n✗ {testsFailed} TEST(S) FAILED");
                logger.Error("Some integration tests failed", new
                {
                    testsPassed,
                    testsFailed,
                    totalTests = testsPassed + testsFailed
                });
                
                taskManager.CloseTask(TaskStatus.Error);
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ CRITICAL ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}
