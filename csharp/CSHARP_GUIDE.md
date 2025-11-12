# EYWA C# Client - Complete Guide

**Version:** 0.2.1  
**Last Updated:** November 2024  
**Philosophy:** Dynamic GraphQL-first, staying as close to GraphQL as possible

---

## 📋 Table of Contents

- [🎯 Overview & Philosophy](#-overview--philosophy)
- [🚀 Quick Start](#-quick-start)
- [🏗 Architecture](#-architecture)
- [📖 Core Concepts](#-core-concepts)
- [🔧 Installation & Setup](#-installation--setup)
- [💻 Basic Usage Patterns](#-basic-usage-patterns)
- [📁 File Operations](#-file-operations)
- [🔍 GraphQL Operations](#-graphql-operations)
- [📊 Task Management & Reporting](#-task-management--reporting)
- [⚠️ Error Handling](#️-error-handling)
- [🎯 Best Practices](#-best-practices)
- [🚫 Common Pitfalls](#-common-pitfalls)
- [🧪 Testing](#-testing)
- [🔧 Troubleshooting](#-troubleshooting)
- [📚 Examples](#-examples)

---

## 🎯 Overview & Philosophy

### What is EYWA C# Client?

The EYWA C# Client is a **dynamic, GraphQL-first** client library that provides seamless integration with the EYWA platform for robotics automation. It embraces a dictionary-based approach using `Dictionary<string, object>` to stay as close to GraphQL as possible, eliminating translation layers and "JsonElement hell."

### Core Philosophy

1. **Dynamic-first**: Uses `Dictionary<string, object>` for all data interchange
2. **GraphQL-native**: Single map arguments that mirror GraphQL schema exactly  
3. **Protocol abstraction only**: Abstracts complex S3 upload/download, not query complexity
4. **Zero translation layers**: What you write in GraphQL is what you pass to functions
5. **Client-controlled UUIDs**: For idempotent operations and retry safety

### Key Benefits

- ✅ **No "impedance mismatch"** - C# maps directly mirror GraphQL
- ✅ **Future-proof** - New GraphQL features work immediately
- ✅ **Simple to maintain** - Less code, no complex type mappings
- ✅ **Natural for C# developers** - `Dictionary<string, object>` is familiar
- ✅ **Follows FILES_SPEC.md exactly** - Protocol abstraction, not query abstraction

---

## 🚀 Quick Start

### Installation

```bash
dotnet add package EywaClient
```

### 30-Second Example

```csharp
using EywaClient;
using EywaClient.Core;

var eywa = new Eywa();

try
{
    eywa.OpenPipe();
    
    // Get current task
    var task = await eywa.Tasks.GetTaskAsync();
    await eywa.Logger.InfoAsync("Robot started", new { taskId = task["euuid"] });
    
    // Update status  
    await eywa.Tasks.UpdateTaskAsync(Status.Processing);
    
    // Direct GraphQL - the power of dynamic approach!
    var result = await eywa.GraphQLAsync(@"
        query MyFiles($limit: Int!) {
            searchFile(_limit: $limit) {
                euuid name size
                folder { name path }
            }
        }", new { limit = 10 });
    
    // Access results dynamically - just like JavaScript!
    var files = (List<object>)result["data"]["searchFile"];
    foreach (Dictionary<string, object> file in files)
    {
        Console.WriteLine($"{file["name"]} - {file["size"]} bytes");
    }
    
    await eywa.Tasks.CloseTaskAsync(Status.Success);
}
catch (Exception ex)
{
    await eywa.Logger.ErrorAsync("Robot failed", new { error = ex.Message });
    await eywa.Tasks.CloseTaskAsync(Status.Error);
    throw;
}
finally
{
    eywa.Dispose(); // CRITICAL for cleanup
}
```

---

## 🏗 Architecture

### Component Overview

```
EywaClient (Eywa class)
├── Tasks (TaskManager)     - Task lifecycle & reporting
├── Logger (Logger)         - Structured logging
├── Files (FilesClient)     - File operations (FILES_SPEC.md)
└── GraphQLAsync()         - Direct GraphQL access

Core Infrastructure:
├── JsonRpcClient          - JSON-RPC over stdin/stdout
├── HttpS3Client           - S3 protocol handling
└── MimeTypeDetector       - Content type detection
```

### Design Principles

1. **Dynamic First**: All operations use `Dictionary<string, object>`
2. **GraphQL Native**: Mirror GraphQL schema structures exactly
3. **Controlled UUIDs**: Client generates UUIDs for idempotent operations
4. **Resource Management**: Proper disposal patterns throughout
5. **Structured Logging**: Rich contextual logging for observability

---

## 📖 Core Concepts

### 1. Dynamic Data Structures

**✅ DO: Use consistent dictionary patterns**

```csharp
// ✅ GOOD - GraphQL-native data structures
var fileData = new Dictionary<string, object>
{
    ["euuid"] = Guid.NewGuid().ToString(),
    ["name"] = "report.pdf",
    ["folder"] = new Dictionary<string, object> { ["euuid"] = folderUuid },
    ["content_type"] = "application/pdf"
};
```

**❌ DON'T: Fight the dynamic nature**

```csharp
// ❌ BAD - Trying to force strong typing
public class FileDto 
{
    public string Euuid { get; set; }
    public string Name { get; set; }
    public FolderDto Folder { get; set; }
}
// This creates unnecessary complexity and translation layers
```

### 2. Protocol vs Query Abstraction

**✅ DO: Write GraphQL directly**

```csharp
// ✅ GOOD - Full GraphQL power with complex filtering
var result = await eywa.GraphQLAsync(@"
    query ComplexQuery($userId: UUID!, $dateRange: DateRange!) {
        searchFile(_where: {
            _and: [
                {uploaded_by: {euuid: {_eq: $userId}}},
                {uploaded_at: {_gte: $dateRange.start}},
                {folder: {path: {_ilike: ""/reports%""}}},
                {size: {_gt: 1000}}
            ]
        }, _order_by: [{size: desc}, {uploaded_at: desc}]) {
            euuid name size uploaded_at
            folder { name path }
            uploaded_by { name }
        }
    }", variables);
```

**❌ DON'T: Expect ORM-style query builders**

```csharp
// ❌ BAD - This doesn't exist (by design)
var files = await eywa.Files
    .Where(f => f.UploadedBy.Euuid == userId)
    .Where(f => f.Size > 1000)
    .OrderByDescending(f => f.Size)
    .ToListAsync(); // This API doesn't exist and won't be added
```

### 3. UUID Management

**✅ DO: Control your UUIDs**

```csharp
// ✅ GOOD - Client-controlled UUIDs enable idempotent operations
var fileUuid = Guid.NewGuid().ToString();
var fileData = new Dictionary<string, object>
{
    ["euuid"] = fileUuid,  // You control the UUID
    ["name"] = "document.pdf",
    ["folder"] = new Dictionary<string, object> { ["euuid"] = folderUuid }
};

// If upload fails, you can retry with same UUID safely
await eywa.Files.UploadAsync("local-file.pdf", fileData);
```

---

## 🔧 Installation & Setup

### Requirements

- **.NET 9.0** or later (also supports .NET 6.0, 8.0)
- **EYWA Server** running and accessible
- **EYWA CLI** for executing robots

### Installation

```bash
# Install via NuGet
dotnet add package EywaClient

# Or via Package Manager
Install-Package EywaClient
```

### Project Setup

```csharp
// Program.cs
using EywaClient;
using EywaClient.Core;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            using var eywa = new Eywa();
            eywa.OpenPipe();
            
            await ProcessCommandLineArgs(args, eywa);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }
}
```

---

## 💻 Basic Usage Patterns

### 1. Standard Robot Pattern

```csharp
using EywaClient;
using EywaClient.Core;

class MyRobot
{
    static async Task Main(string[] args)
    {
        using var eywa = new Eywa();
        
        try
        {
            // 1. Initialize
            eywa.OpenPipe();
            var task = await eywa.Tasks.GetTaskAsync();
            
            // 2. Start logging
            await eywa.Logger.InfoAsync("Robot started", new { 
                robotVersion = "1.0.0",
                taskId = task["euuid"] 
            });
            
            // 3. Update status
            await eywa.Tasks.UpdateTaskAsync(Status.Processing);
            
            // 4. Main logic with progress updates
            await ProcessData(eywa);
            
            // 5. Success
            await eywa.Logger.InfoAsync("Robot completed successfully");
            await eywa.Tasks.CloseTaskAsync(Status.Success);
        }
        catch (Exception ex)
        {
            await eywa.Logger.ErrorAsync("Robot failed", new { 
                error = ex.Message,
                stackTrace = ex.StackTrace 
            });
            await eywa.Tasks.CloseTaskAsync(Status.Error);
            throw;
        }
    }
    
    static async Task ProcessData(Eywa eywa)
    {
        // Your specific robot logic
        await eywa.Logger.InfoAsync("Processing data...");
        // ... implementation
    }
}
```

### 2. Resource Management Pattern

```csharp
// ✅ GOOD - Guaranteed cleanup
using var eywa = new Eywa();
eywa.OpenPipe();
// Work here - eywa automatically disposed

// OR with explicit try/finally
var eywa = new Eywa();
try 
{
    eywa.OpenPipe();
    // Work here
}
finally
{
    eywa.Dispose();
}
```

### 3. Safe Data Extraction Pattern

```csharp
// ✅ GOOD - Safe data extraction utility
static T GetValueSafely<T>(Dictionary<string, object> dict, string key, T defaultValue = default)
{
    if (!dict.TryGetValue(key, out var value)) 
        return defaultValue;
    
    if (value is JsonElement jsonElement)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
    
    if (value is T directValue)
        return directValue;
        
    try
    {
        return (T)Convert.ChangeType(value, typeof(T));
    }
    catch
    {
        return defaultValue;
    }
}

// Usage
var fileName = GetValueSafely<string>(fileDict, "name", "unknown.txt");
var fileSize = GetValueSafely<long>(fileDict, "size", 0L);
```

---

## 📁 File Operations

### Upload Operations

The client implements all 8 core functions from FILES_SPEC.md:

#### 1. Upload from File Path

```csharp
// For existing files on disk
await eywa.Files.UploadAsync("document.pdf", new Dictionary<string, object>
{
    ["euuid"] = Guid.NewGuid().ToString(),
    ["name"] = "important.pdf",
    ["folder"] = new Dictionary<string, object> { ["euuid"] = eywa.Files.RootUuid },
    ["content_type"] = "application/pdf"
});
```

#### 2. Upload from Stream

```csharp
// For large files or network sources
using var stream = File.OpenRead("large-file.zip");
await eywa.Files.UploadStreamAsync(stream, new Dictionary<string, object>
{
    ["euuid"] = Guid.NewGuid().ToString(),
    ["name"] = "data-archive.zip",
    ["size"] = stream.Length,
    ["folder"] = eywa.Files.RootFolder,
    ["content_type"] = "application/zip"
});
```

#### 3. Upload from Content

```csharp
// For in-memory string or binary data
var csvContent = "Name,Age,City\nJohn,30,NYC\nJane,25,LA";
await eywa.Files.UploadContentAsync(csvContent, new Dictionary<string, object>
{
    ["euuid"] = Guid.NewGuid().ToString(),
    ["name"] = "export.csv",
    ["folder"] = eywa.Files.RootFolder,
    ["content_type"] = "text/csv"
});
```

### Download Operations

#### 4. Download as Stream

```csharp
// ✅ GOOD - Proper stream handling
var streamResult = await eywa.Files.DownloadStreamAsync(fileUuid);
using var stream = streamResult.Stream;

// Process stream
using var reader = new StreamReader(stream);
var content = await reader.ReadToEndAsync();
// Both streams automatically disposed
```

#### 5. Download as Byte Array

```csharp
// For smaller files
var downloadedBytes = await eywa.Files.DownloadAsync(fileUuid);
var downloadedContent = Encoding.UTF8.GetString(downloadedBytes);
```

### Folder Management

#### 6. Create Folder

```csharp
await eywa.Files.CreateFolderAsync(new Dictionary<string, object>
{
    ["euuid"] = Guid.NewGuid().ToString(),
    ["name"] = "Reports", 
    ["parent"] = new Dictionary<string, object> { ["euuid"] = eywa.Files.RootUuid }
});
```

### Cleanup Operations

#### 7. Delete File

```csharp
await eywa.Files.DeleteFileAsync(fileUuid);
```

#### 8. Delete Folder

```csharp
await eywa.Files.DeleteFolderAsync(folderUuid); // Must be empty
```

### Progress Tracking

```csharp
// ✅ GOOD - Progress tracking for user experience
var fileData = new Dictionary<string, object>
{
    ["euuid"] = fileUuid,
    ["name"] = "large-file.zip",
    ["folder"] = eywa.Files.RootFolder,
    ["progressFn"] = new HttpS3Client.ProgressCallback((sent, total) => 
    {
        var percent = (int)((double)sent / total * 100);
        Console.WriteLine($"Upload progress: {percent}% ({sent:N0}/{total:N0} bytes)");
    })
};

await eywa.Files.UploadAsync("large-file.zip", fileData);
```

### Constants

```csharp
// Root folder references
var rootUuid = eywa.Files.RootUuid;        // String UUID
var rootFolder = eywa.Files.RootFolder;   // Dictionary for GraphQL
```

---

## 🔍 GraphQL Operations

### Basic Queries

```csharp
// Simple query
var result = await eywa.GraphQLAsync(@"
    query GetFiles($limit: Int!) {
        searchFile(_limit: $limit) {
            euuid name size
            folder { name }
        }
    }", new { limit = 10 });

// Extract data
var files = (List<object>)result["data"]["searchFile"];
```

### Complex Queries with Filtering

```csharp
var advancedQuery = @"
    query AdvancedFileAnalysis($userId: UUID!, $startDate: DateTime!, $tags: [String!]) {
        searchFile(_where: {
            _and: [
                {uploaded_by: {euuid: {_eq: $userId}}},
                {uploaded_at: {_gte: $startDate}},
                {tags: {tag: {name: {_in: $tags}}}},
                {_or: [
                    {content_type: {_ilike: ""image%""}},
                    {size: {_gt: 1048576}}
                ]}
            ]
        }, _order_by: [
            {folder: {path: asc}},
            {uploaded_at: desc}
        ]) {
            euuid name size content_type uploaded_at checksum
            folder { euuid name path parent { name } }
            uploaded_by { euuid name email }
            tags { tag { euuid name color } }
        }
    }";

var result = await eywa.GraphQLAsync(advancedQuery, new {
    userId = userUuid,
    startDate = DateTime.UtcNow.AddDays(-30),
    tags = new[] { "important", "report", "client" }
});
```

### Mutations

```csharp
// Create user
var mutation = @"
    mutation CreateUser($user: UserInput!) {
        stackUser(data: $user) {
            euuid name modified_on
        }
    }";
    
var variables = new Dictionary<string, object>
{
    ["user"] = new Dictionary<string, object>
    {
        ["euuid"] = Guid.NewGuid().ToString(),
        ["name"] = "John Doe",
        ["email"] = "john@example.com"
    }
};

var result = await eywa.GraphQLAsync(mutation, variables);
```

### Batch Operations

```csharp
// Batch file updates
var batchMutation = @"
    mutation BatchUpdate($files: [FileInput!]!) {
        stackFileList(data: $files) {
            euuid name
        }
    }";

var fileBatch = fileUpdates.Take(100).Select(update => new Dictionary<string, object>
{
    ["euuid"] = update.Id,
    ["tags"] = update.Tags.Select(tag => new { euuid = tag.Id })
}).ToArray();

await eywa.GraphQLAsync(batchMutation, new { files = fileBatch });
```

### Safe GraphQL Execution

```csharp
async Task<Dictionary<string, object>?> SafeGraphQLQuery(Eywa eywa, string query, object? variables = null)
{
    try
    {
        var result = await eywa.GraphQLAsync(query, variables);
        
        // Check for GraphQL errors in response
        if (result.ContainsKey("errors"))
        {
            var errors = JsonSerializer.Serialize(result["errors"]);
            await eywa.Logger.ErrorAsync("GraphQL query returned errors", new { 
                query = query.Substring(0, Math.Min(100, query.Length)) + "...",
                errors = errors 
            });
            return null;
        }
        
        return result;
    }
    catch (GraphQLException ex)
    {
        await eywa.Logger.ErrorAsync("GraphQL exception", new { 
            query = query.Substring(0, Math.Min(100, query.Length)) + "...",
            error = ex.Message 
        });
        return null;
    }
}
```

---

## 📊 Task Management & Reporting

### Task Lifecycle

```csharp
// Get current task
var task = await eywa.Tasks.GetTaskAsync();

// Update status
await eywa.Tasks.UpdateTaskAsync(Status.Processing);

// Close with final status
await eywa.Tasks.CloseTaskAsync(Status.Success);
// or
await eywa.Tasks.CloseTaskAsync(Status.Error);
```

### Status Enum

```csharp
public enum Status
{
    Success,     // Task completed successfully
    Error,       // Task failed with error
    Processing,  // Task is currently processing
    Exception    // Task encountered an exception
}
```

### Simple Reports

```csharp
// Simple markdown card report
await eywa.Tasks.ReportAsync("Daily Summary", new ReportOptions
{
    Data = new ReportData
    {
        Card = """
            # Success! 
            Processed **1,000 records** with 0 errors.
            """
    }
});
```

### Complex Reports with Tables

```csharp
await eywa.Tasks.ReportAsync("Performance Analysis", new ReportOptions
{
    Data = new ReportData
    {
        Card = """
            # Performance Report
            ## Summary
            System performance exceeded targets by **15%**.
            
            ### Key Metrics
            - **Throughput:** 1,200 req/sec  
            - **Latency:** P95 < 100ms
            - **Errors:** 0.01%
            """,
        Tables = new Dictionary<string, TableData>
        {
            ["Endpoint Performance"] = new TableData
            {
                Headers = ["Endpoint", "Requests", "Avg Response", "Error Rate"],
                Rows = new object[][]
                {
                    ["/api/users", "15,420", "45ms", "0.01%"],
                    ["/api/orders", "8,930", "123ms", "0.02%"],
                    ["/api/reports", "2,100", "890ms", "0.00%"]
                }
            },
            ["System Health"] = new TableData
            {
                Headers = ["Service", "Uptime", "Response Time", "Status"],
                Rows = new object[][]
                {
                    ["API Gateway", "99.9%", "85ms", "Healthy"],
                    ["Database", "100%", "12ms", "Healthy"],
                    ["Cache Layer", "99.8%", "3ms", "Healthy"]
                }
            }
        }
    }
});
```

### Reports with Images

```csharp
// Generate base64 chart (example)
var chartBase64 = GenerateChartBase64();

await eywa.Tasks.ReportAsync("Visual Analysis", new ReportOptions
{
    Data = new ReportData
    {
        Card = """
            # Monthly Trends
            
            Chart shows **significant growth** in Q4 2024.
            """,
        Tables = new Dictionary<string, TableData>
        {
            ["Monthly Metrics"] = new TableData
            {
                Headers = ["Month", "Revenue", "Users", "Conversion"],
                Rows = new object[][]
                {
                    ["October", "$125K", "8,450", "3.2%"],
                    ["November", "$156K", "9,230", "3.8%"],
                    ["December", "$189K", "11,200", "4.1%"]
                }
            }
        }
    },
    Image = chartBase64
});
```

### Structured Logging

```csharp
// ✅ GOOD - Structured logging with context
await eywa.Logger.InfoAsync("Processing batch", new {
    batchId = batchUuid,
    itemCount = items.Count,
    batchNumber = currentBatch,
    totalBatches = totalBatches,
    estimatedTimeRemaining = CalculateETA()
});

await eywa.Logger.ErrorAsync("Failed to process item", new {
    itemId = item.Id,
    itemType = item.GetType().Name,
    error = ex.Message,
    stackTrace = ex.StackTrace,
    processingContext = new {
        batchId = batchUuid,
        attemptNumber = retryCount
    }
});
```

**❌ DON'T: Use simple string logging**

```csharp
// ❌ BAD - Unstructured logging
await eywa.Logger.InfoAsync($"Processing {items.Count} items");
await eywa.Logger.ErrorAsync($"Error: {ex.Message}");
```

---

## ⚠️ Error Handling

### Exception Hierarchy

```
Exception
├── GraphQLException           - GraphQL operation errors
├── FileUploadError           - Upload-specific errors  
├── FileDownloadError         - Download-specific errors
└── InvalidOperationException - Task/lifecycle errors
```

### Proper Error Handling Pattern

```csharp
try 
{
    await eywa.Files.UploadAsync(filepath, fileData);
}
catch (FileUploadError uploadEx)
{
    // Handle upload-specific issues
    await eywa.Logger.ErrorAsync($"Upload failed: {uploadEx.Message}", 
        new { file = filepath, code = uploadEx.Code });
    
    if (uploadEx.Code == 413) // File too large
    {
        // Handle specifically
        await HandleFileTooLargeError(filepath);
    }
}
catch (GraphQLException gqlEx)
{
    // Handle GraphQL errors
    await eywa.Logger.ErrorAsync($"GraphQL error: {gqlEx.Message}");
}
catch (Exception ex)
{
    // Handle unexpected errors
    await eywa.Logger.ErrorAsync($"Unexpected error: {ex.Message}");
    throw; // Re-throw if you can't handle
}
```

### Retry Pattern with Exponential Backoff

```csharp
async Task<string> UploadFileWithRetry(Eywa eywa, string filepath, 
    Dictionary<string, object> fileData, int maxRetries = 3)
{
    var fileUuid = fileData["euuid"].ToString();
    
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await eywa.Files.UploadAsync(filepath, fileData);
            await eywa.Logger.InfoAsync($"Upload successful on attempt {attempt}", 
                new { file = filepath, uuid = fileUuid });
            return fileUuid;
        }
        catch (FileUploadError ex) when (attempt < maxRetries)
        {
            await eywa.Logger.WarnAsync($"Upload attempt {attempt} failed, retrying...", 
                new { file = filepath, error = ex.Message, attemptsRemaining = maxRetries - attempt });
            
            // Exponential backoff
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        }
    }
    
    throw new Exception($"Failed to upload {filepath} after {maxRetries} attempts");
}
```

### **❌ DON'T: Swallow exceptions silently**

```csharp
// ❌ BAD - Silent failures hide problems
try 
{
    await eywa.Files.UploadAsync(filepath, fileData);
}
catch 
{
    // Silent failure - impossible to debug!
    return; // Very bad practice
}
```

---

## 🎯 Best Practices

### 1. Resource Management

```csharp
// ✅ ALWAYS dispose properly
using var eywa = new Eywa();

// ✅ Handle streams properly  
using var stream = await eywa.Files.DownloadStreamAsync(fileUuid);
```

### 2. UUID Control

```csharp
// ✅ Client-controlled UUIDs for retry safety
var fileUuid = Guid.NewGuid().ToString();
var fileData = new Dictionary<string, object>
{
    ["euuid"] = fileUuid,  // You control the UUID
    ["name"] = "document.pdf"
};
```

### 3. Concurrency Control

```csharp
// ✅ GOOD - Controlled concurrency with rate limiting
var semaphore = new SemaphoreSlim(5); // Max 5 concurrent operations
var uploadTasks = filePaths.Select(async filepath =>
{
    await semaphore.WaitAsync();
    try
    {
        await UploadSingleFile(filepath);
    }
    finally
    {
        semaphore.Release();
    }
});

await Task.WhenAll(uploadTasks);
semaphore.Dispose();
```

### 4. Memory Management for Large Files

```csharp
// ✅ GOOD - Streaming approach for large files
using var inputStream = File.OpenRead("large-file.zip");
await eywa.Files.UploadStreamAsync(inputStream, fileData);

// ❌ BAD - Loading large files into memory
var largeFileBytes = await File.ReadAllBytesAsync("huge-file.zip"); // Memory spike!
```

### 5. Batch Processing

```csharp
async Task ProcessFilesInBatches(Eywa eywa, List<string> filePaths)
{
    const int batchSize = 10;
    var total = filePaths.Count;
    
    for (int i = 0; i < total; i += batchSize)
    {
        var batch = filePaths.Skip(i).Take(batchSize);
        var batchNum = (i / batchSize) + 1;
        var totalBatches = (int)Math.Ceiling((double)total / batchSize);
        
        await eywa.Logger.InfoAsync($"Processing batch {batchNum}/{totalBatches}");
        
        // Process batch
        var tasks = batch.Select(filepath => ProcessSingleFile(eywa, filepath));
        await Task.WhenAll(tasks);
        
        // Update progress
        var processed = Math.Min(i + batchSize, total);
        var percent = (int)((double)processed / total * 100);
        await eywa.Logger.InfoAsync($"Progress: {percent}% ({processed}/{total} files)");
    }
}
```

---

## 🚫 Common Pitfalls

### ❌ DON'T: Forget Disposal

```csharp
// ❌ BAD - Resource leak
var eywa = new Eywa();
eywa.OpenPipe();
// Exiting without disposal = potential resource leak
```

### ❌ DON'T: Mix Data Structure Patterns

```csharp
// ❌ BAD - Inconsistent patterns
var userData = new { name = "John" };  // Anonymous object
var updateData = new Dictionary<string, string> { ["name"] = "Jane" }; // Wrong type
var moreData = new Dictionary<string, object> { ["name"] = 123 }; // Wrong value type
```

### ❌ DON'T: Use Uncontrolled Concurrency

```csharp
// ❌ BAD - Potential resource exhaustion
var tasks = filePaths.Select(filepath => UploadSingleFile(filepath)); // No limits!
await Task.WhenAll(tasks); // Could create 1000s of concurrent connections
```

### ❌ DON'T: Assume Direct Type Access

```csharp
// ❌ BAD - Assumes direct type access
var fileName = (string)fileDict["name"]; // Might be JsonElement!
var fileSize = (long)fileDict["size"];   // Might fail with InvalidCastException!
```

### ❌ DON'T: Forget Task Lifecycle Management

```csharp
// ❌ BAD - No task status management
var eywa = new Eywa();
eywa.OpenPipe();

// Process data but never update status
await ProcessData();

// Exit without closing task properly
// The task will remain in PROCESSING state forever
```

---

## 🧪 Testing

### Integration Test Pattern

```csharp
[Test]
public async Task TestCompleteFileWorkflow()
{
    using var eywa = new Eywa();
    eywa.OpenPipe();
    
    var testContent = "Test file content";
    var fileUuid = Guid.NewGuid().ToString();
    var folderUuid = Guid.NewGuid().ToString();
    
    try
    {
        // Create folder
        await eywa.Files.CreateFolderAsync(new Dictionary<string, object>
        {
            ["euuid"] = folderUuid,
            ["name"] = "Test Folder",
            ["parent"] = eywa.Files.RootFolder
        });
        
        // Upload file
        var fileData = new Dictionary<string, object>
        {
            ["euuid"] = fileUuid,
            ["name"] = "test.txt",
            ["content_type"] = "text/plain",
            ["folder"] = new Dictionary<string, object> { ["euuid"] = folderUuid }
        };
        
        await eywa.Files.UploadContentAsync(testContent, fileData);
        
        // Verify upload with GraphQL
        var result = await eywa.GraphQLAsync(@"
            query GetFile($uuid: UUID!) {
                getFile(euuid: $uuid) {
                    euuid name size
                    folder { euuid name }
                }
            }", new { uuid = fileUuid });
        
        // Verify data
        var fileResult = ExtractFileFromResult(result);
        Assert.AreEqual("test.txt", GetValueSafely<string>(fileResult, "name"));
        
        // Download and verify content
        var downloadedBytes = await eywa.Files.DownloadAsync(fileUuid);
        var downloadedContent = Encoding.UTF8.GetString(downloadedBytes);
        Assert.AreEqual(testContent, downloadedContent);
    }
    finally
    {
        // Cleanup
        try { await eywa.Files.DeleteFileAsync(fileUuid); } catch { }
        try { await eywa.Files.DeleteFolderAsync(folderUuid); } catch { }
    }
}
```

**❌ DON'T: Test only with mocks**

```csharp
// ❌ BAD - Mock testing doesn't catch integration issues
[Test]
public void TestFileUpload_Mocked()
{
    var mockClient = new Mock<IEywaClient>();
    mockClient.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
           .Returns(Task.CompletedTask);
    
    // This doesn't test real EYWA integration
    // Won't catch GraphQL schema changes
    // Won't catch S3 upload issues
}
```

---

## 🔧 Troubleshooting

### Issue: JsonElement Conversion Problems

**Problem**: Getting JsonElement when expecting native types

**Solution**: Use safe conversion helper

```csharp
static T GetValueSafely<T>(Dictionary<string, object> dict, string key, T defaultValue = default)
{
    if (!dict.TryGetValue(key, out var value)) 
        return defaultValue;
    
    if (value is JsonElement jsonElement)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
    
    if (value is T directValue)
        return directValue;
    
    try
    {
        return (T)Convert.ChangeType(value, typeof(T));
    }
    catch
    {
        return defaultValue;
    }
}
```

### Issue: File Upload Failures

**Problem**: Uploads failing with timeout or connection errors

**Solution**: Use retry with exponential backoff (see Error Handling section)

### Issue: Memory Leaks

**Problem**: Application memory usage grows over time

**Solution**: Ensure proper disposal
- Use `using` statements for EYWA client and streams
- Don't keep references to large objects
- Process files one at a time for large batches

### Issue: Connection Problems

**Problem**: Cannot connect to EYWA

**Solution**: Check execution context

```csharp
async Task<bool> EnsureEywaConnection(Eywa eywa)
{
    try
    {
        var task = await eywa.Tasks.GetTaskAsync();
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EYWA connection failed: {ex.Message}");
        Console.WriteLine("Ensure 'eywa run -c' command is being used to execute this program");
        return false;
    }
}
```

---

## 📚 Examples

### Complete Robot Example

```csharp
using EywaClient;
using EywaClient.Core;

class DataProcessingRobot
{
    static async Task Main(string[] args)
    {
        using var eywa = new Eywa();
        
        try
        {
            eywa.OpenPipe();
            var task = await eywa.Tasks.GetTaskAsync();
            
            await eywa.Logger.InfoAsync("Data Processing Robot started", new { 
                version = "2.0.0",
                taskId = task["euuid"] 
            });
            
            await eywa.Tasks.UpdateTaskAsync(Status.Processing);
            
            // Main processing logic
            var processedCount = 0;
            var errorCount = 0;
            
            var filesToProcess = await GetFilesToProcess(eywa);
            
            foreach (var file in filesToProcess)
            {
                try
                {
                    await ProcessSingleFile(eywa, file);
                    processedCount++;
                    
                    if (processedCount % 10 == 0)
                    {
                        await eywa.Logger.InfoAsync($"Progress: {processedCount}/{filesToProcess.Count} files processed");
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    await eywa.Logger.ErrorAsync("Failed to process file", new {
                        fileId = file["euuid"],
                        fileName = file["name"],
                        error = ex.Message
                    });
                }
            }
            
            // Generate comprehensive report
            await eywa.Tasks.ReportAsync("Data Processing Complete", new ReportOptions
            {
                Data = new ReportData
                {
                    Card = $"""
                        # Processing Complete ✅
                        
                        Successfully processed **{processedCount:N0}** files with **{errorCount}** errors.
                        
                        ## Summary
                        - **Success Rate:** {(double)processedCount / filesToProcess.Count:P1}
                        - **Total Files:** {filesToProcess.Count:N0}
                        - **Processing Time:** {DateTime.Now:HH:mm:ss}
                        """,
                    Tables = new Dictionary<string, TableData>
                    {
                        ["Results"] = new TableData
                        {
                            Headers = ["Status", "Count", "Percentage"],
                            Rows = new object[][]
                            {
                                ["Successful", processedCount, $"{(double)processedCount / filesToProcess.Count:P1}"],
                                ["Failed", errorCount, $"{(double)errorCount / filesToProcess.Count:P1}"]
                            }
                        }
                    }
                }
            });
            
            await eywa.Logger.InfoAsync("Robot completed successfully", new {
                processedCount = processedCount,
                errorCount = errorCount,
                successRate = (double)processedCount / filesToProcess.Count
            });
            
            await eywa.Tasks.CloseTaskAsync(Status.Success);
        }
        catch (Exception ex)
        {
            await eywa.Logger.ErrorAsync("Robot failed", new { 
                error = ex.Message,
                stackTrace = ex.StackTrace 
            });
            await eywa.Tasks.CloseTaskAsync(Status.Error);
            throw;
        }
    }
    
    static async Task<List<Dictionary<string, object>>> GetFilesToProcess(Eywa eywa)
    {
        var result = await eywa.GraphQLAsync(@"
            query GetProcessableFiles {
                searchFile(_where: {status: {_eq: ""UPLOADED""}}) {
                    euuid name size content_type
                    folder { name }
                }
            }");
            
        var files = (List<object>)result["data"]["searchFile"];
        return files.Cast<Dictionary<string, object>>().ToList();
    }
    
    static async Task ProcessSingleFile(Eywa eywa, Dictionary<string, object> file)
    {
        var fileUuid = file["euuid"].ToString();
        
        // Download file
        var fileBytes = await eywa.Files.DownloadAsync(fileUuid);
        
        // Process file content (your specific logic here)
        var processedContent = ProcessFileContent(fileBytes);
        
        // Upload processed result
        var resultUuid = Guid.NewGuid().ToString();
        await eywa.Files.UploadContentAsync(processedContent, new Dictionary<string, object>
        {
            ["euuid"] = resultUuid,
            ["name"] = $"processed-{file["name"]}",
            ["folder"] = eywa.Files.RootFolder,
            ["content_type"] = "text/plain"
        });
        
        // Update original file status
        await eywa.GraphQLAsync(@"
            mutation UpdateFileStatus($uuid: UUID!) {
                stackFile(data: {euuid: $uuid, status: ""PROCESSED""}) {
                    euuid status
                }
            }", new { uuid = fileUuid });
    }
    
    static string ProcessFileContent(byte[] fileBytes)
    {
        // Your specific file processing logic here
        var content = Encoding.UTF8.GetString(fileBytes);
        return content.ToUpper(); // Example transformation
    }
}
```

### Running Your Robot

```bash
# Via EYWA CLI
eywa run -c "dotnet run --project DataProcessingRobot.csproj"
```

---

## 🏆 Summary

The EYWA C# client excels when you:

1. **Embrace the dynamic approach** - Use `Dictionary<string, object>` naturally
2. **Write GraphQL directly** - Don't fight the GraphQL-first design  
3. **Handle resources properly** - Always dispose, manage streams carefully
4. **Structure for maintainability** - Use consistent patterns and error handling
5. **Test integration scenarios** - Verify end-to-end workflows
6. **Monitor and log effectively** - Use structured logging for observability

**Key Principle**: This client abstracts protocol complexity (S3 uploads, JSON-RPC) but preserves query complexity (GraphQL). This gives you maximum flexibility while handling the tedious protocol details.

---

*For more examples and advanced patterns, see the [examples directory](./examples/) and the comprehensive [User Guide](./CSHARP_CLIENT_USER_GUIDE.md) and [Dos and Don'ts](./CSHARP_CLIENT_DOS_AND_DONTS.md) documents.*
