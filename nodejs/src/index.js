/**
 * EYWA Client - Main Entry Point
 * 
 * GraphQL-aligned EYWA client following the Babashka pattern.
 * All functions use single map arguments that directly mirror GraphQL schema.
 * Client controls UUID management for both creation and updates.
 */

// Import core EYWA functions
import {
  send_request,
  send_notification,
  register_handler,
  open_pipe,
  get_task,
  update_task,
  return_task,
  close_task,
  log,
  info,
  error,
  debug,
  trace,
  warn,
  exception,
  report,
  graphql,
  SUCCESS,
  ERROR,
  PROCESSING,
  EXCEPTION
} from './eywa.js'

// Import GraphQL-aligned file operations
import {
  // File operations
  upload,
  uploadStream,
  uploadContent,
  download,
  downloadStream,
  fileInfo,
  list,
  deleteFile,
  
  // Folder operations
  createFolder,
  listFolders,
  getFolderInfo,
  deleteFolder,
  
  // Constants and utilities
  rootUuid,
  rootFolder,
  
  // Exception types
  FileUploadError,
  FileDownloadError
} from './files.js'

// Export everything as default object
export default {
  // Core JSON-RPC methods
  send_request,
  send_notification,
  register_handler,
  open_pipe,
  
  // Task management
  get_task,
  update_task,
  return_task,
  close_task,
  
  // Logging methods
  log,
  info,
  error,
  debug,
  trace,
  warn,
  exception,
  report,
  graphql,
  
  // File operations (GraphQL-aligned)
  upload,
  uploadStream,
  uploadContent,
  download,
  downloadStream,
  fileInfo,
  list,
  deleteFile,
  
  // Folder operations (GraphQL-aligned)
  createFolder,
  listFolders,
  getFolderInfo,
  deleteFolder,
  
  // Constants
  rootUuid,
  rootFolder,
  FileUploadError,
  FileDownloadError,
  SUCCESS,
  ERROR,
  PROCESSING,
  EXCEPTION
}

// Also export everything as named exports for flexibility
export {
  // Core
  send_request,
  send_notification,
  register_handler,
  open_pipe,
  get_task,
  update_task,
  return_task,
  close_task,
  log,
  info,
  error,
  debug,
  trace,
  warn,
  exception,
  report,
  graphql,
  
  // File operations (GraphQL-aligned)
  upload,
  uploadStream,
  uploadContent,
  download,
  downloadStream,
  fileInfo,
  list,
  deleteFile,
  
  // Folder operations (GraphQL-aligned)
  createFolder,
  listFolders,
  getFolderInfo,
  deleteFolder,
  
  // Constants
  rootUuid,
  rootFolder,
  FileUploadError,
  FileDownloadError,
  SUCCESS,
  ERROR,
  PROCESSING,
  EXCEPTION
}