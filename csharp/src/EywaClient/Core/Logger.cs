/// <summary>
/// EYWA Logger - Simple structured logging
/// 
/// Uses dynamic data structures for logging parameters to stay
/// GraphQL-aligned and flexible.
/// </summary>

using EywaClient.Core;

namespace EywaClient.Core;

/// <summary>
/// Simple structured logger for EYWA
/// </summary>
public class Logger
{
    private readonly JsonRpcClient _client;
    
    public Logger(JsonRpcClient client)
    {
        _client = client;
    }
    
    /// <summary>
    /// Log info message
    /// </summary>
    public async Task InfoAsync(string message, object? data = null)
    {
        await LogAsync("INFO", message, data);
    }
    
    /// <summary>
    /// Log debug message
    /// </summary>
    public async Task DebugAsync(string message, object? data = null)
    {
        await LogAsync("DEBUG", message, data);
    }
    
    /// <summary>
    /// Log warning message
    /// </summary>
    public async Task WarnAsync(string message, object? data = null)
    {
        await LogAsync("WARN", message, data);
    }
    
    /// <summary>
    /// Log error message
    /// </summary>
    public async Task ErrorAsync(string message, object? data = null)
    {
        await LogAsync("ERROR", message, data);
    }
    
    /// <summary>
    /// Log trace message
    /// </summary>
    public async Task TraceAsync(string message, object? data = null)
    {
        await LogAsync("TRACE", message, data);
    }
    
    /// <summary>
    /// Log exception
    /// </summary>
    public async Task ExceptionAsync(string message, object? data = null)
    {
        await LogAsync("EXCEPTION", message, data);
    }
    

    
    /// <summary>
    /// Internal logging method
    /// </summary>
    private async Task LogAsync(string eventType, string message, object? data)
    {
        var parameters = new Dictionary<string, object>
        {
            ["time"] = DateTime.UtcNow,
            ["event"] = eventType,
            ["message"] = message
        };
        
        if (data != null)
            parameters["data"] = data;
        
        await _client.SendNotificationAsync("task.log", parameters);
    }
}