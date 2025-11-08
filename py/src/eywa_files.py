"""
EYWA File Operations - Modernized Python Client

This module provides comprehensive file upload/download capabilities
for the EYWA Python client, following the FILES_SPEC.md requirements
and matching the Babashka client patterns:

- Single map arguments that mirror GraphQL schema
- Client-controlled UUID management
- Modern GraphQL relationship filtering
- Complete folder operations support
- Streaming uploads/downloads with progress tracking

Key Features:
- 3-step upload protocol (request URL → S3 upload → confirm)
- Memory-efficient streaming for large files
- Comprehensive error handling with typed exceptions
- Full folder hierarchy support
- Progress callbacks for all operations

Version: 2.0.0 (Modernized)
"""

import asyncio
import aiohttp
import mimetypes
import hashlib
import os
import ssl
from pathlib import Path
from typing import Optional, Union, Dict, Any, List, Callable, AsyncIterator
# Note: eywa module imported dynamically to avoid circular dependency

# Disable SSL verification for development/testing
# TODO: Remove this for production use
ssl_context = ssl.create_default_context()
ssl_context.check_hostname = False
ssl_context.verify_mode = ssl.CERT_NONE

async def _graphql(query, variables=None):
    """GraphQL call - imports eywa module to avoid circular dependency"""
    try:
        import eywa
        return await eywa.graphql(query, variables)
    except Exception as e:
        raise Exception(f"Could not call eywa.graphql: {e}. Make sure eywa module is imported and eywa.open_pipe() has been called.")

# ============================================================================
# Constants and Exception Types (Per FILES_SPEC.md)
# ============================================================================

# Root folder constants (matching Babashka client)
ROOT_UUID = "87ce50d8-5dfa-4008-a265-053e727ab793"
ROOT_FOLDER = {"euuid": ROOT_UUID}


class FileUploadError(Exception):
    """Raised when file upload fails"""
    
    def __init__(self, message: str, type: str = "upload-error", code: Optional[int] = None):
        super().__init__(message)
        self.type = type
        self.code = code


class FileDownloadError(Exception):
    """Raised when file download fails"""
    
    def __init__(self, message: str, type: str = "download-error", code: Optional[int] = None):
        super().__init__(message)
        self.type = type
        self.code = code


# ============================================================================
# Utility Functions
# ============================================================================

def _detect_mime_type(filename: str) -> str:
    """Detect MIME type from file extension"""
    detected = mimetypes.guess_type(filename)[0]
    if detected:
        return detected
    
    # Fallback detection for common types
    ext = filename.split('.')[-1].lower() if '.' in filename else ''
    
    mime_map = {
        'txt': 'text/plain',
        'html': 'text/html', 
        'css': 'text/css',
        'js': 'application/javascript',
        'json': 'application/json',
        'xml': 'application/xml',
        'pdf': 'application/pdf',
        'png': 'image/png',
        'jpg': 'image/jpeg',
        'jpeg': 'image/jpeg',
        'gif': 'image/gif',
        'svg': 'image/svg+xml',
        'zip': 'application/zip',
        'csv': 'text/csv',
        'doc': 'application/msword',
        'docx': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        'xls': 'application/vnd.ms-excel',
        'xlsx': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    }
    
    return mime_map.get(ext, 'application/octet-stream')


async def _http_put_content(url: str, data: bytes, content_type: str, progress_fn: Optional[Callable] = None) -> Dict[str, Any]:
    """
    HTTP PUT for content upload with proper Content-Length header.
    S3 requires Content-Length and rejects Transfer-Encoding: chunked.
    """
    try:
        content_length = len(data)
        
        if progress_fn:
            progress_fn(0, content_length)
        
        async with aiohttp.ClientSession(connector=aiohttp.TCPConnector(ssl=ssl_context)) as session:
            async with session.put(
                url,
                data=data,
                headers={
                    'Content-Type': content_type,
                    'Content-Length': str(content_length)
                }
            ) as response:
                
                if progress_fn:
                    progress_fn(content_length, content_length)
                
                if response.status == 200:
                    return {"status": "success", "code": response.status}
                else:
                    error_text = await response.text()
                    return {"status": "error", "code": response.status, "message": error_text}
                    
    except Exception as e:
        return {"status": "error", "code": 0, "message": str(e)}


async def _http_put_stream(url: str, input_stream: AsyncIterator[bytes], content_length: int, 
                          content_type: str, progress_fn: Optional[Callable] = None) -> Dict[str, Any]:
    """
    HTTP PUT from stream with progress tracking.
    
    IMPORTANT: Reads entire stream into memory first to avoid chunked transfer encoding.
    S3 requires Content-Length and rejects Transfer-Encoding: chunked.
    """
    try:
        if progress_fn:
            progress_fn(0, content_length)
        
        # Read entire stream into memory to avoid chunked encoding
        chunks = []
        bytes_read = 0
        
        async for chunk in input_stream:
            chunks.append(chunk)
            bytes_read += len(chunk)
            if progress_fn:
                progress_fn(bytes_read, content_length)
        
        data = b''.join(chunks)
        
        # Upload complete buffer
        async with aiohttp.ClientSession(connector=aiohttp.TCPConnector(ssl=ssl_context)) as session:
            async with session.put(
                url,
                data=data,
                headers={
                    'Content-Type': content_type,
                    'Content-Length': str(len(data))
                }
            ) as response:
                
                if response.status == 200:
                    return {"status": "success", "code": response.status}
                else:
                    error_text = await response.text()
                    return {"status": "error", "code": response.status, "message": error_text}
                    
    except Exception as e:
        return {"status": "error", "code": 0, "message": str(e)}


async def _file_to_async_chunks(file_path: Union[str, Path], chunk_size: int = 8192) -> AsyncIterator[bytes]:
    """Convert file to async chunk iterator"""
    with open(file_path, 'rb') as f:
        while True:
            chunk = f.read(chunk_size)
            if not chunk:
                break
            yield chunk


async def _download_to_bytes(url: str) -> Dict[str, Any]:
    """
    Simple HTTP GET that downloads entire content to bytes.
    This is simpler and more reliable than streaming.
    """
    try:
        async with aiohttp.ClientSession(connector=aiohttp.TCPConnector(ssl=ssl_context)) as session:
            async with session.get(url) as response:
                if response.status == 200:
                    content = await response.read()
                    return {
                        "status": "success",
                        "content": content,
                        "content_length": len(content)
                    }
                else:
                    error_text = await response.text() if response.content_length else "Unknown error"
                    return {
                        "status": "error", 
                        "code": response.status,
                        "message": error_text
                    }
                    
    except Exception as e:
        return {"status": "error", "code": 0, "message": str(e)}


# ============================================================================
# Core Upload Operations (Following Babashka Pattern)
# ============================================================================

async def upload(filepath: Union[str, Path], file_data: Dict[str, Any]) -> None:
    """
    Upload a file to EYWA file service.
    
    Args:
        filepath: Path to the file to upload (string or Path object)
        file_data: Dict matching GraphQL FileInput type:
            name: str (optional, defaults to filename)
            euuid: str (optional, client-generated UUID for deduplication)
            folder: dict (optional, {"euuid": "..."} or {"path": "..."})
            content_type: str (optional, auto-detected if not provided)
            size: int (optional, auto-calculated from file)
            progress_fn: callable (optional, not sent to GraphQL)
    
    Returns:
        None on success
        
    Raises:
        FileUploadError: If upload fails at any stage
        
    Examples:
        # Upload new file to root
        await upload("test.txt", {"name": "test.txt"})
        
        # Upload to folder with client UUID
        await upload("test.txt", {
            "name": "test.txt",
            "euuid": "client-generated-uuid",
            "folder": {"euuid": "folder-uuid"}
        })
        
        # Upload with progress tracking
        def progress(current, total):
            print(f"Progress: {current}/{total} ({current/total*100:.1f}%)")
            
        await upload("test.txt", {
            "name": "test.txt", 
            "progress_fn": progress
        })
    """
    try:
        progress_fn = file_data.get('progress_fn')
        
        # Handle file input
        file_path = Path(filepath)
        if not file_path.exists():
            raise FileUploadError(f"File not found: {filepath}")
        if not file_path.is_file():
            raise FileUploadError(f"Path is not a file: {filepath}")
        
        file_size = file_path.stat().st_size
        file_name = file_data.get('name') or file_path.name
        detected_content_type = file_data.get('content_type') or _detect_mime_type(file_name)
        
        # Step 1: Request upload URL
        upload_query = """
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
        """
        
        # Build GraphQL input - use file_data directly, fill in computed values
        file_input = {
            **file_data,
            'name': file_name,
            'content_type': detected_content_type,
            'size': file_size
        }
        # Remove non-GraphQL field
        file_input.pop('progress_fn', None)
        
        result = await _graphql(upload_query, {"file": file_input})
        
        if result.get('error'):
            raise FileUploadError(f"Failed to get upload URL: {result['error']}")
        
        upload_url = result.get('data', {}).get('requestUploadURL')
        if not upload_url:
            raise FileUploadError("No upload URL in response")
        
        # Step 2: Stream file to S3
        upload_result = await _http_put_stream(
            upload_url,
            _file_to_async_chunks(file_path),
            file_size,
            detected_content_type,
            progress_fn
        )
        
        if upload_result['status'] == 'error':
            raise FileUploadError(
                f"S3 upload failed ({upload_result['code']}): {upload_result.get('message', 'Unknown error')}",
                code=upload_result['code']
            )
        
        # Step 3: Confirm upload
        confirm_query = """
        mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }
        """
        
        confirm_result = await _graphql(confirm_query, {"url": upload_url})
        
        if confirm_result.get('error'):
            raise FileUploadError(f"Upload confirmation failed: {confirm_result['error']}")
        
        confirmed = confirm_result.get('data', {}).get('confirmFileUpload')
        if not confirmed:
            raise FileUploadError("Upload confirmation returned false")
        
        return None
        
    except FileUploadError:
        raise
    except Exception as e:
        raise FileUploadError(f"Upload failed: {str(e)}") from e


async def upload_stream(input_stream: AsyncIterator[bytes], file_data: Dict[str, Any]) -> None:
    """
    Upload data from an async stream to EYWA file service.
    
    Args:
        input_stream: AsyncIterator[bytes] - Stream of file data
        file_data: Dict matching GraphQL FileInput type:
            name: str (required)
            size: int (required for streams)
            euuid: str (optional, client-generated UUID)
            folder: dict (optional, {"euuid": "..."} or {"path": "..."})
            content_type: str (optional, default: application/octet-stream)
            progress_fn: callable (optional, not sent to GraphQL)
    
    Returns:
        None on success
        
    Raises:
        FileUploadError: If upload fails at any stage
        
    Examples:
        # Upload from async iterator
        async def data_generator():
            for i in range(100):
                yield f"Line {i}\n".encode()
        
        await upload_stream(data_generator(), {
            "name": "generated.txt",
            "size": 800,  # Must calculate size beforehand
            "content_type": "text/plain"
        })
    """
    try:
        if not file_data.get('name'):
            raise FileUploadError("name is required for stream uploads")
        if not file_data.get('size'):
            raise FileUploadError("size is required for stream uploads")
            
        content_type = file_data.get('content_type', 'application/octet-stream')
        content_length = file_data['size']
        progress_fn = file_data.get('progress_fn')
        
        # Step 1: Request upload URL
        upload_query = """
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
        """
        
        # Build GraphQL input
        file_input = {
            **file_data,
            'content_type': content_type
        }
        file_input.pop('progress_fn', None)
        
        result = await _graphql(upload_query, {"file": file_input})
        
        if result.get('error'):
            raise FileUploadError(f"Failed to get upload URL: {result['error']}")
        
        upload_url = result.get('data', {}).get('requestUploadURL')
        if not upload_url:
            raise FileUploadError("No upload URL in response")
        
        # Step 2: Stream to S3
        upload_result = await _http_put_stream(
            upload_url,
            input_stream,
            content_length,
            content_type,
            progress_fn
        )
        
        if upload_result['status'] == 'error':
            raise FileUploadError(
                f"S3 upload failed ({upload_result['code']}): {upload_result.get('message', 'Unknown error')}",
                code=upload_result['code']
            )
        
        # Step 3: Confirm upload
        confirm_query = """
        mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }
        """
        
        confirm_result = await _graphql(confirm_query, {"url": upload_url})
        
        if confirm_result.get('error'):
            raise FileUploadError(f"Upload confirmation failed: {confirm_result['error']}")
        
        confirmed = confirm_result.get('data', {}).get('confirmFileUpload')
        if not confirmed:
            raise FileUploadError("Upload confirmation returned false")
        
        return None
        
    except FileUploadError:
        raise
    except Exception as e:
        raise FileUploadError(f"Stream upload failed: {str(e)}") from e


async def upload_content(content: Union[str, bytes], file_data: Dict[str, Any]) -> None:
    """
    Upload content directly from memory.
    
    Args:
        content: String or bytes content to upload
        file_data: Dict matching GraphQL FileInput type:
            name: str (required)
            euuid: str (optional, client-generated UUID)
            folder: dict (optional, {"euuid": "..."} or {"path": "..."})
            content_type: str (optional, default: text/plain for strings, application/octet-stream for bytes)
            size: int (optional, auto-calculated from content)
            progress_fn: callable (optional, not sent to GraphQL)
    
    Returns:
        None on success
        
    Raises:
        FileUploadError: If upload fails at any stage
        
    Examples:
        # Upload string content
        await upload_content("Hello World!", {
            "name": "greeting.txt",
            "content_type": "text/plain"
        })
        
        # Upload JSON data
        import json
        data = {"key": "value"}
        await upload_content(json.dumps(data), {
            "name": "data.json",
            "content_type": "application/json",
            "folder": {"euuid": "folder-uuid"}
        })
    """
    try:
        if not file_data.get('name'):
            raise FileUploadError("name is required for content uploads")
        
        # Convert content to bytes
        if isinstance(content, str):
            content_bytes = content.encode('utf-8')
            default_content_type = 'text/plain'
        else:
            content_bytes = content
            default_content_type = 'application/octet-stream'
        
        file_size = len(content_bytes)
        content_type = file_data.get('content_type', default_content_type)
        progress_fn = file_data.get('progress_fn')
        
        # Step 1: Request upload URL
        upload_query = """
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
        """
        
        # Build GraphQL input
        file_input = {
            **file_data,
            'content_type': content_type,
            'size': file_size
        }
        file_input.pop('progress_fn', None)
        
        result = await _graphql(upload_query, {"file": file_input})
        
        if result.get('error'):
            raise FileUploadError(f"Failed to get upload URL: {result['error']}")
        
        upload_url = result.get('data', {}).get('requestUploadURL')
        if not upload_url:
            raise FileUploadError("No upload URL in response")
        
        # Step 2: Upload content to S3
        upload_result = await _http_put_content(upload_url, content_bytes, content_type, progress_fn)
        
        if upload_result['status'] == 'error':
            raise FileUploadError(
                f"S3 upload failed ({upload_result['code']}): {upload_result.get('message', 'Unknown error')}",
                code=upload_result['code']
            )
        
        # Step 3: Confirm upload
        confirm_query = """
        mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }
        """
        
        confirm_result = await _graphql(confirm_query, {"url": upload_url})
        
        if confirm_result.get('error'):
            raise FileUploadError(f"Upload confirmation failed: {confirm_result['error']}")
        
        confirmed = confirm_result.get('data', {}).get('confirmFileUpload')
        if not confirmed:
            raise FileUploadError("Upload confirmation returned false")
        
        return None
        
    except FileUploadError:
        raise
    except Exception as e:
        raise FileUploadError(f"Content upload failed: {str(e)}") from e


# ============================================================================
# Core Download Operations
# ============================================================================

class _BytesStream:
    """Simple stream wrapper that chunks bytes for streaming interface"""
    
    def __init__(self, content: bytes, chunk_size: int = 8192):
        self.content = content
        self.chunk_size = chunk_size
        self.position = 0
        self.content_length = len(content)
    
    def __aiter__(self):
        return self
    
    async def __anext__(self):
        if self.position >= len(self.content):
            raise StopAsyncIteration
        
        end_pos = min(self.position + self.chunk_size, len(self.content))
        chunk = self.content[self.position:end_pos]
        self.position = end_pos
        return chunk


async def download_stream(file_uuid: str) -> Dict[str, Any]:
    """
    Download a file from EYWA and return a stream for memory-efficient processing.
    
    Note: Currently downloads entire file to memory then streams it.
    This ensures reliability while maintaining streaming interface.
    
    Args:
        file_uuid: UUID of the file to download
    
    Returns:
        Dict containing:
            stream: AsyncIterator[bytes] - Stream of file content in chunks
            content_length: int - Content length in bytes
            status: str - Always "success" if no exception
            
    Raises:
        FileDownloadError: If download fails
        
    Examples:
        # Download and process in chunks
        result = await download_stream(file_uuid)
        async for chunk in result["stream"]:
            process_chunk(chunk)
    """
    try:
        # Step 1: Request download URL
        download_query = """
        query RequestDownload($file: FileInput!) {
            requestDownloadURL(file: $file)
        }
        """
        
        result = await _graphql(download_query, {"file": {"euuid": file_uuid}})
        
        if result.get('error'):
            raise FileDownloadError(f"Failed to get download URL: {result['error']}")
        
        download_url = result.get('data', {}).get('requestDownloadURL')
        if not download_url:
            raise FileDownloadError("No download URL in response")
        
        # Step 2: Download content from S3
        download_result = await _download_to_bytes(download_url)
        
        if download_result['status'] != 'success':
            error_msg = download_result.get('message', f"HTTP {download_result.get('code', 'unknown')}")
            raise FileDownloadError(f"Download failed: {error_msg}", code=download_result.get('code'))
        
        # Create streaming wrapper
        content = download_result['content']
        stream = _BytesStream(content)
        
        return {
            "stream": stream,
            "content_length": len(content),
            "status": "success"
        }
        
    except FileDownloadError:
        raise
    except Exception as e:
        raise FileDownloadError(f"Stream download failed: {str(e)}") from e


async def download(file_uuid: str, save_path: Optional[Union[str, Path]] = None, 
                  progress_fn: Optional[Callable] = None) -> Union[str, bytes]:
    """
    Download a file from EYWA file service.
    
    Args:
        file_uuid: UUID of the file to download
        save_path: Path to save the file (if None, returns content as bytes)
        progress_fn: Function called with (bytes_downloaded, total_bytes)
    
    Returns:
        If save_path provided: path to saved file (str)
        If save_path is None: file content as bytes
        
    Raises:
        FileDownloadError: If download fails
        
    Examples:
        # Download to memory
        content = await download(file_uuid)
        
        # Download to file with progress
        def progress(current, total):
            print(f"Downloaded: {current}/{total} bytes")
            
        saved_path = await download(file_uuid, "local_file.txt", progress)
    """
    try:
        # Step 1: Request download URL
        download_query = """
        query RequestDownload($file: FileInput!) {
            requestDownloadURL(file: $file)
        }
        """
        
        result = await _graphql(download_query, {"file": {"euuid": file_uuid}})
        
        if result.get('error'):
            raise FileDownloadError(f"Failed to get download URL: {result['error']}")
        
        download_url = result.get('data', {}).get('requestDownloadURL')
        if not download_url:
            raise FileDownloadError("No download URL in response")
        
        # Step 2: Download content from S3
        download_result = await _download_to_bytes(download_url)
        
        if download_result['status'] != 'success':
            error_msg = download_result.get('message', f"HTTP {download_result.get('code', 'unknown')}")
            raise FileDownloadError(f"Download failed: {error_msg}", code=download_result.get('code'))
        
        content = download_result['content']
        content_length = len(content)
        
        # Progress tracking
        if progress_fn:
            progress_fn(0, content_length)
        
        if save_path:
            # Save to file
            save_path = Path(save_path)
            save_path.parent.mkdir(parents=True, exist_ok=True)
            
            try:
                with open(save_path, 'wb') as f:
                    f.write(content)
                
                # Final progress update
                if progress_fn:
                    progress_fn(content_length, content_length)
                    
                return str(save_path)
            except Exception as e:
                # Clean up partial file on error
                if save_path.exists():
                    save_path.unlink()
                raise
        else:
            # Return content as bytes
            if progress_fn:
                progress_fn(content_length, content_length)
                    
            return content
        
    except FileDownloadError:
        raise
    except Exception as e:
        raise FileDownloadError(f"Download failed: {str(e)}") from e


# ============================================================================
# File Management Operations
# ============================================================================

async def file_info(file_uuid: str) -> Optional[Dict[str, Any]]:
    """
    Get detailed information about a file.
    
    Args:
        file_uuid: UUID of the file
        
    Returns:
        File information dict if found, None if not found
        
    Raises:
        Exception: If GraphQL query fails (network error, etc.)
        
    Examples:
        info = await file_info(file_uuid)
        if info:
            print(f"File: {info['name']} ({info['size']} bytes)")
    """
    try:
        query = """
        query GetFile($uuid: UUID!) {
            getFile(euuid: $uuid) {
                euuid
                name
                status
                content_type
                size
                uploaded_at
                uploaded_by {
                    name
                }
                folder {
                    euuid
                    name
                    path
                }
            }
        }
        """
        
        result = await _graphql(query, {"uuid": file_uuid})
        
        if result.get('error'):
            raise Exception(f"Failed to get file info: {result['error']}")
        
        return result.get('data', {}).get('getFile')
        
    except Exception as e:
        raise e


async def list(filters: Dict[str, Any]) -> List[Dict[str, Any]]:
    """
    List files with modern GraphQL filtering support.
    
    Args:
        filters: Dict of filter criteria:
            limit: int (optional, max results)
            status: str (optional, filter by status like "UPLOADED")
            name: str (optional, SQL LIKE pattern)
            folder: dict (optional, {"euuid": "..."} or {"path": "..."})
    
    Returns:
        List of file dicts (empty if no matches)
        
    Raises:
        Exception: If GraphQL query fails
        
    Examples:
        # List all files
        files = await list({})
        
        # List files by folder UUID
        files = await list({"folder": {"euuid": folder_uuid}})
        
        # List files by folder path with name filter
        files = await list({
            "folder": {"path": "/documents/"},
            "name": "report",
            "limit": 10
        })
    """
    try:
        folder_filter = filters.get('folder')
        
        # Build relationship filter using modern GraphQL patterns
        folder_where_clause = ""
        if folder_filter:
            if 'euuid' in folder_filter:
                euuid_val = folder_filter['euuid']
                folder_where_clause = f'(_where: {{euuid: {{_eq: "{euuid_val}"}}}})'
            elif 'path' in folder_filter:
                path_val = folder_filter['path']
                folder_where_clause = f'(_where: {{path: {{_eq: "{path_val}"}}}})'
            else:
                raise ValueError("Invalid folder filter - must be {'euuid': '...'} or {'path': '...'}")
        
        # Dynamic query with relationship filtering
        query = f"""
        query ListFiles($limit: Int, $where: searchFileOperator) {{
            searchFile(_limit: $limit, _where: $where, _order_by: {{uploaded_at: desc}}) {{
                euuid
                name
                status
                content_type
                size
                uploaded_at
                uploaded_by {{
                    name
                }}
                folder{folder_where_clause} {{
                    euuid
                    name
                    path
                }}
            }}
        }}
        """
        
        # Build file-level WHERE conditions
        where_conditions = []
        
        if filters.get('status'):
            where_conditions.append({"status": {"_eq": filters['status']}})
        
        if filters.get('name'):
            where_conditions.append({"name": {"_ilike": f"%{filters['name']}%"}})
        
        variables = {}
        if filters.get('limit'):
            variables['limit'] = filters['limit']
        
        if where_conditions:
            if len(where_conditions) == 1:
                variables['where'] = where_conditions[0]
            else:
                variables['where'] = {"_and": where_conditions}
        
        result = await _graphql(query, variables)
        
        if result.get('error'):
            raise Exception(f"Failed to list files: {result['error']}")
        
        files = result.get('data', {}).get('searchFile')
        return files or []  # Handle null result when folder filtering returns no matches
        
    except Exception as e:
        raise e


async def delete_file(file_uuid: str) -> bool:
    """
    Delete a file from EYWA file service.
    
    Args:
        file_uuid: UUID of the file to delete
        
    Returns:
        True if deletion successful, False otherwise
        
    Examples:
        success = await delete_file(file_uuid)
        if success:
            print("File deleted successfully")
    """
    try:
        mutation = """
        mutation DeleteFile($uuid: UUID!) {
            deleteFile(euuid: $uuid)
        }
        """
        
        result = await _graphql(mutation, {"uuid": file_uuid})
        
        if result.get('error'):
            raise Exception(f"Failed to delete file: {result['error']}")
        
        return result.get('data', {}).get('deleteFile', False)
        
    except Exception as e:
        raise e


# ============================================================================
# Folder Operations (Complete Support Per FILES_SPEC.md)
# ============================================================================

async def create_folder(folder_data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Create a new folder in EYWA file service.
    
    Args:
        folder_data: Dict matching GraphQL FolderInput type:
            name: str (required)
            euuid: str (optional, client-generated UUID)
            parent: dict (optional, {"euuid": "..."} for parent, omit for root)
    
    Returns:
        Created folder information dict
        
    Raises:
        Exception: If folder creation fails
        
    Examples:
        # Create folder in root
        folder = await create_folder({
            "name": "my-documents",
            "euuid": str(uuid.uuid4())
        })
        
        # Create subfolder
        subfolder = await create_folder({
            "name": "reports", 
            "parent": {"euuid": parent_folder_uuid}
        })
    """
    try:
        mutation = """
        mutation CreateFolder($folder: FolderInput!) {
            stackFolder(data: $folder) {
                euuid
                name
                path
                modified_on
                parent {
                    euuid
                    name
                    path
                }
            }
        }
        """
        
        result = await _graphql(mutation, {"folder": folder_data})
        
        if result.get('error'):
            raise Exception(f"Failed to create folder: {result['error']}")
        
        return result.get('data', {}).get('stackFolder')
        
    except Exception as e:
        raise e


async def list_folders(filters: Dict[str, Any]) -> List[Dict[str, Any]]:
    """
    List folders with parent filtering support.
    
    Args:
        filters: Dict of filter criteria:
            limit: int (optional, max results)
            name: str (optional, SQL LIKE pattern)
            parent: dict|None (optional)
                {"euuid": "..."} - filter by parent UUID
                {"path": "..."} - filter by parent path  
                None - filter for root-level folders (parent=ROOT_UUID)
                (omit key entirely - no parent filtering)
    
    Returns:
        List of folder dicts
        
    Examples:
        # List all folders
        folders = await list_folders({})
        
        # List root-level folders only (folders under ROOT_UUID)
        root_folders = await list_folders({"parent": None})
        
        # List folders by parent UUID
        subfolders = await list_folders({"parent": {"euuid": parent_uuid}})
        
        # List folders with name pattern
        folders = await list_folders({"name": "report"})
    """
    try:
        query = """
        query ListFolders($limit: Int, $where: searchFolderOperator) {
            searchFolder(_limit: $limit, _where: $where, _order_by: {name: asc}) {
                euuid
                name
                path
                modified_on
                parent {
                    euuid
                    name
                }
            }
        }
        """
        
        where_conditions = []
        
        # Handle name filter
        if filters.get('name'):
            where_conditions.append({"name": {"_ilike": f"%{filters['name']}%"}})
        
        # Handle parent filter (CORRECTED: root folders have parent=ROOT_UUID, not null)
        if 'parent' in filters:
            parent_filter = filters['parent']
            if parent_filter is None:
                # Root folders only - folders whose parent is ROOT_UUID (not null!)
                where_conditions.append({"parent": {"euuid": {"_eq": ROOT_UUID}}})
            elif isinstance(parent_filter, dict):
                if 'euuid' in parent_filter:
                    where_conditions.append({"parent": {"euuid": {"_eq": parent_filter['euuid']}}})
                elif 'path' in parent_filter:
                    where_conditions.append({"parent": {"path": {"_eq": parent_filter['path']}}})
                else:
                    raise ValueError("Invalid parent filter - must be {'euuid': '...'} or {'path': '...'}")
            else:
                raise ValueError("Parent filter must be None or dict")
        
        variables = {}
        if filters.get('limit'):
            variables['limit'] = filters['limit']
        
        if where_conditions:
            if len(where_conditions) == 1:
                variables['where'] = where_conditions[0]
            else:
                variables['where'] = {"_and": where_conditions}
        
        result = await _graphql(query, variables)
        
        if result.get('error'):
            raise Exception(f"Failed to list folders: {result['error']}")
        
        return result.get('data', {}).get('searchFolder', [])
        
    except Exception as e:
        raise e


async def get_folder_info(data: Dict[str, str]) -> Optional[Dict[str, Any]]:
    """
    Get information about a specific folder by UUID or path.
    
    Args:
        data: Dict containing either:
            {"euuid": "folder-uuid"} OR {"path": "/folder/path"}
    
    Returns:
        Folder information dict if found, None if not found
        
    Raises:
        Exception: If GraphQL query fails
        
    Examples:
        # Get by UUID
        folder = await get_folder_info({"euuid": folder_uuid})
        
        # Get by path
        folder = await get_folder_info({"path": "/documents/reports"})
    """
    try:
        if 'euuid' in data:
            query = """
            query GetFolder($euuid: UUID!) {
                getFolder(euuid: $euuid) {
                    euuid
                    name
                    path
                    modified_on
                    parent {
                        euuid
                        name
                    }
                }
            }
            """
            variables = {"euuid": data["euuid"]}
        elif 'path' in data:
            query = """
            query GetFolder($path: String!) {
                getFolder(path: $path) {
                    euuid
                    name
                    path
                    modified_on
                    parent {
                        euuid
                        name
                    }
                }
            }
            """
            variables = {"path": data["path"]}
        else:
            raise ValueError("Must provide either 'euuid' or 'path' in data dict")
        
        result = await _graphql(query, variables)
        
        if result.get('error'):
            raise Exception(f"Failed to get folder info: {result['error']}")
        
        return result.get('data', {}).get('getFolder')
        
    except Exception as e:
        raise e


async def delete_folder(folder_uuid: str) -> bool:
    """
    Delete an empty folder from EYWA file service.
    
    Note: Folder must be empty (no files or subfolders) to be deleted.
    
    Args:
        folder_uuid: UUID of the folder to delete
        
    Returns:
        True if deletion successful, False otherwise
        
    Examples:
        success = await delete_folder(folder_uuid)
        if not success:
            print("Folder deletion failed - may not be empty")
    """
    try:
        mutation = """
        mutation DeleteFolder($uuid: UUID!) {
            deleteFolder(euuid: $uuid)
        }
        """
        
        result = await _graphql(mutation, {"uuid": folder_uuid})
        
        if result.get('error'):
            raise Exception(f"Failed to delete folder: {result['error']}")
        
        return result.get('data', {}).get('deleteFolder', False)
        
    except Exception as e:
        raise e


# ============================================================================
# Utility Functions
# ============================================================================

def calculate_file_hash(filepath: Union[str, Path], algorithm: str = 'sha256') -> str:
    """
    Calculate hash of a file for integrity verification.
    
    Args:
        filepath: Path to the file
        algorithm: Hash algorithm ('md5', 'sha1', 'sha256', etc.)
        
    Returns:
        Hex digest of the file hash
        
    Examples:
        hash_value = calculate_file_hash("test.txt", "sha256")
        print(f"SHA256: {hash_value}")
    """
    filepath = Path(filepath)
    hash_obj = hashlib.new(algorithm)
    
    with open(filepath, 'rb') as f:
        for chunk in iter(lambda: f.read(8192), b""):
            hash_obj.update(chunk)
    
    return hash_obj.hexdigest()


# ============================================================================
# Convenience Functions
# ============================================================================

async def quick_upload(filepath: Union[str, Path]) -> str:
    """
    Quick upload with minimal parameters.
    
    Returns:
        File UUID (generated by server if not provided)
    """
    file_path = Path(filepath)
    await upload(filepath, {"name": file_path.name})
    
    # Since we don't control the UUID in this convenience function,
    # we need to find the file by name (most recent)
    files = await list({"name": file_path.name, "limit": 1})
    if files:
        return files[0]['euuid']
    else:
        raise FileUploadError("Could not retrieve uploaded file UUID")


async def quick_download(file_uuid: str, filename: Optional[str] = None) -> str:
    """
    Quick download to current directory.
    
    Returns:
        Path to downloaded file
    """
    if filename is None:
        info = await file_info(file_uuid)
        filename = info['name'] if info else f"download_{file_uuid[:8]}"
    
    return await download(file_uuid, filename)


# ============================================================================
# Export All Functions (Per FILES_SPEC.md)
# ============================================================================

__all__ = [
    # Constants
    'ROOT_UUID',
    'ROOT_FOLDER',
    
    # Exception Types
    'FileUploadError', 
    'FileDownloadError',
    
    # Core Upload Operations
    'upload',
    'upload_stream', 
    'upload_content',
    
    # Core Download Operations
    'download_stream',
    'download',
    
    # File Management Operations  
    'file_info',
    'list',
    'delete_file',
    
    # Folder Operations
    'create_folder',
    'list_folders', 
    'get_folder_info',
    'delete_folder',
    
    # Utility Functions
    'calculate_file_hash',
    
    # Convenience Functions
    'quick_upload',
    'quick_download'
]