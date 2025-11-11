/**
 * Table Report
 * 
 * Creates a report with structured data tables
 * Great for displaying metrics, comparisons, and lists
 */

import eywa from '../../src/index.js';

async function main() {
  try {
    eywa.open_pipe();
    const task = await eywa.get_task();
    
    console.log('📊 Creating table report...');
    eywa.update_task('PROCESSING');
    
    // Create a report with data tables
    await eywa.report("Monthly Sales Summary", {
      data: {
        card: `# Sales Performance Report

## Overview
Strong performance across all regions with **18% growth** over last month.

### Key Achievements
- 🎯 All targets exceeded
- 📈 Record monthly growth
- 💰 Revenue milestones hit`,

        tables: {
          "Sales by Region": {
            headers: ["Region", "Sales", "Growth", "Status"],
            rows: [
              ["North", "$125K", "+12%", "✅ Met"],
              ["South", "$89K", "+18%", "🎯 Exceeded"], 
              ["East", "$156K", "+8%", "✅ Met"],
              ["West", "$134K", "+22%", "🚀 Exceeded"]
            ]
          }
        }
      }
    });
    
    console.log('✅ Table report created');
    eywa.info('Table report generated successfully');
    eywa.close_task('SUCCESS');
    
  } catch (error) {
    console.error('❌ Report failed:', error.message);
    eywa.error(`Table report failed: ${error.message}`);
    eywa.close_task('ERROR');
  }
}

main();
