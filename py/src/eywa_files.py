"""
EYWA File Upload/Download Extensions

This module extends the EYWA client with convenient file upload and download functionality.
Provides high-level functions that handle the complete file lifecycle:
- Upload files with automatic URL generation and S3 upload
- Download files with automatic URL generation  
- List and manage uploaded files
"""

import os
import mimetypes
import aiohttp
import hashlib
from pathlib import Path
from typing import Optional, Union, Dict, Any, List, Callable
from . import eywa


class FileUploadError(Exception):
    """Raised when file upload fails"""
    pass


class FileDownloadError(Exception):
    """Raised when file download fails"""
    pass


async def upload_file(
    filepath: Union[str, Path],
    name: Optional[str] = None,
    content_type: Optional[str] = None,
    folder_uuid: Optional[str] = None,
    progress_callback: Optional[Callable[[int, int], None]] = None
) -> Dict[str, Any]:
    """
    Upload a file to EYWA file service.
    
    Args:
        filepath: Path to the file to upload
        name: Custom name for the file (defaults to filename)
        content_type: MIME type (auto-detected if not provided)
        folder_uuid: UUID of parent folder (optional)
        progress_callback: Function called with (bytes_uploaded, total_bytes)
        
    Returns:
        Dict containing file information:
        {
            'euuid': 'file-uuid',
            'name': 'filename.txt',
            'status': 'UPLOADED',
            'size': 1234,
            'content_type': 'text/plain'
        }
        
    Raises:
        FileUploadError: If upload fails at any stage
    """
    filepath = Path(filepath)
    
    if not filepath.exists():
        raise FileUploadError(f"File not found: {filepath}")
    
    # Get file information
    file_size = filepath.stat().st_size
    file_name = name or filepath.name
    
    if content_type is None:
        content_type, _ = mimetypes.guess_type(str(filepath))
        content_type = content_type or 'application/octet-stream'
    
    eywa.info(f"Starting upload: {file_name} ({file_size} bytes)")
    
    try:
        # Step 1: Request upload URL
        upload_query = """
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
        """
        
        variables = {
            "file": {
                "name": file_name,
                "content_type": content_type,
                "size": file_size
            }
        }
        
        if folder_uuid:
            variables["file"]["folder"] = {"euuid": folder_uuid}
        
        result = await eywa.graphql(upload_query, variables)
        upload_url = result["data"]["requestUploadURL"]
        
        eywa.debug(f"Upload URL received: {upload_url[:50]}...")
        
        # Step 2: Upload file to S3
        async with aiohttp.ClientSession() as session:
            with open(filepath, 'rb') as f:
                file_data = f.read()
            
            headers = {'Content-Type': content_type}
            
            # Upload with progress tracking if callback provided
            if progress_callback:
                progress_callback(0, file_size)
            
            async with session.put(upload_url, data=file_data, headers=headers) as response:
                if not response.ok:
                    response_text = await response.text()
                    raise FileUploadError(f"S3 upload failed ({response.status}): {response_text}")
            
            if progress_callback:
                progress_callback(file_size, file_size)
        
        eywa.debug("File uploaded to S3 successfully")
        
        # Step 3: Confirm upload
        confirm_query = """
        mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }
        """
        
        confirm_result = await eywa.graphql(confirm_query, {"url": upload_url})
        
        if not confirm_result["data"]["confirmFileUpload"]:
            raise FileUploadError("Upload confirmation failed")
        
        eywa.debug("Upload confirmed")
        
        # Step 4: Get file information
        file_info = await get_file_by_name(file_name)
        if not file_info:
            raise FileUploadError("Could not retrieve uploaded file information")
        
        eywa.info(f"Upload completed: {file_name} -> {file_info['euuid']}")
        return file_info
        
    except Exception as e:
        eywa.error(f"Upload failed: {str(e)}")
        raise FileUploadError(f"Upload failed: {str(e)}") from e


async def upload_content(
    content: Union[str, bytes],
    name: str,
    content_type: str = 'text/plain',
    folder_uuid: Optional[str] = None,
    progress_callback: Optional[Callable[[int, int], None]] = None
) -> Dict[str, Any]:
    """
    Upload content directly from memory.
    
    Args:
        content: String or bytes content to upload
        name: Filename for the content
        content_type: MIME type
        folder_uuid: UUID of parent folder (optional)
        progress_callback: Function called with (bytes_uploaded, total_bytes)
        
    Returns:
        Dict containing file information
        
    Raises:
        FileUploadError: If upload fails
    """
    if isinstance(content, str):
        content = content.encode('utf-8')
    
    file_size = len(content)
    
    eywa.info(f"Starting content upload: {name} ({file_size} bytes)")
    
    try:
        # Step 1: Request upload URL
        upload_query = """
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
        """
        
        variables = {
            "file": {
                "name": name,
                "content_type": content_type,
                "size": file_size
            }
        }
        
        if folder_uuid:
            variables["file"]["folder"] = {"euuid": folder_uuid}
        
        result = await eywa.graphql(upload_query, variables)
        upload_url = result["data"]["requestUploadURL"]
        
        # Step 2: Upload content to S3
        async with aiohttp.ClientSession() as session:
            headers = {'Content-Type': content_type}
            
            if progress_callback:
                progress_callback(0, file_size)
            
            async with session.put(upload_url, data=content, headers=headers) as response:
                if not response.ok:
                    response_text = await response.text()
                    raise FileUploadError(f"S3 upload failed ({response.status}): {response_text}")
            
            if progress_callback:
                progress_callback(file_size, file_size)
        
        # Step 3: Confirm upload
        confirm_query = """
        mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }
        """
        
        confirm_result = await eywa.graphql(confirm_query, {"url": upload_url})
        
        if not confirm_result["data"]["confirmFileUpload"]:
            raise FileUploadError("Upload confirmation failed")
        
        # Step 4: Get file information
        file_info = await get_file_by_name(name)
        if not file_info:
            raise FileUploadError("Could not retrieve uploaded file information")
        
        eywa.info(f"Content upload completed: {name} -> {file_info['euuid']}")
        return file_info
        
    except Exception as e:
        eywa.error(f"Content upload failed: {str(e)}")
        raise FileUploadError(f"Content upload failed: {str(e)}") from e


async def download_file(
    file_uuid: str,
    save_path: Optional[Union[str, Path]] = None,
    progress_callback: Optional[Callable[[int, int], None]] = None
) -> Union[str, bytes]:
    """
    Download a file from EYWA file service.
    
    Args:
        file_uuid: UUID of the file to download
        save_path: Path to save the file (if None, returns content)
        progress_callback: Function called with (bytes_downloaded, total_bytes)
        
    Returns:
        If save_path provided: path to saved file
        If save_path is None: file content as bytes
        
    Raises:
        FileDownloadError: If download fails
    """
    eywa.info(f"Starting download: {file_uuid}")
    
    try:
        # Step 1: Request download URL
        download_query = """
        query RequestDownload($file: FileInput!) {
            requestDownloadURL(file: $file)
        }
        """
        
        result = await eywa.graphql(download_query, {"file": {"euuid": file_uuid}})
        download_url = result["data"]["requestDownloadURL"]
        
        eywa.debug(f"Download URL received: {download_url[:50]}...")
        
        # Step 2: Download file
        async with aiohttp.ClientSession() as session:
            async with session.get(download_url) as response:
                if not response.ok:
                    raise FileDownloadError(f"Download failed ({response.status}): {await response.text()}")
                
                total_size = int(response.headers.get('content-length', 0))
                
                if progress_callback and total_size > 0:
                    progress_callback(0, total_size)
                
                content = b''
                async for chunk in response.content.iter_chunked(8192):
                    content += chunk
                    if progress_callback and total_size > 0:
                        progress_callback(len(content), total_size)
        
        if save_path:
            save_path = Path(save_path)
            save_path.parent.mkdir(parents=True, exist_ok=True)
            
            with open(save_path, 'wb') as f:
                f.write(content)
            
            eywa.info(f"Download completed: {file_uuid} -> {save_path}")
            return str(save_path)
        else:
            eywa.info(f"Download completed: {file_uuid} ({len(content)} bytes)")
            return content
        
    except Exception as e:
        eywa.error(f"Download failed: {str(e)}")
        raise FileDownloadError(f"Download failed: {str(e)}") from e


async def list_files(
    limit: Optional[int] = None,
    status: Optional[str] = None,
    name_pattern: Optional[str] = None,
    folder_uuid: Optional[str] = None
) -> List[Dict[str, Any]]:
    """
    List files in EYWA file service.
    
    Args:
        limit: Maximum number of files to return
        status: Filter by status (PENDING, UPLOADED, etc.)
        name_pattern: Filter by name pattern (SQL LIKE)
        folder_uuid: Filter by folder UUID
        
    Returns:
        List of file dictionaries
    """
    eywa.debug(f"Listing files (limit={limit}, status={status})")
    
    try:
        query = """
        query ListFiles($limit: Int, $where: FileWhereInput) {
            searchFile(_limit: $limit, _where: $where, _order_by: {uploaded_at: desc}) {
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
                }
            }
        }
        """
        
        where_conditions = []
        
        if status:
            where_conditions.append({"status": {"_eq": status}})
        
        if name_pattern:
            where_conditions.append({"name": {"_ilike": f"%{name_pattern}%"}})
        
        if folder_uuid:
            where_conditions.append({"folder": {"euuid": {"_eq": folder_uuid}}})
        
        variables = {}
        if limit:
            variables["limit"] = limit
        
        if where_conditions:
            if len(where_conditions) == 1:
                variables["where"] = where_conditions[0]
            else:
                variables["where"] = {"_and": where_conditions}
        
        result = await eywa.graphql(query, variables)
        files = result["data"]["searchFile"]
        
        eywa.debug(f"Found {len(files)} files")
        return files
        
    except Exception as e:
        eywa.error(f"Failed to list files: {str(e)}")
        raise


async def get_file_info(file_uuid: str) -> Optional[Dict[str, Any]]:
    """
    Get information about a specific file.
    
    Args:
        file_uuid: UUID of the file
        
    Returns:
        File information dict or None if not found
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
                }
            }
        }
        """
        
        result = await eywa.graphql(query, {"uuid": file_uuid})
        return result["data"]["getFile"]
        
    except Exception as e:
        eywa.debug(f"File not found or error: {str(e)}")
        return None


async def get_file_by_name(name: str) -> Optional[Dict[str, Any]]:
    """
    Get file information by name (returns most recent if multiple).
    
    Args:
        name: File name to search for
        
    Returns:
        File information dict or None if not found
    """
    files = await list_files(limit=1, name_pattern=name)
    return files[0] if files else None


async def delete_file(file_uuid: str) -> bool:
    """
    Delete a file from EYWA file service.
    
    Args:
        file_uuid: UUID of the file to delete
        
    Returns:
        True if deletion successful
    """
    try:
        query = """
        mutation DeleteFile($uuid: UUID!) {
            deleteFile(euuid: $uuid)
        }
        """
        
        result = await eywa.graphql(query, {"uuid": file_uuid})
        success = result["data"]["deleteFile"]
        
        if success:
            eywa.info(f"File deleted: {file_uuid}")
        else:
            eywa.warn(f"File deletion failed: {file_uuid}")
        
        return success
        
    except Exception as e:
        eywa.error(f"Failed to delete file: {str(e)}")
        return False


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


# Convenience functions for common use cases

async def quick_upload(filepath: Union[str, Path]) -> str:
    """
    Quick upload with minimal parameters.
    
    Returns:
        File UUID
    """
    result = await upload_file(filepath)
    return result['euuid']


async def quick_download(file_uuid: str, filename: Optional[str] = None) -> str:
    """
    Quick download to current directory.
    
    Returns:
        Path to downloaded file
    """
    if filename is None:
        file_info = await get_file_info(file_uuid)
        filename = file_info['name'] if file_info else f"download_{file_uuid[:8]}"
    
    return await download_file(file_uuid, filename)


# Export all functions
__all__ = [
    'upload_file',
    'upload_content', 
    'download_file',
    'list_files',
    'get_file_info',
    'get_file_by_name',
    'delete_file',
    'calculate_file_hash',
    'quick_upload',
    'quick_download',
    'FileUploadError',
    'FileDownloadError'
]
