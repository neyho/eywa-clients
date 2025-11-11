/**
 * Dynamic Report Test Robot
 * 
 * Uses task input data to generate different types of reports
 * Demonstrates how to build reports based on task parameters
 */

import eywa from '../src/index.js';

async function main() {
  try {
    // Open communication pipe
    eywa.open_pipe();
    
    // Get task with input data
    const task = await eywa.get_task();
    const { reportType, title, ...data } = task.data;
    
    console.log(`🤖 Robot starting: ${reportType} report`);
    eywa.info(`Generating ${reportType} report: ${title}`);
    
    eywa.update_task('PROCESSING');
    
    // Generate report based on type
    switch (reportType) {
      case 'card':
        await generateCardReport(title, data);
        break;
        
      case 'table':
        await generateTableReport(title, data);
        break;
        
      case 'complete':
        await generateCompleteReport(title, data);
        break;
        
      case 'system-status':
        await generateSystemStatusReport(title, data);
        break;
        
      default:
        throw new Error(`Unknown report type: ${reportType}`);
    }
    
    eywa.info('Report generation completed successfully');
    eywa.close_task('SUCCESS');
    
  } catch (error) {
    console.error('❌ Robot failed:', error.message);
    eywa.error(`Report generation failed: ${error.message}`);
    eywa.close_task('ERROR');
  }
}

async function generateCardReport(title, data) {
  const { recordsProcessed, duration, errorRate } = data;
  
  await eywa.report(title, {
    data: {
      card: `# Success! ✅

**Records processed:** ${recordsProcessed.toLocaleString()}  
**Duration:** ${duration}  
**Error rate:** ${errorRate}%

${errorRate === 0 ? '🎯 Perfect execution with zero errors!' : '⚠️ Some errors encountered'}

## Summary
${recordsProcessed > 1000 
  ? 'High volume processing completed successfully.' 
  : 'Standard processing completed.'}

> **Status:** All operations completed within expected parameters.`
    }
  });
  
  console.log('✅ Card report generated');
}

async function generateTableReport(title, data) {
  const { regions, totalGrowth } = data;
  
  // Simulate sales data based on regions
  const salesData = regions.map(region => {
    const sales = Math.floor(Math.random() * 100000 + 50000);
    const growth = Math.floor(Math.random() * 20 + 5);
    const target = growth > 15 ? 'Exceeded' : growth > 10 ? 'Met' : 'Below';
    
    return [region, `$${(sales/1000).toFixed(0)}K`, `+${growth}%`, target];
  });
  
  await eywa.report(title, {
    data: {
      card: `# Monthly Sales Report

## Overview
Strong performance across all regions with **${totalGrowth}** growth over last month.

### Highlights
- 🎯 Most regions exceeded targets
- 📈 Consistent growth trend
- 💰 Revenue targets achieved
- 🌟 Outstanding team performance

### Next Steps
1. Analyze top-performing regions
2. Support underperforming areas
3. Scale successful strategies`,

      tables: {
        "Sales by Region": {
          headers: ["Region", "Sales", "Growth", "Target"],
          rows: salesData
        }
      }
    }
  });
  
  console.log('✅ Table report generated');
}

async function generateCompleteReport(title, data) {
  const { systemMetrics, includeChart } = data;
  
  // Create base64 image (1x1 pixel PNG for demo)
  const chartBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
  
  await eywa.report(title, {
    data: {
      card: `# Performance Visualization

This report includes comprehensive performance metrics and trends.

## Key Insights from Analysis
- **Peak Performance:** 15:30 daily
- **Lowest Activity:** 03:00-05:00  
- **Growth Trend:** +15% month-over-month
- **Optimization Opportunity:** Morning hours

${includeChart ? 'The attached chart shows detailed performance metrics across all monitored systems.' : ''}`,
      
      tables: {
        "Performance Metrics": {
          headers: ["Metric", "Current", "Previous Month", "Change"],
          rows: [
            ["Avg Response Time", systemMetrics.avgResponseTime, "110ms", "-13.6%"],
            ["Throughput", systemMetrics.throughput, "1,050 req/s", "+14.3%"],
            ["Error Rate", systemMetrics.errorRate, "0.02%", "-50%"],
            ["Uptime", systemMetrics.uptime, "99.95%", "+0.04%"]
          ]
        }
      }
    },
    image: includeChart ? chartBase64 : undefined
  });
  
  console.log('✅ Complete report with chart generated');
}

async function generateSystemStatusReport(title, data) {
  const { services } = data;
  
  const serviceRows = services.map(service => [
    service.name,
    service.status,
    service.uptime,
    service.responseTime
  ]);
  
  await eywa.report(title, {
    data: {
      tables: {
        "Service Health": {
          headers: ["Service", "Status", "Uptime", "Response Time"],
          rows: serviceRows
        },
        "System Summary": {
          headers: ["Metric", "Value"],
          rows: [
            ["Total Services", services.length.toString()],
            ["Healthy Services", services.filter(s => s.status === 'Healthy').length.toString()],
            ["Warning Services", services.filter(s => s.status === 'Warning').length.toString()],
            ["Critical Services", services.filter(s => s.status === 'Critical').length.toString()],
            ["Overall Health", services.every(s => s.status === 'Healthy') ? '✅ All Healthy' : '⚠️ Some Issues']
          ]
        }
      }
    }
  });
  
  console.log('✅ System status report generated');
}

// Run the robot
main();
