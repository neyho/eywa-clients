using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using EywaClient.Exceptions;
using EywaClient.Models;
using EywaClient.Utilities;

namespace EywaClient.Core;

/// <summary>
/// Handles JSON-RPC 2.0 communication over stdin/stdout.
/// </summary>
public class JsonRpcClient : IDisposable
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _callbacks;
    private readonly ConcurrentDictionary<string, Action<JsonRpcRequest>> _handlers;
    private readonly SemaphoreSlim _writeLock;
    private readonly CancellationTokenSource _cts;
    private Task? _readTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcClient"/> class.
    /// </summary>
    /// <param name="input">The input stream (default: Console.In).</param>
    /// <param name="output">The output stream (default: Console.Out).</param>
    public JsonRpcClient(TextReader? input = null, TextWriter? output = null)
    {
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
        _callbacks = new ConcurrentDictionary<string, TaskCompletionSource<JsonElement>>();
        _handlers = new ConcurrentDictionary<string, Action<JsonRpcRequest>>();
        _writeLock = new SemaphoreSlim(1, 1);
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Initializes stdin/stdout communication and starts reading messages.
    /// Must be called before using other methods.
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new JsonRpcClient();
    /// client.OpenPipe();
    /// 
    /// var result = await client.SendRequestAsync&lt;string&gt;("method", new { param = "value" });
    /// </code>
    /// </example>
    public void OpenPipe()
    {
        if (_readTask != null)
            throw new InvalidOperationException("Pipe is already open");

        _readTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
    }

    /// <summary>
    /// Sends a JSON-RPC request and awaits the response.
    /// </summary>
    /// <typeparam name="T">The expected result type.</typeparam>
    /// <param name="method">The method name to invoke.</param>
    /// <param name="parameters">The method parameters (can be null).</param>
    /// <returns>The result of the method invocation.</returns>
    /// <exception cref="JsonRpcException">Thrown when the RPC call fails.</exception>
    /// <example>
    /// <code>
    /// var task = await client.SendRequestAsync&lt;TaskInfo&gt;("task.get", null);
    /// Console.WriteLine($"Task: {task.Message}");
    /// </code>
    /// </example>
    public async Task<T?> SendRequestAsync<T>(string method, object? parameters = null)
    {
        var id = Guid.NewGuid().ToString();
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Method = method,
            Params = parameters,
            Id = id
        };

        var tcs = new TaskCompletionSource<JsonElement>();
        _callbacks[id] = tcs;

        try
        {
            await WriteAsync(request).ConfigureAwait(false);
            var result = await tcs.Task.ConfigureAwait(false);

            // Deserialize result to target type
            if (typeof(T) == typeof(JsonElement))
            {
                return (T)(object)result;
            }

            return JsonSerializer.Deserialize<T>(result.GetRawText());
        }
        finally
        {
            _callbacks.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Sends a JSON-RPC notification (no response expected).
    /// </summary>
    /// <param name="method">The method name to invoke.</param>
    /// <param name="parameters">The method parameters (can be null).</param>
    /// <example>
    /// <code>
    /// client.SendNotification("task.log", new 
    /// { 
    ///     event = "INFO", 
    ///     message = "Processing started" 
    /// });
    /// </code>
    /// </example>
    public void SendNotification(string method, object? parameters = null)
    {
        var notification = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Method = method,
            Params = parameters
            // No Id field for notifications
        };

        // Fire and forget - don't await
        _ = WriteAsync(notification);
    }

    /// <summary>
    /// Registers a handler for incoming JSON-RPC method calls.
    /// </summary>
    /// <param name="method">The method name to handle.</param>
    /// <param name="handler">The handler function to invoke.</param>
    /// <example>
    /// <code>
    /// client.RegisterHandler("custom.action", (request) =>
    /// {
    ///     Console.WriteLine($"Received: {request.Method}");
    ///     // Handle the request
    /// });
    /// </code>
    /// </example>
    public void RegisterHandler(string method, Action<JsonRpcRequest> handler)
    {
        _handlers[method] = handler;
    }

    /// <summary>
    /// Removes a registered handler.
    /// </summary>
    /// <param name="method">The method name to unregister.</param>
    /// <returns>True if the handler was removed, false if it didn't exist.</returns>
    public bool UnregisterHandler(string method)
    {
        return _handlers.TryRemove(method, out _);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _input.ReadLineAsync().ConfigureAwait(false);
                
                if (line == null)
                    break; // End of stream

                buffer.Append(line);

                try
                {
                    // Try to parse as complete JSON
                    using var document = JsonDocument.Parse(buffer.ToString());
                    var root = document.RootElement;

                    // Successfully parsed - handle the message
                    HandleMessage(root);
                    buffer.Clear();
                }
                catch (JsonException)
                {
                    // Incomplete JSON - continue buffering
                    // This handles large responses split across multiple reads
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in read loop: {ex.Message}");
        }
    }

    private void HandleMessage(JsonElement message)
    {
        // Check if it's a request (has "method" field)
        if (message.TryGetProperty("method", out _))
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(message.GetRawText());
            if (request != null)
            {
                HandleRequest(request);
            }
        }
        // Check if it's a response (has "result" or "error" field)
        else if (message.TryGetProperty("result", out _) || message.TryGetProperty("error", out _))
        {
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(message.GetRawText());
            if (response != null)
            {
                HandleResponse(response);
            }
        }
        else
        {
            Console.Error.WriteLine($"Received invalid JSON-RPC message: {message}");
        }
    }

    private void HandleRequest(JsonRpcRequest request)
    {
        if (_handlers.TryGetValue(request.Method, out var handler))
        {
            try
            {
                handler(request);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error handling request {request.Method}: {ex.Message}");
            }
        }
        else
        {
            Console.Error.WriteLine($"No handler registered for method: {request.Method}");
        }
    }

    private void HandleResponse(JsonRpcResponse response)
    {
        if (string.IsNullOrEmpty(response.Id))
        {
            Console.Error.WriteLine("Received response without ID");
            return;
        }

        if (_callbacks.TryRemove(response.Id, out var tcs))
        {
            if (response.Error != null)
            {
                // Response contains an error
                tcs.SetException(new JsonRpcException(response.Error));
            }
            else if (response.Result != null)
            {
                // Response contains a result
                if (response.Result is JsonElement element)
                {
                    tcs.SetResult(element);
                }
                else
                {
                    // Convert to JsonElement
                    var json = JsonSerializer.Serialize(response.Result);
                    var doc = JsonDocument.Parse(json);
                    tcs.SetResult(doc.RootElement);
                }
            }
            else
            {
                // No result or error - shouldn't happen
                tcs.SetException(new JsonRpcException(new JsonRpcError
                {
                    Code = -32603,
                    Message = "Response contained neither result nor error"
                }));
            }
        }
        else
        {
            Console.Error.WriteLine($"No callback registered for response ID: {response.Id}");
        }
    }

    private async Task WriteAsync(object data)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(data);
            await _output.WriteLineAsync(json).ConfigureAwait(false);
            await _output.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Disposes the JSON-RPC client and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel the read loop
        _cts?.Cancel();

        // Wait for read task to complete
        if (_readTask != null)
        {
            try
            {
                _readTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Expected when task is cancelled
            }
        }

        // Dispose resources
        _cts?.Dispose();
        _writeLock?.Dispose();

        // Fail any pending callbacks
        foreach (var kvp in _callbacks)
        {
            kvp.Value.TrySetException(new ObjectDisposedException(nameof(JsonRpcClient)));
        }
        _callbacks.Clear();
    }
}

/// <summary>
/// Extension methods to add dynamic GraphQL access to JsonRpcClient.
/// This eliminates JsonElement hell by providing hashmap-like access!
/// </summary>
public static class JsonRpcClientExtensions
{
    /// <summary>
    /// Execute GraphQL and get dynamic JSON result - just like Clojure maps!
    /// NO MORE JsonElement.TryGetProperty() hell!
    /// </summary>
    /// <param name="client">The JsonRpcClient instance</param>
    /// <param name="query">GraphQL query/mutation</param>
    /// <param name="variables">Variables (can be anonymous object, Dictionary, or null)</param>
    /// <returns>Dynamic JSON that works like a hashmap</returns>
    /// <example>
    /// <code>
    /// // OLD PAINFUL WAY:
    /// if (result.TryGetProperty("data", out var data)) {
    ///     if (data.TryGetProperty("searchUser", out var users)) {
    ///         // More JsonElement hell...
    ///     }
    /// }
    ///
    /// // NEW SIMPLE WAY:
    /// var result = await client.GraphQLDynamic(query);
    /// var userName = result.data.searchUser[0].name;  // DONE!
    /// </code>
    /// </example>
    public static async Task<dynamic> GraphQLDynamic(this JsonRpcClient client, string query, object? variables = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or empty", nameof(query));

        var result = await client.SendRequestAsync<JsonElement>(
            "eywa.datasets.graphql",
            new { query, variables }
        ).ConfigureAwait(false);

        return new EywaClient.Utilities.DynamicJson(result);
    }

    /// <summary>
    /// Clojure-style get-in access: await client.GetIn("data.searchUser.0.name", query)
    /// </summary>
    /// <typeparam name="T">Type to return</typeparam>
    /// <param name="client">The JsonRpcClient instance</param>
    /// <param name="path">Path to the data (e.g., "data.searchUser.0.name")</param>
    /// <param name="query">GraphQL query</param>
    /// <param name="variables">Optional variables</param>
    /// <returns>The value at the specified path</returns>
    /// <example>
    /// <code>
    /// // Get specific value directly
    /// var userName = await client.GetIn&lt;string&gt;("data.searchUser.0.name", query);
    /// var userCount = await client.GetIn&lt;int&gt;("data.searchUser.length", query);
    /// </code>
    /// </example>
    public static async Task<T?> GetIn<T>(this JsonRpcClient client, string path, string query, object? variables = null)
    {
        var result = await client.GraphQLDynamic(query, variables);
        return ((EywaClient.Utilities.DynamicJson)result).Get<T>(path);
    }

    /// <summary>
    /// Get just the data portion of a GraphQL response
    /// Equivalent to Clojure's (get result "data")
    /// </summary>
    /// <param name="client">The JsonRpcClient instance</param>
    /// <param name="query">GraphQL query</param>
    /// <param name="variables">Optional variables</param>
    /// <returns>The data portion of the response</returns>
    /// <example>
    /// <code>
    /// var data = await client.GetData(query);
    /// var users = data.searchUser;  // Direct access to data
    /// </code>
    /// </example>
    public static async Task<dynamic> GetData(this JsonRpcClient client, string query, object? variables = null)
    {
        var result = await client.GraphQLDynamic(query, variables);
        return result.data;
    }

    /// <summary>
    /// Convert any JsonElement to dynamic access
    /// </summary>
    /// <param name="element">The JsonElement to wrap</param>
    /// <returns>Dynamic wrapper for easy access</returns>
    /// <example>
    /// <code>
    /// var result = await client.SendRequestAsync&lt;JsonElement&gt;(method, params);
    /// dynamic dynamic = result.AsDynamic();
    /// var value = dynamic.some.nested.property;  // Easy!
    /// </code>
    /// </example>
    public static dynamic AsDynamic(this JsonElement element)
    {
        return new EywaClient.Utilities.DynamicJson(element);
    }
}