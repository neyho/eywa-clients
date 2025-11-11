#!/usr/bin/env python3
"""
Test script for the new structured report functionality.

This demonstrates the enhanced report function with validation.
"""

import sys
import os
import base64

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa
import asyncio


async def test_reports():
    """Test the new structured report functionality."""
    
    eywa.open_pipe()
    
    # Test 1: Simple markdown card report
    print("Test 1: Simple markdown card report")
    try:
        eywa.report("Daily Summary", {
            "card": "# Success!\nProcessed **1,000 records** with 0 errors."
        })
        print("✓ Markdown card report sent successfully")
    except Exception as e:
        print(f"✗ Error: {e}")
    
    # Test 2: Report with tables
    print("\nTest 2: Report with tables")
    try:
        eywa.report("Analysis Complete", {
            "tables": {
                "Processing Results": {
                    "headers": ["Category", "Count", "Status"],
                    "rows": [
                        ["Users", 800, "Complete"],
                        ["Orders", 150, "Complete"],
                        ["Errors", 5, "Review Required"]
                    ]
                },
                "Error Details": {
                    "headers": ["ID", "Type", "Message"],
                    "rows": [
                        ["001", "Validation", "Missing email"],
                        ["002", "Network", "Timeout"]
                    ]
                }
            }
        })
        print("✓ Multi-table report sent successfully")
    except Exception as e:
        print(f"✗ Error: {e}")
    
    # Test 3: Combined report with card and tables
    print("\nTest 3: Combined report with card and tables")
    try:
        eywa.report("Monthly Analysis", {
            "card": """# Monthly Report
## Overview  
All systems operational. Performance improved 15%.

**Key Achievements:**
- Zero downtime
- 15% performance improvement
- 99.9% success rate""",
            "tables": {
                "Regional Performance": {
                    "headers": ["Region", "Revenue", "Growth", "Target"],
                    "rows": [
                        ["North America", "$125K", "12%", "✅"],
                        ["Europe", "$89K", "8%", "✅"],
                        ["Asia Pacific", "$156K", "23%", "🎯"]
                    ]
                }
            }
        })
        print("✓ Combined card + tables report sent successfully")
    except Exception as e:
        print(f"✗ Error: {e}")
    
    # Test 4: Report with base64 image (create a small test image)
    print("\nTest 4: Report with base64 image")
    try:
        # Create a minimal base64 encoded "image" (just test data)
        test_image_data = "This is test image data"
        test_image_b64 = base64.b64encode(test_image_data.encode()).decode()
        
        eywa.report("Performance Chart", {
            "card": "# Performance Dashboard\nSee attached chart for details.",
            "tables": {
                "Metrics": {
                    "headers": ["Metric", "Value"],
                    "rows": [["Response Time", "95ms"], ["Throughput", "1.2K/sec"]]
                }
            }
        }, image=test_image_b64)
        print("✓ Report with base64 image sent successfully")
    except Exception as e:
        print(f"✗ Error: {e}")
    
    # Test 5: Error handling - invalid base64
    print("\nTest 5: Error handling - invalid base64")
    try:
        eywa.report("Bad Image Test", {
            "card": "This should fail"
        }, image="not-valid-base64!")
        print("✗ Should have failed with invalid base64")
    except ValueError as e:
        print(f"✓ Correctly caught invalid base64: {e}")
    except Exception as e:
        print(f"✗ Unexpected error: {e}")
    
    # Test 6: Error handling - invalid table structure
    print("\nTest 6: Error handling - invalid table structure")
    try:
        eywa.report("Bad Table Test", {
            "tables": {
                "Invalid Table": {
                    "headers": ["A", "B"],
                    "rows": [
                        ["1", "2"],
                        ["3"]  # Missing column
                    ]
                }
            }
        })
        print("✗ Should have failed with invalid table structure")
    except ValueError as e:
        print(f"✓ Correctly caught invalid table: {e}")
    except Exception as e:
        print(f"✗ Unexpected error: {e}")
    
    print("\n🎉 All tests completed!")
    
    # Get current task info
    try:
        task_info = await eywa.get_task()
        print(f"\nCurrent task: {task_info.get('euuid', 'Unknown')}")
    except Exception as e:
        print(f"Note: Could not get task info (expected in standalone test): {e}")
    
    eywa.exit(0)


if __name__ == "__main__":
    asyncio.run(test_reports())
