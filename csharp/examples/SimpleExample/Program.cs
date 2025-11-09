/**
 * DEAD SIMPLE C# EYWA Test - Just Basic JSON-RPC
 * 
 * Tests ONLY:
 * 1. Get task
 * 2. Log a message  
 * 3. Close task
 */

using EywaClient;
using EywaClient.Core;

class Program
{
    static async Task Main(string[] args)
    {
        var eywa = new Eywa();
        
        try
        {
            // Initialize connection
            eywa.OpenPipe();
            
            // Test 1: Get task
            Console.WriteLine("=== Testing Get Task ===");
            var task = await eywa.Tasks.GetTaskAsync();
            Console.WriteLine($"Task ID: {task.GetValueOrDefault("euuid")}");
            
            // Test 2: Log message
            Console.WriteLine("=== Testing Logging ===");
            await eywa.Logger.InfoAsync("SIMPLE TEST STARTED");
            
            // Test 3: Update status
            await eywa.Tasks.UpdateTaskAsync(Status.Processing);
            
            // Test 4: Success
            Console.WriteLine("=== All Basic Tests Passed ===");
            await eywa.Logger.InfoAsync("SIMPLE TEST COMPLETED");
            await eywa.Tasks.CloseTaskAsync(Status.Success);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            await eywa.Logger.ErrorAsync("SIMPLE TEST FAILED", new { error = ex.Message });
            await eywa.Tasks.CloseTaskAsync(Status.Error);
            throw;
        }
        finally
        {
            eywa.Dispose();
        }
    }
}