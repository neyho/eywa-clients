# Changelog

All notable changes to the EYWA C# Client will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-11-09

### Added
- **Core JSON-RPC Communication**
  - Task management (`task.get`, `task.update`, `task.close`, `task.return`)
  - Structured logging (`task.log`, `task.report`)
  - Clean `Eywa` class with `new Eywa()` initialization
  - `Status` enum for task states (Success, Error, Processing, Exception)

- **Dynamic GraphQL Operations**
  - `GraphQLAsync()` method with `Dictionary<string, object>` parameters
  - Full GraphQL schema support for mutations and queries
  - Proper JSON-RPC to GraphQL bridge
  - JsonElement to native .NET type conversion utilities

- **File Management (S3 Integration)**
  - Upload operations: `UploadAsync()`, `UploadStreamAsync()`, `UploadContentAsync()`
  - Download operations: `DownloadAsync()`, `DownloadStreamAsync()`
  - Folder management: `CreateFolderAsync()`, `DeleteFolderAsync()`
  - File deletion: `DeleteFileAsync()`
  - Three-step S3 protocol: Request URL → Upload → Confirm

- **Examples and Documentation**
  - `SimpleExample` - Basic JSON-RPC operations
  - `GraphQLExample` - User CRUD with GraphQL
  - `FilesExample` - Complete file lifecycle demo
  - Clean console output with emojis and status indicators

- **Multi-Framework Support**
  - .NET 6.0, 8.0, and 9.0 compatibility
  - Framework-specific dependency versions
  - Cross-platform compatibility

- **NuGet Package Configuration**
  - Complete package metadata
  - Multi-framework targeting
  - Documentation generation
  - Symbol packages for debugging

### Technical Details
- **Architecture**: Dictionary-based dynamic approach staying close to GraphQL
- **Dependencies**: System.Text.Json with framework-specific versions
- **Error Handling**: Comprehensive exception handling with GraphQLException
- **JSON Parsing**: Custom JsonElement conversion utilities for proper type handling

### Known Limitations
- File listing operations not yet implemented
- No query builder utilities (by design - uses raw GraphQL)
- Requires EYWA server for functionality

### Breaking Changes
- None (initial release)

---

## [Unreleased]

### Planned
- File listing operations (`ListAsync()`, `ListFoldersAsync()`)
- GraphQL query builder utilities (optional)
- Async enumerable support for large result sets
- Connection pooling and retry policies
- Performance optimizations

---

**Legend:**
- `Added` for new features
- `Changed` for changes in existing functionality  
- `Deprecated` for soon-to-be removed features
- `Removed` for removed features
- `Fixed` for bug fixes
- `Security` for vulnerability fixes
