using System.Text.Json.Serialization;

namespace EywaClient.Models;

/// <summary>
/// Represents a JSON-RPC 2.0 request.
/// </summary>
public class JsonRpcRequest
{
    /// <summary>
    /// The JSON-RPC protocol version. Must be "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// The method name to be invoked.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// The parameters for the method call. Can be null.
    /// </summary>
    [JsonPropertyName("params")]
    public object? Params { get; set; }

    /// <summary>
    /// The request identifier. Omitted for notifications.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }
}

/// <summary>
/// Represents a JSON-RPC 2.0 response.
/// </summary>
public class JsonRpcResponse
{
    /// <summary>
    /// The JSON-RPC protocol version. Must be "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// The result of the method invocation. Present on success.
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    /// <summary>
    /// The error object. Present on failure.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }

    /// <summary>
    /// The request identifier. Must match the request ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Represents a JSON-RPC 2.0 error object.
/// </summary>
public class JsonRpcError
{
    /// <summary>
    /// The error code.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// A short description of the error.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional error data. Optional.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}
