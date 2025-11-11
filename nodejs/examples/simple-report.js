/**
 * Simple Task Report Example
 * 
 * Basic examples of the new structured reporting functionality
 */

import eywa from 'eywa-client';

// Example 1: Simple success report with markdown
await eywa.report("Processing Complete", {
  data: {
    card: `# Success! ✅
    
**Records processed:** 1,500  
**Duration:** 2m 30s  
**Error rate:** 0%

All items processed successfully.`
  }
});

// Example 2: Business report with tables
await eywa.report("Sales Summary", {
  data: {
    card: `# Monthly Sales Report
    
## Overview
Strong performance across all regions with **15% growth** over last month.`,
    
    tables: {
      "Sales by Region": {
        headers: ["Region", "Sales", "Growth", "Target"],
        rows: [
          ["North", "$125K", "+12%", "Met"],
          ["South", "$89K", "+18%", "Exceeded"],
          ["East", "$156K", "+8%", "Met"],
          ["West", "$134K", "+22%", "Exceeded"]
        ]
      }
    }
  }
});

// Example 3: Report with chart/image
const chartBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

await eywa.report("Performance Analysis", {
  data: {
    card: "# Performance Report\n\nSee attached chart for detailed analysis."
  },
  image: chartBase64
});

// Example 4: Tables-only report
await eywa.report("System Status", {
  data: {
    tables: {
      "Service Health": {
        headers: ["Service", "Status", "Uptime", "Response Time"],
        rows: [
          ["API", "Healthy", "99.9%", "45ms"],
          ["Database", "Healthy", "100%", "8ms"],
          ["Cache", "Warning", "98.5%", "120ms"]
        ]
      }
    }
  }
});
