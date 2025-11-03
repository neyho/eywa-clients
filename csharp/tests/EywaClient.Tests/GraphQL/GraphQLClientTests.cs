using System.Text.Json;
using NUnit.Framework;
using Moq;
using EywaClient.Core;
using EywaClient.GraphQL;
using EywaClient.Models;
using EywaClient.Exceptions;

namespace EywaClient.Tests.GraphQL;

[TestFixture]
public class GraphQLClientTests
{
    private Mock<JsonRpcClient> _mockRpcClient = null!;
    private GraphQLClient _graphqlClient = null!;

    [SetUp]
    public void Setup()
    {
        _mockRpcClient = new Mock<JsonRpcClient>(null, null);
        _graphqlClient = new GraphQLClient(_mockRpcClient.Object);
    }

    [Test]
    public void Constructor_ThrowsOnNullClient()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GraphQLClient(null!));
    }

    [Test]
    public void ExecuteAsync_ThrowsOnNullQuery()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _graphqlClient.ExecuteAsync<object>(null!));
    }

    [Test]
    public void ExecuteAsync_ThrowsOnEmptyQuery()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _graphqlClient.ExecuteAsync<object>(""));
    }

    [Test]
    public void ExecuteAsync_ThrowsOnWhitespaceQuery()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _graphqlClient.ExecuteAsync<object>("   "));
    }

    [Test]
    public async Task ExecuteAsync_WithSuccessResponse_ReturnsData()
    {
        // Arrange
        var query = "query { searchUser { euuid name } }";
        var responseJson = @"{
            ""data"": {
                ""searchUser"": [
                    { ""euuid"": ""user-1"", ""name"": ""John"" }
                ]
            }
        }";

        var jsonElement = JsonDocument.Parse(responseJson).RootElement;

        _mockRpcClient
            .Setup(x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.Is<object>(o => 
                    o.GetType().GetProperty("query")!.GetValue(o)!.ToString() == query)))
            .ReturnsAsync(jsonElement);

        // Act
        var result = await _graphqlClient.ExecuteAsync<TestUserResponse>(query);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Errors, Is.Null);
    }

    [Test]
    public void ExecuteAsync_WithGraphQLErrors_ThrowsGraphQLException()
    {
        // Arrange
        var query = "query { invalid }";
        var responseJson = @"{
            ""errors"": [
                {
                    ""message"": ""Field 'invalid' not found"",
                    ""locations"": [{ ""line"": 1, ""column"": 9 }]
                }
            ]
        }";

        var jsonElement = JsonDocument.Parse(responseJson).RootElement;

        _mockRpcClient
            .Setup(x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()))
            .ReturnsAsync(jsonElement);

        // Act & Assert
        var ex = Assert.ThrowsAsync<GraphQLException>(async () =>
            await _graphqlClient.ExecuteAsync<object>(query));

        Assert.That(ex!.Errors, Is.Not.Empty);
        Assert.That(ex.Errors[0].Message, Does.Contain("invalid"));
    }

    [Test]
    public async Task ExecuteAsync_WithVariables_PassesCorrectly()
    {
        // Arrange
        var query = "query GetUser($id: UUID!) { getUser(euuid: $id) { name } }";
        var variables = new { id = "user-123" };
        var responseJson = @"{
            ""data"": {
                ""getUser"": { ""name"": ""John"" }
            }
        }";

        var jsonElement = JsonDocument.Parse(responseJson).RootElement;

        _mockRpcClient
            .Setup(x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.Is<object>(o =>
                    o.GetType().GetProperty("variables") != null)))
            .ReturnsAsync(jsonElement);

        // Act
        var result = await _graphqlClient.ExecuteAsync<TestGetUserResponse>(query, variables);

        // Assert
        Assert.That(result, Is.Not.Null);
        _mockRpcClient.Verify(
            x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithoutVariables_Works()
    {
        // Arrange
        var query = "query { searchUser { euuid } }";
        var responseJson = @"{
            ""data"": {
                ""searchUser"": []
            }
        }";

        var jsonElement = JsonDocument.Parse(responseJson).RootElement;

        _mockRpcClient
            .Setup(x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()))
            .ReturnsAsync(jsonElement);

        // Act
        var result = await _graphqlClient.ExecuteAsync(query);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task QueryAsync_CallsExecuteAsync()
    {
        // Arrange
        var query = "query { searchUser { euuid } }";
        var responseJson = @"{ ""data"": { ""searchUser"": [] } }";
        var jsonElement = JsonDocument.Parse(responseJson).RootElement;

        _mockRpcClient
            .Setup(x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()))
            .ReturnsAsync(jsonElement);

        // Act
        var result = await _graphqlClient.QueryAsync<TestUserResponse>(query);

        // Assert
        Assert.That(result, Is.Not.Null);
        _mockRpcClient.Verify(
            x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public async Task MutateAsync_CallsExecuteAsync()
    {
        // Arrange
        var mutation = "mutation { stackUser(data: {}) { euuid } }";
        var responseJson = @"{ ""data"": { ""stackUser"": { ""euuid"": ""new-id"" } } }";
        var jsonElement = JsonDocument.Parse(responseJson).RootElement;

        _mockRpcClient
            .Setup(x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()))
            .ReturnsAsync(jsonElement);

        // Act
        var result = await _graphqlClient.MutateAsync<TestMutationResponse>(mutation);

        // Assert
        Assert.That(result, Is.Not.Null);
        _mockRpcClient.Verify(
            x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteBatchAsync_ExecutesMultipleOperations()
    {
        // Arrange
        var responseJson = @"{ ""data"": {} }";
        var jsonElement = JsonDocument.Parse(responseJson).RootElement;

        _mockRpcClient
            .Setup(x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()))
            .ReturnsAsync(jsonElement);

        // Act
        var results = await _graphqlClient.ExecuteBatchAsync(
            ("query { searchUser { euuid } }", null),
            ("query { searchFile { euuid } }", null),
            ("query { searchFolder { euuid } }", null)
        );

        // Assert
        Assert.That(results, Has.Count.EqualTo(3));
        _mockRpcClient.Verify(
            x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()),
            Times.Exactly(3));
    }

    [Test]
    public async Task ExecuteBatchAsync_WithEmptyArray_ReturnsEmptyList()
    {
        // Act
        var results = await _graphqlClient.ExecuteBatchAsync();

        // Assert
        Assert.That(results, Is.Empty);
        _mockRpcClient.Verify(
            x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()),
            Times.Never);
    }

    // NOTE: Test removed - JsonElement is now a non-nullable struct,
    // so null responses are not possible from SendRequestAsync<JsonElement>.
    // Error handling is tested through ExecuteAsync_WithGraphQLErrors test instead.

    [Test]
    public async Task ExecuteAsync_WithComplexVariables_Serializes()
    {
        // Arrange
        var query = "mutation CreateUser($user: UserInput!) { stackUser(data: $user) { euuid } }";
        var variables = new
        {
            user = new
            {
                name = "John Doe",
                email = "john@example.com",
                roles = new[] { "admin", "user" },
                metadata = new { age = 30, active = true }
            }
        };

        var responseJson = @"{ ""data"": { ""stackUser"": { ""euuid"": ""new-id"" } } }";
        var jsonElement = JsonDocument.Parse(responseJson).RootElement;

        _mockRpcClient
            .Setup(x => x.SendRequestAsync<JsonElement>(
                "eywa.datasets.graphql",
                It.IsAny<object>()))
            .ReturnsAsync(jsonElement);

        // Act
        var result = await _graphqlClient.ExecuteAsync<TestMutationResponse>(query, variables);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    // Helper test response types
    private class TestUserResponse
    {
        public List<TestUser>? SearchUser { get; set; }
    }

    private class TestUser
    {
        public string? Euuid { get; set; }
        public string? Name { get; set; }
    }

    private class TestGetUserResponse
    {
        public TestUser? GetUser { get; set; }
    }

    private class TestMutationResponse
    {
        public TestUser? StackUser { get; set; }
    }
}