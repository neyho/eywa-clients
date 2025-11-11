#!/usr/bin/env python3
"""
Test script for the new structured report functionality with helper functions.
"""

import sys
import os
import tempfile

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa
import asyncio


async def test_helper_functions():
    """Test the new helper functions for easier report construction."""
    
    eywa.open_pipe()
    
    print("Testing Helper Functions for Structured Reports")
    print("=" * 50)
    
    # Test 1: create_table helper
    print("Test 1: create_table helper function")
    try:
        results_table = eywa.create_table(
            headers=["Category", "Count", "Success Rate", "Status"],
            rows=[
                ["Users", 1000, "99.2%", "✅"],
                ["Orders", 750, "98.8%", "✅"],
                ["Payments", 650, "99.9%", "✅"],
                ["Errors", 8, "0.8%", "⚠️"]
            ]
        )
        print("✓ Table created successfully")
        print(f"  Headers: {results_table['headers']}")
        print(f"  Row count: {len(results_table['rows'])}")
    except Exception as e:
        print(f"✗ Error creating table: {e}")
        return
    
    # Test 2: create_report_data with multiple tables
    print("\\nTest 2: create_report_data with multiple tables")
    try:
        performance_table = eywa.create_table(
            headers=["Metric", "Value", "Target", "Status"],
            rows=[
                ["Response Time", "95ms", "<100ms", "✅"],
                ["Throughput", "1,200/sec", ">1,000/sec", "✅"],
                ["Error Rate", "0.1%", "<0.5%", "✅"],
                ["Uptime", "99.98%", ">99.9%", "✅"]
            ]
        )
        
        report_data = eywa.create_report_data(
            card="# Daily Operations Report\\n## Status: All Systems Operational ✅",
            Performance=performance_table,
            Results=results_table
        )
        
        eywa.report("Daily Operations Report", report_data)
        print("✓ Multi-table report with card sent successfully")
        print(f"  Tables included: {list(report_data['tables'].keys())}")
        
    except Exception as e:
        print(f"✗ Error creating multi-table report: {e}")
    
    # Test 3: Dynamic table building
    print("\\nTest 3: Dynamic table building with add_table_row") 
    try:
        dynamic_table = eywa.create_table(
            headers=["Process", "Duration", "Memory", "CPU", "Status"],
            rows=[]  # Start empty
        )
        
        processes = [
            ["Web Server", "2h 15m", "245MB", "15%", "Running"],
            ["Database", "5h 42m", "1.2GB", "8%", "Running"],
            ["Cache", "1h 33m", "89MB", "3%", "Running"]
        ]
        
        for process_data in processes:
            eywa.add_table_row(dynamic_table, process_data)
        
        process_report = eywa.create_report_data(
            card="# System Process Monitor\\nReal-time process status overview.",
            Processes=dynamic_table
        )
        
        eywa.report("Process Monitor", process_report)
        print("✓ Dynamic table report sent successfully")
        print(f"  Final row count: {len(dynamic_table['rows'])}")
        
    except Exception as e:
        print(f"✗ Error with dynamic table: {e}")
    
    # Test 4: Image encoding
    print("\\nTest 4: Image encoding with encode_image_file")
    try:
        # Create a temporary file for testing
        with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False) as temp_file:
            temp_file.write("This simulates image data for testing purposes.")
            temp_file_path = temp_file.name
        
        try:
            encoded_image = eywa.encode_image_file(temp_file_path)
            
            chart_report = eywa.create_report_data(
                card="# Performance Dashboard\\nSee attached chart for details.",
                Metrics=eywa.create_table(
                    headers=["Time", "CPU %", "Memory %", "Disk I/O"],
                    rows=[
                        ["08:00", "25%", "67%", "120 MB/s"],
                        ["12:00", "78%", "82%", "340 MB/s"],
                        ["16:00", "65%", "75%", "280 MB/s"]
                    ]
                )
            )
            
            eywa.report("Performance Dashboard", chart_report, image=encoded_image)
            print("✓ Report with encoded image sent successfully")
            print(f"  Image data length: {len(encoded_image)} characters")
            
        finally:
            os.unlink(temp_file_path)
            
    except Exception as e:
        print(f"✗ Error with image encoding: {e}")
    
    # Test 5: Error handling
    print("\\nTest 5: Error handling in helper functions")
    
    # Test invalid table creation
    try:
        eywa.create_table(["A", "B"], [["1", "2"], ["3"]])  # Mismatched columns
        print("✗ Should have failed with mismatched columns")
    except ValueError as e:
        print(f"✓ Correctly caught table error: {str(e)[:50]}...")
    
    # Test invalid file encoding
    try:
        eywa.encode_image_file("/nonexistent/file.png")
        print("✗ Should have failed with missing file")
    except ValueError as e:
        print(f"✓ Correctly caught file error: {str(e)[:50]}...")
    
    print("\\n🎉 All helper function tests completed successfully!")
    
    print("\\nSummary of new capabilities:")
    print("• create_table() - Easy table construction")
    print("• create_report_data() - Structured report building")
    print("• add_table_row() - Dynamic table updates")
    print("• encode_image_file() - Simple image encoding")
    print("• Enhanced validation - Comprehensive error checking")
    
    try:
        task_info = await eywa.get_task()
        print(f"\\nTask: {task_info.get('euuid', 'Unknown')}")
    except Exception as e:
        print("\\nNote: Running in standalone test mode")
    
    eywa.exit(0)


if __name__ == "__main__":
    asyncio.run(test_helper_functions())
