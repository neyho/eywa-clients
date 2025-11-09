#!/usr/bin/env node

/**
 * EYWA File Operations - Cleanup Example
 * 
 * Demonstrates safe cleanup of files and folders using pre-generated UUIDs.
 * This pattern ensures cleanup works even if upload operations fail.
 * 
 * Run with: eywa run -c 'node examples/cleanup-demo.js'
 */

import eywa from '../src/index.js'

// Start the EYWA client
eywa.open_pipe()

console.log('\\n=== EYWA Cleanup Demo ===\\n')

// Pre-generated UUIDs for guaranteed cleanup
const testUuids = {
  folders: {
    demo: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    subfolder: 'bbbbbbbb-cccc-dddd-eeee-ffffffffffff'
  },
  files: {
    file1: '11111111-2222-3333-4444-555555555555',
    file2: '22222222-3333-4444-5555-666666666666',
    file3: '33333333-4444-5555-6666-777777777777'
  }
}

async function cleanupResources() {
  console.log('🧹 Starting cleanup...')
  
  let deletedFiles = 0
  let deletedFolders = 0
  
  // Delete files first
  console.log('\\n📄 Cleaning up files:')
  for (const [name, uuid] of Object.entries(testUuids.files)) {
    try {
      const info = await eywa.fileInfo(uuid)
      if (info) {
        await eywa.deleteFile(uuid)
        console.log(`  ✓ Deleted file: ${info.name} (${name})`)
        deletedFiles++
      } else {
        console.log(`  - File not found: ${name}`)
      }
    } catch (error) {
      console.log(`  ❌ Failed to delete file ${name}: ${error.message}`)
    }
  }
  
  // Delete folders (deepest first)
  console.log('\\n📁 Cleaning up folders:')
  const folderOrder = ['subfolder', 'demo'] // Deepest first
  
  for (const name of folderOrder) {
    const uuid = testUuids.folders[name]
    try {
      const info = await eywa.getFolderInfo({ euuid: uuid })
      if (info) {
        await eywa.deleteFolder(uuid)
        console.log(`  ✓ Deleted folder: ${info.path} (${name})`)
        deletedFolders++
      } else {
        console.log(`  - Folder not found: ${name}`)
      }
    } catch (error) {
      console.log(`  ❌ Failed to delete folder ${name}: ${error.message}`)
    }
  }
  
  console.log(`\\n📊 Cleanup Summary:`)
  console.log(`  Files deleted: ${deletedFiles}`)
  console.log(`  Folders deleted: ${deletedFolders}`)
  
  if (deletedFiles === 0 && deletedFolders === 0) {
    console.log('\\n✨ No resources to clean up - already clean!')
  } else {
    console.log('\\n✅ Cleanup completed successfully!')
  }
}

async function createTestResources() {
  console.log('🏗️  Creating test resources...')
  
  try {
    // Create folder structure
    await eywa.createFolder({
      name: 'cleanup-demo',
      euuid: testUuids.folders.demo,
      parent: { euuid: eywa.rootUuid }
    })
    console.log('  ✓ Created demo folder')
    
    await eywa.createFolder({
      name: 'subfolder',
      euuid: testUuids.folders.subfolder,
      parent: { euuid: testUuids.folders.demo }
    })
    console.log('  ✓ Created subfolder')
    
    // Upload test files
    await eywa.uploadContent('Test content 1', {
      name: 'test1.txt',
      euuid: testUuids.files.file1,
      folder: { euuid: testUuids.folders.demo }
    })
    console.log('  ✓ Uploaded test1.txt')
    
    await eywa.uploadContent('Test content 2', {
      name: 'test2.txt',
      euuid: testUuids.files.file2,
      folder: { euuid: testUuids.folders.subfolder }
    })
    console.log('  ✓ Uploaded test2.txt')
    
    await eywa.uploadContent('Test content 3', {
      name: 'test3.txt',
      euuid: testUuids.files.file3,
      folder: { euuid: testUuids.folders.demo }
    })
    console.log('  ✓ Uploaded test3.txt')
    
    console.log('\\n✅ Test resources created!')
    
  } catch (error) {
    console.error('\\n❌ Failed to create test resources:', error.message)
    // Continue to cleanup anyway
  }
}

async function main() {
  try {
    // Check command line arguments
    const args = process.argv.slice(2)
    const cleanupOnly = args.includes('--cleanup-only')
    
    if (!cleanupOnly) {
      await createTestResources()
      console.log('\\n⏳ Waiting 2 seconds before cleanup...')
      await new Promise(resolve => setTimeout(resolve, 2000))
    }
    
    await cleanupResources()
    
    console.log('\\n💡 Key Patterns Demonstrated:')
    console.log('  • Pre-generated UUIDs ensure cleanup always works')
    console.log('  • Delete files before folders (dependency order)')
    console.log('  • Check if resources exist before attempting deletion')
    console.log('  • Graceful error handling for missing resources')
    console.log('  • Use --cleanup-only flag to clean without creating')
    
    console.log('\\n👋 Demo completed!')
    process.exit(0)
    
  } catch (error) {
    console.error('\\n❌ Demo failed:', error.message)
    process.exit(1)
  }
}

main()
