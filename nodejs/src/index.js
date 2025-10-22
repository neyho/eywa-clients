/**
 * EYWA Client - Main Entry Point
 * 
 * This module combines core EYWA functionality with file operations
 * into a single unified export.
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

// Import file operations
import {
  createFolder,
  deleteFolder,
  uploadFile,
  downloadFile,
  deleteFile,
  FileUploadError,
  FileDownloadError
} from './eywa_files.js'

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
  
  // File operations
  createFolder,
  deleteFolder,
  uploadFile,
  downloadFile,
  deleteFile,
  FileUploadError,
  FileDownloadError,
  
  // Constants
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
  
  // File operations
  createFolder,
  deleteFolder,
  uploadFile,
  downloadFile,
  deleteFile,
  FileUploadError,
  FileDownloadError,
  
  // Constants
  SUCCESS,
  ERROR,
  PROCESSING,
  EXCEPTION
}
