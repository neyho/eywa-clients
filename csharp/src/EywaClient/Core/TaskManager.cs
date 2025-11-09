/// <summary>
/// EYWA Task Manager - Simple task lifecycle management
/// 
/// Handles task status updates using dynamic data structures
/// to stay GraphQL-aligned.
/// </summary>

using EywaClient.Core;

namespace EywaClient.Core;

/// <summary>
/// EYWA execution status
/// </summary>
/// <summary>
/// Task status values for EYWA operations
/// </summary>
public enum Status
{
    /// <summary>Task completed successfully</summary>
    Success,
    /// <summary>Task failed with error</summary>
    Error,
    /// <summary>Task is currently processing</summary>
    Processing,
    /// <summary>Task encountered an exception</summary>
    Exception
}

/// <summary>
/// Simple task lifecycle manager
/// </summary>
public class TaskManager
{
    private readonly JsonRpcClient _client;
    
    /// <summary>
    /// Creates a new TaskManager instance
    /// </summary>
    /// <param name="client">The JSON-RPC client to use for task operations</param>
    public TaskManager(JsonRpcClient client)
    {
        _client = client;
    }
    
    /// <summary>
    /// Get current task information
    /// </summary>
    public async Task<Dictionary<string, object>> GetTaskAsync()
    {
        var response = await _client.SendRequestAsync("task.get");
        return (response["result"] as Dictionary<string, object>) ?? new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Update task status
    /// </summary>
    public async Task UpdateTaskAsync(Status status)
    {
        var statusString = status switch
        {
            Status.Success => "SUCCESS",
            Status.Error => "ERROR", 
            Status.Processing => "PROCESSING",
            Status.Exception => "EXCEPTION",
            _ => "PROCESSING"
        };
        
        await _client.SendNotificationAsync("task.update", new Dictionary<string, object>
        {
            ["status"] = statusString
        });
    }
    
    /// <summary>
    /// Return task (without closing)
    /// </summary>
    public async Task ReturnTaskAsync()
    {
        await _client.SendNotificationAsync("task.return");
    }
    
    /// <summary>
    /// Close task with final status
    /// </summary>
    public async Task CloseTaskAsync(Status status)
    {
        var statusString = status switch
        {
            Status.Success => "SUCCESS",
            Status.Error => "ERROR",
            Status.Processing => "PROCESSING", 
            Status.Exception => "EXCEPTION",
            _ => "SUCCESS"
        };
        
        await _client.SendNotificationAsync("task.close", new Dictionary<string, object>
        {
            ["status"] = statusString
        });
    }
}