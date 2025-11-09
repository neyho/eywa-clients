#!/usr/bin/env python3
"""
EYWA Files - Simplified API Demo

Demonstrates the new simplified approach:
- Protocol abstraction for upload/download
- Simple CRUD mutations
- Direct GraphQL for queries/verification

Usage: eywa run -c "python examples/simple_files_demo.py"
"""

import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import asyncio
import eywa
from eywa_files import (
    upload,
    upload_content,
    download,
    create_folder,
    delete_file,
    delete_folder,
    ROOT_UUID,
    ROOT_FOLDER,
    FileUploadError,
    FileDownloadError,
)
import uuid
import tempfile
from pathlib import Path


class SimplifiedFilesDemo:
    def __init__(self):
        self.test_resources = []

    async def cleanup(self):
        """Clean up test resources"""
        eywa.info("🧹 Cleaning up test resources...")

        # Delete files first
        for resource in self.test_resources:
            try:
                if resource["type"] == "file":
                    await delete_file(resource["uuid"])
                    eywa.debug(f"Deleted file: {resource['uuid']}")
                elif resource["type"] == "folder":
                    await delete_folder(resource["uuid"])
                    eywa.debug(f"Deleted folder: {resource['uuid']}")
            except Exception as e:
                eywa.warn(
                    f"Failed to delete {resource['type']} {resource['uuid']}: {e}"
                )

    def track_resource(self, resource_type: str, resource_uuid: str, name: str):
        """Track resource for cleanup"""
        self.test_resources.append(
            {"type": resource_type, "uuid": resource_uuid, "name": name}
        )

    async def demo_folder_operations(self):
        """Demo folder creation and verification with GraphQL"""
        eywa.info("📁 DEMO: Folder Operations")

        # Create test folder
        folder_uuid = str(uuid.uuid4())
        self.track_resource("folder", folder_uuid, "demo-folder")

        eywa.info("Creating folder with client-controlled UUID...")
        folder = await create_folder(
            {
                "euuid": folder_uuid,
                "name": "demo-folder",
                "parent": {"euuid": ROOT_UUID},
            }
        )

        eywa.info(f"✅ Created folder: {folder['name']} at {folder['path']}")

        # Verify with direct GraphQL
        eywa.info("Verifying folder creation with GraphQL...")
        verification = await eywa.graphql(
            """
            query GetFolder($uuid: UUID!) {
                getFolder(euuid: $uuid) {
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
        """,
            {"uuid": folder_uuid},
        )

        folder_info = verification.get("data", {}).get("getFolder")
        if folder_info:
            eywa.info(
                f"✅ GraphQL verification: Folder exists with path {folder_info['path']}"
            )
            return folder_uuid
        else:
            eywa.error("❌ GraphQL verification failed: Folder not found")
            return None

    async def demo_file_upload(self, folder_uuid: str = None):
        """Demo file upload and verification with GraphQL"""
        eywa.info("📤 DEMO: File Upload Operations")

        # Create a temporary test file
        temp_file = tempfile.NamedTemporaryFile(mode="w", suffix=".txt", delete=False)
        temp_file.write(
            "Hello from EYWA!\nThis is a test file.\nTimestamp: "
            + str(asyncio.get_event_loop().time())
        )
        temp_file.close()

        try:
            # Upload file with client-controlled UUID
            file_uuid = str(uuid.uuid4())
            self.track_resource("file", file_uuid, "demo-file.txt")

            folder_ref = {"euuid": folder_uuid} if folder_uuid else {"euuid": ROOT_UUID}

            eywa.info("Uploading file with protocol abstraction...")
            await upload(
                temp_file.name,
                {"euuid": file_uuid, "name": "demo-file.txt", "folder": folder_ref},
            )

            eywa.info(f"✅ File uploaded successfully with UUID: {file_uuid}")

            # Verify with GraphQL
            eywa.info("Verifying upload with GraphQL...")
            verification = await eywa.graphql(
                """
                query GetFile($uuid: UUID!) {
                    getFile(euuid: $uuid) {
                        euuid
                        name
                        status
                        size
                        content_type
                        uploaded_at
                        folder {
                            euuid
                            name
                            path
                        }
                    }
                }
            """,
                {"uuid": file_uuid},
            )

            file_info = verification.get("data", {}).get("getFile")
            if file_info:
                eywa.info(
                    f"✅ GraphQL verification: File {file_info['name']} ({file_info['size']} bytes) in {file_info['folder']['path'] if file_info['folder'] else 'root'}"
                )
                return file_uuid
            else:
                eywa.error("❌ GraphQL verification failed: File not found")
                return None

        finally:
            # Clean up temp file
            os.unlink(temp_file.name)

    async def demo_content_upload(self, folder_uuid: str = None):
        """Demo content upload (string/JSON)"""
        eywa.info("📝 DEMO: Content Upload")

        # Upload JSON content
        file_uuid = str(uuid.uuid4())
        self.track_resource("file", file_uuid, "demo-data.json")

        import json

        content = json.dumps(
            {
                "message": "Hello from EYWA!",
                "timestamp": asyncio.get_event_loop().time(),
                "test_data": [1, 2, 3, 4, 5],
            },
            indent=2,
        )

        folder_ref = {"euuid": folder_uuid} if folder_uuid else {"euuid": ROOT_UUID}

        eywa.info("Uploading JSON content...")
        await upload_content(
            content,
            {
                "euuid": file_uuid,
                "name": "demo-data.json",
                "content_type": "application/json",
                "folder": folder_ref,
            },
        )

        eywa.info(f"✅ JSON content uploaded with UUID: {file_uuid}")
        return file_uuid

    async def demo_file_download(self, file_uuid: str):
        """Demo file download and verification"""
        eywa.info("📥 DEMO: File Download")

        # Download to memory
        eywa.info("Downloading file to memory...")
        content = await download(file_uuid)

        eywa.info(f"✅ Downloaded {len(content)} bytes to memory")

        # Show content preview
        if len(content) < 500:  # Small files
            try:
                text_content = content.decode("utf-8")
                eywa.info(f"Content preview: {text_content[:100]}...")
            except:
                eywa.info("Content is binary data")

        # Download to file
        temp_dir = tempfile.mkdtemp()
        save_path = Path(temp_dir) / "downloaded_file"

        eywa.info("Downloading file to disk...")
        saved_path = await download(file_uuid, save_path)

        eywa.info(f"✅ File saved to: {saved_path}")

        # Verify file size matches
        downloaded_size = Path(saved_path).stat().st_size
        if downloaded_size == len(content):
            eywa.info(f"✅ File size verification: {downloaded_size} bytes")
        else:
            eywa.error(f"❌ File size mismatch: {downloaded_size} != {len(content)}")

        # Clean up
        os.unlink(saved_path)
        os.rmdir(temp_dir)

    async def demo_graphql_queries(self, folder_uuid: str = None):
        """Demo direct GraphQL queries for listing and searching"""
        eywa.info("🔍 DEMO: Direct GraphQL Queries")

        # List all files in our test folder
        if folder_uuid:
            eywa.info(f"Listing files in folder {folder_uuid}...")
            files_query = await eywa.graphql(
                """
                query ListFilesInFolder($folderId: UUID!) {
                    searchFile(, _order_by: {uploaded_at: desc}) {
                        euuid
                        name
                        size
                        content_type
                        uploaded_at
                        folder(_where: {euuid: {_eq: $folderId}}) {
                            name
                            path
                        }
                    }
                }
            """,
                {"folderId": folder_uuid},
            )

            files = files_query.get("data", {}).get("searchFile", [])
            eywa.info(f"✅ Found {len(files)} files in folder:")
            for file in files:
                eywa.info(
                    f"  - {file['name']} ({file['size']} bytes, {file['content_type']})"
                )

        # List all our test folders
        eywa.info("Listing folders starting with 'demo'...")
        folders_query = await eywa.graphql("""
            query ListDemoFolders {
                searchFolder(_where: {
                    name: {_ilike: "demo%"}
                }, _order_by: {name: asc}) {
                    euuid
                    name
                    path
                    modified_on
                    _count {
                        files
                    }
                }
            }
        """)

        folders = folders_query.get("data", {}).get("searchFolder", [])
        eywa.info(f"✅ Found {len(folders)} demo folders:")
        for folder in folders:
            file_count = folder.get("_count", {}).get("files", 0)
            eywa.info(f"  - {folder['name']} at {folder['path']} ({file_count} files)")

    async def demo_error_handling(self):
        """Demo error handling"""
        eywa.info("⚠️ DEMO: Error Handling")

        # Try to download non-existent file
        fake_uuid = str(uuid.uuid4())
        try:
            await download(fake_uuid)
            eywa.error("❌ Should have failed for non-existent file")
        except FileDownloadError as e:
            eywa.info(f"✅ Correctly caught FileDownloadError: {e}")

        # Try to upload non-existent file
        try:
            await upload("/non/existent/file.txt", {"name": "test.txt"})
            eywa.error("❌ Should have failed for non-existent file")
        except FileUploadError as e:
            eywa.info(f"✅ Correctly caught FileUploadError: {e}")

    async def run_demo(self):
        """Run the complete simplified files demo"""
        eywa.info("🚀 EYWA Files - Simplified API Demo")

        try:
            # 1. Demo folder operations
            folder_uuid = await self.demo_folder_operations()

            # 2. Demo file uploads
            text_file_uuid = await self.demo_file_upload(folder_uuid)
            json_file_uuid = await self.demo_content_upload(folder_uuid)

            # 3. Demo file downloads
            if text_file_uuid:
                await self.demo_file_download(text_file_uuid)

            # 4. Demo GraphQL queries
            await self.demo_graphql_queries(folder_uuid)

            # 5. Demo error handling
            await self.demo_error_handling()

            eywa.info("✨ Demo completed successfully!")

        except Exception as e:
            eywa.error(f"💥 Demo failed: {e}")
            import traceback

            eywa.debug(traceback.format_exc())
            raise

        finally:
            # Always clean up
            await self.cleanup()


async def main():
    eywa.open_pipe()

    try:
        demo = SimplifiedFilesDemo()
        await demo.run_demo()
        eywa.close_task(eywa.SUCCESS)

    except Exception as e:
        eywa.error(f"💥 Demo execution failed: {e}")
        eywa.close_task(eywa.ERROR)


if __name__ == "__main__":
    asyncio.run(main())
