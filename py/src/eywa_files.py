"""
EYWA File Operations - Simplified Protocol-Focused Client

This module provides file upload/download protocol abstraction for the EYWA Python client.
It focuses on handling the complex 3-step S3 protocol while letting users write their own
GraphQL queries for data retrieval.

Key Features:
- 3-step upload protocol (request URL → S3 upload → confirm)
- Memory-efficient streaming for large files
- Simple CRUD mutations for folders and files
- Direct GraphQL usage for all queries

Users should use eywa.graphql() directly for:
- Listing files/folders
- Searching/filtering
- Getting file/folder information
- Complex relationship queries

Version: 2.0.0 (Simplified)
"""

import asyncio
import aiohttp
import mimetypes
import hashlib
import os
import ssl
from pathlib import Path
from typing import Optional, Union, Dict, Any, List, Callable, AsyncIterator

# Disable SSL verification for development/testing
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
# Constants and Exception Types
# ============================================================================

# Root folder constants
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
# Core Upload Operations (Protocol Abstraction)
# ============================================================================

async def upload(filepath: Union[str, Path], file_data: Dict[str, Any]) -> None:
    """
    Upload a file to EYWA using the 3-step protocol.
    
    Args:
        filepath: Path to the file to upload (string or Path object)
        file_data: Dict with file metadata:
            euuid: str (optional, auto-generated if not provided)
            name: str (optional, defaults to filename)
            folder: dict (optional, {\"euuid\": \"...\"} or {\"path\": \"...\"})
            content_type: str (optional, auto-detected if not provided)
            size: int (optional, auto-calculated from file)
            progress_fn: callable (optional, not sent to GraphQL)
    
    Returns:
        None on success
        
    Raises:
        FileUploadError: If upload fails at any stage
        
    Examples:
        # Simple upload
        await upload(\"test.txt\", {\"name\": \"test.txt\"})
        
        # Upload to folder with client UUID
        await upload(\"test.txt\", {
            \"euuid\": str(uuid.uuid4()),
            \"name\": \"test.txt\",
            \"folder\": {\"euuid\": folder_uuid}
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
        
        # Generate UUID if not provided
        import uuid
        file_uuid = file_data.get('euuid') or str(uuid.uuid4())
        
        # Step 1: Request upload URL
        upload_query = """
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
        """
        
        # Build GraphQL input
        file_input = {
            **file_data,
            'euuid': file_uuid,
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
    Upload data from an async stream using the 3-step protocol.
    
    Args:
        input_stream: AsyncIterator[bytes] - Stream of file data
        file_data: Dict with file metadata:
            name: str (required)
            size: int (required for streams)
            euuid: str (optional, auto-generated)
            folder: dict (optional, {\"euuid\": \"...\"} or {\"path\": \"...\"}
            content_type: str (optional, default: application/octet-stream)
            progress_fn: callable (optional)
    
    Returns:
        None on success
        
    Raises:
        FileUploadError: If upload fails at any stage
    """
    try:
        if not file_data.get('name'):
            raise FileUploadError("name is required for stream uploads")
        if not file_data.get('size'):
            raise FileUploadError("size is required for stream uploads")
            
        content_type = file_data.get('content_type', 'application/octet-stream')
        content_length = file_data['size']
        progress_fn = file_data.get('progress_fn')
        
        # Generate UUID if not provided
        import uuid
        file_uuid = file_data.get('euuid') or str(uuid.uuid4())
        
        # Step 1: Request upload URL
        upload_query = """
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
        """
        
        # Build GraphQL input
        file_input = {
            **file_data,
            'euuid': file_uuid,
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
    Upload content directly from memory using the 3-step protocol.
    
    Args:
        content: String or bytes content to upload
        file_data: Dict with file metadata:
            name: str (required)
            euuid: str (optional, auto-generated)
            folder: dict (optional, {\"euuid\": \"...\"} or {\"path\": \"...\"}
            content_type: str (optional, default: text/plain for strings)
            size: int (optional, auto-calculated from content)
            progress_fn: callable (optional)
    
    Returns:
        None on success
        
    Raises:
        FileUploadError: If upload fails at any stage
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
        
        # Generate UUID if not provided
        import uuid
        file_uuid = file_data.get('euuid') or str(uuid.uuid4())
        
        # Step 1: Request upload URL
        upload_query = """
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
        """
        
        # Build GraphQL input
        file_input = {
            **file_data,
            'euuid': file_uuid,
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
# Core Download Operations (Protocol Abstraction)
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
    Download a file and return a stream for memory-efficient processing.
    
    Args:
        file_uuid: UUID of the file to download
    
    Returns:
        Dict containing:
            stream: AsyncIterator[bytes] - Stream of file content in chunks
            content_length: int - Content length in bytes
            
    Raises:
        FileDownloadError: If download fails
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
            "content_length": len(content)
        }
        
    except FileDownloadError:
        raise
    except Exception as e:
        raise FileDownloadError(f"Stream download failed: {str(e)}") from e


async def download(file_uuid: str, save_path: Optional[Union[str, Path]] = None, 
                  progress_fn: Optional[Callable] = None) -> Union[str, bytes]:
    """
    Download a file from EYWA.
    
    Args:
        file_uuid: UUID of the file to download
        save_path: Path to save the file (if None, returns content as bytes)
        progress_fn: Function called with (bytes_downloaded, total_bytes)
    
    Returns:
        If save_path provided: path to saved file (str)
        If save_path is None: file content as bytes
        
    Raises:
        FileDownloadError: If download fails
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
# Simple CRUD Operations
# ============================================================================

async def create_folder(folder_data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Create a new folder.
    
    Args:
        folder_data: Dict with folder definition:
            name: str (required)
            euuid: str (optional, auto-generated)
            parent: dict (optional, {\"euuid\": \"...\"} for parent, omit for root)
    
    Returns:
        Created folder information dict
        
    Raises:
        Exception: If folder creation fails
    """
    try:
        # Generate UUID if not provided
        import uuid
        folder_uuid = folder_data.get('euuid') or str(uuid.uuid4())
        
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
        
        # Build GraphQL input
        folder_input = {
            **folder_data,
            'euuid': folder_uuid
        }
        
        result = await _graphql(mutation, {"folder": folder_input})
        
        if result.get('error'):
            raise Exception(f"Failed to create folder: {result['error']}")
        
        return result.get('data', {}).get('stackFolder')
        
    except Exception as e:
        raise e


async def delete_file(file_uuid: str) -> bool:
    """
    Delete a file from EYWA.
    
    Args:
        file_uuid: UUID of the file to delete
        
    Returns:
        True if deletion successful, False otherwise
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


async def delete_folder(folder_uuid: str) -> bool:
    """
    Delete an empty folder.
    
    Note: Folder must be empty (no files or subfolders) to be deleted.
    
    Args:
        folder_uuid: UUID of the folder to delete
        
    Returns:
        True if deletion successful, False otherwise
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
    """
    filepath = Path(filepath)
    hash_obj = hashlib.new(algorithm)
    
    with open(filepath, 'rb') as f:
        for chunk in iter(lambda: f.read(8192), b""):
            hash_obj.update(chunk)
    
    return hash_obj.hexdigest()


# ============================================================================
# Export All Functions
# ============================================================================

__all__ = [
    # Constants
    'ROOT_UUID',
    'ROOT_FOLDER',
    
    # Exception Types
    'FileUploadError', 
    'FileDownloadError',
    
    # Upload Operations (Protocol Abstraction)
    'upload',
    'upload_stream', 
    'upload_content',
    
    # Download Operations (Protocol Abstraction)
    'download_stream',
    'download',
    
    # Simple CRUD Operations
    'create_folder',
    'delete_file',
    'delete_folder',
    
    # Utility Functions
    'calculate_file_hash',
]
