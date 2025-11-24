#!/usr/bin/env python3
"""
Test EYWA Robot - Infinite Loop with Signal Handling
This is a proper EYWA robot using eywa-client library.
"""

import os
import eywa
import asyncio
import signal
import sys

# Global flag for graceful shutdown
running = True


def signal_handler(signum, frame):
    """Handle termination signals gracefully"""
    global running
    eywa.warn(f"Received signal: {signum}")
    eywa.info("Initiating graceful shutdown...")
    running = False


async def main():
    """Main robot execution loop"""
    global running

    # Register signal handlers
    signal.signal(signal.SIGINT, signal_handler)
    if hasattr(signal, "SIGBREAK"):
        signal.signal(signal.SIGBREAK, signal_handler)
    if hasattr(signal, "SIGTERM"):
        signal.signal(signal.SIGTERM, signal_handler)

    current_pid = os.getpid()

    print(f"The current PID is: {current_pid}")
    # Open EYWA pipe for JSON-RPC communication
    eywa.open_pipe()

    try:
        # Get task from EYWA
        task = await eywa.get_task()
        input_data = task.get("data", {})

        eywa.info("EYWA Test Robot started")
        eywa.info(f"Platform: {sys.platform}")
        eywa.info(f"Input data: {input_data}")
        eywa.update_task(eywa.PROCESSING)

        iteration = 0

        # Infinite loop until signal received
        while running:
            iteration += 1

            # Log progress
            eywa.info(f"Iteration {iteration}: Robot is working...")
            print(f"The current PID is: {current_pid}")

            # Report progress periodically
            if iteration % 10 == 0:
                eywa.report(
                    "Progress Report", {"iteration": iteration, "status": "running"}
                )

            # Sleep for a bit (allows signal handling)
            await asyncio.sleep(2)

        # Graceful shutdown
        eywa.info(f"Robot stopped after {iteration} iterations")
        eywa.report(
            "Final Report",
            {"total_iterations": iteration, "status": "stopped_gracefully"},
        )
        eywa.close_task(eywa.SUCCESS)

    except KeyboardInterrupt:
        eywa.warn("KeyboardInterrupt caught")
        eywa.close_task(eywa.SUCCESS)
    except Exception as e:
        eywa.error(f"Error: {str(e)}")
        eywa.close_task(eywa.ERROR)


if __name__ == "__main__":
    asyncio.run(main())
