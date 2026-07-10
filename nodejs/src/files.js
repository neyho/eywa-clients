/**
 * EYWA File Operations for Node.js
 *
 * GraphQL-aligned file operations following the Babashka client pattern.
 * All functions use single map arguments that directly mirror GraphQL schema.
 * Client controls UUID management for both creation and updates.
 */

import { Readable } from 'stream'
import fetch from 'node-fetch'
import { lookup } from 'mime-types'
import { graphql, EywaError, EywaGraphQLError } from './eywa.js'

// ============================================================================
// Errors
// ============================================================================
//
// File upload/download wrap any underlying transport or GraphQL failure so the
// caller can `catch (e) { if (e instanceof FileUploadError) ... }` without
// losing the original error. Inspect `.cause` for the underlying problem and
// `.cause.code` / `.cause.data` for structured RPC details.

export class FileUploadError extends EywaError {
  constructor(message, options = {}) {
    super(message, options)
    this.name = 'FileUploadError'
    this.type = options.type || 'upload-error'
    if (options.code !== undefined) this.code = options.code
  }
}

export class FileDownloadError extends EywaError {
  constructor(message, options = {}) {
    super(message, options)
    this.name = 'FileDownloadError'
    this.type = options.type || 'download-error'
    if (options.code !== undefined) this.code = options.code
  }
}

// Root folder UUID (matches Babashka client)
export const rootUuid = '87ce50d8-5dfa-4008-a265-053e727ab793'
export const rootFolder = { euuid: rootUuid }

function mimeType(filename) {
  const detected = lookup(filename)
  if (detected) return detected

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

async function httpPutStream(url, inputStream, contentLength, contentType, progressFn) {
  try {
    if (progressFn) progressFn(0, contentLength)

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

// Wrap any thrown error from a graphql() call into a FileUploadError /
// FileDownloadError, preserving the original on `.cause`. `EywaGraphQLError`s
// carry `.code` and `.data` from the server; re-export those for parity.
function wrapGraphqlError(err, Wrap, prefix) {
  if (err instanceof Wrap) return err
  if (err instanceof EywaGraphQLError) {
    return new Wrap(`${prefix}: ${err.message}`, {
      code: err.code,
      data: err.data,
      cause: err
    })
  }
  return new Wrap(`${prefix}: ${err.message || err}`, { cause: err })
}

// ============================================================================
// Core File Operations (Following Babashka Pattern)
// ============================================================================

/**
 * Upload a file to EYWA file service using streaming.
 */
export async function upload(filepath, fileData) {
  const progressFn = fileData.progressFn

  let fileStats, fileName, fileSize

  if (typeof filepath === 'string') {
    const fs = await import('fs/promises')
    const path = await import('path')

    try {
      fileStats = await fs.stat(filepath)
      if (!fileStats.isFile()) {
        throw new FileUploadError(`Path is not a file: ${filepath}`)
      }
    } catch (err) {
      if (err instanceof FileUploadError) throw err
      throw new FileUploadError(`File not found: ${filepath}`, { cause: err })
    }

    fileSize = fileStats.size
    fileName = fileData.name || path.basename(filepath)
  } else {
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

  const uploadQuery = `mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }`

  const fileInput = {
    ...fileData,
    progressFn: undefined,
    name: fileName,
    content_type: detectedContentType,
    size: fileSize
  }

  let uploadUrl
  try {
    const result = await graphql(uploadQuery, { file: fileInput })
    uploadUrl = result?.requestUploadURL
  } catch (err) {
    throw wrapGraphqlError(err, FileUploadError, 'Failed to get upload URL')
  }

  if (!uploadUrl) {
    throw new FileUploadError('No upload URL in response')
  }

  let uploadResult
  if (typeof filepath === 'string') {
    const fs = await import('fs')
    const fileStream = fs.createReadStream(filepath)
    uploadResult = await httpPutStream(uploadUrl, fileStream, fileSize, detectedContentType, progressFn)
  } else {
    const stream = filepath.stream ? filepath.stream() : Readable.from(filepath)
    uploadResult = await httpPutStream(uploadUrl, stream, fileSize, detectedContentType, progressFn)
  }

  if (uploadResult.status === 'error') {
    throw new FileUploadError(
      `S3 upload failed (${uploadResult.code}): ${uploadResult.message}`,
      { code: uploadResult.code }
    )
  }

  const confirmQuery = `mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }`

  let confirmed
  try {
    const confirmResult = await graphql(confirmQuery, { url: uploadUrl })
    confirmed = confirmResult?.confirmFileUpload
  } catch (err) {
    throw wrapGraphqlError(err, FileUploadError, 'Upload confirmation failed')
  }

  if (!confirmed) {
    throw new FileUploadError('Upload confirmation returned false')
  }

  return null
}

/**
 * Upload data from a stream to EYWA file service.
 */
export async function uploadStream(inputStream, fileData) {
  const contentType = fileData.content_type || 'application/octet-stream'
  const contentLength = fileData.size
  const progressFn = fileData.progressFn

  if (!contentLength) {
    throw new FileUploadError('size is required for stream uploads')
  }

  const uploadQuery = `mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }`

  const fileInput = {
    ...fileData,
    progressFn: undefined,
    content_type: contentType
  }

  let uploadUrl
  try {
    const result = await graphql(uploadQuery, { file: fileInput })
    uploadUrl = result?.requestUploadURL
  } catch (err) {
    throw wrapGraphqlError(err, FileUploadError, 'Failed to get upload URL')
  }

  if (!uploadUrl) {
    throw new FileUploadError('No upload URL in response')
  }

  const uploadResult = await httpPutStream(uploadUrl, inputStream, contentLength, contentType, progressFn)

  if (uploadResult.status === 'error') {
    throw new FileUploadError(
      `S3 upload failed (${uploadResult.code}): ${uploadResult.message}`,
      { code: uploadResult.code }
    )
  }

  const confirmQuery = `mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }`

  let confirmed
  try {
    const confirmResult = await graphql(confirmQuery, { url: uploadUrl })
    confirmed = confirmResult?.confirmFileUpload
  } catch (err) {
    throw wrapGraphqlError(err, FileUploadError, 'Upload confirmation failed')
  }

  if (!confirmed) {
    throw new FileUploadError('Upload confirmation returned false')
  }

  return null
}

/**
 * Upload content directly from memory.
 */
export async function uploadContent(content, fileData) {
  const contentBuffer = Buffer.isBuffer(content) ? content : Buffer.from(content, 'utf8')
  const fileSize = contentBuffer.length
  const contentType = fileData.content_type || 'text/plain'
  const progressFn = fileData.progressFn

  const uploadQuery = `mutation RequestUpload($file: FileInput!) {
            requestUploadURL(file: $file)
        }`

  const fileInput = {
    ...fileData,
    progressFn: undefined,
    content_type: contentType,
    size: fileSize
  }

  let uploadUrl
  try {
    const result = await graphql(uploadQuery, { file: fileInput })
    uploadUrl = result?.requestUploadURL
  } catch (err) {
    throw wrapGraphqlError(err, FileUploadError, 'Failed to get upload URL')
  }

  if (!uploadUrl) {
    throw new FileUploadError('No upload URL in response')
  }

  const uploadResult = await httpPut(uploadUrl, contentBuffer, contentType, progressFn)

  if (uploadResult.status === 'error') {
    throw new FileUploadError(
      `S3 upload failed (${uploadResult.code}): ${uploadResult.message}`,
      { code: uploadResult.code }
    )
  }

  const confirmQuery = `mutation ConfirmUpload($url: String!) {
            confirmFileUpload(url: $url)
        }`

  let confirmed
  try {
    const confirmResult = await graphql(confirmQuery, { url: uploadUrl })
    confirmed = confirmResult?.confirmFileUpload
  } catch (err) {
    throw wrapGraphqlError(err, FileUploadError, 'Upload confirmation failed')
  }

  if (!confirmed) {
    throw new FileUploadError('Upload confirmation returned false')
  }

  return null
}

/**
 * Download a file from EYWA and return a stream.
 */
export async function downloadStream(fileUuid) {
  const query = `query RequestDownload($file: FileInput!) {
            requestDownloadURL(file: $file)
        }`

  let downloadUrl
  try {
    const result = await graphql(query, { file: { euuid: fileUuid } })
    downloadUrl = result?.requestDownloadURL
  } catch (err) {
    throw wrapGraphqlError(err, FileDownloadError, 'Failed to get download URL')
  }

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
}

/**
 * Download a file from EYWA and return the content as a Buffer.
 */
export async function download(fileUuid) {
  const result = await downloadStream(fileUuid)

  const chunks = []
  for await (const chunk of result.stream) {
    chunks.push(chunk)
  }

  return Buffer.concat(chunks)
}

// ============================================================================
// File Management Operations
// ============================================================================

/**
 * Get information about a specific file.
 */
export async function fileInfo(fileUuid) {
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
  return result?.getFile ?? null
}

/**
 * List files in EYWA file service.
 */
export async function list(filters = {}) {
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

  // When using folder relationship filtering, null result means no matches.
  if (result?.searchFile === null) {
    return []
  }

  return result?.searchFile ?? []
}

/**
 * Delete a file from EYWA file service.
 */
export async function deleteFile(fileUuid) {
  const mutation = `mutation DeleteFile($uuid: UUID!) {
            deleteFile(euuid: $uuid)
        }`

  const result = await graphql(mutation, { uuid: fileUuid })
  return result?.deleteFile === true
}

// ============================================================================
// Folder Operations
// ============================================================================

/**
 * Create a new folder in EYWA file service.
 */
export async function createFolder(folderData) {
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

  const result = await graphql(mutation, { folder: folderData })
  return result?.stackFolder ?? null
}

/**
 * List folders in EYWA file service.
 */
export async function listFolders(filters = {}) {
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
      whereConditions.push({ parent: { _is_null: true } })
    } else if (parentFilter.euuid) {
      whereConditions.push({ parent: { euuid: { _eq: parentFilter.euuid } } })
    } else if (parentFilter.path) {
      whereConditions.push({ parent: { path: { _eq: parentFilter.path } } })
    } else {
      throw new EywaError('Invalid parent filter - must be null, {euuid: ...}, or {path: ...}')
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
  return result?.searchFolder ?? []
}

/**
 * Get information about a specific folder.
 */
export async function getFolderInfo(data) {
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
  return result?.getFolder ?? null
}

/**
 * Delete a folder from EYWA file service.
 */
export async function deleteFolder(folderUuid) {
  const mutation = `mutation DeleteFolder($uuid: UUID!) {
            deleteFolder(euuid: $uuid)
        }`

  const result = await graphql(mutation, { uuid: folderUuid })
  return result?.deleteFolder === true
}
