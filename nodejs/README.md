# EYWA Client for Node.js

[![npm version](https://badge.fury.io/js/eywa-client.svg)](https://badge.fury.io/js/eywa-client)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**GraphQL-aligned EYWA client library for Node.js** following the Babashka pattern. Provides JSON-RPC communication, GraphQL queries, and stream-based file operations with client-controlled UUID management.

## Installation

```bash
npm install eywa-client
```

## 🎯 Key Features

- 🚀 **GraphQL-Aligned API** - Single map arguments that directly mirror GraphQL schema
- 🔧 **Client-Controlled UUIDs** - Full control over file and folder UUIDs for deterministic operations
- 📁 **Stream-Based File Operations** - Memory-efficient upload/download with progress tracking
- 📊 **JSON-RPC Communication** - Bidirectional communication with EYWA runtime
- 📋 **Task Management** - Update status, report progress, handle task lifecycle
- 📝 **Comprehensive Logging** - Multiple log levels with metadata support
- ⚡ **Modern Async/Await** - Promise-based API throughout

## 🏗️ Architecture Philosophy

This client follows the **Babashka pattern** for maximum GraphQL compatibility:

- ✅ **Direct GraphQL Mapping** - Function arguments directly mirror GraphQL input types
- ✅ **No Parameter Mangling** - Data flows directly to GraphQL without transformation
- ✅ **Single Map Arguments** - All functions use single object parameters
- ✅ **Client UUID Control** - UUIDs are always client-managed for both creation and updates
- ✅ **No Sugar Wrapping** - Direct GraphQL calls without abstraction layers

## Quick Start

### Basic Usage

```javascript
import eywa from 'eywa-client'

// Initialize the client
eywa.open_pipe()

// Log messages  
eywa.info('Robot started')

// Execute GraphQL queries
const result = await eywa.graphql(`
  query {
    searchUser(_limit: 10) {
      euuid
      name
    }
  }
`)

// Complete the task
eywa.close_task(eywa.SUCCESS)
```

## 📁 File Operations

### GraphQL-Aligned File Upload

```javascript
import { randomUUID } from 'crypto'

// Upload new file with client-generated UUID
const fileUuid = randomUUID()
await eywa.upload('document.pdf', {
  name: 'document.pdf',
  euuid: fileUuid,                    // Client controls UUID
  folder: { euuid: folderUuid },      // Direct GraphQL reference
  content_type: 'application/pdf'     // Explicit MIME type
})

// Replace existing file (same UUID = update)
await eywa.uploadContent('Updated content', {
  name: 'document.txt',
  euuid: fileUuid,                    // Same UUID = replace existing
  content_type: 'text/plain'
})
```

### Upload from Different Sources

```javascript
// Upload file from disk
await eywa.upload('/path/to/file.txt', {
  name: 'file.txt',
  euuid: randomUUID(),
  folder: { euuid: folderUuid }
})

// Upload from memory/string
await eywa.uploadContent('Hello EYWA!', {
  name: 'greeting.txt', 
  euuid: randomUUID(),
  content_type: 'text/plain'
})

// Upload from stream with progress
await eywa.uploadStream(readableStream, {
  name: 'data.bin',
  euuid: randomUUID(), 
  size: streamSize,                   // Required for streams
  progressFn: (uploaded, total) => {
    console.log(`Progress: ${(uploaded/total*100).toFixed(1)}%`)
  }
})
```

### Download Files

```javascript
// Download as Buffer
const content = await eywa.download(fileUuid)
console.log(content.toString()) 

// Download as stream (memory efficient)
const { stream, contentLength } = await eywa.downloadStream(fileUuid)
for await (const chunk of stream) {
  process.stdout.write(chunk)
}
```

### File Management

```javascript
// Get file information
const fileInfo = await eywa.fileInfo(fileUuid)
console.log(`File: ${fileInfo.name} (${fileInfo.size} bytes)`)

// List files with filters
const files = await eywa.list({
  folder: { euuid: folderUuid },      // Filter by folder UUID
  name: 'report',                     // Filter by name pattern
  limit: 10                           // Limit results
})

// Delete file
await eywa.deleteFile(fileUuid)
```

## 📂 Folder Operations

### Create Folder Hierarchy

```javascript
// Create root folder
const rootFolderId = randomUUID()
await eywa.createFolder({
  name: 'project-documents',
  euuid: rootFolderId,
  parent: { euuid: eywa.rootUuid }    // Under system root
})

// Create nested folder
const subFolderId = randomUUID() 
await eywa.createFolder({
  name: 'reports',
  euuid: subFolderId,
  parent: { euuid: rootFolderId }     // Under project folder
})
```

### Folder Management

```javascript
// Get folder information
const folderInfo = await eywa.getFolderInfo({ euuid: folderId })
console.log(`Folder: ${folderInfo.path}`)

// List folders
const folders = await eywa.listFolders({
  parent: { euuid: parentId },        // Filter by parent
  name: 'test',                       // Filter by name pattern
  limit: 5
})

// Delete empty folder
await eywa.deleteFolder(folderId)
```

## 🔄 UUID Management Patterns

### Deterministic Testing

```javascript
// Pre-generate UUIDs for guaranteed cleanup
const testData = {
  folders: {
    demo: { 
      euuid: 'c7f49a8c-4b0e-4d5a-9f3a-7e8b2c1d0e9f',
      name: 'demo-files',
      parent: { euuid: eywa.rootUuid }
    }
  },
  files: {
    sample: {
      euuid: '11111111-2222-3333-4444-555555555555',
      name: 'sample.txt',
      folder: { euuid: 'c7f49a8c-4b0e-4d5a-9f3a-7e8b2c1d0e9f' }
    }
  }
}

// Use in operations
await eywa.createFolder(testData.folders.demo)
await eywa.uploadContent('Test content', testData.files.sample)
```

### File Updates and Deduplication

```javascript
const documentId = randomUUID()

// Initial upload
await eywa.uploadContent('Version 1', {
  name: 'document.txt',
  euuid: documentId,                  // Client-provided UUID
  folder: { euuid: folderId }
})

// Update same file (same UUID = replace)
await eywa.uploadContent('Version 2', {
  name: 'document.txt',
  euuid: documentId,                  // Same UUID = update existing
  folder: { euuid: folderId }
})

// Check deduplication worked
const info = await eywa.fileInfo(documentId)
console.log('Same UUID after update:', info.euuid === documentId) // true
```

## 📊 GraphQL Integration

### Direct Query Execution

```javascript
// Simple query
const users = await eywa.graphql(`
  query GetUsers($limit: Int!) {
    searchUser(_limit: $limit) {
      euuid
      name
      email
    }
  }
`, { limit: 10 })

// File operations using GraphQL directly
const result = await eywa.graphql(`
  mutation CreateFile($file: FileInput!) {
    requestUploadURL(file: $file)
  }
`, {
  file: {
    name: 'document.pdf',
    euuid: randomUUID(),
    folder: { euuid: folderUuid },
    content_type: 'application/pdf',
    size: 12345
  }
})
```

## 🔧 Task Management

```javascript
// Update task status
eywa.update_task(eywa.PROCESSING)

// Log progress with data
eywa.info('Processing file', { filename: 'data.csv', rows: 150 })

// Report results
eywa.report('Analysis complete', {
  processed: 150,
  errors: 0,
  duration: '2.3s'
})

// Complete successfully
eywa.close_task(eywa.SUCCESS)
```

## 🚨 Error Handling

```javascript
try {
  await eywa.upload('/nonexistent/file.txt', {
    name: 'file.txt',
    euuid: randomUUID()
  })
} catch (error) {
  if (error instanceof eywa.FileUploadError) {
    eywa.error('Upload failed', { 
      message: error.message,
      code: error.code 
    })
  }
}
```

## 📈 Advanced Usage

### Progress Tracking

```javascript
let lastPercent = 0
await eywa.uploadStream(largeFileStream, {
  name: 'large-file.zip',
  euuid: randomUUID(),
  size: fileSize,
  progressFn: (uploaded, total) => {
    const percent = Math.round((uploaded / total) * 100)
    if (percent !== lastPercent && percent % 10 === 0) {
      eywa.info(`Upload progress: ${percent}%`)
      lastPercent = percent
    }
  }
})
```

### Concurrent Operations

```javascript
// Upload multiple files concurrently
const uploads = files.map(file => 
  eywa.upload(file.path, {
    name: file.name,
    euuid: randomUUID(),
    folder: { euuid: targetFolder }
  })
)

await Promise.all(uploads)
eywa.info(`Uploaded ${uploads.length} files`)
```

## 🔍 Examples

### Complete File Management Workflow

```javascript
import eywa, { randomUUID } from 'eywa-client'

eywa.open_pipe()

async function main() {
  try {
    // Create project structure
    const projectId = randomUUID()
    const docsId = randomUUID()
    
    await eywa.createFolder({
      name: 'my-project',
      euuid: projectId,
      parent: { euuid: eywa.rootUuid }
    })
    
    await eywa.createFolder({
      name: 'documents',
      euuid: docsId,
      parent: { euuid: projectId }
    })
    
    // Upload files
    const files = [
      { path: 'README.md', uuid: randomUUID() },
      { path: 'config.json', uuid: randomUUID() }
    ]
    
    for (const file of files) {
      await eywa.upload(file.path, {
        name: file.path.split('/').pop(),
        euuid: file.uuid,
        folder: { euuid: docsId }
      })
      eywa.info(`Uploaded ${file.path}`)
    }
    
    // List uploaded files
    const uploadedFiles = await eywa.list({
      folder: { euuid: docsId }
    })
    
    eywa.report('Project setup complete', {
      folders: 2,
      files: uploadedFiles.length
    })
    
  } catch (error) {
    eywa.error('Workflow failed', { message: error.message })
    eywa.close_task(eywa.ERROR)
    return
  }
  
  eywa.close_task(eywa.SUCCESS)
}

main()
```

## 📚 API Reference

### Core Functions

| Function | Description |
|----------|-------------|
| `eywa.open_pipe()` | Initialize JSON-RPC communication |
| `eywa.graphql(query, variables?)` | Execute GraphQL query/mutation |
| `eywa.close_task(status)` | Complete task with status |

### File Operations

| Function | Description |
|----------|-------------|
| `upload(filepath, fileData)` | Upload file with GraphQL FileInput |
| `uploadStream(stream, fileData)` | Upload from ReadableStream |
| `uploadContent(content, fileData)` | Upload string/Buffer content |
| `download(fileUuid)` | Download file as Buffer |
| `downloadStream(fileUuid)` | Download file as stream |
| `fileInfo(fileUuid)` | Get file information |
| `list(filters?)` | List files with optional filters |
| `deleteFile(fileUuid)` | Delete file by UUID |

### Folder Operations

| Function | Description |
|----------|-------------|
| `createFolder(folderData)` | Create folder with GraphQL FolderInput |
| `listFolders(filters?)` | List folders with optional filters |
| `getFolderInfo(data)` | Get folder info by UUID or path |
| `deleteFolder(folderUuid)` | Delete empty folder |

### Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `eywa.rootUuid` | `'87ce50d8-5dfa-4008-a265-053e727ab793'` | System root folder UUID |
| `eywa.rootFolder` | `{euuid: rootUuid}` | Root folder reference object |
| `eywa.SUCCESS` | `'SUCCESS'` | Task completed successfully |
| `eywa.ERROR` | `'ERROR'` | Task failed |

## 🔗 Related Projects

- [EYWA Core](https://github.com/neyho/eywa) - The main EYWA platform
- [Babashka Client](https://github.com/neyho/eywa/tree/master/clients/bb) - Reference implementation
- [Python Client](https://github.com/neyho/eywa/tree/master/clients/py) - Python version
- [C# Client](https://github.com/neyho/eywa/tree/master/clients/csharp) - C# version

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit changes: `git commit -m 'Add amazing feature'`
4. Push to branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

## 📞 Support

- 📧 Email: robi@neyho.com
- 🐛 Issues: [GitHub Issues](https://github.com/neyho/eywa/issues)
- 📖 Documentation: [EYWA Documentation](https://neyho.github.io/eywa/)
