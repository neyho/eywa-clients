# EYWA C# Client - Integration Tests

## Purpose

These are **real integration tests** that run inside the EYWA environment via `eywa run`. They test actual JSON-RPC communication over stdin/stdout with a live EYWA server.

## What Gets Tested

1. ✅ **JsonRpcClient** - Real stdin/stdout communication
2. ✅ **TaskManager** - Task lifecycle (get, update, close)
3. ✅ **Logger** - All log levels (Info, Debug, Warn, Error, Trace, Exception)
4. ✅ **GraphQL** - Query execution
5. ✅ **Structured Data** - JSON serialization
6. ✅ **Error Handling** - Null data, exceptions

## Prerequisites

1. **EYWA Server Running:**
   ```bash
   eywa server start
   ```

2. **EYWA CLI Connected:**
   ```bash
   eywa connect http://localhost:8080
   ```

3. **User Authenticated:**
   ```bash
   eywa status  # Should show connected
   ```

## Running Tests

### From Project Root

```bash
cd /Users/robi/dev/EYWA/core/clients/csharp

# Run integration tests via EYWA
eywa run -c "dotnet run --project tests/EywaClient.IntegrationTests"
```

### Expected Output

```
=== EYWA C# Client - Integration Tests ===
Running in real EYWA environment via stdin/stdout

✓ JsonRpcClient initialized and connected

TEST 1: Task Management
  ✓ Got task: abc-123-def
    Message: Integration test task
    Status: PENDING

TEST 2: Update Task Status
  ✓ UpdateTask(Processing) succeeded

TEST 3: Logger - All Log Levels
  ✓ All log levels sent successfully

TEST 4: Logger with Structured Data
  ✓ Log with structured data sent

TEST 5: Report Functionality
  ✓ Report sent successfully

TEST 6: GraphQL Query Execution
  ✓ GraphQL query executed successfully

TEST 7: Exception Logging
  ✓ Exception logged successfully

TEST 8: Null Data Handling
  ✓ Null data handled correctly

==================================================
INTEGRATION TEST RESULTS
==================================================
Tests Passed: 8
Tests Failed: 0
Total Tests:  8

✓ ALL TESTS PASSED!
```

### Check EYWA Logs

```bash
# View server logs to see JSON-RPC messages
tail -f ~/.eywa/logs/system.log
```

You should see:
- Task assignment
- JSON-RPC notifications for logs
- GraphQL query execution
- Task completion

## What This Proves

✅ **Protocol Compliance** - JSON-RPC 2.0 communication works  
✅ **Stdin/Stdout** - Proper IPC with EYWA CLI  
✅ **Serialization** - JSON serialization/deserialization works  
✅ **Error Handling** - Graceful handling of edge cases  
✅ **Real World** - Tests actual production environment  

## vs Unit Tests

| Unit Tests (with Moq) | Integration Tests (Real) |
|------------------------|--------------------------|
| Mock JsonRpcClient | Real JsonRpcClient |
| Fake responses | Real EYWA server |
| Fast (milliseconds) | Slower (seconds) |
| No EYWA needed | Requires EYWA running |
| Test code logic | Test actual protocol |

**Integration tests are better for this library** because they prove it actually works with EYWA!

## Troubleshooting

### Test Hangs
- Check if EYWA server is running
- Check if CLI is connected
- Check ~/.eywa/logs/system.log for errors

### Test Fails
- Check exit code (0 = success, 1 = failure)
- Review console output for specific test failures
- Check EYWA logs for protocol errors

### GraphQL Test Fails
- This is often expected if GraphQL schema access is restricted
- The test will show a warning but won't fail overall

## Adding New Tests

To add a new test:

1. Add a new test section in `Program.cs`
2. Use try/catch for error handling
3. Increment testsPassed or testsFailed
4. Log the result

Example:

```csharp
Console.WriteLine("\nTEST 9: My New Test");
try
{
    // Your test code here
    
    Console.WriteLine("  ✓ My test passed");
    testsPassed++;
}
catch (Exception ex)
{
    Console.WriteLine($"  ✗ My test failed: {ex.Message}");
    testsFailed++;
}
```

## CI/CD Integration

These tests can run in CI/CD if:
- EYWA server is running in the CI environment
- EYWA CLI is installed and authenticated
- Tests are executed via `eywa run`

Example GitHub Actions:

```yaml
- name: Run Integration Tests
  run: |
    eywa server start
    eywa connect http://localhost:8080
    eywa run -c "dotnet run --project tests/EywaClient.IntegrationTests"
```

---

**Status:** Ready to run!  
**Last Updated:** November 3, 2025
