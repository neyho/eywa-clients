#!/usr/bin/env python3
"""
EYWA Task Echo Example

Demonstrates task data retrieval and reporting.
This robot receives task data, processes it, and echoes it back as a report.

Usage: eywa run --task-json '{"data": {"list": ["item1", "item2", "item3"]}}' -c "python -m examples.echo"
"""

import sys
import os
import json
from datetime import datetime

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa
import asyncio

async def main():
    eywa.open_pipe()

    try:
        eywa.info("🔊 EYWA Task Echo Example")
        eywa.update_task(status=eywa.PROCESSING)

        # Retrieve task data
        task = await eywa.get_task()
        task_data = task.get("data", {})
        task_euuid = task.get("euuid", "unknown")
        task_type = task.get("type", "unknown")

        eywa.info(f"Task UUID: {task_euuid}")
        eywa.info(f"Task Type: {task_type}")

        # Extract the list from task data
        input_list = task_data.get("list", [])
        eywa.info(f"Received list with {len(input_list)} items")

        # Log each item
        for idx, item in enumerate(input_list, 1):
            eywa.info(f"  {idx}. {item}")

        # Process the list (example: count, categorize, etc.)
        processed_data = {
            "total_items": len(input_list),
            "items": input_list,
            "processed_at": datetime.now().isoformat(),
            "task_info": {
                "euuid": task_euuid,
                "type": task_type
            }
        }

        # Create detailed report
        items_table = eywa.create_table(
            headers=["Index", "Value", "Length", "Type"],
            rows=[
                [str(idx), str(item), str(len(str(item))), type(item).__name__]
                for idx, item in enumerate(input_list, 1)
            ]
        )

        summary_table = eywa.create_table(
            headers=["Metric", "Value"],
            rows=[
                ["Total Items", str(len(input_list))],
                ["Task UUID", task_euuid],
                ["Task Type", task_type],
                ["Processed At", datetime.now().strftime("%Y-%m-%d %H:%M:%S")],
                ["Status", "Success"]
            ]
        )

        report_data = eywa.create_report_data(
            card=f"""# Task Echo Report

## Summary
Received and processed **{len(input_list)} items** from task input.

### Task Information
- **Task UUID:** `{task_euuid}`
- **Task Type:** {task_type}
- **Items Received:** {len(input_list)}
- **Processing Time:** {datetime.now().strftime("%Y-%m-%d %H:%M:%S")}

### Input List
{chr(10).join(f'{idx}. `{item}`' for idx, item in enumerate(input_list, 1))}

### Status
✅ All items received successfully
✅ Data validated and processed
✅ Report generated

> This robot echoes back the received task data for verification and testing purposes.
""",
            Items=items_table,
            Summary=summary_table
        )

        eywa.report("Task Echo Results", report_data)

        # Also log the raw JSON for debugging
        eywa.debug(f"Raw task data: {json.dumps(task_data, indent=2)}")
        eywa.debug(f"Processed data: {json.dumps(processed_data, indent=2)}")

        eywa.info(f"✅ Echo example completed - processed {len(input_list)} items")
        eywa.close_task(eywa.SUCCESS)

    except Exception as e:
        eywa.error(f"❌ Echo example failed: {e}")
        import traceback
        eywa.debug(traceback.format_exc())
        eywa.close_task(eywa.ERROR)

if __name__ == "__main__":
    asyncio.run(main())
