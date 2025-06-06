# Changelog

## [0.3.0] - 2024-06-06
### Added
- Initial public release of EYWA Ruby client
- JSON-RPC communication protocol support
- GraphQL query execution capabilities
- Task management and lifecycle handling
- Asynchronous request/response handling
- Comprehensive logging methods (info, warn, error, debug, trace)
- Handler registration for custom method handling
- Examples for feature demonstration and simple robot usage

### Features
- `open_pipe()` - Initialize stdin/stdout communication
- `send_request()` - Send JSON-RPC requests with response handling
- `send_notification()` - Fire-and-forget notifications
- `graphql()` - Execute GraphQL queries against EYWA server
- `register_handler()` - Register custom handlers for incoming methods
- Task status updates and completion
