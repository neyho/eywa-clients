# CHANGELOG

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
