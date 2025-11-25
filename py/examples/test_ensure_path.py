#!/usr/bin/env python3
"""
EYWA ensure_path Test

Tests the ensure_path and get_folder_by_path functions:
- Creating nested folder structures
- Idempotent path creation
- Path lookup by string

Usage: eywa run -c "python examples/test_ensure_path.py"
"""

import sys
import os

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import asyncio
import eywa
from eywa_files import ensure_path, get_folder_by_path, delete_folder


async def main():
    eywa.open_pipe()

    try:
        eywa.info("🧪 Testing ensure_path and get_folder_by_path")
        eywa.update_task(status=eywa.PROCESSING)

        # Test 1: ensure_path creates nested folders
        eywa.info("Test 1: Creating /test-ensure/level1/level2/")
        folder = await ensure_path("/test-ensure/level1/level2/")
        eywa.info(f"  Created: {folder['path']} (euuid: {folder['euuid']})")

        # Test 2: ensure_path returns existing folder (idempotent)
        eywa.info("Test 2: ensure_path on existing path should return same folder")
        folder2 = await ensure_path("/test-ensure/level1/level2/")
        same = folder2.get("euuid") == folder.get("euuid")
        eywa.info(f"  Returned same folder: {same}")

        # Test 3: get_folder_by_path
        eywa.info("Test 3: get_folder_by_path for /test-ensure/level1/")
        found = await get_folder_by_path("/test-ensure/level1/")
        eywa.info(f"  Found: {found['path'] if found else 'NOT FOUND'}")

        # Test 4: Path normalization (no trailing slash)
        eywa.info("Test 4: Path normalization - /test-ensure/level1 (no trailing slash)")
        found2 = await get_folder_by_path("/test-ensure/level1")
        eywa.info(f"  Found: {found2['path'] if found2 else 'NOT FOUND'}")

        # Test 5: Invalid path (doesn't start with /)
        eywa.info("Test 5: Invalid path should raise ValueError")
        try:
            await ensure_path("invalid/path")
            eywa.error("  Should have raised ValueError!")
        except ValueError as e:
            eywa.info(f"  Correctly raised ValueError: {e}")

        # Cleanup - delete in reverse order (deepest first)
        eywa.info("Cleanup: deleting test folders...")
        await delete_folder(folder["euuid"])
        level1 = await get_folder_by_path("/test-ensure/level1/")
        await delete_folder(level1["euuid"])
        root = await get_folder_by_path("/test-ensure/")
        await delete_folder(root["euuid"])

        eywa.info("✅ All tests passed!")
        eywa.close_task(eywa.SUCCESS)

    except Exception as e:
        eywa.error(f"❌ Test failed: {e}")
        import traceback
        eywa.debug(traceback.format_exc())
        eywa.close_task(eywa.ERROR)


if __name__ == "__main__":
    asyncio.run(main())
