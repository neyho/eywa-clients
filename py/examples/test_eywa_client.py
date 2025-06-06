#!/usr/bin/env python3
"""Test script for EYWA Python client"""

import sys
import os
# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src'))

import asyncio
import eywa
import traceback


async def test_client():
    """Test all EYWA Python client features"""
    print("Starting EYWA Python client test...\n")
    
    # Initialize the pipe
    eywa.open_pipe()
    
    try:
        # Test 1: Logging functions
        eywa.info("Testing info logging", {"test": "info"})
        eywa.warn("Testing warning logging", {"test": "warn"})
        eywa.error("Testing error logging (not a real error)", {"test": "error"})
        eywa.debug("Testing debug logging", {"test": "debug"})
        eywa.trace("Testing trace logging", {"test": "trace"})
        eywa.exception("Testing exception logging", {"test": "exception"})
        
        # Test 2: Custom log with all parameters
        eywa.log(
            event="INFO",
            message="Custom log with all parameters",
            data={"custom": True},
            duration=1234,
            coordinates={"x": 10, "y": 20}
        )
        
        # Test 3: Report
        eywa.report("Test report message", {"reportData": "test"})
        
        # Test 4: Task management
        eywa.update_task(eywa.PROCESSING)
        eywa.info("Updated task status to PROCESSING")
        
        # Test 5: Get current task
        try:
            task = await eywa.get_task()
            eywa.info("Retrieved task:", task)
        except Exception as e:
            eywa.warn("Could not get task (normal if not in task context)", {"error": str(e)})
        
        # Test 6: GraphQL query
        eywa.info("Testing GraphQL query...")
        try:
            result = await eywa.graphql("""
                {
                    searchUser(_limit: 2) {
                        euuid
                        name
                        type
                        active
                    }
                }
            """)
            eywa.info("GraphQL query successful", {"resultCount": len(result.get("data", {}).get("searchUser", []))})
            
            # Show first user if available
            users = result.get("data", {}).get("searchUser", [])
            if users:
                eywa.info("First user:", users[0])
        except Exception as e:
            eywa.error("GraphQL query failed", {"error": str(e)})
        
        # Test 7: Constants
        eywa.info("Testing constants", {
            "SUCCESS": eywa.SUCCESS,
            "ERROR": eywa.ERROR,
            "PROCESSING": eywa.PROCESSING,
            "EXCEPTION": eywa.EXCEPTION
        })
        
        # Test 8: Table/Sheet classes
        sheet = eywa.Sheet("TestSheet")
        sheet.set_columns(["name", "value"])
        sheet.add_row({"name": "test1", "value": 100})
        sheet.add_row({"name": "test2", "value": 200})
        
        table = eywa.Table("TestTable")
        table.add_sheet(sheet)
        
        eywa.info("Testing Table/Sheet classes", {
            "table_json": table.toJSON()
        })
        
        # Test complete
        eywa.info("All tests completed successfully!")
        eywa.close_task(eywa.SUCCESS)
        
    except Exception as e:
        eywa.error("Test failed with unexpected error", {
            "error": str(e),
            "stack": traceback.format_exc()
        })
        eywa.close_task(eywa.ERROR)


if __name__ == "__main__":
    # Run the async test
    try:
        asyncio.run(test_client())
    except Exception as e:
        print(f"Unhandled error: {e}")
        import sys
        sys.exit(1)
