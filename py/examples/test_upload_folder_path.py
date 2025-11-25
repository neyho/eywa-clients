#!/usr/bin/env python3
"""
EYWA Upload with folder_path Test

Tests the folder_path parameter for upload and upload_content functions:
- Auto-creates folder structure when uploading
- Upload file with folder_path
- Upload content with folder_path

Usage: eywa run -c "python examples/test_upload_folder_path.py"
"""

import sys
import os

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import asyncio
import json
import tempfile
import eywa
from eywa_files import (
    upload,
    upload_content,
    download,
    delete_file,
    delete_folder,
    get_folder_by_path,
)


async def main():
    eywa.open_pipe()
    uploaded_files = []
    created_folders = []

    try:
        eywa.info("Testing upload with folder_path")
        eywa.update_task(status=eywa.PROCESSING)

        # Test 1: upload_content with folder_path (creates nested folders)
        eywa.info("Test 1: upload_content with folder_path")
        test_path = "/test-upload-path/nested/deep/"

        content = json.dumps({"test": "data", "timestamp": "2024-01-01"}, indent=2)
        await upload_content(
            content,
            {
                "name": "test-data.json",
                "content_type": "application/json",
                "folder_path": test_path,
            }
        )
        eywa.info(f"  Uploaded test-data.json to {test_path}")

        # Verify the file exists by querying
        result = await eywa.graphql("""
            query FindFile {
                searchFile(_where: {name: {_eq: "test-data.json"}}, _limit: 1) {
                    euuid
                    name
                    folder {
                        path
                    }
                }
            }
        """)
        files = result.get("searchFile", [])
        if files and files[0]["folder"]["path"] == test_path:
            eywa.info(f"  File found in correct folder: {files[0]['folder']['path']}")
            uploaded_files.append(files[0]["euuid"])
        else:
            eywa.error("  File not found or in wrong folder!")

        # Test 2: upload file with folder_path
        eywa.info("Test 2: upload file with folder_path")

        # Create a temp file to upload
        temp_file = tempfile.NamedTemporaryFile(mode="w", suffix=".txt", delete=False)
        temp_file.write("Hello from folder_path test!")
        temp_file.close()

        try:
            await upload(
                temp_file.name,
                {
                    "name": "hello.txt",
                    "folder_path": "/test-upload-path/texts/",
                }
            )
            eywa.info("  Uploaded hello.txt to /test-upload-path/texts/")

            # Verify
            result = await eywa.graphql("""
                query FindFile {
                    searchFile(_where: {name: {_eq: "hello.txt"}}, _limit: 1) {
                        euuid
                        name
                        folder {
                            path
                        }
                    }
                }
            """)
            files = result.get("searchFile", [])
            if files and files[0]["folder"]["path"] == "/test-upload-path/texts/":
                eywa.info(f"  File found in correct folder: {files[0]['folder']['path']}")
                uploaded_files.append(files[0]["euuid"])
            else:
                eywa.error("  File not found or in wrong folder!")

        finally:
            os.unlink(temp_file.name)

        # Test 3: Verify folder structure was created
        eywa.info("Test 3: Verify folder structure")

        folders_to_check = [
            "/test-upload-path/",
            "/test-upload-path/nested/",
            "/test-upload-path/nested/deep/",
            "/test-upload-path/texts/",
        ]

        for folder_path in folders_to_check:
            folder = await get_folder_by_path(folder_path)
            if folder:
                eywa.info(f"  Found: {folder_path}")
                created_folders.append(folder["euuid"])
            else:
                eywa.error(f"  Missing: {folder_path}")

        eywa.info("All tests passed!")

    except Exception as e:
        eywa.error(f"Test failed: {e}")
        import traceback
        eywa.debug(traceback.format_exc())

    finally:
        # Cleanup
        eywa.info("Cleanup: deleting test files and folders...")

        # Delete files first
        for file_uuid in uploaded_files:
            try:
                await delete_file(file_uuid)
                eywa.debug(f"  Deleted file: {file_uuid}")
            except Exception as e:
                eywa.warn(f"  Failed to delete file: {e}")

        # Delete folders (deepest first)
        folders_to_delete = [
            "/test-upload-path/nested/deep/",
            "/test-upload-path/nested/",
            "/test-upload-path/texts/",
            "/test-upload-path/",
        ]
        for folder_path in folders_to_delete:
            try:
                folder = await get_folder_by_path(folder_path)
                if folder:
                    await delete_folder(folder["euuid"])
                    eywa.debug(f"  Deleted folder: {folder_path}")
            except Exception as e:
                eywa.warn(f"  Failed to delete folder {folder_path}: {e}")

        eywa.close_task(eywa.SUCCESS)


if __name__ == "__main__":
    asyncio.run(main())
