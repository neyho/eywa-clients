# Changelog

All notable changes to eywa-client will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.0] - 2025-01-11

### Added

#### Enhanced Task Reports 📊
- **`report(message, options)`** - Completely rewritten for structured business intelligence
  - **Markdown Cards** - Rich text formatting with headers, lists, bold/italic text
  - **Named Tables** - Structured data tables like Excel sheets with headers and rows
  - **Base64 Image Support** - Attach charts, graphs, and visualizations
  - **Auto-Flag Generation** - Automatically sets `has_card`, `has_table`, `has_image` flags
  - **GraphQL Integration** - Uses proper GraphQL mutations instead of notifications
  - **Data Validation** - Validates table structure and base64 encoding
  - **Task Linking** - Reports automatically linked to parent tasks
  - **TypeScript Support** - Full type definitions for report structures
  - **Error Handling** - Comprehensive validation with clear error messages
  - **Metadata Support** - Optional metadata for additional context

#### Business Intelligence Features
- **Dashboard Ready** - Reports render perfectly in EYWA frontend dashboards
- **Multi-Table Architecture** - Named tables enable complex business reports
- **Mobile Responsive** - Markdown cards work beautifully on all devices
- **Analytics Integration** - Structured data supports BI tools and analysis
- **Export Capability** - Table data structure supports Excel/CSV export

#### Developer Experience
- **Backward Compatibility** - Old `report(message, data, image)` syntax still works
- **Comprehensive Examples** - Multiple example files demonstrating all features
- **Test Suite** - Automated tests for validation and error handling
- **Rich Documentation** - Updated README with migration guide and examples

### Changed
- **`report()` function** - Now returns Promise<TaskReport> instead of void
- **Report data structure** - Now uses GraphQL mutations for reliable delivery
- **Flag management** - Automatic flag setting based on content analysis

### Technical Details
- Enhanced TypeScript definitions with `ReportData`, `ReportOptions`, `TaskReport` interfaces
- Base64 validation using browser-compatible `atob`/`btoa` functions
- Table structure validation with row/column consistency checks
- Comprehensive error handling for invalid data structures
- GraphQL mutation: `stackTaskReport(data: $report)` with full schema compliance

## [0.2.0] - 2025-10-22

### Added

#### File Operations
- **`uploadFile(stream, eywaPath, options)`** - Stream-based file upload
  - Support for file streams (auto-detects size)
  - Support for memory streams (requires size option)
  - Support for orphan files (no folder)
  - Auto-create folder hierarchy
  - Progress callback support
  - MIME type auto-detection

- **`downloadFile(fileUuid, options)`** - Stream-based file download
  - Returns readable stream
  - Progress callback support
  - Memory-efficient streaming

- **`createFolder(folderPath)`** - Create nested folder hierarchies
  - Handles complex multi-level folder creation
  - Idempotent (safe to call multiple times)

- **`deleteFolder(folderPath)`** - Delete folders by path

- **`deleteFile(fileUuid)`** - Delete files by UUID

#### Error Classes
- `FileUploadError` - Thrown on upload failures
- `FileDownloadError` - Thrown on download failures

#### Documentation
- Complete file operations documentation
- GraphQL-first approach explanation
- Stream usage examples
- Complex test suite examples

### Removed

These functions were removed to keep the library focused. Use `graphql()` directly for queries:

- **`uploadContent()`** - Use `uploadFile()` with `Readable.from(buffer)` instead
- **`searchFiles()`** - Use `graphql()` with `searchFile` query
- **`listFiles()`** - Use `graphql()` with `searchFile` query
- **`getFileInfo()`** - Use `graphql()` with `getFile` or `searchFile` query
- **`getFileByName()`** - Use `graphql()` with `searchFile` query
- **`calculateFileHash()`** - Use Node.js `crypto` module directly
- **`quickUpload()`** - Use `uploadFile()` directly
- **`quickDownload()`** - Use `downloadFile()` with pipe to file

### Changed

- **Philosophy shift**: Library now handles protocols (upload/download flows, folder hierarchy), not query building
- GraphQL queries should be written directly by users for maximum flexibility
- Reduced library size by ~60% (from 30KB to 12.5KB)
- Eliminated schema coupling - library won't break when GraphQL schema changes

### Migration Guide

#### Before (0.1.x)
```javascript
// Old way - limited abstraction
const files = await eywa.searchFiles({ 
  namePattern: 'report',
  limit: 10 
})
```

#### After (0.2.0)
```javascript
// New way - direct GraphQL (more powerful)
const result = await eywa.graphql(`
  query {
    searchFile(_where: { name: { _ilike: "%report%" } }, _limit: 10) {
      euuid
      name
      size
    }
  }
`)
const files = result.data.searchFile
```

#### Upload Changes

Before:
```javascript
// File upload
await eywa.uploadFile('/local/file.pdf', '/docs/file.pdf')

// Memory upload
await eywa.uploadContent(jsonData, 'data.json', { folderPath: '/api/' })
```

After:
```javascript
import { createReadStream } from 'fs'
import { Readable } from 'stream'

// File upload
const stream = createReadStream('/local/file.pdf')
await eywa.uploadFile(stream, '/docs/file.pdf')

// Memory upload
const data = Buffer.from(jsonData)
const stream = Readable.from(data)
await eywa.uploadFile(stream, '/api/data.json', { size: data.length })
```

## [0.1.1] - 2024-XX-XX

### Added
- Initial release
- JSON-RPC communication
- GraphQL query execution
- Task management
- Logging functions
- Basic file operations (deprecated in 0.2.0)

## [0.1.0] - 2024-XX-XX

### Added
- Initial release
- Core EYWA client functionality

[0.2.0]: https://github.com/neyho/eywa/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/neyho/eywa/releases/tag/v0.1.1
[0.1.0]: https://github.com/neyho/eywa/releases/tag/v0.1.0