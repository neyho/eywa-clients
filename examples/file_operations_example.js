#!/usr/bin/env node
/**
 * EYWA File Operations Example (JavaScript/Node.js)
 * 
 * This example demonstrates the enhanced file upload/download capabilities
 * of the EYWA JavaScript client. Shows both basic and advanced usage patterns.
 * 
 * Usage:
 *     eywa run -c "node file_operations_example.js"
 */

import eywa from '../nodejs/src/eywa.js'
import fs from 'fs/promises'
import { createWriteStream } from 'fs'
import { tmpdir } from 'os'
import { join } from 'path'

async function fileOperationsDemo() {
    console.log('🚀 EYWA File Operations Demo Starting')
    
    try {
        // Example 1: Quick Upload/Download
        eywa.info('\n📤 Example 1: Quick File Upload')
        
        // Create a test file
        const testContent = `Hello EYWA! This is a test file created by the JavaScript client.
Generated at: ${new Date().toISOString()}
This demonstrates the enhanced file capabilities.`
        
        const tempFile = join(tmpdir(), 'eywa_test.txt')
        await fs.writeFile(tempFile, testContent)
        
        try {
            // Quick upload
            const fileUuid = await eywa.quickUpload(tempFile)
            eywa.info(`✅ Quick upload successful! File UUID: ${fileUuid}`)
            
            // Quick download
            const downloadedPath = await eywa.quickDownload(fileUuid, 'downloaded_file.txt')
            eywa.info(`✅ Quick download successful! Saved to: ${downloadedPath}`)
            
            // Verify content
            const downloadedContent = await fs.readFile(downloadedPath, 'utf-8')
            
            if (downloadedContent === testContent) {
                eywa.info('✅ Content verification: Perfect match!')
            } else {
                eywa.error('❌ Content verification failed')
            }
            
        } finally {
            // Cleanup
            try {
                await fs.unlink(tempFile)
                await fs.unlink('downloaded_file.txt')
            } catch (e) {
                // Ignore cleanup errors
            }
        }
        
        // Example 2: Advanced Upload with Progress
        eywa.info('\n📤 Example 2: Advanced Upload with Progress Tracking')
        
        const uploadProgress = (current, total) => {
            const percentage = total > 0 ? (current / total) * 100 : 0
            eywa.debug(`Upload progress: ${current}/${total} bytes (${percentage.toFixed(1)}%)`)
        }
        
        // Create larger test content
        const largeContent = 'EYWA Large File Test\n' + 'x'.repeat(10000) // 10KB+ file
        
        const fileInfo = await eywa.uploadContent(largeContent, 'large_test_file.txt', {
            contentType: 'text/plain',
            progressCallback: uploadProgress
        })
        
        eywa.info(`✅ Advanced upload completed: ${fileInfo.name} -> ${fileInfo.euuid}`)
        
        // Example 3: File Listing and Management
        eywa.info('\n📋 Example 3: File Listing and Management')
        
        // List all files
        const allFiles = await eywa.listFiles({ limit: 5 })
        eywa.info(`📁 Found ${allFiles.length} recent files:`)
        allFiles.forEach(file => {
            const statusEmoji = file.status === 'UPLOADED' ? '✅' : '⏳'
            eywa.info(`  ${statusEmoji} ${file.name} (${file.status}) - ${file.size} bytes`)
        })
        
        // List only uploaded files
        const uploadedFiles = await eywa.listFiles({ status: 'UPLOADED', limit: 3 })
        eywa.info(`📁 Found ${uploadedFiles.length} uploaded files`)
        
        // Search by name pattern
        const testFiles = await eywa.listFiles({ namePattern: 'test' })
        eywa.info(`🔍 Found ${testFiles.length} files matching 'test'`)
        
        // Example 4: Download with Progress
        eywa.info('\n📥 Example 4: Download with Progress Tracking')
        
        if (uploadedFiles.length > 0) {
            const testFile = uploadedFiles[0]
            
            const downloadProgress = (current, total) => {
                const percentage = total > 0 ? (current / total) * 100 : 0
                eywa.debug(`Download progress: ${current}/${total} bytes (${percentage.toFixed(1)}%)`)
            }
            
            // Download to memory
            const content = await eywa.downloadFile(testFile.euuid, null, downloadProgress)
            eywa.info(`✅ Downloaded ${testFile.name} to memory (${content.length} bytes)`)
            
            // Download to file
            const downloadPath = `downloaded_${testFile.name}`
            const savedPath = await eywa.downloadFile(testFile.euuid, downloadPath, downloadProgress)
            eywa.info(`✅ Downloaded ${testFile.name} to file: ${savedPath}`)
            
            // Calculate file hash for integrity
            const originalHash = await eywa.calculateFileHash(savedPath)
            eywa.info(`🔑 File hash (SHA256): ${originalHash.substring(0, 16)}...`)
            
            // Cleanup
            try {
                await fs.unlink(downloadPath)
            } catch (e) {
                // Ignore cleanup errors
            }
        }
        
        // Example 5: File Information and Metadata
        eywa.info('\n📊 Example 5: File Information and Metadata')
        
        if (allFiles.length > 0) {
            const sampleFile = allFiles[0]
            
            // Get detailed file info
            const detailedInfo = await eywa.getFileInfo(sampleFile.euuid)
            if (detailedInfo) {
                eywa.info(`📄 File Details for ${detailedInfo.name}:`)
                eywa.info(`  • UUID: ${detailedInfo.euuid}`)
                eywa.info(`  • Status: ${detailedInfo.status}`)
                eywa.info(`  • Content Type: ${detailedInfo.content_type}`)
                eywa.info(`  • Size: ${detailedInfo.size} bytes`)
                eywa.info(`  • Uploaded: ${detailedInfo.uploaded_at || 'Unknown'}`)
                
                if (detailedInfo.uploaded_by) {
                    eywa.info(`  • Uploaded by: ${detailedInfo.uploaded_by.name || 'Unknown'}`)
                }
            }
        }
        
        // Example 6: Content Upload (Memory to EYWA)
        eywa.info('\n💾 Example 6: Content Upload from Memory')
        
        // Generate JSON data
        const jsonData = {
            demo: 'EYWA File Operations',
            timestamp: new Date().toISOString(),
            features: [
                'Upload files from disk',
                'Upload content from memory',
                'Download to file or memory',
                'Progress tracking',
                'File listing and search',
                'Metadata management'
            ],
            status: 'success'
        }
        
        const jsonContent = JSON.stringify(jsonData, null, 2)
        
        const jsonFileInfo = await eywa.uploadContent(jsonContent, 'demo_data.json', {
            contentType: 'application/json'
        })
        
        eywa.info(`✅ JSON data uploaded: ${jsonFileInfo.name} -> ${jsonFileInfo.euuid}`)
        
        // Download and parse the JSON
        const downloadedJson = await eywa.downloadFile(jsonFileInfo.euuid)
        const parsedData = JSON.parse(downloadedJson.toString())
        
        eywa.info(`✅ JSON verification: Found ${parsedData.features.length} features`)
        
        // Final Summary
        eywa.info('\n🎉 File Operations Demo Complete!')
        eywa.info('📊 Summary of capabilities demonstrated:')
        eywa.info('  ✅ Quick upload/download')
        eywa.info('  ✅ Advanced upload with progress tracking')
        eywa.info('  ✅ Content upload from memory')
        eywa.info('  ✅ File listing with filters')
        eywa.info('  ✅ Download with progress tracking')
        eywa.info('  ✅ File metadata and information')
        eywa.info('  ✅ Hash calculation for integrity')
        eywa.info('  ✅ JSON data handling')
        
        eywa.report('File Operations Demo', {
            status: 'completed',
            features_tested: 8,
            files_created: 3,
            operations_successful: true
        })
        
    } catch (error) {
        eywa.error(`Demo failed: ${error.message}`)
        console.error(error.stack)
        throw error
    }
}

async function main() {
    try {
        eywa.open_pipe()
        
        // Wait for pipe to initialize
        await new Promise(resolve => setTimeout(resolve, 100))
        
        // Get task context
        const task = await eywa.get_task()
        eywa.info(`Task received: ${task.name || 'File Operations Demo'}`)
        
        // Update task status
        eywa.update_task(eywa.PROCESSING)
        
        // Run the demo
        await fileOperationsDemo()
        
        // Complete successfully
        eywa.close_task(eywa.SUCCESS)
        
    } catch (error) {
        eywa.error(`Task failed: ${error.message}`)
        eywa.close_task(eywa.ERROR)
    }
}

main().catch(console.error)
