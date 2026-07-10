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

/**
 * EYWA's data endpoint (`eywa.datasets.graphql`) does NOT wrap responses in
 * a `{ data, errors }` envelope at this transport layer. On success,
 * `graphql()` resolves with the GraphQL `data` field directly. On failure it
 * throws an `EywaGraphQLError`.
 *
 * Kept here only as a documentation alias.
 */
export type GraphQLData<T = any> = T;

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
// Report Types
// ============================================================================

export interface ReportData {
  /** Markdown content for rich formatting */
  card?: string;
  /** Named tables with headers and rows */
  tables?: {
    [tableName: string]: {
      headers: string[];
      rows: any[][];
    };
  };
}

export interface ReportOptions {
  /** Structured report content */
  data?: ReportData;
  /** Base64 encoded image/chart data */
  image?: string;
  /** Additional metadata */
  metadata?: Record<string, any>;
}

export interface TaskReport {
  /** Report UUID */
  euuid: string;
  /** Report message/title */
  message: string;
  /** Whether report has markdown card */
  has_card: boolean;
  /** Whether report has data tables */
  has_table: boolean;
  /** Whether report has image */
  has_image: boolean;
  /** Report creation timestamp */
  created_on: string;
}

// ============================================================================
// Exception Types
// ============================================================================

export interface EywaErrorOptions {
  /** JSON-RPC error code from the server (or HTTP status for transport errors). */
  code?: number | string;
  /** Structured error payload from the server (path, extensions, ...). */
  data?: any;
  /** Underlying error that triggered this one — useful for diagnostics. */
  cause?: unknown;
}

/** Base class for every error thrown by this client. */
export class EywaError extends Error {
  name: string;
  code?: number | string;
  data?: any;
  cause?: unknown;

  constructor(message: string, options?: EywaErrorOptions);
}

/** Thrown when a JSON-RPC call to the server fails (any method). */
export class EywaRPCError extends EywaError {
  name: 'EywaRPCError';
  /** The JSON-RPC method that failed (e.g. 'eywa.datasets.graphql'). */
  method?: string;

  constructor(message: string, options?: EywaErrorOptions & { method?: string });
}

/**
 * Thrown by `graphql()` when the server returns a GraphQL error.
 *
 * `.code` is the JSON-RPC error code (-32602 for GraphQL errors, -32603 for
 * server-internal). `.data` contains the remaining fields of the originating
 * GraphQL error (path, extensions, locations, ...). `.query` and `.variables`
 * carry the failing request for diagnostics.
 */
export class EywaGraphQLError extends EywaRPCError {
  name: 'EywaGraphQLError';
  query?: string;
  variables?: any;

  constructor(message: string, options?: EywaErrorOptions & {
    method?: string;
    query?: string;
    variables?: any;
  });
}

export class FileUploadError extends EywaError {
  name: 'FileUploadError';
  type: string;
  code?: number | string;

  constructor(message: string, options?: EywaErrorOptions & { type?: string });
}

export class FileDownloadError extends EywaError {
  name: 'FileDownloadError';
  type: string;
  code?: number | string;

  constructor(message: string, options?: EywaErrorOptions & { type?: string });
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
export function report(message: string, options?: ReportOptions): Promise<TaskReport | null>;

/**
 * Execute a GraphQL operation against EYWA's data endpoint.
 *
 * Resolves with the GraphQL `data` field directly (no `{data, errors}` envelope).
 * Throws `EywaGraphQLError` on failure — inspect `.code`, `.data`, `.query`, and
 * `.variables` for diagnostics.
 */
export function graphql<T = any>(query: string, variables?: any): Promise<T>;

export interface AccessTokenInfo {
  /** Short-lived JWT bound to this robot's currently-executing root task. */
  token: string;
  /** Lifetime in seconds. */
  expires_in: number;
  /** Token scheme — currently always "Bearer". */
  token_type: string;
}

/**
 * Request a short-lived access token bound to this robot's currently-executing
 * root task. Pass `token` to a downstream app so it can authenticate back to
 * EYWA on behalf of this robot.
 */
export function access_token(): Promise<AccessTokenInfo | null>;

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
  access_token: typeof access_token;

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

  // Errors
  EywaError: typeof EywaError;
  EywaRPCError: typeof EywaRPCError;
  EywaGraphQLError: typeof EywaGraphQLError;
  FileUploadError: typeof FileUploadError;
  FileDownloadError: typeof FileDownloadError;

  SUCCESS: typeof SUCCESS;
  ERROR: typeof ERROR;
  PROCESSING: typeof PROCESSING;
  EXCEPTION: typeof EXCEPTION;
};

export default eywa;