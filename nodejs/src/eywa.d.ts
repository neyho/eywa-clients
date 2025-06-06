/**
 * EYWA Client Library for Node.js
 * Provides JSON-RPC communication, GraphQL queries, and task management for EYWA processes
 */

/**
 * Task status constants
 */
export const SUCCESS: "SUCCESS";
export const ERROR: "ERROR";
export const PROCESSING: "PROCESSING";
export const EXCEPTION: "EXCEPTION";

/**
 * Log event types
 */
export type LogEvent = "INFO" | "WARN" | "ERROR" | "TRACE" | "DEBUG" | "EXCEPTION";

/**
 * Task status types
 */
export type TaskStatus = typeof SUCCESS | typeof ERROR | typeof PROCESSING | typeof EXCEPTION;

/**
 * JSON-RPC request structure
 */
export interface JsonRpcRequest {
  jsonrpc?: "2.0";
  method: string;
  params?: any;
  id?: string;
}

/**
 * JSON-RPC response structure
 */
export interface JsonRpcResponse {
  jsonrpc: "2.0";
  result?: any;
  error?: JsonRpcError;
  id: string;
}

/**
 * JSON-RPC error structure
 */
export interface JsonRpcError {
  code: number;
  message: string;
  data?: any;
}

/**
 * Log record structure
 */
export interface LogRecord {
  event?: LogEvent;
  message: string;
  data?: any;
  duration?: number;
  coordinates?: any;
  time?: Date;
}

/**
 * GraphQL variables
 */
export interface GraphQLVariables {
  [key: string]: any;
}

/**
 * Task information
 */
export interface Task {
  euuid?: string;
  message?: string;
  status?: TaskStatus;
  data?: any;
  [key: string]: any;
}

/**
 * Send a JSON-RPC request and wait for response
 * @param data - The request data containing method and params
 * @returns Promise that resolves with the result or rejects with error
 * @example
 * const result = await send_request({
 *   method: 'custom.method',
 *   params: { foo: 'bar' }
 * });
 */
export function send_request(data: Omit<JsonRpcRequest, 'jsonrpc' | 'id'>): Promise<any>;

/**
 * Send a JSON-RPC notification (no response expected)
 * @param data - The notification data containing method and params
 * @example
 * send_notification({
 *   method: 'task.log',
 *   params: { message: 'Processing started' }
 * });
 */
export function send_notification(data: Omit<JsonRpcRequest, 'jsonrpc' | 'id'>): void;

/**
 * Register a handler for incoming JSON-RPC method calls
 * @param method - The method name to handle
 * @param handler - Function to handle the incoming request
 * @example
 * register_handler('custom.action', (data) => {
 *   console.log('Received:', data.params);
 * });
 */
export function register_handler(method: string, handler: (data: JsonRpcRequest) => void): void;

/**
 * Initialize stdin/stdout communication with EYWA runtime
 * Must be called before using any other EYWA functions
 * @example
 * open_pipe();
 * // Now you can use other EYWA functions
 */
export function open_pipe(): void;

/**
 * Get current task information
 * @returns Promise that resolves with task data
 * @example
 * const task = await get_task();
 * console.log('Current task:', task.message);
 */
export function get_task(): Promise<Task>;

/**
 * Log a message with full control over all parameters
 * @param record - The log record with event type, message, and optional metadata
 * @example
 * log({
 *   event: 'INFO',
 *   message: 'Processing item',
 *   data: { itemId: 123 },
 *   duration: 1500
 * });
 */
export function log(record: LogRecord): void;

/**
 * Log an info message
 * @param message - The message to log
 * @param data - Optional structured data to include
 * @example
 * info('User logged in', { userId: 'abc123' });
 */
export function info(message: string, data?: any): void;

/**
 * Log an error message
 * @param message - The error message to log
 * @param data - Optional error details or context
 * @example
 * error('Failed to process file', { filename: 'data.csv', error: err.message });
 */
export function error(message: string, data?: any): void;

/**
 * Log a warning message
 * @param message - The warning message to log
 * @param data - Optional warning context
 * @example
 * warn('API rate limit approaching', { remaining: 10 });
 */
export function warn(message: string, data?: any): void;

/**
 * Log a debug message
 * @param message - The debug message to log
 * @param data - Optional debug data
 * @example
 * debug('Cache hit', { key: 'user:123' });
 */
export function debug(message: string, data?: any): void;

/**
 * Log a trace message (most verbose level)
 * @param message - The trace message to log
 * @param data - Optional trace data
 * @example
 * trace('Entering function processData', { args: [1, 2, 3] });
 */
export function trace(message: string, data?: any): void;

/**
 * Log an exception message
 * @param message - The exception message to log
 * @param data - Optional exception details
 * @example
 * exception('Unhandled error in worker', { stack: err.stack });
 */
export function exception(message: string, data?: any): void;

/**
 * Send a task report with optional data and image
 * @param message - The report message
 * @param data - Optional structured data for the report
 * @param image - Optional image data (base64 or URL)
 * @example
 * report('Analysis complete', { accuracy: 0.95 }, chartImageBase64);
 */
export function report(message: string, data?: any, image?: any): void;

/**
 * Update the current task status
 * @param status - The new status (defaults to PROCESSING)
 * @example
 * update_task(PROCESSING);
 * // Do some work...
 * update_task(SUCCESS);
 */
export function update_task(status?: TaskStatus): void;

/**
 * Return control to EYWA without closing the task
 * Exits the process with code 0
 * @example
 * // Hand back control to EYWA
 * return_task();
 */
export function return_task(): void;

/**
 * Close the current task with a final status
 * Exits the process with code 0 for SUCCESS, 1 for other statuses
 * @param status - The final task status (defaults to SUCCESS)
 * @example
 * try {
 *   // Do work...
 *   close_task(SUCCESS);
 * } catch (err) {
 *   error('Task failed', err);
 *   close_task(ERROR);
 * }
 */
export function close_task(status?: TaskStatus): void;

/**
 * Execute a GraphQL query against the EYWA server
 * @param query - The GraphQL query string
 * @param variables - Optional variables for the query
 * @returns Promise that resolves with the query result
 * @example
 * const result = await graphql(`
 *   query GetUser($id: UUID!) {
 *     getUser(euuid: $id) {
 *       name
 *       email
 *     }
 *   }
 * `, { id: 'user-uuid' });
 */
export function graphql(query: string, variables?: GraphQLVariables): Promise<any>;

/**
 * Default export containing all functions and constants
 */
declare const eywa: {
  // Core JSON-RPC methods
  send_request: typeof send_request;
  send_notification: typeof send_notification;
  register_handler: typeof register_handler;
  open_pipe: typeof open_pipe;
  
  // Task management
  get_task: typeof get_task;
  update_task: typeof update_task;
  return_task: typeof return_task;
  close_task: typeof close_task;
  
  // Logging methods
  log: typeof log;
  info: typeof info;
  error: typeof error;
  debug: typeof debug;
  trace: typeof trace;
  warn: typeof warn;
  exception: typeof exception;
  report: typeof report;
  
  // GraphQL
  graphql: typeof graphql;
  
  // Constants
  SUCCESS: typeof SUCCESS;
  ERROR: typeof ERROR;
  PROCESSING: typeof PROCESSING;
  EXCEPTION: typeof EXCEPTION;
};

export default eywa;
