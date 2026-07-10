#!/usr/bin/env python3
"""
EYWA File Input Example

Demonstrates TaskFile input argument. Frontend uploads a file through EYWA
file service (requestUploadURL -> S3 PUT -> confirmFileUpload) and stores
the file reference in task data:

    {"document": {"euuid": "...", "name": "...", "content_type": "...", "size": ...}}

This robot reads the reference, downloads the file content and echoes
file metadata back as a report.

Usage: eywa run --task-json '{"data": {"document": {"euuid": "<file-uuid>", "name": "test.txt"}}}' -c "python -m examples.file_input"
"""

import sys
import os
import hashlib
from datetime import datetime

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa
import asyncio
from eywa_files import download


async def main():
    eywa.open_pipe()

    try:
        eywa.info("📄 EYWA File Input Example")
        eywa.update_task(status=eywa.PROCESSING)

        task = await eywa.get_task()
        task_data = task.get("data") or {}
        document = task_data.get("document")
        note = task_data.get("note")

        if not document or not document.get("euuid"):
            eywa.error("Task data doesn't contain 'document' file reference")
            eywa.close_task(eywa.ERROR)
            return

        file_uuid = document["euuid"]
        file_name = document.get("name", "unknown")
        eywa.info(f"Downloading file {file_name} [{file_uuid}]")

        content = await download(file_uuid)
        digest = hashlib.sha256(content).hexdigest()
        eywa.info(f"Downloaded {len(content)} bytes, sha256={digest}")

        summary_table = eywa.create_table(
            headers=["Property", "Value"],
            rows=[
                ["Name", file_name],
                ["UUID", file_uuid],
                ["Declared content type", str(document.get("content_type"))],
                ["Declared size", str(document.get("size"))],
                ["Downloaded size", str(len(content))],
                ["SHA-256", digest],
                ["Note", str(note)],
            ],
        )

        report_data = eywa.create_report_data(
            card=f"""# File Input Report

Downloaded **{file_name}** ({len(content)} bytes) from EYWA file service.

- **UUID:** `{file_uuid}`
- **SHA-256:** `{digest}`
- **Processed at:** {datetime.now().strftime("%Y-%m-%d %H:%M:%S")}

✅ File reference resolved and content downloaded
""",
            File=summary_table,
        )
        eywa.report("File Input Results", report_data)

        eywa.info("✅ File input example completed")
        eywa.close_task(eywa.SUCCESS)

    except Exception as e:
        eywa.error(f"❌ File input example failed: {e}")
        import traceback

        eywa.debug(traceback.format_exc())
        eywa.close_task(eywa.ERROR)


if __name__ == "__main__":
    asyncio.run(main())
