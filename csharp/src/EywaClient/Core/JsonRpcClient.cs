/// <summary>
/// EYWA C# Client - Clean JSON-RPC 2.0 Implementation
/// 
/// Pure dynamic approach for GraphQL-first communication over stdin/stdout.
/// Uses Dictionary&lt;string, object&gt; for all data interchange to stay as close
/// to GraphQL as possible.
/// </summary>

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace EywaClient.Core;

/// <summary>
/// JSON-RPC 2.0 client for EYWA communication over stdin/stdout.
/// Handles bidirectional communication using dynamic data structures.
/// </summary>
public class JsonRpcClient
{
    private readonly object _lockObject = new();
    private readonly Dictionary<string, TaskCompletionSource<Dictionary<string, object>>> _pendingRequests = new();
    private readonly Dictionary<string, Func<Dictionary<string, object>, object>> _requestHandlers = new();
    
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private Task? _readerTask;
    private bool _isDisposed;
    private int _nextRequestId = 1;

    /// <summary>
    /// Open communication pipe to EYWA server
    /// </summary>
    public void OpenPipe()
    {
        if (_stdin != null)
            throw new InvalidOperationException("Pipe is already open");

        // Use current process stdin/stdout for EYWA communication
        _stdin = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        _stdout = new StreamReader(Console.OpenStandardInput());
        
        // Start reading incoming messages
        _readerTask = Task.Run(ReadMessages);
    }

    /// <summary>
    /// Send JSON-RPC request and await response
    /// </summary>
    public async Task<Dictionary<string, object>> SendRequestAsync(string method, object? parameters = null)
    {
        if (_stdin == null) throw new InvalidOperationException("Pipe not open");
        
        var requestId = Interlocked.Increment(ref _nextRequestId).ToString();
        var tcs = new TaskCompletionSource<Dictionary<string, object>>();
        
        lock (_lockObject)
        {
            _pendingRequests[requestId] = tcs;
        }
        
        var request = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = method
        };
        
        if (parameters != null)
            request["params"] = parameters;
        
        var json = JsonSerializer.Serialize(request);
        
        await _stdin.WriteLineAsync(json);
        
        return await tcs.Task;
    }

    /// <summary>
    /// Send JSON-RPC notification (no response expected)
    /// </summary>
    public async Task SendNotificationAsync(string method, object? parameters = null)
    {
        if (_stdin == null) throw new InvalidOperationException("Pipe not open");
        
        var notification = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method
        };
        
        if (parameters != null)
            notification["params"] = parameters;
        
        var json = JsonSerializer.Serialize(notification);
        await _stdin.WriteLineAsync(json);
    }

    /// <summary>
    /// Register handler for incoming JSON-RPC requests
    /// </summary>
    public void RegisterHandler(string method, Func<Dictionary<string, object>, object> handler)
    {
        lock (_lockObject)
        {
            _requestHandlers[method] = handler;
        }
    }

    /// <summary>
    /// Read and process incoming JSON-RPC messages
    /// </summary>
    private async Task ReadMessages()
    {
        if (_stdout == null) return;
        
        try
        {
            string? line;
            while ((line = await _stdout.ReadLineAsync()) != null && !_isDisposed)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                try
                {
                    var message = JsonSerializer.Deserialize<Dictionary<string, object>>(line);
                    if (message != null)
                    {
                        await ProcessMessage(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing message: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading messages: {ex.Message}");
        }
    }

    /// <summary>
    /// Process incoming JSON-RPC message
    /// </summary>
    private async Task ProcessMessage(Dictionary<string, object> message)
    {
        // Check if it's a response to our request
        if (message.ContainsKey("id") && !message.ContainsKey("method"))
        {
            var id = message["id"]?.ToString();
            if (id != null)
            {
                TaskCompletionSource<Dictionary<string, object>>? tcs = null;
                
                lock (_lockObject)
                {
                    if (_pendingRequests.TryGetValue(id, out tcs))
                    {
                        _pendingRequests.Remove(id);
                    }
                }
                
                if (tcs != null)
                {
                if (message.ContainsKey("error"))
                {
                    // The error can be either a Dictionary or JsonElement
                    var errorObj = message["error"];
                    string errorMessage = "Unknown error";
                    string errorCode = "0";
                    
                    if (errorObj is JsonElement errorElement)
                    {
                        if (errorElement.TryGetProperty("message", out var msgProp))
                            errorMessage = msgProp.GetString() ?? "Unknown error";
                        if (errorElement.TryGetProperty("code", out var codeProp))
                            errorCode = codeProp.ToString();
                    }
                    else if (errorObj is Dictionary<string, object> errorDict)
                    {
                        errorMessage = errorDict.GetValueOrDefault("message")?.ToString() ?? "Unknown error";
                        errorCode = errorDict.GetValueOrDefault("code")?.ToString() ?? "0";
                    }
                    
                    Console.WriteLine($"ERROR: {errorMessage}");
                    tcs.SetException(new JsonRpcException(errorMessage, errorCode));
                    }
                    else
                    {
                        tcs.SetResult(message);
                    }
                }
            }
        }
        // Check if it's an incoming request
        else if (message.ContainsKey("method") && message.ContainsKey("id"))
        {
            var method = message["method"]?.ToString();
            var id = message["id"]?.ToString();
            
            if (method != null && id != null)
            {
                Func<Dictionary<string, object>, object>? handler = null;
                
                lock (_lockObject)
                {
                    _requestHandlers.TryGetValue(method, out handler);
                }
                
                if (handler != null)
                {
                    try
                    {
                        var parameters = message.GetValueOrDefault("params") as Dictionary<string, object> ?? new();
                        var result = handler(parameters);
                        
                        var response = new Dictionary<string, object>
                        {
                            ["jsonrpc"] = "2.0",
                            ["id"] = id,
                            ["result"] = result
                        };
                        
                        if (_stdin != null)
                        {
                            var json = JsonSerializer.Serialize(response);
                            await _stdin.WriteLineAsync(json);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorResponse = new Dictionary<string, object>
                        {
                            ["jsonrpc"] = "2.0",
                            ["id"] = id,
                            ["error"] = new Dictionary<string, object>
                            {
                                ["code"] = -32603,
                                ["message"] = ex.Message
                            }
                        };
                        
                        if (_stdin != null)
                        {
                            var json = JsonSerializer.Serialize(errorResponse);
                            await _stdin.WriteLineAsync(json);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        
        try
        {
            _stdin?.Close();
            _stdout?.Close();
        }
        catch
        {
            // Ignore cleanup errors
        }
        
        // Complete any pending requests with cancellation
        lock (_lockObject)
        {
            foreach (var tcs in _pendingRequests.Values)
            {
                tcs.SetCanceled();
            }
            _pendingRequests.Clear();
        }
    }
}

/// <summary>
/// Exception for JSON-RPC errors
/// </summary>
public class JsonRpcException : Exception
{
    public string Code { get; }
    
    public JsonRpcException(string message, string code) : base(message)
    {
        Code = code;
    }
}