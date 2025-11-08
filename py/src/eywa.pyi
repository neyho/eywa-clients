"""
Type hints for EYWA Python client library v2.0.
Provides async JSON-RPC communication, GraphQL queries, task management, 
and comprehensive file operations for EYWA processes.

Version 2.0 includes modernized file operations with:
- Single map arguments matching GraphQL schema
- Client-controlled UUID management
- Complete folder operations support
- Streaming upload/download capabilities
"""

from typing import Any, Dict, Optional, List, Callable, Union, Awaitable, AsyncIterator
from datetime import datetime, date
from asyncio import Future
from pathlib import Path

# Task status constants
SUCCESS: str
ERROR: str
PROCESSING: str
EXCEPTION: str

# Log event types
LogEvent = Union[
    "INFO", "WARN", "ERROR", "TRACE", "DEBUG", "EXCEPTION"
]

# Task status types  
TaskStatus = Union[
    "SUCCESS", "ERROR", "PROCESSING", "EXCEPTION"
]

class JSONRPCException(Exception):
    """Exception raised when JSON-RPC returns an error."""
    data: Dict[str, Any]
    
    def __init__(self, data: Dict[str, Any]) -> None: ...

class Sheet:
    """Represents a sheet with rows and columns for structured data."""
    name: str
    rows: List[Dict[str, Any]]
    columns: List[str]
    
    def __init__(self, name: str = 'Sheet') -> None: ...
    def add_row(self, row: Dict[str, Any]) -> None: ...
    def remove_row(self, row: Dict[str, Any]) -> None: ...
    def set_columns(self, columns: List[str]) -> None: ...
    def toJSON(self) -> str: ...

class Table:
    """Represents a table containing multiple sheets."""
    name: str
    sheets: List[Sheet]
    
    def __init__(self, name: str = 'Table') -> None: ...
    def add_sheet(self, sheet: Sheet) -> None: ...
    def remove_sheet(self, idx: int = 0) -> None: ...
    def toJSON(self) -> str: ...

class TaskReport:
    """Represents a task report with message, data, and optional image."""
    message: str
    data: Optional[Any]
    image: Optional[Any]
    
    def __init__(self, message: str, data: Optional[Any] = None, image: Optional[Any] = None) -> None: ...

async def send_request(data: Dict[str, Any]) -> Any:
    """
    Send a JSON-RPC request and wait for response.
    
    Args:
        data: Dictionary containing 'method' and optional 'params'
        
    Returns:
        The result from the JSON-RPC response
        
    Raises:
        JSONRPCException: If the response contains an error
        
    Example:
        >>> result = await send_request({
        ...     'method': 'custom.method',
        ...     'params': {'foo': 'bar'}
        ... })
    """
    ...

def send_notification(data: Dict[str, Any]) -> None:
    """
    Send a JSON-RPC notification (no response expected).
    
    Args:
        data: Dictionary containing 'method' and optional 'params'
        
    Example:
        >>> send_notification({
        ...     'method': 'task.log',
        ...     'params': {'message': 'Processing started'}
        ... })
    """
    ...

def register_handler(method: str, func: Callable[[Dict[str, Any]], None]) -> None:
    """
    Register a handler for incoming JSON-RPC method calls.
    
    Args:
        method: The method name to handle
        func: Function to handle the incoming request
        
    Example:
        >>> def my_handler(data):
        ...     print('Received:', data['params'])
        >>> register_handler('custom.action', my_handler)
    """
    ...

def open_pipe() -> None:
    """
    Initialize stdin/stdout communication with EYWA runtime.
    Must be called before using any other EYWA functions.
    
    Example:
        >>> open_pipe()
        >>> # Now you can use other EYWA functions
    """
    ...

async def get_task() -> Dict[str, Any]:
    """
    Get current task information.
    
    Returns:
        Dictionary containing task data with fields like:
        - euuid: Task UUID
        - message: Task message
        - status: Current status
        - input: Task input data
        - data: Additional task data
        
    Example:
        >>> task = await get_task()
        >>> print('Current task:', task['message'])
    """
    ...

def log(
    event: str = "INFO",
    message: str = "",
    data: Optional[Any] = None,
    duration: Optional[float] = None,
    coordinates: Optional[Any] = None,
    time: Optional[datetime] = None
) -> None:
    """
    Log a message with full control over all parameters.
    
    Args:
        event: Log level (INFO, WARN, ERROR, DEBUG, TRACE, EXCEPTION)
        message: The message to log
        data: Optional structured data to include
        duration: Optional duration in milliseconds
        coordinates: Optional coordinate data
        time: Optional timestamp (defaults to now)
        
    Example:
        >>> log(
        ...     event='INFO',
        ...     message='Processing item',
        ...     data={'itemId': 123},
        ...     duration=1500
        ... )
    """
    ...

def info(message: str, data: Optional[Any] = None) -> None:
    """
    Log an info message.
    
    Args:
        message: The message to log
        data: Optional structured data to include
        
    Example:
        >>> info('User logged in', {'userId': 'abc123'})
    """
    ...

def error(message: str, data: Optional[Any] = None) -> None:
    """
    Log an error message.
    
    Args:
        message: The error message to log
        data: Optional error details or context
        
    Example:
        >>> error('Failed to process file', {'filename': 'data.csv', 'error': str(e)})
    """
    ...

def warn(message: str, data: Optional[Any] = None) -> None:
    """
    Log a warning message.
    
    Args:
        message: The warning message to log
        data: Optional warning context
        
    Example:
        >>> warn('API rate limit approaching', {'remaining': 10})
    """
    ...

def debug(message: str, data: Optional[Any] = None) -> None:
    """
    Log a debug message.
    
    Args:
        message: The debug message to log
        data: Optional debug data
        
    Example:
        >>> debug('Cache hit', {'key': 'user:123'})
    """
    ...

def trace(message: str, data: Optional[Any] = None) -> None:
    """
    Log a trace message (most verbose level).
    
    Args:
        message: The trace message to log
        data: Optional trace data
        
    Example:
        >>> trace('Entering function processData', {'args': [1, 2, 3]})
    """
    ...

def exception(message: str, data: Optional[Any] = None) -> None:
    """
    Log an exception message.
    
    Args:
        message: The exception message to log
        data: Optional exception details
        
    Example:
        >>> exception('Unhandled error in worker', {'stack': traceback.format_exc()})
    """
    ...

def report(message: str, data: Optional[Any] = None, image: Optional[Any] = None) -> None:
    """
    Send a task report with optional data and image.
    
    Args:
        message: The report message
        data: Optional structured data for the report
        image: Optional image data (base64 or URL)
        
    Example:
        >>> report('Analysis complete', {'accuracy': 0.95}, chart_image_base64)
    """
    ...

def update_task(status: TaskStatus = "PROCESSING") -> None:
    """
    Update the current task status.
    
    Args:
        status: The new status (defaults to PROCESSING)
        
    Example:
        >>> update_task("PROCESSING")
        >>> # Do some work...
        >>> update_task("SUCCESS")
    """
    ...

def return_task() -> None:
    """
    Return control to EYWA without closing the task.
    Exits the process with code 0.
    
    Example:
        >>> # Hand back control to EYWA
        >>> return_task()
    """
    ...

def close_task(status: TaskStatus = "SUCCESS") -> None:
    """
    Close the current task with a final status.
    Exits the process with code 0 for SUCCESS, 1 for other statuses.
    
    Args:
        status: The final task status (defaults to SUCCESS)
        
    Example:
        >>> try:
        ...     # Do work...
        ...     close_task("SUCCESS")
        ... except Exception as e:
        ...     error('Task failed', {'error': str(e)})
        ...     close_task("ERROR")
    """
    ...

async def graphql(query: str, variables: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    """
    Execute a GraphQL query against the EYWA server.
    
    Args:
        query: The GraphQL query string
        variables: Optional variables for the query
        
    Returns:
        Dictionary containing the query result with 'data' and/or 'errors'
        
    Example:
        >>> result = await graphql('''
        ...     query GetUser($id: UUID!) {
        ...         getUser(euuid: $id) {
        ...             name
        ...             email
        ...         }
        ...     }
        ... ''', {'id': 'user-uuid'})
        >>> user = result['data']['getUser']
    """
    ...

def exit(status: int = 0) -> None:
    """
    Exit the process with proper cleanup.
    
    Args:
        status: Exit code (0 for success, non-zero for error)
        
    Example:
        >>> exit(0)  # Success
        >>> exit(1)  # Error
    """
    ...

# ==============================================================================
# File Operations v2.0 - Modernized API
# ==============================================================================

# Constants
ROOT_UUID: str
ROOT_FOLDER: Dict[str, str]

# Exception Types
class FileUploadError(Exception):
    """Raised when file upload operations fail."""
    type: str
    code: Optional[int]
    
    def __init__(self, message: str, type: str = "upload-error", code: Optional[int] = None) -> None: ...

class FileDownloadError(Exception):
    """Raised when file download operations fail."""
    type: str
    code: Optional[int]
    
    def __init__(self, message: str, type: str = "download-error", code: Optional[int] = None) -> None: ...

# Core Upload Operations
async def upload(filepath: Union[str, Path], file_data: Dict[str, Any]) -> None:
    """
    Upload a file to EYWA using modern single-map argument API.
    
    Args:
        filepath: Path to file to upload
        file_data: File metadata dict:
            name: str (optional, defaults to filename)
            euuid: str (optional, client-generated UUID)
            folder: dict (optional, {"euuid": "..."} or {"path": "..."})
            content_type: str (optional, auto-detected)
            size: int (optional, auto-calculated)
            progress_fn: callable (optional, progress callback)
    
    Returns:
        None on success
        
    Raises:
        FileUploadError: If upload fails
        
    Example:
        >>> import uuid
        >>> await upload("test.txt", {
        ...     "name": "test.txt",
        ...     "euuid": str(uuid.uuid4()),
        ...     "folder": {"euuid": folder_uuid}
        ... })
    """
    ...

async def upload_stream(input_stream: AsyncIterator[bytes], file_data: Dict[str, Any]) -> None:
    """
    Upload from an async stream.
    
    Args:
        input_stream: Async iterator of bytes
        file_data: File metadata with required 'name' and 'size' fields
        
    Raises:
        FileUploadError: If upload fails
    """
    ...

async def upload_content(content: Union[str, bytes], file_data: Dict[str, Any]) -> None:
    """
    Upload content directly from memory.
    
    Args:
        content: String or bytes to upload
        file_data: File metadata with required 'name' field
        
    Raises:
        FileUploadError: If upload fails
        
    Example:
        >>> await upload_content("Hello World", {
        ...     "name": "greeting.txt",
        ...     "content_type": "text/plain"
        ... })
    """
    ...

# Core Download Operations
async def download_stream(file_uuid: str) -> Dict[str, Any]:
    """
    Download file as stream for memory efficiency.
    
    Args:
        file_uuid: UUID of file to download
        
    Returns:
        Dict with 'stream' and 'content_length' keys
        
    Raises:
        FileDownloadError: If download fails
        
    Example:
        >>> result = await download_stream(file_uuid)
        >>> async for chunk in result["stream"]:
        ...     process_chunk(chunk)
    """
    ...

async def download(file_uuid: str, save_path: Optional[Union[str, Path]] = None, 
                  progress_fn: Optional[Callable[[int, int], None]] = None) -> Union[str, bytes]:
    """
    Download file to memory or disk.
    
    Args:
        file_uuid: UUID of file to download
        save_path: Optional path to save file
        progress_fn: Optional progress callback
        
    Returns:
        If save_path: path to saved file
        If no save_path: file content as bytes
        
    Raises:
        FileDownloadError: If download fails
    """
    ...

# File Management Operations
async def file_info(file_uuid: str) -> Optional[Dict[str, Any]]:
    """
    Get detailed file information.
    
    Args:
        file_uuid: UUID of file
        
    Returns:
        File info dict or None if not found
        
    Example:
        >>> info = await file_info(file_uuid)
        >>> print(f"File: {info['name']} ({info['size']} bytes)")
    """
    ...

async def list(filters: Dict[str, Any]) -> List[Dict[str, Any]]:
    """
    List files with modern GraphQL filtering.
    
    Args:
        filters: Filter criteria:
            limit: int (optional)
            status: str (optional)
            name: str (optional, SQL LIKE pattern)
            folder: dict (optional, {"euuid": "..."} or {"path": "..."})
            
    Returns:
        List of file dicts
        
    Example:
        >>> files = await list({
        ...     "folder": {"euuid": folder_uuid},
        ...     "limit": 10
        ... })
    """
    ...

async def delete_file(file_uuid: str) -> bool:
    """
    Delete a file.
    
    Args:
        file_uuid: UUID of file to delete
        
    Returns:
        True if deletion successful
    """
    ...

# Folder Operations (NEW in v2.0)
async def create_folder(folder_data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Create a new folder.
    
    Args:
        folder_data: Folder definition:
            name: str (required)
            euuid: str (optional, client-generated)
            parent: dict (optional, {"euuid": "..."} for parent)
            
    Returns:
        Created folder info dict
        
    Example:
        >>> folder = await create_folder({
        ...     "name": "reports",
        ...     "euuid": str(uuid.uuid4()),
        ...     "parent": {"euuid": ROOT_UUID}
        ... })
    """
    ...

async def list_folders(filters: Dict[str, Any]) -> List[Dict[str, Any]]:
    """
    List folders with parent filtering.
    
    Args:
        filters: Filter criteria:
            limit: int (optional)
            name: str (optional)
            parent: dict|None (optional, {"euuid": "..."}, {"path": "..."}, or None for root)
            
    Returns:
        List of folder dicts
    """
    ...

async def get_folder_info(data: Dict[str, str]) -> Optional[Dict[str, Any]]:
    """
    Get folder information by UUID or path.
    
    Args:
        data: {"euuid": "..."} or {"path": "..."}
        
    Returns:
        Folder info dict or None if not found
    """
    ...

async def delete_folder(folder_uuid: str) -> bool:
    """
    Delete an empty folder.
    
    Args:
        folder_uuid: UUID of folder to delete
        
    Returns:
        True if deletion successful
    """
    ...

# Utility Functions
def calculate_file_hash(filepath: Union[str, Path], algorithm: str = 'sha256') -> str:
    """
    Calculate hash of a file.
    
    Args:
        filepath: Path to file
        algorithm: Hash algorithm (md5, sha1, sha256, etc.)
        
    Returns:
        Hex digest of file hash
    """
    ...

# Convenience Functions
async def quick_upload(filepath: Union[str, Path]) -> str:
    """Quick upload with minimal parameters. Returns file UUID."""
    ...

async def quick_download(file_uuid: str, filename: Optional[str] = None) -> str:
    """Quick download to current directory. Returns saved file path."""
    ...

# Legacy Aliases (DEPRECATED - use new function names)
upload_file = upload
download_file = download 
get_file_info = file_info
list_files = list

# Internal functions (not typically used directly)
def handle_data(data: Dict[str, Any]) -> None: ...
def handle_request(data: Dict[str, Any]) -> None: ...
def handle_result(id_: str, result: Any) -> None: ...
def handle_error(id_: str, error: Dict[str, Any]) -> None: ...
def custom_serializer(obj: Any) -> Any: ...
async def read_stdin() -> None: ...