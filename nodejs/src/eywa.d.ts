/**
 * EYWA Client TypeScript Definitions
 * 
 * GraphQL-aligned EYWA client following the Babashka pattern.
 * All functions use single map arguments that directly mirror GraphQL schema.
 * Client controls UUID management for both creation and updates.
 */

// ============================================================================
// Core Types
// ============================================================================

export interface GraphQLResponse<T = any> {
  data?: T;
  error?: any;
}

export interface TaskInfo {
  id: string;
  status: string;
  [key: string]: any;
}

export type TaskStatus = 'SUCCESS' | 'ERROR' | 'PROCESSING' | 'EXCEPTION';

export interface LogOptions {
  event?: string;
  message: string;
  data?: any;
  duration?: number;
  coordinates?: any;
  time?: string | Date;
}

// ============================================================================
// GraphQL Input Types (Mirror Server Schema)
// ============================================================================

export interface FileInput {
  /** Filename (required) */
  name: string;
  /** File UUID - provide for updates, omit for new files (EYWA generates) */
  euuid?: string;
  /** Parent folder reference */
  folder?: FolderReference;
  /** MIME content type */
  content_type?: string;
  /** File size in bytes */
  size?: number;
}

export interface FolderInput {
  /** Folder name (required) */
  name: string;
  /** Folder UUID - provide for deterministic creation, omit for auto-generation */
  euuid?: string;
  /** Parent folder reference - omit for root folder */
  parent?: FolderReference;
}

export interface FolderReference {
  /** Folder UUID */
  euuid: string;
}

export interface FileReference {
  /** File UUID */
  euuid: string;
}

// ============================================================================
// GraphQL Output Types
// ============================================================================

export interface FileInfo {
  euuid: string;
  name: string;
  status: string;
  content_type: string;
  size: number;
  uploaded_at: string;
  uploaded_by?: {
    name: string;
  };
  folder?: {
    euuid: string;
    name: string;
    path: string;
  };
}

export interface FolderInfo {
  euuid: string;
  name: string;
  path: string;
  modified_on: string;
  parent?: {
    euuid: string;
    name: string;
  };
}

// ============================================================================
// Filter Types for List Operations
// ============================================================================

export interface FileListFilters {
  /** Maximum number of files to return */
  limit?: number;
  /** Filter by file status */
  status?: string;
  /** Filter by name pattern (SQL LIKE) */
  name?: string;
  /** Filter by folder */
  folder?: {
    /** Filter by folder UUID */
    euuid?: string;
    /** Filter by folder path */
    path?: string;
  };
}

export interface FolderListFilters {
  /** Maximum number of folders to return */
  limit?: number;
  /** Filter by name pattern (SQL LIKE) */
  name?: string;
  /** Filter by parent folder */
  parent?: {
    /** Filter by parent UUID */
    euuid?: string;
    /** Filter by parent path */
    path?: string;
  } | null; // null = root folders only
}

// ============================================================================
// Upload Options (Non-GraphQL Fields)
// ============================================================================

export interface UploadOptions extends Omit<FileInput, 'size'> {
  /** Progress callback function (bytes_uploaded, total_bytes) */
  progressFn?: (uploaded: number, total: number) => void;
  /** File size (auto-detected for file paths, required for streams) */
  size?: number;
}

export interface StreamUploadOptions extends FileInput {
  /** Progress callback function (bytes_uploaded, total_bytes) */
  progressFn?: (uploaded: number, total: number) => void;
  /** Content length in bytes (required for streams) */
  size: number;
}

export interface ContentUploadOptions extends Omit<FileInput, 'size'> {
  /** Progress callback function (bytes_uploaded, total_bytes) */
  progressFn?: (uploaded: number, total: number) => void;
}

// ============================================================================
// Download Types
// ============================================================================

export interface DownloadStreamResult {
  /** Readable stream of file content */
  stream: NodeJS.ReadableStream;
  /** Content length in bytes (0 if unknown) */
  contentLength: number;
}

// ============================================================================
// Exception Types
// ============================================================================

export class FileUploadError extends Error {
  name: 'FileUploadError';
  type: string;
  code?: number;
  
  constructor(message: string, options?: { type?: string; code?: number });
}

export class FileDownloadError extends Error {
  name: 'FileDownloadError';
  type: string;
  code?: number;
  
  constructor(message: string, options?: { type?: string; code?: number });
}

// ============================================================================
// Core EYWA Client Functions
// ============================================================================

export function send_request(data: any): Promise<any>;
export function send_notification(data: any): void;
export function register_handler(method: string, handler: (data: any) => void): void;
export function open_pipe(): void;

export function get_task(): Promise<TaskInfo>;
export function update_task(status?: TaskStatus): void;
export function return_task(): void;
export function close_task(status?: TaskStatus): void;

export function log(options: LogOptions): void;
export function info(message: string, data?: any): void;
export function error(message: string, data?: any): void;
export function debug(message: string, data?: any): void;
export function trace(message: string, data?: any): void;
export function warn(message: string, data?: any): void;
export function exception(message: string, data?: any): void;
export function report(message: string, data?: any, image?: any): void;

export function graphql<T = any>(query: string, variables?: any): Promise<GraphQLResponse<T>>;

// ============================================================================
// File Operations (GraphQL-Aligned)
// ============================================================================

/**
 * Upload a file to EYWA using the 3-step protocol.
 * 
 * @param filepath File path string or File object
 * @param fileData File input matching GraphQL FileInput type
 * @returns Promise that resolves to null on success or throws FileUploadError
 */
export function upload(filepath: string | File, fileData: UploadOptions): Promise<null>;

/**
 * Upload data from a stream to EYWA.
 * 
 * @param inputStream Readable stream to upload
 * @param fileData File input with required size field
 * @returns Promise that resolves to null on success or throws FileUploadError
 */
export function uploadStream(inputStream: NodeJS.ReadableStream, fileData: StreamUploadOptions): Promise<null>;

/**
 * Upload content directly from memory.
 * 
 * @param content String or Buffer content
 * @param fileData File input matching GraphQL FileInput type
 * @returns Promise that resolves to null on success or throws FileUploadError
 */
export function uploadContent(content: string | Buffer, fileData: ContentUploadOptions): Promise<null>;

/**
 * Download a file as a Buffer.
 * 
 * @param fileUuid UUID of the file to download
 * @returns Promise that resolves to file content as Buffer
 */
export function download(fileUuid: string): Promise<Buffer>;

/**
 * Download a file as a stream.
 * 
 * @param fileUuid UUID of the file to download
 * @returns Promise that resolves to stream and content length
 */
export function downloadStream(fileUuid: string): Promise<DownloadStreamResult>;

/**
 * Get information about a specific file.
 * 
 * @param fileUuid UUID of the file
 * @returns Promise that resolves to file info or null if not found
 */
export function fileInfo(fileUuid: string): Promise<FileInfo | null>;

/**
 * List files with optional filters.
 * 
 * @param filters Optional filter criteria
 * @returns Promise that resolves to array of file objects
 */
export function list(filters?: FileListFilters): Promise<FileInfo[]>;

/**
 * Delete a file.
 * 
 * @param fileUuid UUID of the file to delete
 * @returns Promise that resolves to true if successful
 */
export function deleteFile(fileUuid: string): Promise<boolean>;

// ============================================================================
// Folder Operations (GraphQL-Aligned)
// ============================================================================

/**
 * Create a new folder.
 * 
 * @param folderData Folder input matching GraphQL FolderInput type
 * @returns Promise that resolves to created folder information
 */
export function createFolder(folderData: FolderInput): Promise<FolderInfo>;

/**
 * List folders with optional filters.
 * 
 * @param filters Optional filter criteria
 * @returns Promise that resolves to array of folder objects
 */
export function listFolders(filters?: FolderListFilters): Promise<FolderInfo[]>;

/**
 * Get information about a specific folder.
 * 
 * @param data Object with either euuid or path
 * @returns Promise that resolves to folder info or null if not found
 */
export function getFolderInfo(data: { euuid: string } | { path: string }): Promise<FolderInfo | null>;

/**
 * Delete a folder (must be empty).
 * 
 * @param folderUuid UUID of the folder to delete
 * @returns Promise that resolves to true if successful
 */
export function deleteFolder(folderUuid: string): Promise<boolean>;

// ============================================================================
// Constants
// ============================================================================

/** Root folder UUID */
export const rootUuid: string;

/** Root folder reference object */
export const rootFolder: FolderReference;

/** Task status constants */
export const SUCCESS: 'SUCCESS';
export const ERROR: 'ERROR';
export const PROCESSING: 'PROCESSING';
export const EXCEPTION: 'EXCEPTION';

// ============================================================================
// Default Export
// ============================================================================

declare const eywa: {
  // Core functions
  send_request: typeof send_request;
  send_notification: typeof send_notification;
  register_handler: typeof register_handler;
  open_pipe: typeof open_pipe;
  get_task: typeof get_task;
  update_task: typeof update_task;
  return_task: typeof return_task;
  close_task: typeof close_task;
  log: typeof log;
  info: typeof info;
  error: typeof error;
  debug: typeof debug;
  trace: typeof trace;
  warn: typeof warn;
  exception: typeof exception;
  report: typeof report;
  graphql: typeof graphql;
  
  // File operations
  upload: typeof upload;
  uploadStream: typeof uploadStream;
  uploadContent: typeof uploadContent;
  download: typeof download;
  downloadStream: typeof downloadStream;
  fileInfo: typeof fileInfo;
  list: typeof list;
  deleteFile: typeof deleteFile;
  
  // Folder operations
  createFolder: typeof createFolder;
  listFolders: typeof listFolders;
  getFolderInfo: typeof getFolderInfo;
  deleteFolder: typeof deleteFolder;
  
  // Constants
  rootUuid: typeof rootUuid;
  rootFolder: typeof rootFolder;
  FileUploadError: typeof FileUploadError;
  FileDownloadError: typeof FileDownloadError;
  SUCCESS: typeof SUCCESS;
  ERROR: typeof ERROR;
  PROCESSING: typeof PROCESSING;
  EXCEPTION: typeof EXCEPTION;
};

export default eywa;
