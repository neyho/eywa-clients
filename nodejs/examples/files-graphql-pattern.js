#!/usr/bin/env node

/**
 * EYWA File Operations - GraphQL Pattern Example
 * 
 * This example follows the exact Babashka client pattern:
 * - Pre-generated UUIDs for deterministic testing
 * - Single map arguments that directly mirror GraphQL schema
 * - Client-controlled UUID management
 * - No path parsing or abstraction layers
 * 
 * Run with: eywa run -c 'node examples/files-graphql-pattern.js'
 */

import eywa from '../src/index.js'
import { createReadStream, createWriteStream } from 'fs'
import { writeFile, mkdir, rm } from 'fs/promises'
import { createHash } from 'crypto'
import { Readable } from 'stream'

// Start the EYWA client (handles stdin/stdout RPC)
eywa.open_pipe()

const TEST_FOLDER = '/tmp/eywa-node-test'

console.log('\\n=== EYWA File Operations - GraphQL Pattern ===\\n')

// Helper to format file sizes
function formatSize(bytes) {
  if (bytes < 1024) return `${bytes} bytes`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

// Helper to calculate content hash for verification
function calculateHash(content) {
  return createHash('sha256').update(content).digest('hex')
}

// ============================================
// TEST DATA - Pre-generated UUIDs and structure
// (Following exact Babashka pattern)
// ============================================

const testData = {
  folders: {
    root: {
      euuid: 'c7f49a8c-4b0e-4d5a-9f3a-7e8b2c1d0e9f',
      name: 'node-demo',
      parent: { euuid: eywa.rootUuid }
    },
    reports: {
      euuid: 'a1b2c3d4-e5f6-7890-1234-567890abcdef',
      name: 'reports',
      parent: { euuid: 'c7f49a8c-4b0e-4d5a-9f3a-7e8b2c1d0e9f' }
    },
    archive: {
      euuid: 'f9e8d7c6-b5a4-3210-9876-543210fedcba',
      name: 'archive',
      parent: { euuid: 'a1b2c3d4-e5f6-7890-1234-567890abcdef' }
    }
  },
  files: {
    sampleRoot: {
      euuid: '11111111-2222-3333-4444-555555555555',
      name: 'sample.txt',
      folder: { euuid: 'c7f49a8c-4b0e-4d5a-9f3a-7e8b2c1d0e9f' },
      content: 'Hello from Node.js EYWA client!\\nThis is a test file.\\n'
    },
    reportData: {
      euuid: '22222222-3333-4444-5555-666666666666',
      name: 'report-data.json',
      folder: { euuid: 'a1b2c3d4-e5f6-7890-1234-567890abcdef' },
      content: JSON.stringify({
        timestamp: new Date().toISOString(),
        type: 'test-report',
        data: { items: 10, processed: 8, errors: 0 }
      }, null, 2)
    },
    generated: {
      euuid: '33333333-4444-5555-6666-777777777777',
      name: 'generated.txt',
      folder: { euuid: 'c7f49a8c-4b0e-4d5a-9f3a-7e8b2c1d0e9f' },
      content: `Generated at: ${new Date().toISOString()}\\nClient: Node.js\\nPattern: GraphQL-aligned\\n`
    },
    updated: {
      euuid: '44444444-5555-6666-7777-888888888888',
      name: 'updated.txt',
      folder: { euuid: 'f9e8d7c6-b5a4-3210-9876-543210fedcba' },
      content: 'Initial content for update test'
    }
  }
}

console.log('📋 Test Data Generated:')
console.log('\\nFolders:')
Object.entries(testData.folders).forEach(([key, folder]) => {
  console.log(`  ${key}: ${folder.name} -> ${folder.euuid}`)
})
console.log('\\nFiles:')
Object.entries(testData.files).forEach(([key, file]) => {
  console.log(`  ${key}: ${file.name} -> ${file.euuid}`)
})

// Store resolved data for verification
const resolvedData = { folders: {}, files: {} }

async function main() {
  try {

    // ============================================
    // EXAMPLE 1: Create Folder Structure
    // ============================================

    console.log('\\n\\n📁 EXAMPLE 1: Create Folder Structure')

    // Create node-demo folder under system root
    const demoFolder = testData.folders.root
    console.log(`Creating folder: ${demoFolder.name}`)
    const createdDemo = await eywa.createFolder(demoFolder)
    resolvedData.folders.root = createdDemo
    console.log(`  ✓ Created: ${createdDemo.path} (${createdDemo.euuid})`)

    // Create reports subfolder
    const reportsFolder = testData.folders.reports
    console.log(`Creating folder: ${reportsFolder.name}`)
    const createdReports = await eywa.createFolder(reportsFolder)
    resolvedData.folders.reports = createdReports
    console.log(`  ✓ Created: ${createdReports.path} (${createdReports.euuid})`)

    // Create archive subfolder
    const archiveFolder = testData.folders.archive
    console.log(`Creating folder: ${archiveFolder.name}`)
    const createdArchive = await eywa.createFolder(archiveFolder)
    resolvedData.folders.archive = createdArchive
    console.log(`  ✓ Created: ${createdArchive.path} (${createdArchive.euuid})`)

    // ============================================
    // EXAMPLE 2: Upload Content to Folder
    // ============================================

    console.log('\\n\\n📤 EXAMPLE 2: Upload Content to Folder')

    const sampleFile = testData.files.sampleRoot
    console.log(`Uploading ${sampleFile.name} to ${demoFolder.name}`)
    await eywa.uploadContent(sampleFile.content, {
      name: sampleFile.name,
      euuid: sampleFile.euuid,
      folder: sampleFile.folder,
      content_type: 'text/plain'
    })
    console.log(`  ✓ Uploaded: ${sampleFile.name}`)

    // Verify
    const sampleInfo = await eywa.fileInfo(sampleFile.euuid)
    resolvedData.files.sampleRoot = sampleInfo
    console.log(`  📋 Info: ${sampleInfo.name} (${formatSize(sampleInfo.size)}) in ${sampleInfo.folder?.name}`)

    // ============================================
    // EXAMPLE 3: Upload JSON Data  
    // ============================================

    console.log('\\n\\n📤 EXAMPLE 3: Upload JSON Data')

    const reportFile = testData.files.reportData
    console.log(`Uploading ${reportFile.name} to reports folder`)
    await eywa.uploadContent(reportFile.content, {
      name: reportFile.name,
      euuid: reportFile.euuid,
      folder: reportFile.folder,
      content_type: 'application/json'
    })
    console.log(`  ✓ Uploaded: ${reportFile.name}`)

    // Verify
    const reportInfo = await eywa.fileInfo(reportFile.euuid)
    resolvedData.files.reportData = reportInfo
    console.log(`  📋 Info: ${reportInfo.name} (${formatSize(reportInfo.size)})`)

    // ============================================
    // EXAMPLE 4: Upload from File (Stream)
    // ============================================

    console.log('\\n\\n📤 EXAMPLE 4: Upload from File (Stream)')

    // Create temp file
    await rm(TEST_FOLDER, { recursive: true, force: true })
    await mkdir(TEST_FOLDER, { recursive: true })

    const tempContent = 'This is test content from a real file.\\nUploaded via stream.\\n'
    const tempFile = `${TEST_FOLDER}/temp.txt`
    await writeFile(tempFile, tempContent)

    const generatedFile = testData.files.generated
    console.log(`Uploading ${generatedFile.name} from file stream`)
    await eywa.upload(tempFile, {
      name: generatedFile.name,
      euuid: generatedFile.euuid,
      folder: generatedFile.folder,
      content_type: 'text/plain'
    })
    console.log(`  ✓ Uploaded: ${generatedFile.name} from file`)

    // Verify
    const generatedInfo = await eywa.fileInfo(generatedFile.euuid)
    resolvedData.files.generated = generatedInfo
    console.log(`  📋 Info: ${generatedInfo.name} (${formatSize(generatedInfo.size)})`)

    // ============================================
    // EXAMPLE 5: Upload via ReadableStream
    // ============================================

    console.log('\\n\\n📤 EXAMPLE 5: Upload via Stream with Progress')

    const updatedFile = testData.files.updated
    const content = Buffer.from(updatedFile.content)
    const stream = Readable.from([content])

    console.log(`Uploading ${updatedFile.name} via ReadableStream`)

    // Track progress
    let lastProgress = 0
    const progressCallback = (uploaded, total) => {
      const percent = Math.round((uploaded / total) * 100)
      if (percent !== lastProgress && percent % 25 === 0) {
        console.log(`  📊 Progress: ${percent}% (${uploaded}/${total} bytes)`)
        lastProgress = percent
      }
    }

    await eywa.uploadStream(stream, {
      name: updatedFile.name,
      euuid: updatedFile.euuid,
      folder: updatedFile.folder,
      size: content.length,
      content_type: 'text/plain',
      progressFn: progressCallback
    })
    console.log(`  ✓ Uploaded: ${updatedFile.name} via stream`)

    // ============================================
    // EXAMPLE 6: Replace/Update File Content
    // ============================================

    console.log('\\n\\n📤 EXAMPLE 6: Replace File Content (Same UUID)')

    // Get current info
    const beforeInfo = await eywa.fileInfo(updatedFile.euuid)
    console.log(`  📋 Before: ${beforeInfo.name} (${formatSize(beforeInfo.size)})`)

    // Update with new content using same UUID
    const newContent = `UPDATED CONTENT\\nFile replaced at: ${new Date().toISOString()}\\nSame UUID: ${updatedFile.euuid}\\n`
    await eywa.uploadContent(newContent, {
      name: updatedFile.name,
      euuid: updatedFile.euuid, // Same UUID = replace
      folder: updatedFile.folder
    })
    console.log(`  ✓ Replaced file content`)

    // Verify update  
    const afterInfo = await eywa.fileInfo(updatedFile.euuid)
    resolvedData.files.updated = afterInfo
    console.log(`  📋 After: ${afterInfo.name} (${formatSize(afterInfo.size)})`)
    console.log(`  🔄 Same UUID: ${beforeInfo.euuid === afterInfo.euuid}`)

    // ============================================
    // EXAMPLE 7: List Files in Folder
    // ============================================

    console.log('\\n\\n📋 EXAMPLE 7: List Files in Folders')

    // List files in reports folder using relationship filtering
    const reportsFiles = await eywa.list({
      folder: { euuid: testData.folders.reports.euuid },
      limit: 10
    })
    console.log(`  📁 Reports folder (${reportsFiles.length} files):`)
    reportsFiles.forEach(file => {
      console.log(`    - ${file.name} (${formatSize(file.size)})`)
    })

    // List all files with name pattern
    const allSampleFiles = await eywa.list({
      name: 'sample',
      limit: 5
    })
    console.log(`  🔍 Files with 'sample' in name (${allSampleFiles.length} files):`)
    allSampleFiles.forEach(file => {
      console.log(`    - ${file.name} in ${file.folder?.name || 'ROOT'}`)
    })

    // ============================================
    // EXAMPLE 8: Download and Verify Content
    // ============================================

    console.log('\\n\\n📥 EXAMPLE 8: Download File Content')

    console.log(`Downloading ${sampleFile.name}`)
    const downloadedContent = await eywa.download(sampleFile.euuid)
    const downloadedText = downloadedContent.toString('utf8')

    console.log(`  ✓ Downloaded ${formatSize(downloadedContent.length)}`)
    console.log(`  📄 Content preview: "${downloadedText.substring(0, 50)}..."`)

    // Verify content matches
    const originalHash = calculateHash(sampleFile.content)
    const downloadedHash = calculateHash(downloadedText)
    const matches = originalHash === downloadedHash
    console.log(`  🔍 Content verification: ${matches ? '✓ MATCH' : '❌ MISMATCH'}`)

    // ============================================
    // EXAMPLE 9: Download as Stream
    // ============================================

    console.log('\\n\\n📥 EXAMPLE 9: Download as Stream')

    console.log(`Downloading ${reportFile.name} as stream`)
    const streamResult = await eywa.downloadStream(reportFile.euuid)

    console.log(`  📊 Content-Length: ${formatSize(streamResult.contentLength)}`)

    // Stream to file
    const outputFile = `${TEST_FOLDER}/downloaded-report.json`
    const writeStream = createWriteStream(outputFile)

    let downloadedBytes = 0
    for await (const chunk of streamResult.stream) {
      writeStream.write(chunk)
      downloadedBytes += chunk.length
    }
    writeStream.end()

    console.log(`  ✓ Streamed ${formatSize(downloadedBytes)} to ${outputFile}`)

    // ============================================
    // EXAMPLE 10: List All Demo Folders
    // ============================================

    console.log('\\n\\n📋 EXAMPLE 10: List Demo Folders')

    const demoFolders = await eywa.listFolders({
      name: 'demo',
      limit: 10
    })
    console.log(`  📁 Folders with 'demo' in name (${demoFolders.length} folders):`)
    demoFolders.forEach(folder => {
      console.log(`    - ${folder.path}`)
    })

    // ============================================
    // VERIFICATION SUMMARY
    // ============================================

    console.log('\\n\\n✅ VERIFICATION SUMMARY')
    console.log('======================================')

    console.log('\\n📁 Folders Created:')
    Object.entries(resolvedData.folders).forEach(([key, folder]) => {
      console.log(`  ${key}: ${folder.path}`)
    })

    console.log('\\n📄 Files Uploaded:')
    Object.entries(resolvedData.files).forEach(([key, file]) => {
      console.log(`  ${key}: ${file.folder?.path || '/'}${file.name} (${formatSize(file.size)})`)
    })

    console.log('\\n💡 Key Features Demonstrated:')
    console.log('  • Pre-generated UUIDs for deterministic testing')
    console.log('  • Single map arguments that mirror GraphQL schema')
    console.log('  • Client-controlled UUID management')
    console.log('  • File replacement using same UUID')
    console.log('  • Multiple upload methods: content, file, stream')
    console.log('  • GraphQL-aligned folder operations')
    console.log('  • Progress tracking for uploads')
    console.log('  • Stream-based downloads')
    console.log('  • No path parsing or abstraction layers')

    console.log('\\n🎯 Babashka Pattern Compliance:')
    console.log('  ✓ Direct GraphQL argument mapping')
    console.log('  ✓ No parameter mangling or transformation')
    console.log('  ✓ Client controls all UUIDs')
    console.log('  ✓ Single map arguments throughout')
    console.log('  ✓ Exact same API philosophy as Babashka client')

    console.log('\\n👋 Demo finished!')
    process.exit(0)

  } catch (err) {
    console.error('\\n❌ Demo failed:', err.message)
    console.error(err.stack)
    process.exit(1)
  }
}

main()
