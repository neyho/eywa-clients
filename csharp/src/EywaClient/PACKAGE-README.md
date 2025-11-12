# EYWA C# Client

Dynamic GraphQL-first client for the EYWA automation platform. Provides comprehensive integration with EYWA's GraphQL API, file management capabilities, and task reporting.

## Quick Start

```csharp
using EywaClient;

var eywa = new Eywa();
eywa.OpenPipe();

// Task reporting with rich content
await eywa.Tasks.ReportAsync("Processing Complete", new ReportOptions
{
    Data = new ReportData
    {
        Card = """
            # Success! ✅
            Processed **1,000 records** with 0 errors.
            """,
        Tables = new Dictionary<string, TableData>
        {
            ["Results"] = new TableData
            {
                Headers = ["Item", "Count", "Status"],
                Rows = new object[][]
                {
                    ["Orders", "1,000", "✅ Complete"],
                    ["Errors", "0", "✅ None"]
                }
            }
        }
    }
});

await eywa.Tasks.CloseTaskAsync(Status.Success);
```

## Features

### ✅ **Task Management & Reporting**
- **Rich Reports** - Markdown cards, structured tables, base64 images
- **Logging** - Info, error, debug, trace with structured data
- **Task Lifecycle** - Get task info, update status, close tasks

### ✅ **Dynamic GraphQL Operations**
- **Type-safe Queries** - Execute any GraphQL operation
- **Variable Support** - Pass parameters safely
- **Error Handling** - Comprehensive GraphQL error management

### ✅ **File Operations**
- **Upload/Download** - Stream-based file handling
- **S3 Integration** - Compatible with S3 APIs
- **Metadata Management** - File attributes and relationships

### ✅ **JSON-RPC Communication**
- **Bi-directional** - Handle requests and responses
- **Type Safety** - Dictionary-based dynamic approach
- **Error Handling** - Structured error responses

## API Overview

### Task Operations
```csharp
// Get current task
var task = await eywa.Tasks.GetTaskAsync();

// Create reports
await eywa.Tasks.ReportAsync("Title", options);

// Logging
await eywa.Logger.InfoAsync("Message", data);
await eywa.Logger.ErrorAsync("Error occurred", errorData);

// Close task
await eywa.Tasks.CloseTaskAsync(Status.Success);
```

### GraphQL Operations
```csharp
// Execute queries
var result = await eywa.GraphQLAsync(query, variables);

// Access data dynamically
var users = result["data"]["searchUser"] as JsonElement[];
```

### File Operations
```csharp
// Upload files
var fileId = await eywa.Files.UploadAsync("file.pdf", stream);

// Download files
using var stream = await eywa.Files.DownloadAsync(fileId);
```

## Installation

```bash
dotnet add package EywaClient
```

## Requirements

- .NET 6.0, 8.0, or 9.0
- System.Text.Json (included automatically)

## Architecture

The client uses a **dynamic Dictionary-based approach** that:
- ✅ Eliminates "JsonElement hell" 
- ✅ Provides JavaScript/Clojure-like property access
- ✅ Maintains full GraphQL schema compatibility
- ✅ Reduces code complexity significantly

## Documentation

- [Task Reporting Guide](https://github.com/neyho/eywa/blob/master/clients/csharp/TASK_REPORTING.md)
- [File Operations Guide](https://github.com/neyho/eywa/blob/master/clients/FILES_SPEC.md)
- [API Reference](https://github.com/neyho/eywa/tree/master/clients/csharp)

## Support

- **Repository:** [github.com/neyho/eywa](https://github.com/neyho/eywa)
- **Issues:** [Report bugs and feature requests](https://github.com/neyho/eywa/issues)
- **Examples:** [Complete examples](https://github.com/neyho/eywa/tree/master/clients/csharp/examples)

Built for the EYWA automation platform - GraphQL-powered data modeling and task automation.
