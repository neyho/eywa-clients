#!/usr/bin/env python3
"""
Quick EYWA Report Test

Simple test of the new structured reporting functionality.
Tests basic card, table, and image reporting.

Run with: eywa run -c 'python examples/quick_report_test.py'
"""

import sys
import os
import asyncio
import base64
import tempfile
from datetime import datetime

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa


async def quick_test():
    """Quick test of reporting functionality."""
    
    print("🧪 Quick EYWA Report Test")
    print("=" * 40)
    
    eywa.open_pipe()
    
    try:
        # Get task info if available
        try:
            task = await eywa.get_task()
            task_id = task.get('euuid', 'test-task')
            eywa.info(f"Testing with task: {task_id}")
        except:
            eywa.info("Running in standalone test mode")
        
        # Test 1: Simple card report
        print("\n1. Testing markdown card report...")
        eywa.report("Test 1 - Card Report", {
            "card": """# Quick Test Report
## Status: Testing ✅

This is a **markdown card** with:
- *Italic text*
- **Bold text**  
- `Code snippets`
- [Links](https://example.com)

### Results
All basic formatting working correctly!
"""
        })
        print("✅ Card report sent")
        
        # Test 2: Table report
        print("\n2. Testing table report...")
        results_table = eywa.create_table(
            headers=["Test", "Status", "Duration", "Result"],
            rows=[
                ["Import Test", "✅ Pass", "0.1s", "Success"],
                ["Function Test", "✅ Pass", "0.2s", "Success"],
                ["Validation Test", "✅ Pass", "0.1s", "Success"],
                ["Integration Test", "⏳ Running", "1.2s", "In Progress"]
            ]
        )
        
        test_data = eywa.create_report_data(
            card="## Test Results\nAll core functionality tests passing.",
            Results=results_table
        )
        
        eywa.report("Test 2 - Table Report", test_data)
        print("✅ Table report sent")
        
        # Test 3: Combined report with multiple tables
        print("\n3. Testing multi-table report...")
        
        # Add final result to table
        eywa.add_table_row(results_table, ["Integration Test", "✅ Pass", "1.2s", "Success"])
        
        performance_table = eywa.create_table(
            headers=["Metric", "Value", "Status"],
            rows=[
                ["Response Time", "< 0.5s", "🎯 Excellent"],
                ["Memory Usage", "2.1 MB", "✅ Normal"],
                ["CPU Usage", "12%", "✅ Low"],
                ["Error Rate", "0%", "🎯 Perfect"]
            ]
        )
        
        complete_report = eywa.create_report_data(
            card=f"""# 🎉 All Tests Complete!
## Summary
Completed testing of enhanced EYWA reporting at **{datetime.now().strftime('%H:%M:%S')}**

### Key Features Tested
✅ **Markdown Cards** - Rich text formatting  
✅ **Structured Tables** - Tabular data display  
✅ **Helper Functions** - Easy report construction  
✅ **Multi-Table Reports** - Complex data organization  

### Performance
- **Total Tests:** 4  
- **Success Rate:** 100%  
- **Duration:** ~2 seconds  
- **Memory Efficient:** Minimal overhead

> **Conclusion:** Enhanced reporting system working perfectly! 🚀
""",
            TestResults=results_table,
            Performance=performance_table
        )
        
        eywa.report("Test 3 - Complete Report", complete_report)
        print("✅ Multi-table report sent")
        
        # Test 4: Report with image
        print("\n4. Testing report with image...")
        
        # Create a simple test image (text file as example)
        with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False) as temp_file:
            temp_file.write("This represents image data for testing")
            temp_path = temp_file.name
        
        try:
            # Encode the "image"
            image_data = eywa.encode_image_file(temp_path)
            
            image_report = eywa.create_report_data(
                card="""# 📸 Image Test Report
## Image Processing Complete

Successfully attached encoded image data to report.

### Image Details
- **Format:** Base64 encoded
- **Size:** Small test image
- **Content:** Sample data for validation
- **Status:** ✅ Successfully attached
""",
                ImageInfo=eywa.create_table(
                    headers=["Property", "Value"],
                    rows=[
                        ["Encoding", "Base64"],
                        ["Length", f"{len(image_data)} characters"],
                        ["Type", "Test data"],
                        ["Status", "Valid"]
                    ]
                )
            )
            
            eywa.report("Test 4 - Image Report", image_report, image=image_data)
            print("✅ Image report sent")
            
        finally:
            # Clean up temp file
            os.unlink(temp_path)
        
        # Final summary
        print("\n5. Sending final summary...")
        
        final_summary = eywa.create_report_data(
            card=f"""# ✨ Quick Test Summary
## All Tests Completed Successfully!

**Timestamp:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

### Tests Performed
1. ✅ **Markdown Card** - Rich text formatting
2. ✅ **Table Creation** - Structured data
3. ✅ **Multi-Table Report** - Complex layouts  
4. ✅ **Image Encoding** - Binary data support

### System Status
- **Reporting:** Fully functional
- **Helper Functions:** Working correctly
- **Validation:** All checks passed
- **Performance:** Optimal

**Ready for production use! 🚀**
""",
            
            TestSummary=eywa.create_table(
                headers=["Component", "Status", "Notes"],
                rows=[
                    ["Card Rendering", "✅ Pass", "Markdown formatting works"],
                    ["Table Creation", "✅ Pass", "Headers and rows validated"],
                    ["Multi-Table", "✅ Pass", "Multiple named tables supported"],
                    ["Image Encoding", "✅ Pass", "Base64 encoding functional"],
                    ["Helper Functions", "✅ Pass", "All utilities working"],
                    ["Error Handling", "✅ Pass", "Graceful validation"]
                ]
            )
        )
        
        eywa.report("Final Summary - All Tests Complete", final_summary)
        
        # Close task
        eywa.info("All tests completed successfully!")
        print("\n🎉 All tests completed successfully!")
        print("Check your EYWA UI or logs to see the structured reports.")
        
        eywa.close_task(eywa.SUCCESS)
        
    except Exception as e:
        print(f"\n❌ Test failed: {e}")
        eywa.error(f"Quick test failed: {str(e)}")
        eywa.close_task(eywa.ERROR)


if __name__ == "__main__":
    asyncio.run(quick_test())
