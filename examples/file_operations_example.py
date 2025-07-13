#!/usr/bin/env python3
"""
EYWA File Operations Example

This example demonstrates the enhanced file upload/download capabilities
of the EYWA Python client. Shows both basic and advanced usage patterns.

Usage:
    eywa run -c "python file_operations_example.py"
"""

import asyncio
import eywa
import tempfile
import os
import json
import datetime
from pathlib import Path


async def file_operations_demo():
    """Demonstrate all file operations"""
    
    eywa.info("🚀 EYWA File Operations Demo Starting")
    
    try:
        # Example 1: Quick Upload/Download
        eywa.info("\n📤 Example 1: Quick File Upload")
        
        # Create a test file
        test_content = "Hello EYWA! This is a test file created by the Python client.\n"
        test_content += f"Generated at: {datetime.datetime.now()}\n"
        test_content += "This demonstrates the enhanced file capabilities."
        
        with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False) as f:
            f.write(test_content)
            temp_file = f.name
        
        try:
            # Quick upload
            file_uuid = await eywa.quick_upload(temp_file)
            eywa.info(f"✅ Quick upload successful! File UUID: {file_uuid}")
            
            # Quick download
            downloaded_path = await eywa.quick_download(file_uuid, "downloaded_file.txt")
            eywa.info(f"✅ Quick download successful! Saved to: {downloaded_path}")
            
            # Verify content
            with open(downloaded_path, 'r') as f:
                downloaded_content = f.read()
            
            if downloaded_content == test_content:
                eywa.info("✅ Content verification: Perfect match!")
            else:
                eywa.error("❌ Content verification failed")
                
        finally:
            # Cleanup
            os.unlink(temp_file)
            if os.path.exists("downloaded_file.txt"):
                os.unlink("downloaded_file.txt")
        
        # Example 2: Advanced Upload with Progress
        eywa.info("\n📤 Example 2: Advanced Upload with Progress Tracking")
        
        def upload_progress(current, total):
            percentage = (current / total) * 100 if total > 0 else 0
            eywa.debug(f"Upload progress: {current}/{total} bytes ({percentage:.1f}%)")
        
        # Create larger test content
        large_content = "EYWA Large File Test\n" + "x" * 10000  # 10KB+ file
        
        file_info = await eywa.upload_content(
            content=large_content,
            name="large_test_file.txt",
            content_type='text/plain',
            progress_callback=upload_progress
        )
        
        eywa.info(f"✅ Advanced upload completed: {file_info['name']} -> {file_info['euuid']}")
        
        # Example 3: File Listing and Management
        eywa.info("\n📋 Example 3: File Listing and Management")
        
        # List all files
        all_files = await eywa.list_files(limit=5)
        eywa.info(f"📁 Found {len(all_files)} recent files:")
        for file in all_files:
            status_emoji = "✅" if file['status'] == 'UPLOADED' else "⏳"
            eywa.info(f"  {status_emoji} {file['name']} ({file['status']}) - {file['size']} bytes")
        
        # List only uploaded files
        uploaded_files = await eywa.list_files(status='UPLOADED', limit=3)
        eywa.info(f"📁 Found {len(uploaded_files)} uploaded files")
        
        # Search by name pattern
        test_files = await eywa.list_files(name_pattern='test')
        eywa.info(f"🔍 Found {len(test_files)} files matching 'test'")
        
        # Example 4: Download with Progress
        eywa.info("\n📥 Example 4: Download with Progress Tracking")
        
        if uploaded_files:
            test_file = uploaded_files[0]
            
            def download_progress(current, total):
                percentage = (current / total) * 100 if total > 0 else 0
                eywa.debug(f"Download progress: {current}/{total} bytes ({percentage:.1f}%)")
            
            # Download to memory
            content = await eywa.download_file(
                file_uuid=test_file['euuid'],
                progress_callback=download_progress
            )
            
            eywa.info(f"✅ Downloaded {test_file['name']} to memory ({len(content)} bytes)")
            
            # Download to file
            download_path = f"downloaded_{test_file['name']}"
            saved_path = await eywa.download_file(
                file_uuid=test_file['euuid'],
                save_path=download_path,
                progress_callback=download_progress
            )
            
            eywa.info(f"✅ Downloaded {test_file['name']} to file: {saved_path}")
            
            # Calculate file hash for integrity
            original_hash = eywa.calculate_file_hash(saved_path)
            eywa.info(f"🔑 File hash (SHA256): {original_hash[:16]}...")
            
            # Cleanup
            if os.path.exists(download_path):
                os.unlink(download_path)
        
        # Example 5: File Information and Metadata
        eywa.info("\n📊 Example 5: File Information and Metadata")
        
        if all_files:
            sample_file = all_files[0]
            
            # Get detailed file info
            detailed_info = await eywa.get_file_info(sample_file['euuid'])
            if detailed_info:
                eywa.info(f"📄 File Details for {detailed_info['name']}:")
                eywa.info(f"  • UUID: {detailed_info['euuid']}")
                eywa.info(f"  • Status: {detailed_info['status']}")
                eywa.info(f"  • Content Type: {detailed_info['content_type']}")
                eywa.info(f"  • Size: {detailed_info['size']} bytes")
                eywa.info(f"  • Uploaded: {detailed_info.get('uploaded_at', 'Unknown')}")
                
                if detailed_info.get('uploaded_by'):
                    eywa.info(f"  • Uploaded by: {detailed_info['uploaded_by'].get('name', 'Unknown')}")
        
        # Example 6: Content Upload (Memory to EYWA)
        eywa.info("\n💾 Example 6: Content Upload from Memory")
        
        # Generate JSON data
        json_data = {
            "demo": "EYWA File Operations",
            "timestamp": str(datetime.datetime.now()),
            "features": [
                "Upload files from disk",
                "Upload content from memory", 
                "Download to file or memory",
                "Progress tracking",
                "File listing and search",
                "Metadata management"
            ],
            "status": "success"
        }
        
        json_content = json.dumps(json_data, indent=2)
        
        json_file_info = await eywa.upload_content(
            content=json_content,
            name="demo_data.json",
            content_type='application/json'
        )
        
        eywa.info(f"✅ JSON data uploaded: {json_file_info['name']} -> {json_file_info['euuid']}")
        
        # Download and parse the JSON
        downloaded_json = await eywa.download_file(json_file_info['euuid'])
        parsed_data = json.loads(downloaded_json.decode('utf-8'))
        
        eywa.info(f"✅ JSON verification: Found {len(parsed_data['features'])} features")
        
        # Final Summary
        eywa.info("\n🎉 File Operations Demo Complete!")
        eywa.info("📊 Summary of capabilities demonstrated:")
        eywa.info("  ✅ Quick upload/download")
        eywa.info("  ✅ Advanced upload with progress tracking")
        eywa.info("  ✅ Content upload from memory")
        eywa.info("  ✅ File listing with filters")
        eywa.info("  ✅ Download with progress tracking")
        eywa.info("  ✅ File metadata and information")
        eywa.info("  ✅ Hash calculation for integrity")
        eywa.info("  ✅ JSON data handling")
        
        eywa.report("File Operations Demo", {
            "status": "completed",
            "features_tested": 8,
            "files_created": 3,
            "operations_successful": True
        })
        
    except Exception as e:
        eywa.error(f"Demo failed: {str(e)}")
        import traceback
        eywa.debug(traceback.format_exc())
        raise


async def main():
    """Main function"""
    try:
        eywa.open_pipe()
        
        # Get task context
        task = await eywa.get_task()
        eywa.info(f"Task received: {task.get('name', 'File Operations Demo')}")
        
        # Update task status
        eywa.update_task(eywa.PROCESSING)
        
        # Run the demo
        await file_operations_demo()
        
        # Complete successfully
        eywa.close_task(eywa.SUCCESS)
        
    except Exception as e:
        eywa.error(f"Task failed: {str(e)}")
        eywa.close_task(eywa.ERROR)


if __name__ == "__main__":
    asyncio.run(main())
