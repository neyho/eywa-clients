#!/usr/bin/env python3
"""
Comprehensive EYWA Task Reporting Test

This example demonstrates the enhanced structured reporting capabilities
in a realistic data processing scenario. It shows:

1. Task initialization and data retrieval
2. Progressive reporting during processing
3. Multi-table reports with rich markdown cards
4. Error handling and recovery
5. Final comprehensive reporting

Run with: eywa run --task-json '{"data": {"target": "users", "batch_size": 25}}' -c 'python examples/task_reporting_test.py'
"""

import sys
import os
import asyncio
import json
import random
import time
from datetime import datetime

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa


async def simulate_data_processing():
    """Main robot function that demonstrates comprehensive reporting."""
    
    print("🤖 Starting EYWA Task Reporting Test Robot")
    print("=" * 60)
    
    eywa.open_pipe()
    
    try:
        # Initialize task
        task = await eywa.get_task()
        input_data = task.get('data', {})
        target = input_data.get('target', 'users')
        batch_size = input_data.get('batch_size', 10)
        
        eywa.update_task(eywa.PROCESSING)
        
        # Initial report with task context
        initial_report = eywa.create_report_data(
            card=f"""# Data Processing Task Started
## Configuration
**Target:** {target}  
**Batch Size:** {batch_size}  
**Started:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

## Status
✅ Task initialized successfully  
🔄 Beginning data processing pipeline  
📊 Reporting enabled with structured output

> **Note:** This task demonstrates enhanced EYWA reporting capabilities
""",
            Configuration=eywa.create_table(
                headers=["Parameter", "Value", "Type"],
                rows=[
                    ["Target", target, "string"],
                    ["Batch Size", str(batch_size), "integer"],
                    ["Task UUID", task.get('euuid', 'N/A'), "uuid"],
                    ["Processing Mode", "Enhanced Reporting", "mode"]
                ]
            )
        )
        
        eywa.report("Task Initialization Complete", initial_report)
        eywa.info("Task initialized with enhanced reporting")
        
        # Simulate data discovery phase
        await simulate_data_discovery(target)
        
        # Simulate batch processing with progressive reporting
        await simulate_batch_processing(target, batch_size)
        
        # Generate final comprehensive report
        await generate_final_report(target, batch_size)
        
        # Close task successfully
        eywa.close_task(eywa.SUCCESS)
        
    except Exception as e:
        eywa.error(f"Task failed with error: {str(e)}")
        
        # Generate error report
        error_report = eywa.create_report_data(
            card=f"""# Task Failed ❌
## Error Details
**Error Type:** {type(e).__name__}  
**Message:** {str(e)}  
**Timestamp:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

## Recovery Actions
- Task marked as failed
- Error details logged
- Cleanup procedures initiated
- Admin notification scheduled
""",
            ErrorAnalysis=eywa.create_table(
                headers=["Aspect", "Status", "Action Required"],
                rows=[
                    ["Data Integrity", "Unknown", "Manual verification needed"],
                    ["Partial Results", "May exist", "Review processing logs"],
                    ["System State", "Stable", "No action required"],
                    ["Next Attempt", "Possible", "Fix root cause first"]
                ]
            )
        )
        
        eywa.report("Task Execution Failed", error_report)
        eywa.close_task(eywa.ERROR)


async def simulate_data_discovery(target):
    """Simulate discovering data to process."""
    
    eywa.info(f"Starting data discovery for target: {target}")
    
    # Simulate some work
    await asyncio.sleep(0.5)
    
    # Generate mock discovery results
    discovered_items = random.randint(50, 200)
    categories = ["active", "inactive", "pending", "archived"]
    category_counts = {cat: random.randint(5, 50) for cat in categories}
    
    discovery_report = eywa.create_report_data(
        card=f"""# Data Discovery Complete 🔍
## Summary
Found **{discovered_items:,} total items** for processing

### Distribution
- **Active:** {category_counts['active']} items
- **Inactive:** {category_counts['inactive']} items  
- **Pending:** {category_counts['pending']} items
- **Archived:** {category_counts['archived']} items

### Next Steps
1. Validate data integrity
2. Plan batch processing strategy
3. Initialize processing pipeline
4. Monitor progress and quality
""",
        
        Discovery=eywa.create_table(
            headers=["Category", "Count", "Percentage", "Priority"],
            rows=[
                ["Active", category_counts['active'], f"{category_counts['active']/discovered_items*100:.1f}%", "High"],
                ["Inactive", category_counts['inactive'], f"{category_counts['inactive']/discovered_items*100:.1f}%", "Medium"],
                ["Pending", category_counts['pending'], f"{category_counts['pending']/discovered_items*100:.1f}%", "High"],
                ["Archived", category_counts['archived'], f"{category_counts['archived']/discovered_items*100:.1f}%", "Low"]
            ]
        ),
        
        QualityMetrics=eywa.create_table(
            headers=["Metric", "Value", "Threshold", "Status"],
            rows=[
                ["Completeness", "94.2%", ">90%", "✅ Pass"],
                ["Validity", "97.8%", ">95%", "✅ Pass"],
                ["Consistency", "89.1%", ">85%", "✅ Pass"],
                ["Freshness", "2.3 hours", "<24h", "✅ Pass"]
            ]
        )
    )
    
    eywa.report("Data Discovery Results", discovery_report)
    return discovered_items


async def simulate_batch_processing(target, batch_size):
    """Simulate processing data in batches with progressive reporting."""
    
    total_items = random.randint(75, 150)
    batches = (total_items + batch_size - 1) // batch_size  # Ceiling division
    
    eywa.info(f"Starting batch processing: {batches} batches of {batch_size} items each")
    
    # Create dynamic progress table
    progress_table = eywa.create_table(
        headers=["Batch", "Items", "Success", "Errors", "Duration", "Status"],
        rows=[]
    )
    
    processing_start = time.time()
    total_success = 0
    total_errors = 0
    
    for batch_num in range(1, batches + 1):
        batch_start = time.time()
        
        # Simulate batch processing
        await asyncio.sleep(random.uniform(0.3, 0.8))
        
        # Generate batch results
        items_in_batch = min(batch_size, total_items - (batch_num - 1) * batch_size)
        success_count = random.randint(max(1, items_in_batch - 3), items_in_batch)
        error_count = items_in_batch - success_count
        batch_duration = time.time() - batch_start
        
        total_success += success_count
        total_errors += error_count
        
        # Add batch result to progress table
        eywa.add_table_row(progress_table, [
            f"Batch {batch_num}",
            str(items_in_batch),
            str(success_count),
            str(error_count),
            f"{batch_duration:.2f}s",
            "✅ Complete" if error_count == 0 else "⚠️ Partial"
        ])
        
        # Report progress every few batches or on completion
        if batch_num % 3 == 0 or batch_num == batches:
            progress_report = eywa.create_report_data(
                card=f"""# Batch Processing Progress 📊
## Current Status
**Completed:** {batch_num}/{batches} batches ({batch_num/batches*100:.1f}%)  
**Items Processed:** {total_success + total_errors}/{total_items}  
**Success Rate:** {total_success/(total_success + total_errors)*100:.1f}%  

### Performance
- **Average Duration:** {(time.time() - processing_start)/batch_num:.2f}s per batch
- **Throughput:** {(total_success + total_errors)/(time.time() - processing_start):.1f} items/second
- **ETA:** {((batches - batch_num) * ((time.time() - processing_start)/batch_num)):.1f}s remaining

{"🎯 **All batches complete!**" if batch_num == batches else "⏳ Processing continues..."}
""",
                Progress=progress_table
            )
            
            eywa.report(f"Processing Update - Batch {batch_num}/{batches}", progress_report)
            eywa.info(f"Completed batch {batch_num}/{batches}")
    
    return total_success, total_errors, time.time() - processing_start


async def generate_final_report(target, batch_size):
    """Generate a comprehensive final report with all results."""
    
    eywa.info("Generating final comprehensive report")
    
    # Simulate gathering final statistics
    await asyncio.sleep(0.3)
    
    # Generate summary statistics
    total_processed = random.randint(75, 150)
    success_count = random.randint(int(total_processed * 0.85), total_processed - 2)
    error_count = total_processed - success_count
    processing_duration = random.uniform(8.5, 15.2)
    
    # Performance metrics
    performance_table = eywa.create_table(
        headers=["Metric", "Value", "Target", "Performance"],
        rows=[
            ["Items Processed", f"{total_processed:,}", f"{batch_size * 5}+", "🎯 Exceeded"],
            ["Success Rate", f"{success_count/total_processed*100:.1f}%", ">95%", "✅ Met" if success_count/total_processed > 0.95 else "⚠️ Below"],
            ["Processing Time", f"{processing_duration:.1f}s", "<30s", "✅ Met"],
            ["Throughput", f"{total_processed/processing_duration:.1f} items/s", ">5 items/s", "🎯 Exceeded"],
            ["Error Rate", f"{error_count/total_processed*100:.1f}%", "<5%", "✅ Met" if error_count/total_processed < 0.05 else "⚠️ Above"]
        ]
    )
    
    # Quality breakdown
    quality_table = eywa.create_table(
        headers=["Category", "Items", "Quality Score", "Issues", "Action"],
        rows=[
            ["High Quality", str(int(success_count * 0.8)), "9.2/10", "0", "None required"],
            ["Medium Quality", str(int(success_count * 0.2)), "7.8/10", "Minor", "Monitor"],
            ["Processing Errors", str(error_count), "N/A", str(error_count), "Review logs"],
            ["Validation Passed", str(success_count - 2), "8.9/10", "2", "Reprocess"]
        ]
    )
    
    # Resource utilization
    resources_table = eywa.create_table(
        headers=["Resource", "Usage", "Peak", "Average", "Efficiency"],
        rows=[
            ["CPU", "45%", "72%", "38%", "High"],
            ["Memory", "2.1GB", "2.8GB", "1.9GB", "Optimal"],
            ["Network", "125KB/s", "890KB/s", "67KB/s", "Low"],
            ["Disk I/O", "15MB/s", "34MB/s", "12MB/s", "Medium"]
        ]
    )
    
    # Comprehensive final report
    final_report = eywa.create_report_data(
        card=f"""# 🎉 Data Processing Task Complete
## Executive Summary
Successfully processed **{total_processed:,} {target}** items in {processing_duration:.1f} seconds with a **{success_count/total_processed*100:.1f}% success rate**.

### Key Achievements
✅ **High Performance** - Exceeded throughput targets by 40%  
✅ **Quality Assurance** - Maintained data integrity throughout  
✅ **Error Recovery** - Handled {error_count} errors gracefully  
✅ **Resource Efficiency** - Optimal resource utilization  

### Processing Statistics
- **Total Duration:** {processing_duration:.1f} seconds
- **Average Throughput:** {total_processed/processing_duration:.1f} items/second
- **Batch Configuration:** {batch_size} items per batch
- **Memory Peak:** 2.8GB
- **Zero Critical Failures**

### Quality Metrics
- **Data Validation:** 98.7% passed
- **Business Rules:** 97.2% compliance  
- **Format Compliance:** 99.8% correct
- **Duplicate Detection:** 15 removed

### Next Steps
1. **Archive Results** - Move processed data to long-term storage
2. **Update Metrics** - Refresh business intelligence dashboards  
3. **Schedule Next Run** - Queue follow-up processing tasks
4. **Performance Review** - Analyze optimization opportunities

> **Recommendation:** Consider increasing batch size to {batch_size + 5} for future runs to improve throughput.

---
*Task completed successfully at {datetime.now().strftime('%Y-%m-%d %H:%M:%S')} UTC*
""",
        
        Performance=performance_table,
        Quality=quality_table,
        Resources=resources_table,
        
        Summary=eywa.create_table(
            headers=["Aspect", "Result", "Status", "Notes"],
            rows=[
                ["Data Processing", f"{total_processed} items", "Complete", "All batches processed"],
                ["Quality Check", f"{success_count} passed", "Passed", f"{error_count} items need review"],
                ["Performance", f"{total_processed/processing_duration:.1f} items/s", "Excellent", "Above target threshold"],
                ["Resource Usage", "2.8GB peak", "Optimal", "Within allocated limits"],
                ["Error Handling", f"{error_count} errors", "Managed", "All errors logged and recoverable"]
            ]
        )
    )
    
    eywa.report("Final Processing Report", final_report)
    
    # Also generate a simple summary for quick reference
    eywa.report("Quick Summary", {
        "card": f"## Task Complete ✅\n**{total_processed} items processed** with **{success_count/total_processed*100:.1f}% success rate** in {processing_duration:.1f}s"
    })
    
    eywa.info(f"Final report generated - {total_processed} items processed successfully")


if __name__ == "__main__":
    print("🚀 EYWA Task Reporting Test Robot")
    print("This robot demonstrates enhanced structured reporting capabilities")
    print()
    
    try:
        asyncio.run(simulate_data_processing())
    except KeyboardInterrupt:
        print("\n⚠️ Robot interrupted by user")
        eywa.close_task(eywa.ERROR)
    except Exception as e:
        print(f"\n❌ Robot crashed: {e}")
        eywa.close_task(eywa.ERROR)
