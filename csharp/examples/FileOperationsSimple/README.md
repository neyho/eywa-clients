# EYWA C# File Operations - Simple Example

A minimal, straightforward example demonstrating core EYWA file operations in C#. This example is inspired by the Babashka `file_operations_simple.clj` pattern.

## What This Example Demonstrates

1. **Creating folder structures** with pre-generated UUIDs
2. **Uploading files** to specific folders
3. **Uploading generated content** directly (no file required)
4. **Listing files** in folders
5. **Downloading files** and reading content
6. **Replacing/updating files** using the same UUID

## Running the Example

### Prerequisites

- .NET 9.0 SDK installed
- EYWA server running and accessible
- EYWA CLI configured and authenticated

### Option 1: Run via EYWA CLI (Recommended)

```bash
cd /Users/robi/dev/EYWA/core/clients/csharp
eywa run -c "dotnet run --project examples/FileOperationsSimple"
```

The EYWA CLI will automatically:
- Set up the connection to EYWA server
- Handle authentication
- Provide the necessary environment variables

### Option 2: Run Directly with .NET

```bash
cd examples/FileOperationsSimple
dotnet run
```

**Note:** When running directly, you need to ensure EYWA connection settings are configured in your environment or application configuration.

## Key Features

### Pre-generated UUIDs

The example uses pre-generated UUIDs for predictable structure:

```csharp
var demoFolderUuid = Guid.Parse("9bd6fe99-7540-4a54-9998-138405ea8d2c");
var reportsFolderUuid = Guid.Parse("4e2dfc2f-d46e-499a-b008-2104b9214aa1");
```

This allows for:
- **Idempotent operations** - run the script multiple times without creating duplicates
- **File replacement** - upload with same UUID to replace existing content
- **Predictable references** - know folder/file UUIDs in advance

### Complete Metadata Control

Users specify all metadata via dictionaries:

```csharp
await _folderManager.CreateFolderAsync(new Dictionary<string, object>
{
    { "name", "demo-files" },
    { "euuid", demoFolderUuid },
    { "parent", FileConstants.RootFolder }
});
```

### Folder Relationships

Folders reference their parents explicitly:

```csharp
await _folderManager.CreateFolderAsync(new Dictionary<string, object>
{
    { "name", "reports" },
    { "euuid", reportsFolderUuid },
    { "parent", new Dictionary<string, object> { { "euuid", demoFolderUuid } } }
});
```

### File Upload Options

Upload text content directly:

```csharp
await _uploader.UploadTextAsync(content, new Dictionary<string, object>
{
    { "name", "sample.txt" },
    { "euuid", fileUuid },
    { "content_type", "text/plain" },
    { "folder", new Dictionary<string, object> { { "euuid", folderUuid } } }
});
```

Or upload from file path:

```csharp
await _uploader.UploadFileAsync("path/to/file.pdf", new Dictionary<string, object>
{
    { "name", "document.pdf" },
    { "folder", new Dictionary<string, object> { { "euuid", folderUuid } } }
});
```

### File Replacement

Use the same UUID to replace existing files:

```csharp
// Initial upload
await _uploader.UploadTextAsync("Original content", new Dictionary<string, object>
{
    { "euuid", fileUuid },
    { "name", "config.txt" }
});

// Later: Replace the file
await _uploader.UploadTextAsync("Updated content", new Dictionary<string, object>
{
    { "euuid", fileUuid }, // Same UUID = replacement
    { "name", "config.txt" }
});
```

## Expected Output

```
=== EYWA File Operations - Simple Examples ===

📋 Test Data Generated:
  demo-files folder: 9bd6fe99-7540-4a54-9998-138405ea8d2c
  reports folder:    4e2dfc2f-d46e-499a-b008-2104b9214aa1
  sample file:       3f0f4173-4ef7-4499-857e-37568adeab48
  generated file:    b986f49c-b91b-48fb-b4df-e749f6ca735a


📁 EXAMPLE 1: Create Folder Structure
Creating folder: demo-files
✅ SUCCESS
  UUID: 9bd6fe99-7540-4a54-9998-138405ea8d2c
  Name: demo-files
  Path: /demo-files

Creating folder: reports
✅ SUCCESS
  UUID: 4e2dfc2f-d46e-499a-b008-2104b9214aa1
  Path: /demo-files/reports
  Parent: demo-files


📤 EXAMPLE 2: Upload File to Folder
Uploading sample.txt to demo-files folder
✅ SUCCESS
  UUID: 3f0f4173-4ef7-4499-857e-37568adeab48
  Name: sample.txt
  Size: 42 bytes
  Path: /demo-files/sample.txt

... (additional examples) ...

✅ VERIFICATION SUMMARY
======================================

📁 Folders Created:
  demo-files: /demo-files
  reports:    /demo-files/reports

📄 Files Uploaded:
  sample.txt:    /demo-files/sample.txt (42 bytes)
  generated.txt: /demo-files/generated.txt (78 bytes)

💡 Key Takeaways:
  • Pre-generate UUIDs and pass complete folder/file definitions
  • CreateFolderAsync accepts { name, euuid, parent }
  • Folder can be specified as { euuid: ... } in file metadata
  • EYWA automatically computes file paths based on folder hierarchy
  • Same UUID can be used to replace/update file content
  • Upload returns void on success - verify with GetFileInfoAsync

👋 Demo finished!
```

## Comparison with Other Examples

| Example | Complexity | Use Case |
|---------|-----------|----------|
| `FileOperationsSimple` | ⭐ Minimal | Quick start, basic operations |
| `SimpleFileExample.cs` | ⭐⭐ Moderate | Multiple approaches, concise |
| `FileOperationsExample.cs` | ⭐⭐⭐ Comprehensive | All features, detailed examples |

## Next Steps

1. Run this example to understand the basics
2. Check `SimpleFileExample.cs` for more upload patterns
3. Review `FileOperationsExample.cs` for comprehensive coverage
4. Read the main documentation at `README_FILE_OPERATIONS.md`

## Troubleshooting

### Connection Issues

If you see connection errors, ensure:
- EYWA server is running
- You're authenticated via `eywa connect <server-url>`
- Environment variables are properly set when running via EYWA CLI

### Missing Folders

The example creates its own folder structure. If you want to use existing folders:
1. Query the folder first: `await _folderManager.GetFolderByPathAsync("/your-path")`
2. Use the returned UUID in your file metadata

### UUID Conflicts

If UUIDs already exist in your EYWA instance, the operations will update existing resources. To avoid this:
- Generate new UUIDs: `Guid.NewGuid()`
- Let EYWA generate them by omitting the `euuid` field
