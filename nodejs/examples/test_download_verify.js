import eywa from '../src/index.js'
import { readFile } from 'fs/promises'
import { createHash } from 'crypto'

eywa.open_pipe()

const STATE_FILE = '/tmp/eywa-test-state.json'

function calculateHash(content) {
  return createHash('sha256').update(content).digest('hex')
}

function formatSize(bytes) {
  if (bytes < 1024) return `${bytes} bytes`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

async function main() {
  console.log('=== Download Verification Test ===\n')
  
  try {
    // Step 1: Read state
    console.log(`Reading state from ${STATE_FILE}...`)
    const stateJson = await readFile(STATE_FILE, 'utf-8')
    const state = JSON.parse(stateJson)
    
    console.log(`Found ${state.files.length} files to verify\n`)
    
    // Step 2: Verify each file
    console.log('Verifying downloads...\n')
    
    let verified = 0
    let failed = 0
    
    for (let i = 0; i < state.files.length; i++) {
      const file = state.files[i]
      const num = `[${i + 1}/${state.files.length}]`
      
      console.log(`${num} ${file.name}`)
      
      try {
        // Download
        console.log('      Downloading... ', { end: '' })
        const stream = await eywa.downloadFile(file.euuid)
        
        // Collect chunks
        const chunks = []
        for await (const chunk of stream) {
          chunks.push(chunk)
        }
        const downloaded = Buffer.concat(chunks)
        
        console.log('✓')
        
        // Get source content
        let sourceContent
        if (file.sourceType === 'memory') {
          sourceContent = file.sourceContent
          console.log(`      Source: memory (${formatSize(file.size)})`)
        } else {
          sourceContent = await readFile(file.sourcePath, 'utf-8')
          console.log(`      Source: ${file.sourcePath}`)
        }
        
        // Compare
        console.log('      Comparing... ', { end: '' })
        const downloadedContent = downloaded.toString('utf-8')
        const downloadedHash = calculateHash(downloadedContent)
        
        if (downloadedHash === file.contentHash) {
          console.log(`✓ Match (SHA256: ${downloadedHash.substring(0, 16)}...)`)
          
          // Validate JSON if applicable
          if (file.name.endsWith('.json')) {
            console.log('      Validating JSON... ', { end: '' })
            try {
              JSON.parse(downloadedContent)
              console.log('✓ Valid')
            } catch (e) {
              console.log('✗ Invalid JSON')
              failed++
              continue
            }
          }
          
          verified++
        } else {
          console.log(`✗ Mismatch!`)
          console.log(`         Expected: ${file.contentHash.substring(0, 16)}...`)
          console.log(`         Got:      ${downloadedHash.substring(0, 16)}...`)
          failed++
        }
        
      } catch (err) {
        console.log(`✗ Error: ${err.message}`)
        failed++
      }
      
      console.log('')
    }
    
    // Step 3: Summary
    console.log('📊 Verification Summary:')
    if (failed === 0) {
      console.log(`   ✅ ${verified}/${state.files.length} files verified successfully`)
      console.log('   ✅ All content matches source')
      
      // Check for JSON files
      const jsonFiles = state.files.filter(f => f.name.endsWith('.json'))
      if (jsonFiles.length > 0) {
        console.log(`   ✅ All ${jsonFiles.length} JSON file(s) valid`)
      }
      
      console.log('\n✅ Verification completed successfully!')
      process.exit(0)
    } else {
      console.log(`   ❌ ${verified}/${state.files.length} files verified`)
      console.log(`   ❌ ${failed} file(s) failed verification`)
      
      console.log('\n❌ Verification failed!')
      process.exit(1)
    }
    
  } catch (err) {
    console.error('\n❌ Verification test failed:', err.message)
    console.error(err.stack)
    process.exit(1)
  }
}

main()
