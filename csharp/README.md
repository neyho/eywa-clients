# EYWA C# Client

[![NuGet](https://img.shields.io/nuget/v/EywaClient.svg)](https://www.nuget.org/packages/EywaClient/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Official .NET client library for [EYWA](https://github.com/neyho/eywa) - an integrated Identity Access Management and Data Modeling platform with GraphQL API generation.

## Features

- 🔄 **JSON-RPC 2.0** - Bidirectional communication over stdin/stdout
- 📊 **GraphQL Client** - Execute queries and mutations against EYWA datasets
- 📁 **File Operations** - Stream-based upload/download with S3 backend
- 📝 **Task Management** - Complete task lifecycle support
- 📋 **Comprehensive Logging** - Multi-level structured logging (Info, Debug, Warn, Error, Trace, Exception)
- 🎯 **Strongly Typed** - Full C# type safety with async/await patterns
- ✅ **Production Ready** - Fully tested with real EYWA server integration

## Installation

```bash
dotnet add package EywaClient
```

## Quick Start

### Basic Robot Example

```csharp
using EywaClient.Core;
using EywaClient.Models;

// Initialize client (connects to EYWA via stdin/stdout)
var client = new JsonRpcClient();
client.OpenPipe();

// Initialize components
var taskManager = new TaskManager(client);
var logger = new Logger(client);

try
{
    // Get current task
    var task = await taskManager.GetTaskAsync();
    logger.Info("Task started", new { taskId = task.Euuid });
    
    // Update status
    taskManager.UpdateTask(TaskStatus.Processing);
    
    // Your robot logic here
    await DoWork();
    
    // Close task successfully
    logger.Info("Task completed successfully");
    taskManager.CloseTask(TaskStatus.Success);
}
catch (Exception ex)
{
    logger.Error("Task failed", new { error = ex.Message });
    taskManager.CloseTask(TaskStatus.Error);
}
```

### GraphQL Example

```csharp
using EywaClient.GraphQL;

var graphqlClient = new GraphQLClient(client);

// Execute a query
var result = await graphqlClient.ExecuteAsync<SearchUserResponse>(@"
    query {
        searchUser(_limit: 10) {
            euuid
            name
            email
        }
    }
");

foreach (var user in result.Data.SearchUser)
{
    Console.WriteLine($"{user.Name}: {user.Email}");
}

// Execute a mutation
var createResult = await graphqlClient.MutateAsync<CreateUserResponse>(@"
    mutation CreateUser($user: UserInput!) {
        stackUser(data: $user) {
            euuid
            name
        }
    }
", new { 
    user = new { 
        name = "John Doe", 
        email = "john@example.com" 
    } 
});
```

### Logging Examples

```csharp
var logger = new Logger(client);

// Simple logging
logger.Info("Processing started");
logger.Debug("Detailed debug information");
logger.Warn("Warning message");
logger.Error("Error occurred");
logger.Trace("Trace-level details");

// Structured logging with data
logger.Info("User created", new
{
    userId = Guid.NewGuid(),
    username = "john.doe",
    timestamp = DateTime.Now
});

// Exception logging
try
{
    // ... code that might fail
}
catch (Exception ex)
{
    logger.Exception("Operation failed", new
    {
        exceptionType = ex.GetType().Name,
        message = ex.Message,
        stackTrace = ex.StackTrace
    });
}

// Task reporting with rich data
logger.Report("Monthly report generated", new
{
    reportId = Guid.NewGuid(),
    recordsProcessed = 1500,
    duration = TimeSpan.FromMinutes(5),
    status = "completed"
});
```

## Running Your Robot

### Via EYWA CLI

```bash
# Run your C# robot via EYWA
eywa run -c "dotnet run --project MyRobot.csproj"

# Or as a compiled executable
eywa run -c "./MyRobot"
```

### Project Setup

Create a console application:

```bash
dotnet new console -n MyRobot
cd MyRobot
dotnet add package EywaClient
```

Your `Program.cs`:

```csharp
using EywaClient.Core;
using EywaClient.Models;

class Program
{
    static async Task Main(string[] args)
    {
        var client = new JsonRpcClient();
        client.OpenPipe();
        
        var taskManager = new TaskManager(client);
        var logger = new Logger(client);
        
        try
        {
            var task = await taskManager.GetTaskAsync();
            logger.Info("Robot started");
            
            taskManager.UpdateTask(TaskStatus.Processing);
            
            // Your robot logic here
            
            taskManager.CloseTask(TaskStatus.Success);
        }
        catch (Exception ex)
        {
            logger.Error("Robot failed", new { error = ex.Message });
            taskManager.CloseTask(TaskStatus.Error);
        }
    }
}
```

## API Overview

### Core Components

#### JsonRpcClient
Low-level JSON-RPC 2.0 communication over stdin/stdout.

```csharp
var client = new JsonRpcClient();
client.OpenPipe();

// Send request and await response
var result = await client.SendRequestAsync<MyResponse>("method.name", parameters);

// Send notification (fire-and-forget)
client.SendNotification("method.name", parameters);

// Register handler for incoming requests
client.RegisterHandler("method.name", (parameters) => {
    // Handle incoming request
    return responseData;
});
```

#### TaskManager
Manage EYWA task lifecycle.

```csharp
var taskManager = new TaskManager(client);

// Get current task information
var task = await taskManager.GetTaskAsync();

// Update task status
taskManager.UpdateTask(TaskStatus.Processing);

// Close task with final status
taskManager.CloseTask(TaskStatus.Success);

// Return control to EYWA (without closing)
taskManager.ReturnTask();
```

#### Logger
Multi-level structured logging.

```csharp
var logger = new Logger(client);

// Log levels
logger.Info("message", data);
logger.Debug("message", data);
logger.Warn("message", data);
logger.Error("message", data);
logger.Trace("message", data);
logger.Exception("message", data);

// Task reporting
logger.Report("message", data, imageBase64);
```

#### GraphQLClient
Execute GraphQL operations.

```csharp
var graphqlClient = new GraphQLClient(client);

// Execute query
var result = await graphqlClient.ExecuteAsync<T>(query, variables);

// Semantic methods
var queryResult = await graphqlClient.QueryAsync<T>(query, variables);
var mutationResult = await graphqlClient.MutateAsync<T>(mutation, variables);

// Batch operations
var results = await graphqlClient.ExecuteBatchAsync(
    (query1, vars1),
    (query2, vars2),
    (query3, vars3)
);
```

## Task Statuses

```csharp
public enum TaskStatus
{
    Success,      // Task completed successfully
    Error,        // Task failed
    Processing,   // Task is being processed
    Exception     // Task encountered an exception
}
```

## Requirements

- **.NET 9.0** or later
- **EYWA Server** running and accessible
- **EYWA CLI** for executing robots

## Documentation

- **EYWA Documentation:** https://neyho.github.io/eywa/
- **GitHub Repository:** https://github.com/neyho/eywa
- **Examples:** https://github.com/neyho/eywa-examples

## Advanced Features

### Custom Request Handlers

Handle incoming requests from EYWA:

```csharp
client.RegisterHandler("custom.method", (parameters) =>
{
    // Process the request
    var data = JsonSerializer.Deserialize<MyData>(parameters);
    
    // Return response
    return new { result = "success", data = processedData };
});
```

### GraphQL with Variables

```csharp
var result = await graphqlClient.ExecuteAsync<GetUserResponse>(@"
    query GetUser($id: UUID!) {
        getUser(euuid: $id) {
            euuid
            name
            email
        }
    }
", new { id = "user-uuid-here" });
```

### Error Handling

```csharp
try
{
    var result = await graphqlClient.ExecuteAsync<T>(query);
}
catch (GraphQLException ex)
{
    // Handle GraphQL-specific errors
    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"GraphQL Error: {error.Message}");
    }
}
catch (JsonRpcException ex)
{
    // Handle JSON-RPC errors
    Console.WriteLine($"RPC Error: {ex.Code} - {ex.Message}");
}
```

## Testing

The library includes comprehensive integration tests that run against a real EYWA server:

```bash
# Run integration tests via EYWA
eywa run -c "dotnet run --project tests/EywaClient.IntegrationTests"
```

## Contributing

Contributions are welcome! Please see the [EYWA repository](https://github.com/neyho/eywa) for contribution guidelines.

## License

MIT License - see [LICENSE](https://github.com/neyho/eywa/blob/master/LICENSE) for details.

## Support

- **Issues:** https://github.com/neyho/eywa/issues
- **Email:** robi@neyho.com

## About EYWA

EYWA is a platform that combines Identity Access Management (OAuth2.1/OIDC) with deployable data modeling. Once you model and deploy your data, EYWA automatically generates GraphQL queries and mutations, enabling rapid application development with built-in authentication and authorization.

---

**Built with ❤️ for the EYWA ecosystem**
