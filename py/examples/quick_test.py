#!/usr/bin/env python3
"""
Quick test for basic upload/download functionality
Usage: eywa run -c "python examples/quick_test.py"
"""

import sys
import os
# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src'))

import asyncio
import eywa                    # Core EYWA functionality
from eywa_files import (       # File operations
    upload_content,
    download,
    file_info,
    delete_file
)
import uuid
import tempfile

async def quick_test():
    eywa.open_pipe()
    
    try:
        eywa.info("🧪 Quick Upload/Download Test")
        
        # Test content upload
        file_uuid = str(uuid.uuid4())
        test_content = f"Test file content - UUID: {file_uuid}"
        
        await upload_content(test_content, {
            "name": "quick-test.txt",
            "euuid": file_uuid,
            "content_type": "text/plain"
        })
        
        eywa.info(f"✅ Upload successful: {file_uuid}")
        
        # Test download
        downloaded_content = await download(file_uuid)
        downloaded_text = downloaded_content.decode('utf-8')
        
        eywa.info(f"✅ Download successful: {len(downloaded_content)} bytes")
        eywa.info(f"📄 Content: {downloaded_text[:50]}...")
        
        # Test file info
        info = await file_info(file_uuid)
        eywa.info(f"📊 File info: {info['name']} ({info['size']} bytes)")
        
        # Test delete
        deleted = await delete_file(file_uuid)
        eywa.info(f"🗑️ Delete successful: {deleted}")
        
        eywa.info("✅ All basic operations working!")
        eywa.close_task(eywa.SUCCESS)
        
    except Exception as e:
        eywa.error(f"❌ Test failed: {e}")
        import traceback
        eywa.debug(traceback.format_exc())
        eywa.close_task(eywa.ERROR)

if __name__ == "__main__":
    asyncio.run(quick_test())