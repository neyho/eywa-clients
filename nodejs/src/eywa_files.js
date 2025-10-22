/**
 * EYWA File Operations for Node.js
 * 
 * Stream-based file upload/download for EYWA file service.
 * Library handles the EYWA protocol. Users control I/O via streams.
 * 
 * For file queries and searches, use the graphql() function directly.
 */

import { stat } from 'fs/promises'
import { Readable } from 'stream'
import fetch from 'node-fetch'
import { lookup } from 'mime-types'
import { graphql, info, debug, error, warn } from './eywa.js'

export class FileUploadError extends Error {
    constructor(message) {
        super(message)
        this.name = 'FileUploadError'
    }
}

export class FileDownloadError extends Error {
    constructor(message) {
        super(message)
        this.name = 'FileDownloadError'
    }
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Parse EYWA path into folder path and filename
 */
function parsePath(eywaPath) {
    if (!eywaPath || !eywaPath.includes('/')) {
        return { folderPath: null, fileName: eywaPath }
    }

    const normalized = eywaPath.startsWith('/') ? eywaPath : '/' + eywaPath
    const lastSlash = normalized.lastIndexOf('/')

    if (lastSlash === 0) {
        // ROOT level: '/file.txt'
        return {
            folderPath: '/',
            fileName: normalized.substring(1)
        }
    }

    return {
        folderPath: normalized.substring(0, lastSlash + 1),
        fileName: normalized.substring(lastSlash + 1)
    }
}

/**
 * Query existing folders in a hierarchy
 */
async function queryFolderHierarchy(targetPath) {
    const paths = []
    const parts = targetPath.split('/').filter(p => p)
    let current = '/'

    for (const part of parts) {
        current += part + '/'
        paths.push(current)
    }

    if (paths.length === 0) {
        return {}
    }

    const aliases = paths.map((path, i) =>
        `level${i}: getFolder(path: "${path}") { euuid name path }`
    ).join('\n    ')

    const query = `query CheckFolderHierarchy { ${aliases} }`

    try {
        const result = await graphql(query)
        const folders = {}
        paths.forEach((path, i) => {
            folders[path] = result.data[`level${i}`]
        })
        return folders
    } catch (err) {
        error(`Failed to query folder hierarchy: ${err.message}`)
        throw err
    }
}

/**
 * Get stream size
 */
async function getStreamSize(stream, providedSize) {
    // If size provided, use it
    if (providedSize !== undefined) {
        return providedSize
    }

    // Try to detect from file stream
    if (stream.path) {
        try {
            const stats = await stat(stream.path)
            return stats.size
        } catch (err) {
            throw new FileUploadError(`Cannot stat file: ${stream.path}`)
        }
    }

    throw new FileUploadError('Stream size required in options for non-file streams')
}

// ============================================================================
// Public API - Folder Management
// ============================================================================

/**
 * Create folder hierarchy in EYWA.
 * Handles the complex logic of creating nested folders.
 * 
 * @param {string} folderPath - Folder path (must end with '/')
 * @param {Object} options - Options
 * @returns {Promise<Object>} Final folder object
 */
export async function createFolder(folderPath, options = {}) {
    if (!folderPath.endsWith('/')) {
        throw new Error(`Folder path must end with '/': ${folderPath}`)
    }

    if (folderPath === '/') {
        throw new Error('Cannot create ROOT folder - it already exists')
    }

    const existingFolders = await queryFolderHierarchy(folderPath)
    const parts = folderPath.split('/').filter(p => p)
    let currentPath = '/'

    // Get ROOT UUID
    const rootQuery = 'query { getFolder(path: "/") { euuid } }'
    const rootResult = await graphql(rootQuery)
    let parentEuuid = rootResult.data.getFolder.euuid

    for (const part of parts) {
        currentPath += part + '/'
        const existing = existingFolders[currentPath]

        if (existing) {
            parentEuuid = existing.euuid
        } else {
            const mutation = `
                mutation CreateFolder($folder: FolderInput!) {
                    stackFolder(data: $folder) {
                        euuid
                        name
                        path
                    }
                }
            `

            const result = await graphql(mutation, {
                folder: {
                    name: part,
                    parent: { euuid: parentEuuid }
                }
            })

            const created = result.data.stackFolder
            if (!created) {
                throw new Error(`Failed to create folder: ${currentPath}`)
            }

            parentEuuid = created.euuid
            existingFolders[currentPath] = created
        }
    }

    return existingFolders[folderPath]
}

/**
 * Delete a folder.
 * 
 * @param {string} folderPath - Folder path to delete
 * @returns {Promise<boolean>} True if deleted
 */
export async function deleteFolder(folderPath) {
    const mutation = `mutation { deleteFolder(path: "${folderPath}") }`
    await graphql(mutation)
    return true
}

// ============================================================================
// Public API - Stream-Based File Upload
// ============================================================================

/**
 * Upload a stream to EYWA file service.
 * Handles the multi-step upload protocol: request URL → S3 → confirm.
 * 
 * @param {ReadableStream} stream - Readable stream to upload
 * @param {string|null} eywaPath - Target path or null for orphan
 * @param {Object} options - Upload options
 * @param {string} [options.fileName] - Required if eywaPath is null
 * @param {number} [options.size] - Stream size (auto-detected for file streams)
 * @param {string} [options.contentType] - MIME type (auto-detected from path/name)
 * @param {boolean} [options.createFolders=true] - Auto-create missing folders
 * @param {Function} [options.progressCallback] - Progress callback (uploaded, total)
 * @returns {Promise<Object>} File object with euuid, name, content_type, size, status
 */
export async function uploadFile(stream, eywaPath, options = {}) {
    const {
        fileName: optFileName,
        size: optSize,
        contentType: optContentType,
        createFolders = true,
        progressCallback
    } = options

    // Parse path
    let folderPath = null
    let fileName = null

    if (eywaPath === null) {
        if (!optFileName) {
            throw new FileUploadError('fileName required when eywaPath is null')
        }
        fileName = optFileName
    } else {
        const parsed = parsePath(eywaPath)
        folderPath = parsed.folderPath
        fileName = parsed.fileName
    }

    // Get stream size
    const fileSize = await getStreamSize(stream, optSize)

    // Detect content type
    const contentType = optContentType ||
        (eywaPath ? lookup(eywaPath) : null) ||
        (fileName ? lookup(fileName) : null) ||
        'application/octet-stream'

    info(`Uploading: ${fileName} (${fileSize} bytes)`)

    // Handle folder
    let folderEuuid = null
    if (folderPath && folderPath !== '/') {
        if (createFolders) {
            const folder = await createFolder(folderPath)
            folderEuuid = folder.euuid
        } else {
            const folders = await queryFolderHierarchy(folderPath)
            const folder = folders[folderPath]
            if (!folder) {
                throw new FileUploadError(`Folder does not exist: ${folderPath}`)
            }
            folderEuuid = folder.euuid
        }
    }

    // Step 1: Request upload URL
    const uploadQuery = `
        mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }
    `

    const variables = {
        file: {
            name: fileName,
            content_type: contentType,
            size: fileSize
        }
    }

    if (folderEuuid) {
        variables.file.folder = { euuid: folderEuuid }
    }

    const result = await graphql(uploadQuery, variables)
    const uploadUrl = result.data.requestUploadURL

    // Step 2: Upload stream to S3
    if (progressCallback) {
        progressCallback(0, fileSize)
    }

    // Convert stream to buffer for fetch (Node.js fetch doesn't support streams well)
    const chunks = []
    for await (const chunk of stream) {
        chunks.push(chunk)
    }
    const buffer = Buffer.concat(chunks)

    const response = await fetch(uploadUrl, {
        method: 'PUT',
        body: buffer,
        headers: {
            'Content-Type': contentType,
            'Content-Length': fileSize
        }
    })

    if (!response.ok) {
        const text = await response.text()
        throw new FileUploadError(`S3 upload failed (${response.status}): ${text}`)
    }

    if (progressCallback) {
        progressCallback(fileSize, fileSize)
    }

    // Step 3: Confirm upload
    const confirmQuery = `
        mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }
    `

    const confirmResult = await graphql(confirmQuery, { url: uploadUrl })

    if (!confirmResult.data.confirmFileUpload) {
        throw new FileUploadError('Upload confirmation failed')
    }

    // Step 4: Query the uploaded file to get its info
    // Search for the file we just uploaded (most recent with this name)
    const fileQuery = `
        query {
            searchFile(
                _where: { name: { _eq: "${fileName}" } }
                _order_by: { uploaded_at: desc }
                _limit: 1
            ) {
                euuid
                name
                content_type
                size
                status
                uploaded_at
            }
        }
    `

    const fileResult = await graphql(fileQuery)
    const files = fileResult.data.searchFile

    if (!files || files.length === 0) {
        throw new FileUploadError('Could not retrieve uploaded file info')
    }

    const fileInfo = files[0]
    info(`Upload completed: ${fileName}`)
    return fileInfo
}

// ============================================================================
// Public API - Stream-Based File Download
// ============================================================================

/**
 * Download a file as a stream.
 * Handles the download protocol: request URL → stream from S3.
 * 
 * @param {string} fileUuid - File UUID
 * @param {Object} options - Download options
 * @param {Function} [options.progressCallback] - Progress callback (downloaded, total)
 * @returns {Promise<ReadableStream>} Download stream
 */
export async function downloadFile(fileUuid, options = {}) {
    const { progressCallback } = options

    const query = `
        query RequestDownload($file: FileInput!) {
            requestDownloadURL(file: $file)
        }
    `

    const result = await graphql(query, { file: { euuid: fileUuid } })
    const downloadUrl = result.data.requestDownloadURL

    const response = await fetch(downloadUrl)
    if (!response.ok) {
        throw new FileDownloadError(`Download failed (${response.status})`)
    }

    const totalSize = parseInt(response.headers.get('content-length') || '0')

    if (progressCallback && totalSize > 0) {
        let downloaded = 0
        const progressStream = new Readable({
            async read() {
                for await (const chunk of response.body) {
                    downloaded += chunk.length
                    progressCallback(downloaded, totalSize)
                    this.push(chunk)
                }
                this.push(null)
            }
        })
        return progressStream
    }

    return Readable.from(response.body)
}

// ============================================================================
// Public API - File Management
// ============================================================================

/**
 * Delete a file.
 * 
 * @param {string} fileUuid - File UUID to delete
 * @returns {Promise<boolean>} True if deleted
 */
export async function deleteFile(fileUuid) {
    const mutation = `mutation { deleteFile(euuid: "${fileUuid}") }`
    const result = await graphql(mutation)
    return result.data.deleteFile === true
}
