/**
 * EYWA File Operations for Node.js
 * 
 * GraphQL-aligned file operations following the Babashka client pattern.
 * All functions use single map arguments that directly mirror GraphQL schema.
 * Client controls UUID management for both creation and updates.
 */

import { createHash } from 'crypto'
import { Readable } from 'stream'
import fetch from 'node-fetch'
import { lookup } from 'mime-types'
import { graphql, info as logInfo, debug, error } from './eywa.js'

// Exception types for file operations
export class FileUploadError extends Error {
  constructor(message, options = {}) {
    super(message)
    this.name = 'FileUploadError'
    this.type = options.type || 'upload-error'
    if (options.code) this.code = options.code
  }
}

export class FileDownloadError extends Error {
  constructor(message, options = {}) {
    super(message)
    this.name = 'FileDownloadError'
    this.type = options.type || 'download-error'
    if (options.code) this.code = options.code
  }
}

// Root folder UUID (matches Babashka client)
export const rootUuid = '87ce50d8-5dfa-4008-a265-053e727ab793'
export const rootFolder = { euuid: rootUuid }

/**
 * Detect MIME type from filename
 */
function mimeType(filename) {
  const detected = lookup(filename)
  if (detected) return detected

  // Fallback detection for common types
  const ext = filename.split('.').pop()?.toLowerCase()
  switch (ext) {
    case 'txt': return 'text/plain'
    case 'html': return 'text/html'
    case 'css': return 'text/css'
    case 'js': return 'application/javascript'
    case 'json': return 'application/json'
    case 'xml': return 'application/xml'
    case 'pdf': return 'application/pdf'
    case 'png': return 'image/png'
    case 'jpg':
    case 'jpeg': return 'image/jpeg'
    case 'gif': return 'image/gif'
    case 'svg': return 'image/svg+xml'
    case 'zip': return 'application/zip'
    case 'csv': return 'text/csv'
    default: return 'application/octet-stream'
  }
}

/**
 * Perform HTTP PUT request to upload data
 */
async function httpPut(url, data, contentType, progressFn) {
  try {
    const contentLength = Buffer.isBuffer(data) ? data.length : Buffer.byteLength(data, 'utf8')

    if (progressFn) progressFn(0, contentLength)

    const response = await fetch(url, {
      method: 'PUT',
      body: data,
      headers: {
        'Content-Type': contentType,
        'Content-Length': contentLength.toString()
      }
    })

    if (progressFn) progressFn(contentLength, contentLength)

    if (response.status === 200) {
      return { status: 'success', code: response.status }
    } else {
      const message = await response.text().catch(() => 'Unknown error')
      return { status: 'error', code: response.status, message }
    }
  } catch (e) {
    return { status: 'error', code: 0, message: e.message }
  }
}

/**
 * Perform HTTP PUT request from a stream
 */
async function httpPutStream(url, inputStream, contentLength, contentType, progressFn) {
  try {
    if (progressFn) progressFn(0, contentLength)

    // Read entire stream into buffer to avoid chunked transfer encoding
    const chunks = []
    let bytesRead = 0

    for await (const chunk of inputStream) {
      chunks.push(chunk)
      bytesRead += chunk.length
      if (progressFn) {
        progressFn(bytesRead, contentLength)
      }
    }

    const data = Buffer.concat(chunks)

    const response = await fetch(url, {
      method: 'PUT',
      body: data,
      headers: {
        'Content-Type': contentType,
        'Content-Length': data.length.toString()
      }
    })

    if (response.status === 200) {
      return { status: 'success', code: response.status }
    } else {
      const message = await response.text().catch(() => 'Unknown error')
      return { status: 'error', code: response.status, message }
    }
  } catch (e) {
    return { status: 'error', code: 0, message: e.message }
  }
}

/**
 * Perform HTTP GET request and return stream
 */
async function httpGetStream(url) {
  try {
    const response = await fetch(url)

    if (response.status === 200) {
      const contentLength = parseInt(response.headers.get('content-length') || '0')
      return {
        status: 'success',
        stream: Readable.from(response.body),
        contentLength
      }
    } else {
      return { status: 'error', code: response.status }
    }
  } catch (e) {
    return { status: 'error', code: 0, message: e.message }
  }
}

// ============================================================================
// Core File Operations (Following Babashka Pattern)
// ============================================================================

/**
 * Upload a file to EYWA file service using streaming.
 */
export async function upload(filepath, fileData) {
  try {
    const progressFn = fileData.progressFn

    // Handle different input types
    let fileStats, fileName, fileSize

    if (typeof filepath === 'string') {
      // File path
      const fs = await import('fs/promises')
      const path = await import('path')

      try {
        fileStats = await fs.stat(filepath)
        if (!fileStats.isFile()) {
          throw new FileUploadError(`Path is not a file: ${filepath}`)
        }
      } catch (err) {
        throw new FileUploadError(`File not found: ${filepath}`)
      }

      fileSize = fileStats.size
      fileName = fileData.name || path.basename(filepath)
    } else {
      // Assume it's a File-like object with .name and .size
      fileName = fileData.name || filepath.name
      fileSize = fileData.size || filepath.size

      if (!fileName) {
        throw new FileUploadError('File name is required')
      }
      if (!fileSize) {
        throw new FileUploadError('File size is required for File objects')
      }
    }

    const detectedContentType = fileData.content_type || mimeType(fileName)

    // Step 1: Request upload URL
    const uploadQuery = `mutation RequestUpload($file: FileInput!) { 
            requestUploadURL(file: $file)
        }`

    // Build file input - use fileData directly, just fill in computed values
    const fileInput = {
      ...fileData,
      progressFn: undefined, // Remove non-GraphQL field
      name: fileName,
      content_type: detectedContentType,
      size: fileSize
    }

    const result = await graphql(uploadQuery, { file: fileInput })

    if (result.error) {
      throw new FileUploadError(`Failed to get upload URL: ${JSON.stringify(result.error)}`)
    }

    const uploadUrl = result.data?.requestUploadURL
    if (!uploadUrl) {
      throw new FileUploadError('No upload URL in response')
    }

    // Step 2: Stream file to S3
    let uploadResult
    if (typeof filepath === 'string') {
      // File path - use file stream
      const fs = await import('fs')
      const fileStream = fs.createReadStream(filepath)
      uploadResult = await httpPutStream(uploadUrl, fileStream, fileSize, detectedContentType, progressFn)
    } else {
      // File object - read as stream
      const stream = filepath.stream ? filepath.stream() : Readable.from(filepath)
      uploadResult = await httpPutStream(uploadUrl, stream, fileSize, detectedContentType, progressFn)
    }

    if (uploadResult.status === 'error') {
      throw new FileUploadError(
        `S3 upload failed (${uploadResult.code}): ${uploadResult.message}`,
        { code: uploadResult.code }
      )
    }

    // Step 3: Confirm upload
    const confirmQuery = `mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }`

    const confirmResult = await graphql(confirmQuery, { url: uploadUrl })

    if (confirmResult.error) {
      throw new FileUploadError(`Upload confirmation failed: ${JSON.stringify(confirmResult.error)}`)
    }

    const confirmed = confirmResult.data?.confirmFileUpload
    if (!confirmed) {
      throw new FileUploadError('Upload confirmation returned false')
    }

    return null

  } catch (error) {
    if (error instanceof FileUploadError) {
      throw error
    } else {
      throw new FileUploadError(error.message)
    }
  }
}

/**
 * Upload data from a stream to EYWA file service.
 */
export async function uploadStream(inputStream, fileData) {
  try {
    const contentType = fileData.content_type || 'application/octet-stream'
    const contentLength = fileData.size
    const progressFn = fileData.progressFn

    if (!contentLength) {
      throw new FileUploadError('size is required for stream uploads')
    }

    // Step 1: Request upload URL
    const uploadQuery = `mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }`

    // Build file input - use fileData directly, just fill in defaults
    const fileInput = {
      ...fileData,
      progressFn: undefined, // Remove non-GraphQL field
      content_type: contentType
    }

    const result = await graphql(uploadQuery, { file: fileInput })

    if (result.error) {
      throw new FileUploadError(`Failed to get upload URL: ${JSON.stringify(result.error)}`)
    }

    const uploadUrl = result.data?.requestUploadURL
    if (!uploadUrl) {
      throw new FileUploadError('No upload URL in response')
    }

    // Step 2: Stream to S3
    const uploadResult = await httpPutStream(uploadUrl, inputStream, contentLength, contentType, progressFn)

    if (uploadResult.status === 'error') {
      throw new FileUploadError(
        `S3 upload failed (${uploadResult.code}): ${uploadResult.message}`,
        { code: uploadResult.code }
      )
    }

    // Step 3: Confirm upload
    const confirmQuery = `mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }`

    const confirmResult = await graphql(confirmQuery, { url: uploadUrl })

    if (confirmResult.error) {
      throw new FileUploadError(`Upload confirmation failed: ${JSON.stringify(confirmResult.error)}`)
    }

    const confirmed = confirmResult.data?.confirmFileUpload
    if (!confirmed) {
      throw new FileUploadError('Upload confirmation returned false')
    }

    return null

  } catch (error) {
    if (error instanceof FileUploadError) {
      throw error
    } else {
      throw new FileUploadError(error.message)
    }
  }
}

/**
 * Upload content directly from memory.
 */
export async function uploadContent(content, fileData) {
  try {
    const contentBuffer = Buffer.isBuffer(content) ? content : Buffer.from(content, 'utf8')
    const fileSize = contentBuffer.length
    const contentType = fileData.content_type || 'text/plain'
    const progressFn = fileData.progressFn

    // Step 1: Request upload URL
    const uploadQuery = `mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }`

    // Build file input - use fileData directly, just fill in computed values
    const fileInput = {
      ...fileData,
      progressFn: undefined, // Remove non-GraphQL field
      content_type: contentType,
      size: fileSize
    }

    const result = await graphql(uploadQuery, { file: fileInput })

    if (result.error) {
      throw new FileUploadError(`Failed to get upload URL: ${JSON.stringify(result.error)}`)
    }

    const uploadUrl = result.data?.requestUploadURL
    if (!uploadUrl) {
      throw new FileUploadError('No upload URL in response')
    }

    // Step 2: Upload content to S3
    const uploadResult = await httpPut(uploadUrl, contentBuffer, contentType, progressFn)

    if (uploadResult.status === 'error') {
      throw new FileUploadError(
        `S3 upload failed (${uploadResult.code}): ${uploadResult.message}`,
        { code: uploadResult.code }
      )
    }

    // Step 3: Confirm upload
    const confirmQuery = `mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }`

    const confirmResult = await graphql(confirmQuery, { url: uploadUrl })

    if (confirmResult.error) {
      throw new FileUploadError(`Upload confirmation failed: ${JSON.stringify(confirmResult.error)}`)
    }

    const confirmed = confirmResult.data?.confirmFileUpload
    if (!confirmed) {
      throw new FileUploadError('Upload confirmation returned false')
    }

    return null

  } catch (error) {
    if (error instanceof FileUploadError) {
      throw error
    } else {
      throw new FileUploadError(error.message)
    }
  }
}

/**
 * Download a file from EYWA and return a stream.
 */
export async function downloadStream(fileUuid) {
  try {
    const query = `query RequestDownload($file: FileInput!) {
            requestDownloadURL(file: $file)
        }`

    const result = await graphql(query, { file: { euuid: fileUuid } })

    if (result.error) {
      throw new FileDownloadError(`Failed to get download URL: ${JSON.stringify(result.error)}`)
    }

    const downloadUrl = result.data?.requestDownloadURL
    if (!downloadUrl) {
      throw new FileDownloadError('No download URL in response')
    }

    const downloadResult = await httpGetStream(downloadUrl)

    if (downloadResult.status === 'error') {
      throw new FileDownloadError(
        `Download failed (${downloadResult.code}): ${downloadResult.message}`,
        { code: downloadResult.code }
      )
    }

    return {
      stream: downloadResult.stream,
      contentLength: downloadResult.contentLength
    }

  } catch (error) {
    if (error instanceof FileDownloadError) {
      throw error
    } else {
      throw new FileDownloadError(error.message)
    }
  }
}

/**
 * Download a file from EYWA and return the content as a Buffer.
 */
export async function download(fileUuid) {
  try {
    const result = await downloadStream(fileUuid)

    const chunks = []
    for await (const chunk of result.stream) {
      chunks.push(chunk)
    }

    return Buffer.concat(chunks)

  } catch (error) {
    if (error instanceof FileDownloadError) {
      throw error
    } else {
      throw new FileDownloadError(error.message)
    }
  }
}

// ============================================================================
// File Management Operations
// ============================================================================

/**
 * Get information about a specific file.
 */
export async function fileInfo(fileUuid) {
  try {
    const query = `query GetFile($uuid: UUID!) {
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
                    path
                }
            }
        }`

    const result = await graphql(query, { uuid: fileUuid })

    if (result.error) {
      throw new Error(`Failed to get file info: ${JSON.stringify(result.error)}`)
    }

    return result.data?.getFile || null

  } catch (error) {
    throw error
  }
}

/**
 * List files in EYWA file service.
 */
export async function list(filters = {}) {
  try {
    // Handle folder filtering using relationship filtering
    const folderFilter = filters.folder
    let folderWhere = ''
    if (folderFilter) {
      if (folderFilter.euuid) {
        folderWhere = `(_where: {euuid: {_eq: "${folderFilter.euuid}"}})`
      } else if (folderFilter.path) {
        folderWhere = `(_where: {path: {_eq: "${folderFilter.path}"}})`
      }
    }

    const query = `query ListFiles($limit: Int, $where: searchFileOperator) {
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
                folder${folderWhere} {
                    euuid
                    name
                    path
                }
            }
        }`

    const whereConditions = []

    if (filters.status) {
      whereConditions.push({ status: { _eq: filters.status } })
    }

    if (filters.name) {
      whereConditions.push({ name: { _ilike: `%${filters.name}%` } })
    }

    const variables = {}
    if (filters.limit) {
      variables.limit = filters.limit
    }
    if (whereConditions.length > 0) {
      variables.where = whereConditions.length === 1 ?
        whereConditions[0] :
        { _and: whereConditions }
    }

    const result = await graphql(query, variables)

    if (result.error) {
      throw new Error(`Failed to list files: ${JSON.stringify(result.error)}`)
    }

    // When using folder relationship filtering, null result means no matches
    if (result.data?.searchFile === null) {
      return []
    }

    return result.data?.searchFile || []

  } catch (error) {
    throw error
  }
}

/**
 * Delete a file from EYWA file service.
 */
export async function deleteFile(fileUuid) {
  try {
    const mutation = `mutation DeleteFile($uuid: UUID!) {
            deleteFile(euuid: $uuid)
        }`

    const result = await graphql(mutation, { uuid: fileUuid })

    if (result.error) {
      throw new Error(`Failed to delete file: ${JSON.stringify(result.error)}`)
    }

    return result.data?.deleteFile === true

  } catch (error) {
    throw error
  }
}

// ============================================================================
// Folder Operations  
// ============================================================================

/**
 * Create a new folder in EYWA file service.
 */
export async function createFolder(folderData) {
  try {
    const mutation = `mutation CreateFolder($folder: FolderInput!) {
            stackFolder(data: $folder) {
                euuid
                name
                path
                modified_on
                parent {
                    euuid
                    name
                }
            }
        }`

    const variables = { folder: folderData }

    const result = await graphql(mutation, variables)

    if (result.error) {
      throw new Error(`Failed to create folder: ${JSON.stringify(result.error)}`)
    }

    return result.data?.stackFolder

  } catch (error) {
    throw error
  }
}

/**
 * List folders in EYWA file service.
 */
export async function listFolders(filters = {}) {
  try {
    const query = `query ListFolders($limit: Int, $where: searchFolderOperator) {
            searchFolder(_limit: $limit, _where: $where, _order_by: {name: asc}) {
                euuid
                name
                path
                modified_on
                parent {
                    euuid
                    name
                }
            }
        }`

    const parentFilter = filters.parent
    const whereConditions = []

    if (filters.name) {
      whereConditions.push({ name: { _ilike: `%${filters.name}%` } })
    }

    if (parentFilter !== undefined) {
      if (parentFilter === null) {
        // Root folders only
        whereConditions.push({ parent: { _is_null: true } })
      } else if (parentFilter.euuid) {
        whereConditions.push({ parent: { euuid: { _eq: parentFilter.euuid } } })
      } else if (parentFilter.path) {
        whereConditions.push({ parent: { path: { _eq: parentFilter.path } } })
      } else {
        throw new Error('Invalid parent filter - must be null, {euuid: ...}, or {path: ...}')
      }
    }

    const variables = {}
    if (filters.limit) {
      variables.limit = filters.limit
    }
    if (whereConditions.length > 0) {
      variables.where = whereConditions.length === 1 ?
        whereConditions[0] :
        { _and: whereConditions }
    }

    const result = await graphql(query, variables)

    if (result.error) {
      throw new Error(`Failed to list folders: ${JSON.stringify(result.error)}`)
    }

    return result.data?.searchFolder || []

  } catch (error) {
    throw error
  }
}

/**
 * Get information about a specific folder.
 */
export async function getFolderInfo(data) {
  try {
    const query = data.euuid ?
      `query GetFolder($euuid: UUID!) {
                getFolder(euuid: $euuid) {
                    euuid
                    name
                    path
                    modified_on
                    parent {
                        euuid
                        name
                    }
                }
            }` :
      `query GetFolder($path: String!) {
                getFolder(path: $path) {
                    euuid
                    name
                    path
                    modified_on
                    parent {
                        euuid
                        name
                    }
                }
            }`

    const variables = data.euuid ? { euuid: data.euuid } : { path: data.path }

    const result = await graphql(query, variables)

    if (result.error) {
      throw new Error(`Failed to get folder info: ${JSON.stringify(result.error)}`)
    }

    return result.data?.getFolder || null

  } catch (error) {
    throw error
  }
}

/**
 * Delete a folder from EYWA file service.
 */
export async function deleteFolder(folderUuid) {
  try {
    const mutation = `mutation DeleteFolder($uuid: UUID!) {
            deleteFolder(euuid: $uuid)
        }`

    const result = await graphql(mutation, { uuid: folderUuid })

    if (result.error) {
      throw new Error(`Failed to delete folder: ${JSON.stringify(result.error)}`)
    }

    return result.data?.deleteFolder === true

  } catch (error) {
    throw error
  }
}
