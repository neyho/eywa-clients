using System.Text.Json;
using EywaClient.Core;
using EywaClient.Exceptions;
using EywaClient.Models;

namespace EywaClient.GraphQL;

/// <summary>
/// Provides GraphQL query and mutation execution against EYWA datasets.
/// </summary>
public class GraphQLClient
{
    private readonly JsonRpcClient _rpcClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLClient"/> class.
    /// </summary>
    /// <param name="rpcClient">The JSON-RPC client.</param>
    public GraphQLClient(JsonRpcClient rpcClient)
    {
        _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
    }

    /// <summary>
    /// Executes a GraphQL query or mutation with strongly-typed result.
    /// </summary>
    /// <typeparam name="T">The expected data type.</typeparam>
    /// <param name="query">The GraphQL query or mutation string.</param>
    /// <param name="variables">Optional variables for the operation.</param>
    /// <returns>The GraphQL response with data.</returns>
    /// <exception cref="GraphQLException">Thrown when the GraphQL operation fails.</exception>
    /// <example>
    /// <code>
    /// // Simple query
    /// var result = await client.ExecuteAsync&lt;SearchUserResponse&gt;(@"
    ///     query {
    ///         searchUser(_limit: 10) {
    ///             euuid
    ///             name
    ///             email
    ///         }
    ///     }
    /// ");
    /// 
    /// foreach (var user in result.Data.SearchUser)
    /// {
    ///     Console.WriteLine($"{user.Name}: {user.Email}");
    /// }
    /// </code>
    /// </example>
    public async Task<GraphQLResponse<T>> ExecuteAsync<T>(string query, object? variables = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or empty", nameof(query));

        // Send GraphQL request via JSON-RPC
        var result = await _rpcClient.SendRequestAsync<JsonElement>(
            "eywa.datasets.graphql",
            new { query, variables }
        ).ConfigureAwait(false);

        // Deserialize to GraphQL response
        var json = result.GetRawText();
        var response = JsonSerializer.Deserialize<GraphQLResponse<T>>(json);

        if (response == null)
        {
            throw new GraphQLException(new[] 
            { 
                new GraphQLError { Message = "Failed to deserialize GraphQL response" }
            });
        }

        // Check for GraphQL errors
        if (response.Errors?.Count > 0)
        {
            throw new GraphQLException(response.Errors);
        }

        return response;
    }

    /// <summary>
    /// Executes a GraphQL query or mutation with dynamic result.
    /// Use this when you don't have a strongly-typed response model.
    /// </summary>
    /// <param name="query">The GraphQL query or mutation string.</param>
    /// <param name="variables">Optional variables for the operation.</param>
    /// <returns>The GraphQL response with dynamic data.</returns>
    /// <exception cref="GraphQLException">Thrown when the GraphQL operation fails.</exception>
    /// <example>
    /// <code>
    /// // Query with variables
    /// var result = await client.ExecuteAsync(@"
    ///     query GetUser($id: UUID!) {
    ///         getUser(euuid: $id) {
    ///             euuid
    ///             name
    ///         }
    ///     }
    /// ", new { id = "user-uuid-here" });
    /// 
    /// var userData = result.Data;
    /// // Access data dynamically
    /// </code>
    /// </example>
    public async Task<GraphQLResponse<JsonElement>> ExecuteAsync(string query, object? variables = null)
    {
        return await ExecuteAsync<JsonElement>(query, variables).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a GraphQL query.
    /// Convenience method that's semantically clearer for read operations.
    /// </summary>
    /// <typeparam name="T">The expected data type.</typeparam>
    /// <param name="query">The GraphQL query string.</param>
    /// <param name="variables">Optional variables for the query.</param>
    /// <returns>The GraphQL response with data.</returns>
    /// <example>
    /// <code>
    /// var result = await client.QueryAsync&lt;SearchFileResponse&gt;(@"
    ///     query {
    ///         searchFile(_limit: 5) {
    ///             euuid
    ///             name
    ///             size
    ///         }
    ///     }
    /// ");
    /// </code>
    /// </example>
    public async Task<GraphQLResponse<T>> QueryAsync<T>(string query, object? variables = null)
    {
        return await ExecuteAsync<T>(query, variables).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a GraphQL mutation.
    /// Convenience method that's semantically clearer for write operations.
    /// </summary>
    /// <typeparam name="T">The expected data type.</typeparam>
    /// <param name="mutation">The GraphQL mutation string.</param>
    /// <param name="variables">Optional variables for the mutation.</param>
    /// <returns>The GraphQL response with data.</returns>
    /// <example>
    /// <code>
    /// var result = await client.MutateAsync&lt;CreateUserResponse&gt;(@"
    ///     mutation CreateUser($user: UserInput!) {
    ///         stackUser(data: $user) {
    ///             euuid
    ///             name
    ///         }
    ///     }
    /// ", new { user = new { name = "John Doe", email = "john@example.com" } });
    /// </code>
    /// </example>
    public async Task<GraphQLResponse<T>> MutateAsync<T>(string mutation, object? variables = null)
    {
        return await ExecuteAsync<T>(mutation, variables).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes multiple GraphQL operations in sequence.
    /// Useful for operations that depend on each other.
    /// </summary>
    /// <param name="operations">The operations to execute in order.</param>
    /// <returns>List of responses in the same order as operations.</returns>
    /// <example>
    /// <code>
    /// var responses = await client.ExecuteBatchAsync(
    ///     ("query { searchUser { euuid } }", null),
    ///     ("query { searchFile { euuid } }", null)
    /// );
    /// </code>
    /// </example>
    public async Task<List<GraphQLResponse<JsonElement>>> ExecuteBatchAsync(
        params (string query, object? variables)[] operations)
    {
        var results = new List<GraphQLResponse<JsonElement>>();

        foreach (var (query, variables) in operations)
        {
            var result = await ExecuteAsync(query, variables).ConfigureAwait(false);
            results.Add(result);
        }

        return results;
    }
}