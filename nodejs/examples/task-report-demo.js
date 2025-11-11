/**
 * EYWA Task Reports Demo
 * 
 * Demonstrates the enhanced task reporting functionality with:
 * - Markdown card formatting
 * - Named tables with structured data
 * - Base64 image attachments
 * - Automatic flag generation
 * - Error handling and validation
 */

import eywa from '../src/index.js';
import fs from 'fs';
import path from 'path';

async function runReportDemo() {
  try {
    // Open communication pipe
    eywa.open_pipe();
    
    // Get current task
    const task = await eywa.get_task();
    console.log('Current task:', task);
    
    // Demo 1: Simple markdown card report
    console.log('\n=== Demo 1: Simple Card Report ===');
    await eywa.report("Processing Complete", {
      data: {
        card: `# Success! ✅

**Records processed:** 1,000  
**Success rate:** 99.5%  
**Processing time:** 3m 45s

## Key Achievements
- Zero errors encountered
- Performance improved by 15%
- All validations passed

> **Note:** System ready for next batch`
      }
    });
    console.log('✅ Simple card report created');

    // Demo 2: Multi-table report with comprehensive data
    console.log('\n=== Demo 2: Multi-Table Report ===');
    await eywa.report("Monthly Analysis Report", {
      data: {
        card: `# Monthly Performance Summary

## Overview
All systems operational. Performance exceeded targets by **15%**.

### Key Metrics
- **Uptime:** 99.99%
- **Transactions:** 45,230
- **Revenue:** $123,456
- **Error Rate:** 0.01%

### Notable Achievements
- 🎯 Exceeded performance targets
- 🔧 Zero critical incidents
- 📈 Revenue growth of 12%

### Next Steps
1. Review error patterns
2. Optimize peak-hour performance  
3. Plan capacity expansion`,

        tables: {
          "Regional Performance": {
            headers: ["Region", "Revenue", "Growth %", "Target Met"],
            rows: [
              ["North America", "$125,000", "12%", "✅"],
              ["Europe", "$89,000", "8%", "✅"],
              ["Asia Pacific", "$156,000", "23%", "🎯"],
              ["Latin America", "$45,000", "5%", "⚠️"]
            ]
          },
          "System Health": {
            headers: ["Service", "Uptime %", "Response Time", "Status"],
            rows: [
              ["API Gateway", "99.9%", "85ms", "Healthy"],
              ["Database", "100%", "12ms", "Healthy"],
              ["Cache Layer", "99.8%", "3ms", "Healthy"],
              ["File Storage", "99.95%", "150ms", "Healthy"]
            ]
          },
          "Error Analysis": {
            headers: ["Error Type", "Count", "Impact", "Resolution"],
            rows: [
              ["Timeout", 12, "Low", "Auto-retry"],
              ["Validation", 5, "Medium", "Manual review"],
              ["Network", 3, "Low", "Resolved"]
            ]
          }
        }
      }
    });
    console.log('✅ Multi-table report created');

    // Demo 3: Report with base64 image (simulated)
    console.log('\n=== Demo 3: Report with Chart Image ===');
    
    // Create a simple base64 encoded image (1x1 pixel PNG)
    const sampleImage = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    
    await eywa.report("Performance Analysis with Chart", {
      data: {
        card: `# Performance Visualization

This report includes a performance chart showing trends over the past month.

## Key Insights from Chart
- **Peak Performance:** 15:30 daily
- **Lowest Activity:** 03:00-05:00  
- **Growth Trend:** +15% month-over-month
- **Optimization Opportunity:** Morning hours

The attached chart shows detailed performance metrics across all monitored systems.`,
        
        tables: {
          "Chart Data Summary": {
            headers: ["Metric", "Current", "Previous Month", "Change"],
            rows: [
              ["Avg Response Time", "95ms", "110ms", "-13.6%"],
              ["Throughput", "1,200 req/s", "1,050 req/s", "+14.3%"],
              ["Error Rate", "0.01%", "0.02%", "-50%"],
              ["Uptime", "99.99%", "99.95%", "+0.04%"]
            ]
          }
        }
      },
      image: sampleImage
    });
    console.log('✅ Report with image created');

    // Demo 4: Table-only report
    console.log('\n=== Demo 4: Table-Only Report ===');
    await eywa.report("Database Performance Metrics", {
      data: {
        tables: {
          "Query Performance": {
            headers: ["Query Type", "Count", "Avg Duration", "Max Duration", "Optimization"],
            rows: [
              ["SELECT", "12,450", "15ms", "230ms", "Add index on user_id"],
              ["INSERT", "3,210", "8ms", "45ms", "Batch inserts"],
              ["UPDATE", "890", "12ms", "67ms", "Optimize WHERE clauses"],
              ["DELETE", "156", "5ms", "23ms", "Cascade deletes"]
            ]
          },
          "Connection Pool": {
            headers: ["Pool", "Active", "Idle", "Max", "Wait Time"],
            rows: [
              ["Primary", "15", "5", "20", "2ms"],
              ["Read Replica", "8", "12", "20", "1ms"],
              ["Analytics", "3", "17", "20", "5ms"]
            ]
          }
        }
      }
    });
    console.log('✅ Table-only report created');

    // Demo 5: Report with metadata
    console.log('\n=== Demo 5: Report with Metadata ===');
    await eywa.report("System Backup Report", {
      data: {
        card: `# Backup Operation Completed

Automated backup process completed successfully at ${new Date().toISOString()}.

## Backup Summary
- **Database size:** 2.3 GB
- **Files backed up:** 45,230
- **Compression ratio:** 65%
- **Storage location:** S3 Bucket
- **Retention:** 30 days

All critical data has been safely archived.`
      },
      metadata: {
        backup_id: "backup_" + Date.now(),
        system: "production",
        version: "1.2.3",
        operator: "automated_system",
        storage_used_mb: 1495,
        compression_ratio: 0.65
      }
    });
    console.log('✅ Report with metadata created');

    console.log('\n🎉 All report demos completed successfully!');
    
    // Close task
    eywa.close_task('SUCCESS');
    
  } catch (error) {
    console.error('❌ Demo failed:', error.message);
    eywa.error('Report demo failed', { error: error.message });
    eywa.close_task('ERROR');
  }
}

// Demo error handling
async function runErrorDemo() {
  try {
    console.log('\n=== Error Handling Demo ===');
    
    // Test invalid base64
    try {
      await eywa.report("Invalid Image Test", {
        data: { card: "# Test" },
        image: "invalid-base64-data"
      });
    } catch (error) {
      console.log('✅ Caught invalid base64 error:', error.message);
    }
    
    // Test invalid table structure
    try {
      await eywa.report("Invalid Table Test", {
        data: {
          tables: {
            "Bad Table": {
              headers: ["Col1", "Col2"],
              rows: [
                ["Value1"], // Missing column
                ["Value2", "Value3", "ExtraValue"] // Too many columns
              ]
            }
          }
        }
      });
    } catch (error) {
      console.log('✅ Caught table validation error:', error.message);
    }
    
  } catch (error) {
    console.error('Error demo failed:', error);
  }
}

// Run demos
runReportDemo();
// Uncomment to test error handling:
// runErrorDemo();
