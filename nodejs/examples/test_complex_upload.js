import eywa from '../src/index.js'
import { mkdir, writeFile, rm } from 'fs/promises'
import { createReadStream } from 'fs'
import { Readable } from 'stream'
import { createHash } from 'crypto'

eywa.open_pipe()

const TEST_FOLDER = '/tmp/eywa-complex-test'
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
  console.log('=== Complex Upload Test ===\n')
  
  const state = {
    timestamp: new Date().toISOString(),
    testFolder: TEST_FOLDER,
    files: [],
    folders: []
  }
  
  try {
    // Step 1: Create temp folder and source files
    console.log(`Creating temp files in ${TEST_FOLDER}...`)
    await rm(TEST_FOLDER, { recursive: true, force: true })
    await mkdir(TEST_FOLDER, { recursive: true })
    
    const guideContent = `# User Guide

This is a comprehensive guide for using the system.

## Getting Started
1. Install dependencies
2. Configure settings
3. Run the application

## Advanced Topics
- Performance tuning
- Security best practices
- Deployment strategies
`
    
    const csvContent = `id,name,value,timestamp
1,Item A,100,2024-01-01
2,Item B,200,2024-01-02
3,Item C,150,2024-01-03
4,Item D,300,2024-01-04
5,Item E,250,2024-01-05
`
    
    await writeFile(`${TEST_FOLDER}/guide.txt`, guideContent)
    await writeFile(`${TEST_FOLDER}/sample.csv`, csvContent)
    
    console.log('✓ Created 2 source files\n')
    
    // Step 2: Define files to upload
    const uploads = [
      {
        name: 'README.md',
        eywaPath: '/project-alpha/README.md',
        sourceType: 'memory',
        content: '# Project Alpha\n\nA complex test project for EYWA file upload.\n\n## Features\n- Multi-level folder structure\n- Stream-based uploads\n- Comprehensive testing\n'
      },
      {
        name: 'guide.txt',
        eywaPath: '/project-alpha/docs/guide.txt',
        sourceType: 'file',
        sourcePath: `${TEST_FOLDER}/guide.txt`
      },
      {
        name: 'spec.json',
        eywaPath: '/project-alpha/docs/api/spec.json',
        sourceType: 'memory',
        content: JSON.stringify({
          version: '1.0.0',
          endpoints: [
            { path: '/api/users', method: 'GET' },
            { path: '/api/users/:id', method: 'GET' },
            { path: '/api/users', method: 'POST' }
          ]
        }, null, 2)
      },
      {
        name: 'main.js',
        eywaPath: '/project-alpha/src/main.js',
        sourceType: 'memory',
        content: `// Main application entry point
import { createApp } from './app.js'
import { config } from './config.js'

const app = createApp(config)
app.start()

console.log('Application started')
`
      },
      {
        name: 'button.js',
        eywaPath: '/project-alpha/src/components/button.js',
        sourceType: 'memory',
        content: `// Button component
export function Button({ label, onClick }) {
  return {
    render() {
      return \`<button onclick="\${onClick}">\${label}</button>\`
    }
  }
}
`
      },
      {
        name: 'sample.csv',
        eywaPath: '/project-alpha/data/sample.csv',
        sourceType: 'file',
        sourcePath: `${TEST_FOLDER}/sample.csv`
      },
      {
        name: 'report.txt',
        eywaPath: '/project-alpha/data/exports/2024/report.txt',
        sourceType: 'memory',
        content: `Annual Report 2024
==================

Summary:
- Total revenue: $1.2M
- Growth: 25%
- New customers: 150

Details in attached spreadsheets.
`
      },
      {
        name: 'orphan-config.txt',
        eywaPath: null,
        fileName: 'orphan-config.txt',
        sourceType: 'memory',
        content: 'debug=true\nlog_level=info\nmax_connections=100\n'
      }
    ]
    
    // Step 3: Upload files sequentially
    console.log('Uploading files sequentially...\n')
    
    let totalSize = 0
    const folderSet = new Set()
    
    for (let i = 0; i < uploads.length; i++) {
      const file = uploads[i]
      const num = `[${i + 1}/${uploads.length}]`
      
      console.log(`${num} Uploading ${file.name} → ${file.eywaPath || 'ROOT (orphan)'}`)
      
      let stream, content, size
      
      if (file.sourceType === 'memory') {
        content = file.content
        const buffer = Buffer.from(content)
        stream = Readable.from(buffer)
        size = buffer.length
        console.log(`      Source: memory (${formatSize(size)})`)
      } else {
        const fs = await import('fs/promises')
        content = await fs.readFile(file.sourcePath, 'utf-8')
        stream = createReadStream(file.sourcePath)
        const stats = await fs.stat(file.sourcePath)
        size = stats.size
        console.log(`      Source: file (${formatSize(size)})`)
      }
      
      const hash = calculateHash(content)
      
      const options = {}
      
      // Size is required for non-file streams
      if (file.sourceType === 'memory') {
        options.size = size
      }
      
      // fileName required for orphan files
      if (file.eywaPath === null) {
        options.fileName = file.fileName
      }
      
      const result = await eywa.uploadFile(stream, file.eywaPath, options)
      
      console.log(`      ✓ Uploaded (uuid: ${result.euuid.substring(0, 8)}...)\n`)
      
      totalSize += size
      
      // Track folder
      if (file.eywaPath && file.eywaPath !== null) {
        const folderPath = file.eywaPath.substring(0, file.eywaPath.lastIndexOf('/') + 1)
        if (folderPath !== '/') {
          folderSet.add(folderPath)
        }
      }
      
      // Save to state
      state.files.push({
        euuid: result.euuid,
        name: file.name,
        eywaPath: file.eywaPath,
        folderPath: file.eywaPath ? file.eywaPath.substring(0, file.eywaPath.lastIndexOf('/') + 1) : null,
        sourceType: file.sourceType,
        sourceContent: file.sourceType === 'memory' ? content : undefined,
        sourcePath: file.sourceType === 'file' ? file.sourcePath : undefined,
        contentHash: hash,
        size: size
      })
    }
    
    // Step 4: Build folder list (deepest first)
    const folders = Array.from(folderSet).sort((a, b) => {
      const depthA = (a.match(/\//g) || []).length
      const depthB = (b.match(/\//g) || []).length
      return depthB - depthA // Deepest first
    })
    
    state.folders = folders
    
    // Step 5: Summary
    console.log('📊 Upload Summary:')
    console.log(`   - Files uploaded: ${uploads.length}`)
    console.log(`   - Folders created: ${folders.length}`)
    console.log(`   - Total size: ${formatSize(totalSize)}\n`)
    
    // Step 6: Visualize folder structure
    console.log('🌳 Folder Structure:')
    console.log('/project-alpha/')
    console.log('├── README.md')
    console.log('├── docs/')
    console.log('│   ├── guide.txt')
    console.log('│   └── api/')
    console.log('│       └── spec.json')
    console.log('├── src/')
    console.log('│   ├── main.js')
    console.log('│   └── components/')
    console.log('│       └── button.js')
    console.log('└── data/')
    console.log('    ├── sample.csv')
    console.log('    └── exports/')
    console.log('        └── 2024/')
    console.log('            └── report.txt')
    console.log('')
    console.log('ROOT/')
    console.log('└── orphan-config.txt\n')
    
    // Step 7: Save state
    await writeFile(STATE_FILE, JSON.stringify(state, null, 2))
    console.log(`💾 State saved to ${STATE_FILE}`)
    
    console.log('\n✅ Upload test completed successfully!')
    process.exit(0)
    
  } catch (err) {
    console.error('\n❌ Upload test failed:', err.message)
    console.error(err.stack)
    process.exit(1)
  }
}

main()