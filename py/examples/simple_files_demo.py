#!/usr/bin/env python3
"""
EYWA Files - Simplified API Demo (Idempotent)

Demonstrates the simplified approach with idempotent operations:
- Protocol abstraction for upload/download
- Simple CRUD mutations
- Direct GraphQL for queries/verification
- Pre-defined UUIDs for repeatable operations

Usage:
    eywa run -c "python examples/simple_files_demo.py"         # Full demo + cleanup
    eywa run -c "python examples/simple_files_demo.py create"  # Create test resources only
    eywa run -c "python examples/simple_files_demo.py list"    # List test resources
    eywa run -c "python examples/simple_files_demo.py cleanup" # Clean up test resources

Note: Can be run multiple times safely - uses constant UUIDs.
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

# Pre-defined UUIDs for idempotent operations
DEMO_FOLDER_UUID = "9bd6fe99-7540-4a54-9998-138405ea8d2c"
SAMPLE_FILE_UUID = "3f0f4173-4ef7-4499-857e-37568adeab48"
JSON_FILE_UUID = "ea0fee9a-30d9-4aae-b087-10bce969af57"


class SimplifiedFilesDemo:
    def __init__(self):
        self.test_resources = []

    async def list_resources(self):
        """List all test resources to see what exists"""
        eywa.info("📋 Listing test resources...")

        resources_found = []

        # Check for demo folder
        try:
            folder_result = await eywa.graphql(
                """
                query GetFolder($uuid: UUID!) {
                    getFolder(euuid: $uuid) {
                        euuid
                        name
                        path
                        modified_on
                    }
                }
                """,
                {"uuid": DEMO_FOLDER_UUID}
            )
            if folder_result.get("getFolder"):
                folder = folder_result["getFolder"]
                resources_found.append(("folder", folder))
                eywa.info(f"  📁 Folder: {folder['name']} (UUID: {folder['euuid']}, Path: {folder['path']})")
        except Exception as e:
            eywa.debug(f"Demo folder not found: {e}")

        # Check for sample file
        try:
            file_result = await eywa.graphql(
                """
                query GetFile($uuid: UUID!) {
                    getFile(euuid: $uuid) {
                        euuid
                        name
                        size
                        content_type
                        status
                        folder {
                            name
                            path
                        }
                    }
                }
                """,
                {"uuid": SAMPLE_FILE_UUID}
            )
            if file_result.get("getFile"):
                file = file_result["getFile"]
                resources_found.append(("file", file))
                folder_path = file.get('folder', {}).get('path', 'root')
                eywa.info(f"  📄 File: {file['name']} (UUID: {file['euuid']}, Size: {file['size']} bytes, Folder: {folder_path})")
        except Exception as e:
            eywa.debug(f"Sample file not found: {e}")

        # Check for JSON file
        try:
            json_result = await eywa.graphql(
                """
                query GetFile($uuid: UUID!) {
                    getFile(euuid: $uuid) {
                        euuid
                        name
                        size
                        content_type
                        status
                        folder {
                            name
                            path
                        }
                    }
                }
                """,
                {"uuid": JSON_FILE_UUID}
            )
            if json_result.get("getFile"):
                file = json_result["getFile"]
                resources_found.append(("file", file))
                folder_path = file.get('folder', {}).get('path', 'root')
                eywa.info(f"  📄 File: {file['name']} (UUID: {file['euuid']}, Size: {file['size']} bytes, Folder: {folder_path})")
        except Exception as e:
            eywa.debug(f"JSON file not found: {e}")

        if not resources_found:
            eywa.info("  No test resources found.")
        else:
            eywa.info(f"✅ Found {len(resources_found)} test resources")

        return resources_found

    async def cleanup(self):
        """Clean up test resources"""
        eywa.info("🧹 Cleaning up test resources...")

        # Delete files first (using predefined UUIDs)
        file_uuids = [SAMPLE_FILE_UUID, JSON_FILE_UUID]
        for file_uuid in file_uuids:
            try:
                deleted = await delete_file(file_uuid)
                if deleted:
                    eywa.info(f"  ✅ Deleted file: {file_uuid}")
            except Exception as e:
                eywa.debug(f"File {file_uuid} not found or already deleted: {e}")

        # Then delete folder
        try:
            deleted = await delete_folder(DEMO_FOLDER_UUID)
            if deleted:
                eywa.info(f"  ✅ Deleted folder: {DEMO_FOLDER_UUID}")
        except Exception as e:
            eywa.debug(f"Folder {DEMO_FOLDER_UUID} not found or already deleted: {e}")

        eywa.info("✅ Cleanup complete")

    def track_resource(self, resource_type: str, resource_uuid: str, name: str):
        """Track resource for cleanup"""
        self.test_resources.append(
            {"type": resource_type, "uuid": resource_uuid, "name": name}
        )

    async def demo_folder_operations(self):
        """Demo folder creation and verification with GraphQL"""
        eywa.info("📁 DEMO: Folder Operations")

        # Create test folder with predefined UUID
        folder_uuid = DEMO_FOLDER_UUID
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

        folder_info = verification.get("getFolder")
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
            # Upload file with predefined UUID
            file_uuid = SAMPLE_FILE_UUID
            self.track_resource("file", file_uuid, "demo-file.txt")

            folder_ref = {"euuid": folder_uuid} if folder_uuid else {"euuid": ROOT_UUID}

            eywa.info("Uploading file with protocol abstraction...")
            file_info = await upload(
                temp_file.name,
                {"euuid": file_uuid, "name": "demo-file.txt", "folder": folder_ref},
            )

            eywa.info(f"✅ File uploaded successfully!")
            eywa.info(f"   UUID: {file_info['euuid']}")
            eywa.info(f"   Name: {file_info['name']}")
            eywa.info(f"   Status: {file_info['status']}")
            eywa.info(f"   Size: {file_info['size']} bytes")
            eywa.info(f"   Content-Type: {file_info['content_type']}")
            if file_info.get('folder'):
                eywa.info(f"   Folder: {file_info['folder']['path']}")

            return file_info['euuid']

        finally:
            # Clean up temp file
            os.unlink(temp_file.name)

    async def demo_content_upload(self, folder_uuid: str = None):
        """Demo content upload (string/JSON)"""
        eywa.info("📝 DEMO: Content Upload")

        # Upload JSON content with predefined UUID
        file_uuid = JSON_FILE_UUID
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
        file_info = await upload_content(
            content,
            {
                "euuid": file_uuid,
                "name": "demo-data.json",
                "content_type": "application/json",
                "folder": folder_ref,
            },
        )

        eywa.info(f"✅ JSON content uploaded successfully!")
        eywa.info(f"   UUID: {file_info['euuid']}")
        eywa.info(f"   Name: {file_info['name']}")
        eywa.info(f"   Status: {file_info['status']}")
        eywa.info(f"   Size: {file_info['size']} bytes")
        return file_info['euuid']

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
                    searchFile(_order_by: {uploaded_at: desc}) {
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

            files = files_query.get("searchFile", [])
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

        folders = folders_query.get("searchFolder", [])
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

    async def create_resources(self):
        """Create test resources without cleanup"""
        eywa.info("🚀 Creating test resources...")

        try:
            # 1. Create folder
            folder_uuid = await self.demo_folder_operations()

            # 2. Upload files
            text_file_uuid = await self.demo_file_upload(folder_uuid)
            json_file_uuid = await self.demo_content_upload(folder_uuid)

            eywa.info("✅ Test resources created successfully!")
            eywa.info(f"   Folder UUID: {folder_uuid}")
            eywa.info(f"   Text file UUID: {text_file_uuid}")
            eywa.info(f"   JSON file UUID: {json_file_uuid}")

        except Exception as e:
            eywa.error(f"💥 Resource creation failed: {e}")
            import traceback
            eywa.debug(traceback.format_exc())
            raise

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

    # Parse command line arguments
    command = sys.argv[1] if len(sys.argv) > 1 else None

    try:
        demo = SimplifiedFilesDemo()

        if command == "create":
            # Create test resources without cleanup
            await demo.create_resources()
        elif command == "list":
            # List existing test resources
            await demo.list_resources()
        elif command == "cleanup":
            # Clean up test resources
            await demo.cleanup()
        elif command is None:
            # Run full demo with cleanup
            await demo.run_demo()
        else:
            eywa.error(f"Unknown command: {command}")
            eywa.info("Usage:")
            eywa.info("  python examples/simple_files_demo.py         # Full demo + cleanup")
            eywa.info("  python examples/simple_files_demo.py create  # Create test resources")
            eywa.info("  python examples/simple_files_demo.py list    # List test resources")
            eywa.info("  python examples/simple_files_demo.py cleanup # Clean up test resources")
            eywa.close_task(eywa.ERROR)
            return

        eywa.close_task(eywa.SUCCESS)

    except Exception as e:
        eywa.error(f"💥 Execution failed: {e}")
        eywa.close_task(eywa.ERROR)


if __name__ == "__main__":
    asyncio.run(main())
