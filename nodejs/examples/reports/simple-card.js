/**
 * Simple Card Report
 * 
 * Creates a basic markdown report card
 * Perfect for status updates and simple metrics
 */

import eywa from '../../src/index.js';

async function main() {
  try {
    eywa.open_pipe();
    const task = await eywa.get_task();
    
    console.log('📊 Creating simple card report...');
    eywa.update_task('PROCESSING');
    
    // Create a simple card report
    await eywa.report("Processing Complete", {
      data: {
        card: `# Success! ✅

**Records processed:** 1,500  
**Duration:** 2m 30s  
**Error rate:** 0%

## Summary
All items processed successfully with zero errors.

> **Next step:** Ready for validation phase`
      }
    });
    
    console.log('✅ Card report created');
    eywa.info('Simple card report generated successfully');
    eywa.close_task('SUCCESS');
    
  } catch (error) {
    console.error('❌ Report failed:', error.message);
    eywa.error(`Card report failed: ${error.message}`);
    eywa.close_task('ERROR');
  }
}

main();
