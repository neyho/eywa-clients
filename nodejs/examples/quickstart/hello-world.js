/**
 * Hello World - Your First EYWA Robot
 * 
 * The simplest possible EYWA robot that demonstrates:
 * - Opening communication pipe
 * - Getting task information  
 * - Logging messages
 * - Closing the task successfully
 */

import eywa from '../../src/index.js';

async function main() {
  try {
    // 1. Open communication with EYWA
    eywa.open_pipe();
    console.log('🔗 Connected to EYWA');
    
    // 2. Get the current task
    const task = await eywa.get_task();
    console.log('📋 Task received:', task.euuid);
    
    // 3. Update task status and log progress
    eywa.update_task('PROCESSING');
    eywa.info('Hello World robot started!');
    
    // 4. Do some work (simulate processing)
    console.log('🔄 Processing...');
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    // 5. Log success and close task
    eywa.info('Processing completed successfully');
    eywa.close_task('SUCCESS');
    console.log('✅ Task completed');
    
  } catch (error) {
    console.error('❌ Error:', error.message);
    eywa.error(`Robot failed: ${error.message}`);
    eywa.close_task('ERROR');
  }
}

main();
