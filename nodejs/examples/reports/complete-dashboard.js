/**
 * Complete Dashboard Report
 * 
 * Comprehensive report with card, tables, and image
 * Showcases all reporting features together
 */

import eywa from '../../src/index.js';

async function main() {
  try {
    eywa.open_pipe();
    const task = await eywa.get_task();
    
    console.log('📊 Creating complete dashboard report...');
    eywa.update_task('PROCESSING');
    
    // Sample base64 image (1x1 pixel PNG for demo)
    const chartImage = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    
    // Create comprehensive dashboard report
    await eywa.report("Performance Dashboard", {
      data: {
        card: `# System Performance Dashboard

## Executive Summary
All systems operating at **optimal performance** with record-breaking metrics.

### 🎯 Key Highlights
- **Uptime:** 99.99% (Record high)
- **Performance:** +23% improvement
- **User Satisfaction:** 98.5%
- **Response Time:** 45ms average

### 📈 Trends
- Consistent growth over 6 months
- Zero critical incidents this quarter
- Infrastructure scaling successful

### 🔮 Outlook
System ready for projected 40% traffic increase.`,

        tables: {
          "System Metrics": {
            headers: ["Metric", "Current", "Target", "Status"],
            rows: [
              ["Response Time", "45ms", "< 100ms", "🟢 Excellent"],
              ["Uptime", "99.99%", "> 99.9%", "🟢 Excellent"],
              ["Error Rate", "0.001%", "< 0.1%", "🟢 Excellent"],
              ["CPU Usage", "35%", "< 80%", "🟢 Healthy"]
            ]
          },
          "Regional Performance": {
            headers: ["Region", "Users", "Avg Response", "Health"],
            rows: [
              ["North America", "45,230", "42ms", "🟢"],
              ["Europe", "38,450", "48ms", "🟢"],
              ["Asia Pacific", "52,100", "46ms", "🟢"],
              ["Latin America", "12,800", "51ms", "🟢"]
            ]
          }
        }
      },
      image: chartImage,
      metadata: {
        dashboard_version: "v2.1",
        generated_by: "performance_monitor",
        data_freshness: "real-time"
      }
    });
    
    console.log('✅ Complete dashboard report created');
    eywa.info('Dashboard report with all features generated');
    eywa.close_task('SUCCESS');
    
  } catch (error) {
    console.error('❌ Dashboard report failed:', error.message);
    eywa.error(`Dashboard report failed: ${error.message}`);
    eywa.close_task('ERROR');
  }
}

main();
