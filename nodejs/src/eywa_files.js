/**
 * EYWA File Upload/Download Extensions for Node.js
 * 
 * This module extends the EYWA client with convenient file upload and download functionality.
 * Provides high-level functions that handle the complete file lifecycle:
 * - Upload files with automatic URL generation and S3 upload
 * - Download files with automatic URL generation  
 * - List and manage uploaded files
 */

import fs from 'fs/promises'
import { createReadStream, createWriteStream } from 'fs'
import { basename, dirname } from 'path'
import { createHash } from 'crypto'
import { pipeline } from 'stream/promises'
import fetch from 'node-fetch'
import { lookup } from 'mime-types'
import * as eywa from './eywa.js'

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

/**
 * Upload a file to EYWA file service.
 * 
 * @param {string} filepath - Path to the file to upload
 * @param {Object} options - Upload options
 * @param {string} [options.name] - Custom name for the file (defaults to filename)
 * @param {string} [options.contentType] - MIME type (auto-detected if not provided)
 * @param {string} [options.folderUuid] - UUID of parent folder (optional)
 * @param {Function} [options.progressCallback] - Function called with (bytesUploaded, totalBytes)
 * @returns {Promise<Object>} File information object
 * @throws {FileUploadError} If upload fails at any stage
 */
export async function uploadFile(filepath, options = {}) {
    const {
        name,
        contentType,
        folderUuid,
        progressCallback
    } = options

    try {
        // Check if file exists
        const stats = await fs.stat(filepath)
        if (!stats.isFile()) {
            throw new FileUploadError(`Path is not a file: ${filepath}`)
        }

        // Get file information
        const fileSize = stats.size
        const fileName = name || basename(filepath)
        const detectedContentType = contentType || lookup(filepath) || 'application/octet-stream'

        eywa.info(`Starting upload: ${fileName} (${fileSize} bytes)`)

        // Step 1: Request upload URL
        const uploadQuery = `
            mutation RequestUpload($file: FileInput!) {
                requestUploadURL(file: $file)
            }
        `

        const variables = {
            file: {
                name: fileName,
                content_type: detectedContentType,
                size: fileSize
            }
        }

        if (folderUuid) {
            variables.file.folder = { euuid: folderUuid }
        }

        const result = await eywa.graphql(uploadQuery, variables)
        const uploadUrl = result.data.requestUploadURL

        eywa.debug(`Upload URL received: ${uploadUrl.substring(0, 50)}...`)

        // Step 2: Upload file to S3
        const fileData = await fs.readFile(filepath)

        if (progressCallback) {
            progressCallback(0, fileSize)
        }

        const response = await fetch(uploadUrl, {
            method: 'PUT',
            body: fileData,
            headers: {
                'Content-Type': detectedContentType
            }
        })

        if (!response.ok) {
            const responseText = await response.text()
            throw new FileUploadError(`S3 upload failed (${response.status}): ${responseText}`)
        }

        if (progressCallback) {
            progressCallback(fileSize, fileSize)
        }

        eywa.debug('File uploaded to S3 successfully')

        // Step 3: Confirm upload
        const confirmQuery = `
            mutation ConfirmUpload($url: String!) {
                confirmFileUpload(url: $url)
            }
        `

        const confirmResult = await eywa.graphql(confirmQuery, { url: uploadUrl })

        if (!confirmResult.data.confirmFileUpload) {
            throw new FileUploadError('Upload confirmation failed')
        }

        eywa.debug('Upload confirmed')

        // Step 4: Get file information
        const fileInfo = await getFileByName(fileName)
        if (!fileInfo) {
            throw new FileUploadError('Could not retrieve uploaded file information')
        }

        eywa.info(`Upload completed: ${fileName} -> ${fileInfo.euuid}`)
        return fileInfo

    } catch (error) {
        eywa.error(`Upload failed: ${error.message}`)
        throw new FileUploadError(`Upload failed: ${error.message}`)
    }
}

/**
 * Upload content directly from memory.
 * 
 * @param {string|Buffer} content - Content to upload
 * @param {string} name - Filename for the content
 * @param {Object} options - Upload options
 * @param {string} [options.contentType='text/plain'] - MIME type
 * @param {string} [options.folderUuid] - UUID of parent folder (optional)
 * @param {Function} [options.progressCallback] - Function called with (bytesUploaded, totalBytes)
 * @returns {Promise<Object>} File information object
 * @throws {FileUploadError} If upload fails
 */
export async function uploadContent(content, name, options = {}) {
    const {
        contentType = 'text/plain',
        folderUuid,
        progressCallback
    } = options

    if (typeof content === 'string') {
        content = Buffer.from(content, 'utf-8')
    }

    const fileSize = content.length

    eywa.info(`Starting content upload: ${name} (${fileSize} bytes)`)

    try {
        // Step 1: Request upload URL
        const uploadQuery = `
            mutation RequestUpload($file: FileInput!) {
                requestUploadURL(file: $file)
            }
        `

        const variables = {
            file: {
                name: name,
                content_type: contentType,
                size: fileSize
            }
        }

        if (folderUuid) {
            variables.file.folder = { euuid: folderUuid }
        }

        const result = await eywa.graphql(uploadQuery, variables)
        const uploadUrl = result.data.requestUploadURL

        // Step 2: Upload content to S3
        if (progressCallback) {
            progressCallback(0, fileSize)
        }

        const response = await fetch(uploadUrl, {
            method: 'PUT',
            body: content,
            headers: {
                'Content-Type': contentType
            }
        })

        if (!response.ok) {
            const responseText = await response.text()
            throw new FileUploadError(`S3 upload failed (${response.status}): ${responseText}`)
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

        const confirmResult = await eywa.graphql(confirmQuery, { url: uploadUrl })

        if (!confirmResult.data.confirmFileUpload) {
            throw new FileUploadError('Upload confirmation failed')
        }

        // Step 4: Get file information
        const fileInfo = await getFileByName(name)
        if (!fileInfo) {
            throw new FileUploadError('Could not retrieve uploaded file information')
        }

        eywa.info(`Content upload completed: ${name} -> ${fileInfo.euuid}`)
        return fileInfo

    } catch (error) {
        eywa.error(`Content upload failed: ${error.message}`)
        throw new FileUploadError(`Content upload failed: ${error.message}`)
    }
}

/**
 * Download a file from EYWA file service.
 * 
 * @param {string} fileUuid - UUID of the file to download
 * @param {string} [savePath] - Path to save the file (if not provided, returns content)
 * @param {Function} [progressCallback] - Function called with (bytesDownloaded, totalBytes)
 * @returns {Promise<string|Buffer>} Path to saved file or file content
 * @throws {FileDownloadError} If download fails
 */
export async function downloadFile(fileUuid, savePath = null, progressCallback = null) {
    eywa.info(`Starting download: ${fileUuid}`)

    try {
        // Step 1: Request download URL
        const downloadQuery = `
            query RequestDownload($file: FileInput!) {
                requestDownloadURL(file: $file)
            }
        `

        const result = await eywa.graphql(downloadQuery, { file: { euuid: fileUuid } })
        const downloadUrl = result.data.requestDownloadURL

        eywa.debug(`Download URL received: ${downloadUrl.substring(0, 50)}...`)

        // Step 2: Download file
        const response = await fetch(downloadUrl)
        if (!response.ok) {
            throw new FileDownloadError(`Download failed (${response.status}): ${await response.text()}`)
        }

        const totalSize = parseInt(response.headers.get('content-length') || '0')

        if (progressCallback && totalSize > 0) {
            progressCallback(0, totalSize)
        }

        if (savePath) {
            // Stream to file
            const dirPath = dirname(savePath)
            await fs.mkdir(dirPath, { recursive: true })

            const writeStream = createWriteStream(savePath)
            
            let downloadedBytes = 0
            
            if (progressCallback && totalSize > 0) {
                response.body.on('data', (chunk) => {
                    downloadedBytes += chunk.length
                    progressCallback(downloadedBytes, totalSize)
                })
            }
            
            await pipeline(response.body, writeStream)

            eywa.info(`Download completed: ${fileUuid} -> ${savePath}`)
            return savePath
        } else {
            // Return content as buffer
            const content = await response.buffer()
            
            if (progressCallback) {
                progressCallback(content.length, content.length)
            }

            eywa.info(`Download completed: ${fileUuid} (${content.length} bytes)`)
            return content
        }

    } catch (error) {
        eywa.error(`Download failed: ${error.message}`)
        throw new FileDownloadError(`Download failed: ${error.message}`)
    }
}

/**
 * List files in EYWA file service.
 * 
 * @param {Object} options - Filter options
 * @param {number} [options.limit] - Maximum number of files to return
 * @param {string} [options.status] - Filter by status (PENDING, UPLOADED, etc.)
 * @param {string} [options.namePattern] - Filter by name pattern (SQL LIKE)
 * @param {string} [options.folderUuid] - Filter by folder UUID
 * @returns {Promise<Array>} List of file objects
 */
export async function listFiles(options = {}) {
    const { limit, status, namePattern, folderUuid } = options

    eywa.debug(`Listing files (limit=${limit}, status=${status})`)

    try {
        const query = `
            query ListFiles($limit: Int, $where: FileWhereInput) {
                searchFile(_limit: $limit, _where: $where, _order_by: {uploaded_at: desc}) {
                    euuid
                    name
                    status
                    content_type
                    size
                    uploaded_at
                    uploaded_by {
                        name
                    }
                    folder {
                        euuid
                        name
                    }
                }
            }
        `

        const whereConditions = []

        if (status) {
            whereConditions.push({ status: { _eq: status } })
        }

        if (namePattern) {
            whereConditions.push({ name: { _ilike: `%${namePattern}%` } })
        }

        if (folderUuid) {
            whereConditions.push({ folder: { euuid: { _eq: folderUuid } } })
        }

        const variables = {}
        if (limit) {
            variables.limit = limit
        }

        if (whereConditions.length > 0) {
            if (whereConditions.length === 1) {
                variables.where = whereConditions[0]
            } else {
                variables.where = { _and: whereConditions }
            }
        }

        const result = await eywa.graphql(query, variables)
        const files = result.data.searchFile

        eywa.debug(`Found ${files.length} files`)
        return files

    } catch (error) {
        eywa.error(`Failed to list files: ${error.message}`)
        throw error
    }
}

/**
 * Get information about a specific file.
 * 
 * @param {string} fileUuid - UUID of the file
 * @returns {Promise<Object|null>} File information or null if not found
 */
export async function getFileInfo(fileUuid) {
    try {
        const query = `
            query GetFile($uuid: UUID!) {
                getFile(euuid: $uuid) {
                    euuid
                    name
                    status
                    content_type
                    size
                    uploaded_at
                    uploaded_by {
                        name
                    }
                    folder {
                        euuid
                        name
                    }
                }
            }
        `

        const result = await eywa.graphql(query, { uuid: fileUuid })
        return result.data.getFile

    } catch (error) {
        eywa.debug(`File not found or error: ${error.message}`)
        return null
    }
}

/**
 * Get file information by name (returns most recent if multiple).
 * 
 * @param {string} name - File name to search for
 * @returns {Promise<Object|null>} File information or null if not found
 */
export async function getFileByName(name) {
    const files = await listFiles({ limit: 1, namePattern: name })
    return files.length > 0 ? files[0] : null
}

/**
 * Delete a file from EYWA file service.
 * 
 * @param {string} fileUuid - UUID of the file to delete
 * @returns {Promise<boolean>} True if deletion successful
 */
export async function deleteFile(fileUuid) {
    try {
        const query = `
            mutation DeleteFile($uuid: UUID!) {
                deleteFile(euuid: $uuid)
            }
        `

        const result = await eywa.graphql(query, { uuid: fileUuid })
        const success = result.data.deleteFile

        if (success) {
            eywa.info(`File deleted: ${fileUuid}`)
        } else {
            eywa.warn(`File deletion failed: ${fileUuid}`)
        }

        return success

    } catch (error) {
        eywa.error(`Failed to delete file: ${error.message}`)
        return false
    }
}

/**
 * Calculate hash of a file for integrity verification.
 * 
 * @param {string} filepath - Path to the file
 * @param {string} [algorithm='sha256'] - Hash algorithm ('md5', 'sha1', 'sha256', etc.)
 * @returns {Promise<string>} Hex digest of the file hash
 */
export async function calculateFileHash(filepath, algorithm = 'sha256') {
    const hash = createHash(algorithm)
    const stream = createReadStream(filepath)

    for await (const chunk of stream) {
        hash.update(chunk)
    }

    return hash.digest('hex')
}

// Convenience functions for common use cases

/**
 * Quick upload with minimal parameters.
 * 
 * @param {string} filepath - Path to file to upload
 * @returns {Promise<string>} File UUID
 */
export async function quickUpload(filepath) {
    const result = await uploadFile(filepath)
    return result.euuid
}

/**
 * Quick download to current directory.
 * 
 * @param {string} fileUuid - UUID of file to download
 * @param {string} [filename] - Custom filename (auto-detected if not provided)
 * @returns {Promise<string>} Path to downloaded file
 */
export async function quickDownload(fileUuid, filename = null) {
    if (filename === null) {
        const fileInfo = await getFileInfo(fileUuid)
        filename = fileInfo ? fileInfo.name : `download_${fileUuid.substring(0, 8)}`
    }

    return await downloadFile(fileUuid, filename)
}
