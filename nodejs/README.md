# EYWA Client for Node.js

[![npm version](https://badge.fury.io/js/eywa-client.svg)](https://badge.fury.io/js/eywa-client)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Complete EYWA client library for Node.js providing JSON-RPC communication, GraphQL queries, stream-based file operations, and task management for EYWA robots.

## Installation

```bash
npm install eywa-client
```

## Features

- 🚀 **JSON-RPC Communication** - Bidirectional communication with EYWA runtime
- 📊 **GraphQL Integration** - Execute queries and mutations against EYWA datasets
- 📁 **File Operations** - Stream-based upload/download with folder management
- 📝 **Comprehensive Logging** - Multiple log levels with metadata support
- 🔄 **Task Management** - Update status, report progress, handle task lifecycle
- ⚡ **Async/Promise Based** - Modern async/await support

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

### File Operations

```javascript
import eywa from 'eywa-client'
import { createReadStream } from 'fs'
import { Readable } from 'stream'

eywa.open_pipe()

// Upload file from filesystem
const fileStream = createReadStream('/local/document.pdf')
const file = await eywa.uploadFile(fileStream, '/documents/2024/document.pdf')
console.log('Uploaded:', file.euuid)

// Upload from memory
const data = Buffer.from(JSON.stringify({ status: 'active' }))
const memStream = Readable.from(data)
await eywa.uploadFile(memStream, '/config/settings.json', {
  size: data.length
})

// Download file
const downloadStream = await eywa.downloadFile(file.euuid)
downloadStream.pipe(fs.createWriteStream('/local/downloaded.pdf'))

// Create folders
await eywa.createFolder('/documents/2024/reports/')

// Delete files and folders
await eywa.deleteFile(fileUuid)
await eywa.deleteFolder('/documents/2024/reports/')
```

## API Reference

### Initialization

#### `open_pipe()`
Initialize stdin/stdout communication with EYWA runtime. Must be called before using other functions.

### File Operations

#### `uploadFile(stream, eywaPath, options)`
Upload a stream to EYWA file service.

**Parameters:**
- `stream` (ReadableStream) - Source stream to upload
- `eywaPath` (string|null) - Target path (e.g., `/documents/file.pdf`) or `null` for orphan files
- `options` (Object)
  - `fileName` (string) - Required if eywaPath is null
  - `size` (number) - Stream size in bytes (auto-detected for file streams)
  - `contentType` (string) - MIME type (auto-detected from path/filename)
  - `createFolders` (boolean) - Auto-create missing folders (default: true)
  - `progressCallback` (function) - Progress callback `(uploaded, total) => {}`

**Returns:** Promise<Object> - File object with `euuid`, `name`, `content_type`, `size`, `status`

**Examples:**

```javascript
// From filesystem
import { createReadStream } from 'fs'
const stream = createReadStream('/local/file.pdf')
const file = await eywa.uploadFile(stream, '/documents/file.pdf')

// From memory (requires size)
import { Readable } from 'stream'
const data = Buffer.from('Hello, EYWA!')
const stream = Readable.from(data)
await eywa.uploadFile(stream, '/notes/hello.txt', { size: data.length })

// Orphan file (no folder)
await eywa.uploadFile(stream, null, {
  fileName: 'temp.txt',
  size: 100
})

// With progress tracking
await eywa.uploadFile(stream, '/uploads/large.iso', {
  progressCallback: (uploaded, total) => {
    console.log(`${(uploaded/total*100).toFixed(1)}%`)
  }
})
```

#### `downloadFile(fileUuid, options)`
Download a file as a stream.

**Parameters:**
- `fileUuid` (string) - UUID of file to download
- `options` (Object)
  - `progressCallback` (function) - Progress callback `(downloaded, total) => {}`

**Returns:** Promise<ReadableStream> - Download stream

**Examples:**

```javascript
// Download to file
import { createWriteStream } from 'fs'
import { pipeline } from 'stream/promises'

const stream = await eywa.downloadFile(fileUuid)
await pipeline(stream, createWriteStream('/local/file.pdf'))

// Download to memory
const chunks = []
for await (const chunk of stream) {
  chunks.push(chunk)
}
const content = Buffer.concat(chunks).toString('utf-8')
```

#### `createFolder(folderPath)`
Create nested folder hierarchy.

**Parameters:**
- `folderPath` (string) - Folder path (must end with `/`)

**Returns:** Promise<Object> - Folder object

**Example:**

```javascript
// Creates all missing folders in path
await eywa.createFolder('/documents/2024/Q1/reports/')
```

#### `deleteFolder(folderPath)`
Delete a folder.

**Parameters:**
- `folderPath` (string) - Folder path to delete

**Returns:** Promise<boolean>

**Example:**

```javascript
await eywa.deleteFolder('/documents/2024/Q1/')
```

#### `deleteFile(fileUuid)`
Delete a file.

**Parameters:**
- `fileUuid` (string) - UUID of file to delete

**Returns:** Promise<boolean>

**Example:**

```javascript
await eywa.deleteFile('abc-123-...')
```

### GraphQL

#### `graphql(query, variables?)`
Execute GraphQL queries and mutations.

**For file queries, use GraphQL directly instead of wrapper functions.** This gives you full control over the query structure and access to all GraphQL features.

**Examples:**

```javascript
// Search files
const result = await eywa.graphql(`
  query {
    searchFile(_where: { name: { _ilike: "%report%" } }) {
      euuid
      name
      size
      uploaded_at
    }
  }
`)

// List files in folder
const folder = await eywa.graphql(`
  query {
    getFolder(path: "/documents/") { euuid }
  }
`)

const files = await eywa.graphql(`
  query {
    searchFile(_where: { 
      folder: { euuid: { _eq: "${folder.data.getFolder.euuid}" } }
    }) {
      euuid
      name
    }
  }
`)

// Get file info
const fileInfo = await eywa.graphql(`
  query {
    getFile(euuid: "${fileUuid}") {
      euuid
      name
      content_type
      size
      status
      uploaded_at
      folder {
        path
      }
    }
  }
`)

// Complex query with filters
const result = await eywa.graphql(`
  query {
    searchFile(
      _where: {
        _and: [
          { uploaded_at: { _gte: "2024-01-01" } }
          { size: { _gt: 1048576 } }
          { status: { _eq: "UPLOADED" } }
        ]
      }
      _order_by: { uploaded_at: desc }
      _limit: 10
    ) {
      euuid
      name
      size
    }
  }
`)
```

### Logging Functions

#### `log(record)`
Log with full control over all parameters.

```javascript
eywa.log({
  event: 'INFO',
  message: 'Processing item',
  data: { itemId: 123 },
  duration: 1500
})
```

#### Convenience Methods

`info(message, data?)`, `error(message, data?)`, `warn(message, data?)`, `debug(message, data?)`, `trace(message, data?)`, `exception(message, data?)`

```javascript
eywa.info('User logged in', { userId: 'abc123' })
eywa.error('Failed to process', { error: err.message })
```

### Task Management

#### `get_task()`
Get current task information.

```javascript
const task = await eywa.get_task()
console.log('Processing:', task.message)
```

#### `update_task(status?)`
Update task status.

```javascript
eywa.update_task(eywa.PROCESSING)
```

#### `close_task(status?)`
Close task and exit process.

```javascript
eywa.close_task(eywa.SUCCESS)
```

#### `return_task()`
Return control without closing task.

#### `report(message, data?, image?)`
Send task report.

```javascript
eywa.report('Analysis complete', {
  accuracy: 0.95,
  processed: 1000
})
```

### JSON-RPC

#### `send_request(data)`
Send JSON-RPC request and wait for response.

#### `send_notification(data)`
Send JSON-RPC notification.

#### `register_handler(method, handler)`
Register handler for incoming calls.

### Constants

- `SUCCESS` - Task completed successfully
- `ERROR` - Task failed
- `PROCESSING` - Task is processing
- `EXCEPTION` - Task exception

### Error Classes

- `FileUploadError` - Thrown on upload failures
- `FileDownloadError` - Thrown on download failures

## Complete Example

```javascript
import eywa from 'eywa-client'
import { createReadStream } from 'fs'

async function processFiles() {
  eywa.open_pipe()
  
  try {
    const task = await eywa.get_task()
    eywa.info('Starting file processing', { taskId: task.euuid })
    eywa.update_task(eywa.PROCESSING)
    
    // Create folder structure
    await eywa.createFolder('/processed/2024/')
    
    // Upload file
    const stream = createReadStream('/data/input.csv')
    const file = await eywa.uploadFile(stream, '/processed/2024/input.csv')
    eywa.info('File uploaded', { fileId: file.euuid })
    
    // Query uploaded files
    const files = await eywa.graphql(`
      query {
        searchFile(_where: {
          folder: { path: { _eq: "/processed/2024/" } }
        }) {
          euuid
          name
          size
        }
      }
    `)
    
    eywa.report('Processing complete', {
      filesProcessed: files.data.searchFile.length
    })
    
    eywa.close_task(eywa.SUCCESS)
    
  } catch (error) {
    eywa.error('Task failed', {
      error: error.message,
      stack: error.stack
    })
    eywa.close_task(eywa.ERROR)
  }
}

processFiles()
```

## Why No searchFiles() or listFiles() Functions?

This library intentionally provides minimal query functions. Instead of wrapping GraphQL with limited abstractions, we encourage using `graphql()` directly because:

- ✅ **Full GraphQL power** - Access all operators and features
- ✅ **No schema coupling** - Library doesn't break when schema changes  
- ✅ **Better learning** - You learn the actual EYWA GraphQL API
- ✅ **More flexible** - Complex queries are easier to write
- ✅ **Less maintenance** - Fewer abstractions to maintain

The library focuses on what it does best: **protocols** (upload/download flows) and **complex logic** (folder hierarchy). For queries, GraphQL is already a perfect query language!

## Testing

Test locally using EYWA CLI:

```bash
eywa run -c 'node my-robot.js'
```

## Examples

See the `examples/` directory for:
- Complex folder structure uploads
- Download verification
- Cleanup operations
- More...

## Changelog

### 0.2.0 (2025-10-22)

**Added:**
- Stream-based file upload (`uploadFile`)
- Stream-based file download (`downloadFile`)
- Folder management (`createFolder`, `deleteFolder`)
- File deletion (`deleteFile`)

**Removed:**
- `uploadContent` - use `uploadFile` with stream
- `searchFiles` - use `graphql()` directly
- `listFiles` - use `graphql()` directly
- `getFileInfo` - use `graphql()` directly
- `getFileByName` - use `graphql()` directly
- `calculateFileHash` - user's responsibility
- `quickUpload` - use `uploadFile`
- `quickDownload` - use `downloadFile`

**Philosophy:** Library handles protocols, not queries. Use GraphQL directly for maximum flexibility.

### 0.1.1 (Previous)
- Initial release with JSON-RPC and GraphQL support

## License

MIT © Robert Gersak

## Support

- Documentation: https://github.com/neyho/eywa
- Issues: https://github.com/neyho/eywa/issues
- Website: https://www.eywaonline.com
