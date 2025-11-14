/**
 * EYWA Files - Simplified API Demo (Idempotent)
 * 
 * Demonstrates the simplified approach with idempotent operations:
 * - Protocol abstraction for upload/download  
 * - Simple CRUD mutations
 * - Direct GraphQL for queries/verification
 * - Pre-defined UUIDs for repeatable operations
 * 
 * Usage: eywa run -c "node examples/simple-files-demo.js"
 * 
 * Note: Can be run multiple times safely - uses constant UUIDs.
 */

import eywa, {
  upload,
  uploadContent,
  download,
  downloadStream,
  createFolder,
  deleteFile,
  deleteFolder,
  list,
  fileInfo,
  rootUuid,
  rootFolder,
  FileUploadError,
  FileDownloadError
} from '../src/index.js';
import { createWriteStream, promises as fs } from 'fs';
import { createReadStream } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';
import { randomUUID } from 'crypto';

// Pre-defined UUIDs for idempotent operations
const DEMO_FOLDER_UUID = '9bd6fe99-7540-4a54-9998-138405ea8d2c';
const SAMPLE_FILE_UUID = '3f0f4173-4ef7-4499-857e-37568adeab48';
const JSON_FILE_UUID = 'ea0fee9a-30d9-4aae-b087-10bce969af57';

class SimplifiedFilesDemo {
  constructor() {
    this.testResources = [];
  }

  async cleanup() {
    console.log('🧹 Cleaning up test resources...');

    // Delete files first, then folders
    for (const resource of this.testResources) {
      try {
        if (resource.type === 'file') {
          await deleteFile(resource.uuid);
          console.log(`Deleted file: ${resource.uuid}`);
        } else if (resource.type === 'folder') {
          await deleteFolder(resource.uuid);
          console.log(`Deleted folder: ${resource.uuid}`);
        }
      } catch (error) {
        console.warn(`Failed to delete ${resource.type} ${resource.uuid}: ${error.message}`);
      }
    }
  }

  trackResource(resourceType, resourceUuid, name) {
    this.testResources.push({
      type: resourceType,
      uuid: resourceUuid,
      name: name
    });
  }

  async demoFolderOperations() {
    console.log('\n📁 DEMO: Folder Operations');

    // Create test folder with predefined UUID
    const folderUuid = DEMO_FOLDER_UUID;
    this.trackResource('folder', folderUuid, 'demo-folder');

    console.log('Creating folder with client-controlled UUID...');
    const folder = await createFolder({
      euuid: folderUuid,
      name: 'demo-folder',
      parent: { euuid: rootUuid }
    });

    console.log(`✅ Created folder: ${folder.name} at ${folder.path}`);

    // Verify with direct GraphQL
    console.log('Verifying folder creation with GraphQL...');
    const verification = await eywa.graphql(`
      query GetFolder($uuid: UUID!) {
        getFolder(euuid: $uuid) {
          euuid
          name
          path
          modified_on
          parent {
            euuid
            name
          }
        }
      }
    `, { uuid: folderUuid });

    const folderInfo = verification.data?.getFolder;
    if (folderInfo) {
      console.log(`✅ GraphQL verification: Folder exists with path ${folderInfo.path}`);
      return folderUuid;
    } else {
      console.error('❌ GraphQL verification failed: Folder not found');
      return null;
    }
  }

  async demoFileUpload(folderUuid = null) {
    console.log('\n📤 DEMO: File Upload Operations');

    // Create a temporary test file
    const tempFilePath = join(tmpdir(), 'eywa-test.txt');
    const testContent = `Hello from EYWA!
This is a test file.
Timestamp: ${Date.now()}
Random: ${Math.random()}`;

    await fs.writeFile(tempFilePath, testContent, 'utf8');

    try {
      // Upload file with predefined UUID
      const fileUuid = SAMPLE_FILE_UUID;
      this.trackResource('file', fileUuid, 'demo-file.txt');

      const folderRef = folderUuid ? { euuid: folderUuid } : { euuid: rootUuid };

      console.log('Uploading file with protocol abstraction...');
      await upload(tempFilePath, {
        euuid: fileUuid,
        name: 'demo-file.txt',
        folder: folderRef
      });

      console.log(`✅ File uploaded successfully with UUID: ${fileUuid}`);

      // Verify with GraphQL
      console.log('Verifying upload with GraphQL...');
      const verification = await eywa.graphql(`
        query GetFile($uuid: UUID!) {
          getFile(euuid: $uuid) {
            euuid
            name
            status
            size
            content_type
            uploaded_at
            folder {
              euuid
              name
              path
            }
          }
        }
      `, { uuid: fileUuid });

      const fileInfo = verification.data?.getFile;
      if (fileInfo) {
        const folderPath = fileInfo.folder?.path || 'root';
        console.log(`✅ GraphQL verification: File ${fileInfo.name} (${fileInfo.size} bytes) in ${folderPath}`);
        return fileUuid;
      } else {
        console.error('❌ GraphQL verification failed: File not found');
        return null;
      }
    } finally {
      // Clean up temp file
      try {
        await fs.unlink(tempFilePath);
      } catch (error) {
        console.warn(`Failed to delete temp file: ${error.message}`);
      }
    }
  }

  async demoContentUpload(folderUuid = null) {
    console.log('\n📝 DEMO: Content Upload');

    // Upload JSON content with predefined UUID
    const fileUuid = JSON_FILE_UUID;
    this.trackResource('file', fileUuid, 'demo-data.json');

    const content = JSON.stringify({
      message: 'Hello from EYWA!',
      timestamp: Date.now(),
      testData: [1, 2, 3, 4, 5],
      random: Math.random()
    }, null, 2);

    const folderRef = folderUuid ? { euuid: folderUuid } : { euuid: rootUuid };

    console.log('Uploading JSON content...');
    await uploadContent(content, {
      euuid: fileUuid,
      name: 'demo-data.json',
      content_type: 'application/json',
      folder: folderRef
    });

    console.log(`✅ JSON content uploaded with UUID: ${fileUuid}`);
    return fileUuid;
  }

  async demoFileDownload(fileUuid) {
    console.log('\n📥 DEMO: File Download');

    // Download to memory
    console.log('Downloading file to memory...');
    const content = await download(fileUuid);

    console.log(`✅ Downloaded ${content.length} bytes to memory`);

    // Show content preview
    if (content.length < 500) { // Small files
      try {
        const textContent = content.toString('utf8');
        const preview = textContent.length > 100 ? textContent.slice(0, 100) + '...' : textContent;
        console.log(`Content preview: ${preview}`);
      } catch (error) {
        console.log('Content is binary data');
      }
    }

    // Download to file using stream
    const tempDir = await fs.mkdtemp(join(tmpdir(), 'eywa-download-'));
    const savePath = join(tempDir, 'downloaded_file');

    console.log('Downloading file to disk using stream...');
    const { stream, contentLength } = await downloadStream(fileUuid);
    const writeStream = createWriteStream(savePath);

    // Pipe the stream to file
    stream.pipe(writeStream);
    
    // Wait for stream to complete
    await new Promise((resolve, reject) => {
      writeStream.on('finish', resolve);
      writeStream.on('error', reject);
    });

    console.log(`✅ File saved to: ${savePath}`);

    // Verify file size matches
    const stats = await fs.stat(savePath);
    if (stats.size === content.length) {
      console.log(`✅ File size verification: ${stats.size} bytes`);
    } else {
      console.error(`❌ File size mismatch: ${stats.size} != ${content.length}`);
    }

    // Clean up
    await fs.unlink(savePath);
    await fs.rmdir(tempDir);
  }

  async demoGraphQLQueries(folderUuid = null) {
    console.log('\n🔍 DEMO: Direct GraphQL Queries');

    // List all files in our test folder
    if (folderUuid) {
      console.log(`Listing files in folder ${folderUuid}...`);
      const filesQuery = await eywa.graphql(`
        query ListFilesInFolder($folderId: UUID!) {
          searchFile(
            _order_by: { uploaded_at: desc }
          ) {
            euuid
            name
            size
            content_type
            uploaded_at
            folder(_where: { euuid: { _eq: $folderId } }) {
              name
              path
            }
          }
        }
      `, { folderId: folderUuid });

      const files = filesQuery.data?.searchFile || [];
      console.log(`✅ Found ${files.length} files in folder:`);
      for (const file of files) {
        console.log(`  - ${file.name} (${file.size} bytes, ${file.content_type})`);
      }
    }

    // List all our test folders
    console.log('Listing folders starting with "demo"...');
    const foldersQuery = await eywa.graphql(`
      query ListDemoFolders {
        searchFolder(
          _where: { name: { _ilike: "demo%" } }
          _order_by: { name: asc }
        ) {
          euuid
          name
          path
          modified_on
          _count {
            files
          }
        }
      }
    `);

    const folders = foldersQuery.data?.searchFolder || [];
    console.log(`✅ Found ${folders.length} demo folders:`);
    for (const folder of folders) {
      const fileCount = folder._count?.files || 0;
      console.log(`  - ${folder.name} at ${folder.path} (${fileCount} files)`);
    }
  }

  async demoErrorHandling() {
    console.log('\n⚠️ DEMO: Error Handling');

    // Try to download non-existent file
    const fakeUuid = randomUUID();
    try {
      await download(fakeUuid);
      console.error('❌ Should have failed for non-existent file');
    } catch (error) {
      if (error instanceof FileDownloadError) {
        console.log(`✅ Correctly caught FileDownloadError: ${error.message}`);
      } else {
        console.log(`✅ Caught error: ${error.message}`);
      }
    }

    // Try to upload non-existent file
    try {
      await upload('/non/existent/file.txt', { name: 'test.txt' });
      console.error('❌ Should have failed for non-existent file');
    } catch (error) {
      if (error instanceof FileUploadError) {
        console.log(`✅ Correctly caught FileUploadError: ${error.message}`);
      } else {
        console.log(`✅ Caught error: ${error.message}`);
      }
    }
  }

  async runDemo() {
    console.log('🚀 EYWA Files - Simplified API Demo');

    try {
      // 1. Demo folder operations
      const folderUuid = await this.demoFolderOperations();

      // 2. Demo file uploads
      const textFileUuid = await this.demoFileUpload(folderUuid);
      const jsonFileUuid = await this.demoContentUpload(folderUuid);

      // 3. Demo file downloads
      if (textFileUuid) {
        await this.demoFileDownload(textFileUuid);
      }

      // 4. Demo GraphQL queries
      await this.demoGraphQLQueries(folderUuid);

      // 5. Demo error handling
      await this.demoErrorHandling();

      console.log('\n✨ Demo completed successfully!');

    } catch (error) {
      console.error('💥 Demo failed:', error.message);
      console.error('Stack trace:', error.stack);
      throw error;
    } finally {
      // Always clean up
      await this.cleanup();
    }
  }
}

async function main() {
  try {
    eywa.open_pipe();
    
    const demo = new SimplifiedFilesDemo();
    await demo.runDemo();
    
    eywa.close_task('SUCCESS');
  } catch (error) {
    console.error('💥 Demo execution failed:', error.message);
    eywa.error(`Demo execution failed: ${error.message}`);
    eywa.close_task('ERROR');
  }
}

main();