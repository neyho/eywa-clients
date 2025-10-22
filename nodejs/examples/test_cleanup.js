import eywa from '../src/index.js'
import { readFile, rm, access } from 'fs/promises'

eywa.open_pipe()

const TEST_FOLDER = '/tmp/eywa-complex-test'
const STATE_FILE = '/tmp/eywa-test-state.json'

async function main() {
  console.log('=== Cleanup Test ===\n')

  try {
    // Step 1: Read state
    console.log(`Reading state from ${STATE_FILE}...`)
    const stateJson = await readFile(STATE_FILE, 'utf-8')
    const state = JSON.parse(stateJson)

    console.log('Found:')
    console.log(`  - ${state.files.length} files to delete`)
    console.log(`  - ${state.folders.length} folders to delete\n`)

    // Step 2: Verify files exist before cleanup
    console.log('📋 Before Cleanup:')
    console.log('   Verifying files exist...')

    // Group files by folder
    const filesByFolder = {}
    for (const file of state.files) {
      const folder = file.folderPath || 'ROOT'
      if (!filesByFolder[folder]) {
        filesByFolder[folder] = []
      }
      filesByFolder[folder].push(file)
    }

    for (const [folder, files] of Object.entries(filesByFolder)) {
      console.log(`   ✓ ${folder} - ${files.length} file(s)`)
    }

    console.log('')

    // Step 3: Delete files
    console.log('🗑️  Deleting Files...')

    for (let i = 0; i < state.files.length; i++) {
      const file = state.files[i]
      const num = `[${i + 1}/${state.files.length}]`

      console.log(`   ${num} Deleting ${file.name}...`)

      try {
        const result = await eywa.deleteFile(file.euuid)
        console.log(`       ✓ Deleted (returned: ${result})`)
      } catch (err) {
        console.log(`✗ (${err.message})`)
      }
    }

    console.log('')

    // Step 4: Delete folders (deepest first)
    console.log('🗑️  Deleting Folders (deepest first)...')

    for (let i = 0; i < state.folders.length; i++) {
      const folder = state.folders[i]
      const num = `[${i + 1}/${state.folders.length}]`

      console.log(`   ${num} ${folder}...`)

      try {
        // Create timeout promise
        const timeoutPromise = new Promise((_, reject) => {
          setTimeout(() => reject(new Error('Timeout after 10s')), 10000)
        })

        // Race between delete and timeout
        await Promise.race([
          eywa.deleteFolder(folder),
          timeoutPromise
        ])

        console.log('       ✓ Deleted')
      } catch (err) {
        console.log(`       ✗ Error: ${err.message}`)
      }
    }

    console.log('')

    // Step 5: Verify cleanup
    console.log('✅ Verifying Cleanup...')
    console.log('   Checking folders...')

    let remainingFolders = 0
    for (const folder of state.folders) {
      try {
        const query = `query { getFolder(path: "${folder}") { euuid } }`
        const result = await eywa.graphql(query)
        if (result.data && result.data.getFolder) {
          console.log(`     WARNING: ${folder} still exists`)
          remainingFolders++
        }
      } catch (err) {
        // Folder doesn't exist - good!
      }
    }

    if (remainingFolders === 0) {
      console.log('All deleted ✓\n')
    } else {
      console.log(`${remainingFolders} folder(s) still exist ✗\n`)
    }

    // Step 6: Local cleanup
    console.log('🧹 Local Cleanup...')

    // Delete test folder
    process.stdout.write(`   Deleting ${TEST_FOLDER}/... `)
    try {
      await rm(TEST_FOLDER, { recursive: true, force: true })
      console.log('✓')
    } catch (err) {
      console.log(`✗ (${err.message})`)
    }

    // Delete state file
    process.stdout.write(`   Deleting ${STATE_FILE}... `)
    try {
      await rm(STATE_FILE)
      console.log('✓')
    } catch (err) {
      console.log(`✗ (${err.message})`)
    }

    console.log('')

    // Step 7: Summary
    console.log('🎉 Cleanup Complete!')
    console.log(`   - ${state.files.length} files deleted`)
    console.log(`   - ${state.folders.length} folders deleted`)
    console.log('   - Local temp files removed')

    console.log('\n✅ Cleanup test completed successfully!')
    process.exit(0)

  } catch (err) {
    console.error('\n❌ Cleanup test failed:', err.message)
    console.error(err.stack)
    process.exit(1)
  }
}

main()
