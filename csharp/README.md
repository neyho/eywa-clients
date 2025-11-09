# EYWA C# Client - Dynamic GraphQL-First

A clean, dynamic GraphQL-first client for the EYWA platform that stays as close to GraphQL as possible. Built from scratch with a focus on simplicity and GraphQL alignment.

## 🎯 Core Philosophy

- **Dynamic-first**: Uses `Dictionary<string, object>` for all data interchange
- **GraphQL-native**: Single map arguments that mirror GraphQL schema exactly
- **Protocol abstraction only**: Abstracts complex S3 upload/download, not query complexity
- **Zero translation layers**: What you write in GraphQL is what you pass to functions

## 🚀 Quick Start

### Installation

```bash
dotnet add package EywaClient
```

### Basic Usage

```csharp
using EywaClient;

var eywa = new EywaClient.EywaClient();
eywa.OpenPipe();

// Direct GraphQL - the power of dynamic approach!
var result = await eywa.GraphQLAsync(@"
    query MyFiles($limit: Int!) {
        searchFile(_limit: $limit) {
            euuid name size
            folder { name path }
        }
    }", new Dictionary<string, object> { ["limit"] = 10 });

// Access results dynamically - just like JavaScript!
var files = (List<object>)result["data"]["searchFile"];
foreach (Dictionary<string, object> file in files)
{
    Console.WriteLine($"{file["name"]} - {file["size"]} bytes");
}
```

### File Operations

```csharp
// Upload with GraphQL-aligned parameters
await eywa.Files.UploadAsync("document.pdf", new Dictionary<string, object>
{
    ["euuid"] = Guid.NewGuid().ToString(), // Client-controlled UUID
    ["name"] = "important.pdf",
    ["folder"] = new Dictionary<string, object> { ["euuid"] = eywa.Files.RootUuid },
    ["content_type"] = "application/pdf"
});

// Download as stream
var stream = await eywa.Files.DownloadStreamAsync("file-uuid");

// Create folder
await eywa.Files.CreateFolderAsync(new Dictionary<string, object>
{
    ["name"] = "Reports", 
    ["parent"] = new Dictionary<string, object> { ["euuid"] = eywa.Files.RootUuid }
});
```

## 📋 Complete FILES_SPEC.md Implementation

Implements all 8 core functions from the specification:

### Upload Operations
- `UploadAsync(filepath, fileData)` - Upload from file path
- `UploadStreamAsync(stream, fileData)` - Upload from stream  
- `UploadContentAsync(content, fileData)` - Upload string/bytes directly

### Download Operations
- `DownloadStreamAsync(fileUuid)` - Download as stream
- `DownloadAsync(fileUuid)` - Download as byte array

### CRUD Operations
- `CreateFolderAsync(folderData)` - Create folder
- `DeleteFileAsync(fileUuid)` - Delete file
- `DeleteFolderAsync(folderUuid)` - Delete empty folder

### Constants
- `Files.RootUuid` - Root folder UUID constant
- `Files.RootFolder` - Root folder object for GraphQL

### Exception Types
- `FileUploadError` - Upload operation errors
- `FileDownloadError` - Download operation errors

## 🎨 API Design

### Core Components

```csharp
var eywa = new EywaClient.EywaClient();

// GraphQL operations
var result = await eywa.GraphQLAsync(query, variables);

// File operations (FILES_SPEC.md compliant)
await eywa.Files.UploadAsync(...);
var stream = await eywa.Files.DownloadStreamAsync(...);

// Task management
await eywa.Tasks.UpdateTaskAsync(TaskStatus.Processing);
await eywa.Tasks.CloseTaskAsync(TaskStatus.Success);

// Structured logging
await eywa.Logger.InfoAsync("Message", data);
```

### Dynamic Data Structures

Everything uses `Dictionary<string, object>` to mirror GraphQL exactly:

```csharp
// ✅ This (GraphQL-native)
var fileData = new Dictionary<string, object>
{
    ["euuid"] = "my-uuid",
    ["name"] = "document.pdf",
    ["folder"] = new Dictionary<string, object> { ["euuid"] = "folder-uuid" },
    ["content_type"] = "application/pdf"
};

// ❌ Not this (forced typing)
var fileData = new FileInput 
{ 
    Euuid = "my-uuid",
    Name = "document.pdf",
    Folder = new FolderReference { Euuid = "folder-uuid" }
};
```

## 🔄 Complete Robot Example

```csharp
using EywaClient;

class MyRobot
{
    static async Task Main(string[] args)
    {
        var eywa = new EywaClient.EywaClient();
        
        try
        {
            eywa.OpenPipe();
            
            var task = await eywa.Tasks.GetTaskAsync();
            await eywa.Logger.InfoAsync("Robot started", new { taskId = task["euuid"] });
            
            await eywa.Tasks.UpdateTaskAsync(TaskStatus.Processing);
            
            // Your robot logic here using direct GraphQL
            var data = await eywa.GraphQLAsync(@"
                query ProcessableData {
                    searchFile(_where: {status: {_eq: ""UPLOADED""}}) {
                        euuid name
                    }
                }");
            
            await eywa.Logger.InfoAsync("Robot completed successfully");
            await eywa.Tasks.CloseTaskAsync(TaskStatus.Success);
        }
        catch (Exception ex)
        {
            await eywa.Logger.ErrorAsync("Robot failed", new { error = ex.Message });
            await eywa.Tasks.CloseTaskAsync(TaskStatus.Error);
        }
        finally
        {
            eywa.Dispose();
        }
    }
}
```

## 🏃‍♂️ Running Your Robot

```bash
# Via EYWA CLI
eywa run -c "dotnet run --project MyRobot.csproj"
```

## 🛠 Project Structure

```
src/EywaClient/
├── Core/
│   ├── JsonRpcClient.cs      # Clean JSON-RPC over stdin/stdout
│   ├── TaskManager.cs        # Simple task lifecycle
│   └── Logger.cs             # Structured logging
├── Files/
│   ├── FilesClient.cs        # 8 core functions from FILES_SPEC.md
│   ├── FileExceptions.cs     # FileUploadError, FileDownloadError
│   └── FileConstants.cs      # ROOT_UUID, ROOT_FOLDER
├── Utils/
│   ├── MimeTypeDetector.cs   # Simple MIME detection
│   └── HttpS3Client.cs       # S3 protocol utilities
└── EywaClient.cs            # Main entry point
```

## ✨ Key Benefits

1. **No "impedance mismatch"** - C# maps directly mirror GraphQL
2. **Future-proof** - New GraphQL features work immediately  
3. **Simple to maintain** - Less code, no complex type mappings
4. **Natural for C# developers** - `Dictionary<string, object>` is familiar
5. **Follows FILES_SPEC.md exactly** - Protocol abstraction, not query abstraction

## 🆚 vs. Traditional Approach

### This Dynamic Approach ✅
```csharp
// Write GraphQL directly
var result = await eywa.GraphQLAsync(@"
    query UserFiles($userId: UUID!, $type: String!) {
        searchFile(_where: {
            _and: [
                {uploaded_by: {euuid: {_eq: $userId}}},
                {content_type: {_ilike: $type}}
            ]
        }, _order_by: {uploaded_at: desc}) {
            euuid name size uploaded_at
            folder { name }
        }
    }", new { userId = "uuid", type = "image/%" });
```

### Traditional Typed Approach ❌
```csharp
// Complex query builder
var files = await client.Files
    .Where(f => f.UploadedBy.Euuid == userId)
    .Where(f => f.ContentType.StartsWith("image/"))
    .OrderByDescending(f => f.UploadedAt)
    .Include(f => f.Folder)
    .ToListAsync();
```

## 🔧 Requirements

- **.NET 9.0** or later
- **EYWA Server** running and accessible
- **EYWA CLI** for executing robots

## 📝 License

MIT License - Built for the EYWA ecosystem with ❤️

---

**This is a clean-slate implementation that embraces C#'s dynamic capabilities while staying true to GraphQL's flexible nature.**
