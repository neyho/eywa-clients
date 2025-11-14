/**
 * Cleanup Script for File Examples
 * 
 * Removes any lingering test resources from file operation examples.
 * Useful if examples fail or are interrupted.
 */

import eywa, { 
  deleteFile, 
  deleteFolder,
  list,
  listFolders
} from '../src/index.js';

async function cleanup() {
  try {
    eywa.open_pipe();
    console.log('🧹 Starting cleanup of test resources...');

    // Find and delete demo files
    console.log('\\n1. Cleaning up demo files...');
    const files = await list({ 
      name: 'demo-',
      limit: 50 
    });
    
    console.log(`Found ${files.length} demo files to clean up`);
    for (const file of files) {
      try {
        await deleteFile(file.euuid);
        console.log(`  ✅ Deleted file: ${file.name}`);
      } catch (error) {
        console.warn(`  ⚠️  Failed to delete file ${file.name}: ${error.message}`);
      }
    }

    // Find and delete demo folders
    console.log('\\n2. Cleaning up demo folders...');
    const folders = await listFolders({ 
      name: 'demo',
      limit: 20 
    });
    
    console.log(`Found ${folders.length} demo folders to clean up`);
    for (const folder of folders) {
      try {
        await deleteFolder(folder.euuid);
        console.log(`  ✅ Deleted folder: ${folder.name}`);
      } catch (error) {
        console.warn(`  ⚠️  Failed to delete folder ${folder.name}: ${error.message}`);
      }
    }

    // Clean up test files with common test names
    console.log('\\n3. Cleaning up common test files...');
    const testFileNames = ['test-data.json', 'hello.txt', 'test.txt'];
    
    for (const fileName of testFileNames) {
      const testFiles = await list({ name: fileName, limit: 10 });
      for (const file of testFiles) {
        try {
          await deleteFile(file.euuid);
          console.log(`  ✅ Deleted test file: ${file.name}`);
        } catch (error) {
          console.warn(`  ⚠️  Failed to delete test file ${file.name}: ${error.message}`);
        }
      }
    }

    console.log('\\n✨ Cleanup completed!');
    eywa.close_task('SUCCESS');

  } catch (error) {
    console.error('❌ Cleanup failed:', error.message);
    eywa.error(`Cleanup failed: ${error.message}`);
    eywa.close_task('ERROR');
  }
}

cleanup();