/// <summary>
/// EYWA Client - Main Entry Point
/// 
/// Clean, dynamic GraphQL-first client for EYWA platform.
/// Uses Dictionary&lt;string, object&gt; for all data interchange to stay
/// as close to GraphQL as possible.
/// </summary>

using System.Text.Json;
using EywaClient.Core;
using EywaClient.Files;

namespace EywaClient;

/// <summary>
/// Main EYWA client with GraphQL and file operations
/// </summary>
public class Eywa : IDisposable
{
    private readonly JsonRpcClient _jsonRpcClient;
    private readonly TaskManager _taskManager;
    private readonly Logger _logger;
    private readonly FilesClient _filesClient;
    
    public Eywa()
    {
        _jsonRpcClient = new JsonRpcClient();
        _taskManager = new TaskManager(_jsonRpcClient);
        _logger = new Logger(_jsonRpcClient);
        _filesClient = new FilesClient(_jsonRpcClient);
    }
    
    /// <summary>
    /// Task management operations
    /// </summary>
    public TaskManager Tasks => _taskManager;
    
    /// <summary>
    /// Structured logging operations
    /// </summary>
    public Logger Logger => _logger;
    
    /// <summary>
    /// File operations (implements FILES_SPEC.md)
    /// </summary>
    public FilesClient Files => _filesClient;
    
    /// <summary>
    /// Open communication pipe to EYWA server
    /// </summary>
    public void OpenPipe()
    {
        _jsonRpcClient.OpenPipe();
    }
    
    /// <summary>
    /// Execute GraphQL query or mutation with dynamic parameters and results
    /// This is the core method - everything else builds on this
    /// </summary>
    public async Task<Dictionary<string, object>> GraphQLAsync(string query, object? variables = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["query"] = query
        };
        
        if (variables != null)
            parameters["variables"] = variables;
        
        var response = await _jsonRpcClient.SendRequestAsync("eywa.datasets.graphql", parameters);
        
        // Debug: Log the actual response structure
        Console.WriteLine($"DEBUG: Full GraphQL response: {JsonSerializer.Serialize(response)}");
        
        // Extract result from JSON-RPC response
        var resultObj = response.GetValueOrDefault("result");
        Dictionary<string, object> result;
        
        if (resultObj is JsonElement jsonElement)
        {
            // Convert JsonElement to Dictionary
            var resultJson = jsonElement.GetRawText();
            result = JsonSerializer.Deserialize<Dictionary<string, object>>(resultJson) ?? new Dictionary<string, object>();
        }
        else if (resultObj is Dictionary<string, object> dict)
        {
            result = dict;
        }
        else
        {
            throw new Exception($"Unexpected result type: {resultObj?.GetType()}");
        }
        
        Console.WriteLine($"DEBUG: Parsed result: {JsonSerializer.Serialize(result)}");
        
        // Check for GraphQL errors
        if (result.ContainsKey("error"))
        {
            var error = result["error"];
            throw new GraphQLException($"GraphQL error: {JsonSerializer.Serialize(error)}");
        }
        
        return result;
    }
    
    /// <summary>
    /// Send JSON-RPC request (low-level access)
    /// </summary>
    public async Task<Dictionary<string, object>> SendRequestAsync(string method, object? parameters = null)
    {
        return await _jsonRpcClient.SendRequestAsync(method, parameters);
    }
    
    /// <summary>
    /// Send JSON-RPC notification (low-level access)
    /// </summary>
    public async Task SendNotificationAsync(string method, object? parameters = null)
    {
        await _jsonRpcClient.SendNotificationAsync(method, parameters);
    }
    
    /// <summary>
    /// Register handler for incoming requests (low-level access)
    /// </summary>
    public void RegisterHandler(string method, Func<Dictionary<string, object>, object> handler)
    {
        _jsonRpcClient.RegisterHandler(method, handler);
    }
    
    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        _jsonRpcClient.Dispose();
    }
}

/// <summary>
/// GraphQL-specific exception
/// </summary>
public class GraphQLException : Exception
{
    public GraphQLException(string message) : base(message) { }
    public GraphQLException(string message, Exception innerException) : base(message, innerException) { }
}

// TaskStatus enum is now only in Core namespace to avoid conflicts