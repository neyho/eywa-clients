using System.Text.Json.Serialization;

namespace EywaClient.Models;

/// <summary>
/// Represents a GraphQL request.
/// </summary>
/// <typeparam name="TVariables">The type of variables object.</typeparam>
public class GraphQLRequest<TVariables>
{
    /// <summary>
    /// The GraphQL query or mutation string.
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Optional variables for the GraphQL operation.
    /// </summary>
    [JsonPropertyName("variables")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TVariables? Variables { get; set; }
}

/// <summary>
/// Represents a GraphQL response.
/// </summary>
/// <typeparam name="TData">The type of the data object.</typeparam>
public class GraphQLResponse<TData>
{
    /// <summary>
    /// The GraphQL result data. Present on success.
    /// </summary>
    [JsonPropertyName("data")]
    public TData? Data { get; set; }

    /// <summary>
    /// List of GraphQL errors. Present if there were errors.
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphQLError>? Errors { get; set; }
}

/// <summary>
/// Represents a GraphQL error.
/// </summary>
public class GraphQLError
{
    /// <summary>
    /// The error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The locations in the query where the error occurred.
    /// </summary>
    [JsonPropertyName("locations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphQLErrorLocation>? Locations { get; set; }

    /// <summary>
    /// The path to the field that caused the error.
    /// </summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? Path { get; set; }

    /// <summary>
    /// Additional error extensions.
    /// </summary>
    [JsonPropertyName("extensions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Extensions { get; set; }
}

/// <summary>
/// Represents a location in a GraphQL query where an error occurred.
/// </summary>
public class GraphQLErrorLocation
{
    /// <summary>
    /// The line number (1-indexed).
    /// </summary>
    [JsonPropertyName("line")]
    public int Line { get; set; }

    /// <summary>
    /// The column number (1-indexed).
    /// </summary>
    [JsonPropertyName("column")]
    public int Column { get; set; }
}
