# CHANGELOG

## [2.0.0] - 2024-11-08 - MODERNIZATION RELEASE

### 🚀 BREAKING CHANGES
- **Complete API Modernization** - File operations now use single map arguments that mirror GraphQL schema
- **Client UUID Management** - UUIDs are now client-controlled for deterministic operations
- **Modern GraphQL Patterns** - Replaced broken WHERE clause filtering with relationship filtering

### ✨ NEW FEATURES
- **Complete Folder Operations** - Full folder hierarchy support (create, list, delete, info)
- **Streaming Operations** - Memory-efficient uploads/downloads with progress tracking
- **Modern Upload API** - `upload()`, `upload_stream()`, `upload_content()` with single map arguments
- **Modern Download API** - `download()`, `download_stream()` with progress callbacks
- **Modern List API** - `list()`, `list_folders()` with relationship filtering
- **Client UUID Control** - Pre-generate UUIDs for deterministic file/folder management
- **Progress Callbacks** - Real-time upload/download progress tracking
- **Typed Exceptions** - `FileUploadError`, `FileDownloadError` with detailed error context
- **Constants** - `ROOT_UUID`, `ROOT_FOLDER` for consistent root folder references

### 📋 SPECIFICATION COMPLIANCE
- **FILES_SPEC.md Compliant** - Implements all 12 core functions per specification
- **Babashka Parity** - Matches patterns and functionality of reference implementation
- **GraphQL Aligned** - API arguments directly mirror GraphQL schema structure

### 🔧 IMPLEMENTATION DETAILS
- **S3 Compatibility** - Proper Content-Length headers, no chunked transfer encoding
- **Memory Efficiency** - Streaming operations for large files
- **Error Recovery** - Comprehensive error handling with actionable messages
- **Modern Python** - Async/await patterns throughout

### 📦 API MIGRATION GUIDE

#### Old API (v1.x) - DEPRECATED
```python
# Multiple keyword arguments - OLD
file_info = await upload_file(
    filepath="test.txt",
    name="custom.txt", 
    folder_uuid="123-456",
    progress_callback=callback
)
```

#### New API (v2.0) - CURRENT
```python
# Single map argument - NEW
import uuid

await upload("test.txt", {
    "name": "custom.txt",
    "euuid": str(uuid.uuid4()),  # Client controls UUID
    "folder": {"euuid": "123-456"},
    "progress_fn": callback
})
```

### 🗂️ NEW FUNCTIONS ADDED
- `upload()` - Modern file upload with streaming
- `upload_stream()` - Upload from async iterators
- `upload_content()` - Upload content from memory
- `download_stream()` - Memory-efficient streaming download
- `download()` - Download to file or memory with progress
- `file_info()` - Get detailed file information
- `list()` - List files with modern filtering (aliased as `list_files`)
- `delete_file()` - Delete files by UUID
- `create_folder()` - Create folders with client UUIDs
- `list_folders()` - List folders with parent filtering
- `get_folder_info()` - Get folder info by UUID or path
- `delete_folder()` - Delete empty folders

### 🔄 BACKWARDS COMPATIBILITY
- Legacy function names are aliased for backwards compatibility
- `upload_file` → `upload`
- `download_file` → `download`
- `get_file_info` → `file_info`
- `list_files` → `list`

## [0.3.1] - 2025-07-20

### Fixed
- **Windows Compatibility**: Fixed critical Windows STDIO pipe handling issues
  - Resolved `_ProactorReadPipeTransport._loop_reading()` exceptions
  - Fixed `[WinError 6] The handle is invalid` errors  
  - Fixed `AttributeError: '_ProactorReadPipeTransport' object has no attribute '_empty_waiter'`
  - Added automatic platform detection for cross-platform compatibility
  - Implemented Windows-specific STDIN reader using ThreadPoolExecutor
  - Added fallback mechanisms for pipe connection failures
  - Improved error handling and cleanup procedures

### Added
- **Cross-Platform Support**: Automatic detection of Windows vs Unix systems
- **Windows Event Loop Handling**: Proper event loop policy management for Windows
- **Enhanced Error Handling**: Better error messages and graceful degradation
- **Compatibility Testing**: New Windows compatibility test script
- **Documentation**: Comprehensive Windows troubleshooting guide

### Changed
- **STDIN Reading**: Replaced problematic `connect_read_pipe` with thread-based approach on Windows
- **Buffer Management**: Increased default buffer sizes for better performance
- **Timeout Handling**: Improved timeout management across platforms
- **Logging**: Enhanced debug logging for troubleshooting

### Technical Details
- Windows now uses `ThreadPoolExecutor` for non-blocking STDIN reading
- Unix systems continue to use the original `StreamReader` approach
- Automatic fallback to thread-based reader if pipe connection fails
- Proper cleanup of resources on shutdown
- Better handling of JSON parsing errors

## [0.3.0] - Previous Release
- Initial stable release with core functionality
- GraphQL support
- Task management
- File operations
- Cross-platform base implementation

---

**Migration Note**: This release maintains full backward compatibility. Existing robots will work without modification while gaining Windows stability improvements.