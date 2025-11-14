/**
 * Basic File Operations
 * 
 * Shows essential EYWA file operations:
 * - Upload files and content
 * - Download files
 * - Create folders
 * - List and query files
 */

import eywa, {
  upload,
  uploadContent,
  download,
  createFolder,
  deleteFile,
  deleteFolder,
  list,
  rootUuid
} from '../../src/index.js';
import { promises as fs } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';

async function main() {
  try {
    eywa.open_pipe();
    console.log('📁 Running basic file operations...');

    eywa.update_task('PROCESSING');

    // 1. Create a folder
    console.log('\\n1. Creating folder...');
    const folder = await createFolder({
      name: 'test-folder',
      parent: { euuid: rootUuid }
    });
    console.log(`✅ Created folder: ${folder.name}`);

    // 2. Upload content from memory
    console.log('\\n2. Uploading JSON content...');
    const jsonContent = JSON.stringify({
      message: 'Hello EYWA!',
      timestamp: Date.now()
    }, null, 2);

    await uploadContent(jsonContent, {
      name: 'test-data.json',
      content_type: 'application/json',
      folder: { euuid: folder.euuid }
    });
    console.log('✅ JSON content uploaded');

    // 3. Upload a file
    console.log('\\n3. Uploading text file...');
    const tempFile = join(tmpdir(), 'test.txt');
    await fs.writeFile(tempFile, 'Hello from EYWA file operations!');

    await upload(tempFile, {
      name: 'hello.txt',
      folder: { euuid: folder.euuid }
    });
    console.log('✅ Text file uploaded');

    // 4. List files in the folder
    console.log('\\n4. Listing files...');
    const files = await list({
      folder: { euuid: folder.euuid },
      limit: 10
    });
    console.log(`Found ${files.length} files:`);
    files.forEach(file => {
      console.log(`  - ${file.name} (${file.size} bytes)`);
    });

    // 5. Download a file
    if (files.length > 0) {
      console.log('\\n5. Downloading first file...');
      const fileContent = await download(files[0].euuid);
      console.log(`✅ Downloaded ${fileContent.length} bytes`);

      // Show content for text files
      if (files[0].content_type?.startsWith('text/') ||
        files[0].content_type === 'application/json') {
        console.log(`Content preview: ${fileContent.toString('utf8').slice(0, 100)}...`);
      }
    }

    // 6. Use GraphQL for advanced queries
    console.log('\\n6. GraphQL file search...');
    const searchResult = await eywa.graphql(`
      query SearchFiles($folderUuid: UUID!) {
        searchFile(
          _order_by: { uploaded_at: desc }
        ) {
          name
          size
          content_type
          uploaded_at
          folder (_where:{euuid: {_eq: $folderUuid}}) {
            path
          }
        }
      }
    `, { folderUuid: folder.euuid });

    const foundFiles = searchResult.data?.searchFile || [];
    console.log(`GraphQL found ${foundFiles.length} files`);

    // 7. Cleanup
    console.log('\\n7. Cleaning up...');
    for (const file of files) {
      await deleteFile(file.euuid);
    }
    await deleteFolder(folder.euuid);
    await fs.unlink(tempFile).catch(() => { }); // Ignore errors
    console.log('✅ Cleanup completed');

    eywa.info('Basic file operations completed successfully');
    eywa.close_task('SUCCESS');

  } catch (error) {
    console.error('❌ File operations error:', error.message);
    eywa.error(`File operations failed: ${error.message}`);
    eywa.close_task('ERROR');
  }
}

main();
