import { nanoid } from 'nanoid'
import process from 'node:process'


const rpc_callbacks = new Map()
const handlers = new Map()


// ============================================================================
// Errors
// ============================================================================
//
// The data endpoint speaks JSON-RPC 2.0 over stdio. Server responses follow:
//
//   success: { jsonrpc: "2.0", id, result: <GraphQL data, unwrapped> }
//   error:   { jsonrpc: "2.0", id, error: { code, message, data } }
//
// We surface every error path as a real Error subclass so callers can use the
// familiar try/catch + instanceof flow and still introspect the structured
// fields (code, data, and — for graphql() — the originating query/variables).

export class EywaError extends Error {
  constructor(message, { code, data, cause } = {}) {
    super(message)
    this.name = 'EywaError'
    if (code !== undefined) this.code = code
    if (data !== undefined) this.data = data
    if (cause !== undefined) this.cause = cause
  }
}

export class EywaRPCError extends EywaError {
  constructor(message, options = {}) {
    super(message, options)
    this.name = 'EywaRPCError'
    if (options.method !== undefined) this.method = options.method
  }
}

export class EywaGraphQLError extends EywaRPCError {
  constructor(message, options = {}) {
    super(message, options)
    this.name = 'EywaGraphQLError'
    if (options.query !== undefined) this.query = options.query
    if (options.variables !== undefined) this.variables = options.variables
  }
}


// ============================================================================
// JSON-RPC plumbing
// ============================================================================

const handleData = (data) => {
  let { method, id, result, error } = data
  if (method !== undefined) {
    handleRequest(data)
  } else if (id !== undefined && (result !== undefined || error !== undefined)) {
    handleResponse(data)
  } else {
    console.error('Received invalid JSON-RPC:\n' + JSON.stringify(data))
  }
}


const handleRequest = (data) => {
  let { method } = data
  let handler = handlers.get(method)
  if (handler !== undefined) {
    handler(data)
  } else {
    console.error('Method ' + method + ' doesn\'t have registered handler')
  }
}


const handleResponse = (data) => {
  let { id } = data
  let callback = rpc_callbacks.get(id)
  if (callback !== undefined) {
    rpc_callbacks.delete(id)
    callback(data)
  } else {
    console.error('RPC callback not registered for request with id = ' + id)
  }
}


let writing = false
const queue = []


const write_stdout = async () => {
  if (writing) return
  writing = true

  while (queue.length > 0) {
    const data = queue.shift()
    process.stdout.write(JSON.stringify(data) + "\n")
    await new Promise((resolve) => setImmediate(resolve))
  }

  writing = false
}


export const send_request = (data) => {
  const id = nanoid()
  const method = data.method
  data['jsonrpc'] = "2.0"
  data['id'] = id

  const promise = new Promise((resolve, reject) => {
    rpc_callbacks.set(id, ({ result, error }) => {
      if (error !== undefined) {
        const { code, message, data: errData } = error || {}
        reject(new EywaRPCError(
          message || 'JSON-RPC error',
          { code, data: errData, method }
        ))
      } else {
        resolve(result)
      }
    })
  })

  queue.push(data)
  write_stdout()

  return promise
}


export const send_notification = (data) => {
  data['jsonrpc'] = "2.0"
  process.stdout.write(JSON.stringify(data) + "\n")
}


export const register_handler = (method, f) => {
  handlers.set(method, f)
}


// Line-delimited stdin reader. Matches the bb/py reference clients and the
// server emitter (`(str response \newline)`). Handles three cases that the
// previous implementation got wrong:
//
//   * multiple messages arriving in one chunk (split on \n, drain each)
//   * a single message split across chunks (buffer until \n)
//   * one message containing a literal '\n' — impossible, since JSON encodes
//     newlines inside strings as the two-character sequence `\n`.
export let open_pipe = () => {
  let buffer = ''

  const drain = () => {
    let idx
    while ((idx = buffer.indexOf('\n')) !== -1) {
      const line = buffer.slice(0, idx)
      buffer = buffer.slice(idx + 1)
      if (line.length === 0) continue
      try {
        handleData(JSON.parse(line))
      } catch (err) {
        console.error("Couldn't parse JSON-RPC line:", line, err)
      }
    }
  }

  process.stdin.on('data', (chunk) => {
    buffer += chunk.toString()
    drain()
  })

  process.stdin.on('end', () => {
    if (buffer.length > 0) {
      // Tolerate a trailing message without a final newline.
      try {
        handleData(JSON.parse(buffer))
      } catch (err) {
        console.error("Couldn't parse JSON-RPC at end of stream:", buffer, err)
      }
      buffer = ''
    }
  })
}


export const SUCCESS = "SUCCESS"
export const ERROR = "ERROR"
export const PROCESSING = "PROCESSING"
export const EXCEPTION = "EXCEPTION"

export const log = ({ event = "INFO", message, data, duration, coordinates, time = new Date() }) => {
  send_notification(
    {
      'method': 'task.log',
      'params': {
        'time': time,
        'event': event,
        'message': message,
        'data': data,
        'coordinates': coordinates,
        'duration': duration
      }
    })
}


export const info = (message, data = null) => {
  log({ 'event': 'INFO', 'message': message, 'data': data })
}

export const error = (message, data = null) => {
  log({ 'event': ERROR, 'message': message, 'data': data })
}

export const warn = (message, data = null) => {
  log({ 'event': 'WARN', 'message': message, 'data': data })
}

export const debug = (message, data = null) => {
  log({ 'event': 'DEBUG', 'message': message, 'data': data })
}

export const trace = (message, data = null) => {
  log({ 'event': 'TRACE', 'message': message, 'data': data })
}

export const exception = (message, data = null) => {
  log({ 'event': 'EXCEPTION', 'message': message, 'data': data })
}

export const report = async (message, options = {}) => {
  const { data, image } = options

  let currentTaskUuid
  try {
    const task = await get_task()
    currentTaskUuid = task.euuid || task.id
  } catch (_) {
    throw new EywaError('Cannot create report: No active task found')
  }

  const reportData = {
    message,
    task: { euuid: currentTaskUuid }
  }

  if (data) {
    reportData.data = data
    reportData.has_card = !!(data.card && data.card.trim().length > 0)
    reportData.has_table = !!(data.tables && Object.keys(data.tables).length > 0)
  } else {
    reportData.has_card = false
    reportData.has_table = false
  }

  if (image) {
    if (!isValidBase64(image)) {
      throw new EywaError('Invalid base64 image data')
    }
    reportData.image = image
    reportData.has_image = true
  } else {
    reportData.has_image = false
  }

  if (data && data.tables) {
    validateTables(data.tables)
  }

  const mutation = `
    mutation CreateTaskReport($report: TaskReportInput!) {
      stackTaskReport(data: $report) {
        euuid
        message
        has_table
        has_card
        has_image
      }
    }
  `

  const result = await graphql(mutation, { report: reportData })
  return result?.stackTaskReport ?? null
}

const isValidBase64 = (str) => {
  if (!str || typeof str !== 'string') return false
  try {
    const decoded = atob(str)
    const reencoded = btoa(decoded)
    return reencoded === str
  } catch (_) {
    return false
  }
}

const validateTables = (tables) => {
  if (!tables || typeof tables !== 'object') {
    throw new EywaError('Tables must be an object with named table entries')
  }

  for (const [tableName, tableData] of Object.entries(tables)) {
    if (!tableData || typeof tableData !== 'object') {
      throw new EywaError(`Table '${tableName}' must be an object`)
    }

    if (!Array.isArray(tableData.headers)) {
      throw new EywaError(`Table '${tableName}' must have a 'headers' array`)
    }

    if (!Array.isArray(tableData.rows)) {
      throw new EywaError(`Table '${tableName}' must have a 'rows' array`)
    }

    const headerCount = tableData.headers.length
    for (let i = 0; i < tableData.rows.length; i++) {
      const row = tableData.rows[i]
      if (!Array.isArray(row)) {
        throw new EywaError(`Table '${tableName}' row ${i} must be an array`)
      }
      if (row.length !== headerCount) {
        throw new EywaError(`Table '${tableName}' row ${i} has ${row.length} columns but headers specify ${headerCount}`)
      }
    }
  }
}

export const close_task = (status = SUCCESS) => {
  send_notification({
    'method': 'task.close',
    'params': {
      'status': status
    }
  })

  if (status == SUCCESS) {
    process.exit(0)
  } else {
    process.exit(1)
  }
}

export const update_task = (status = PROCESSING) => {
  send_notification({
    'method': 'task.update',
    'params': {
      'status': status
    }
  })
}


export const get_task = () => {
  return send_request({ 'method': 'task.get' })
}


export const return_task = () => {
  send_notification({
    'method': 'task.return'
  })
  process.exit(0)
}


// Execute a GraphQL operation against the data endpoint (`eywa.datasets.graphql`).
//
// Returns the GraphQL `data` field directly (unwrapped — there is no `{data, errors}`
// envelope at this layer). On failure throws `EywaGraphQLError` carrying:
//   * .message    — first GraphQL error's message (or the RPC error message)
//   * .code       — JSON-RPC error code (e.g. -32602 invalid-params, -32603 internal)
//   * .data       — the remaining GraphQL error fields (path, extensions, ...)
//   * .query      — the offending query (for diagnostics)
//   * .variables  — the variables passed (for diagnostics)
export const graphql = async (query, variables = null) => {
  try {
    return await send_request({
      method: 'eywa.datasets.graphql',
      params: { query, variables }
    })
  } catch (err) {
    if (err instanceof EywaRPCError) {
      throw new EywaGraphQLError(err.message, {
        code: err.code,
        data: err.data,
        method: err.method,
        query,
        variables,
        cause: err
      })
    }
    throw err
  }
}

export const access_token = async (expiresIn = 3600) => {
  const response = await graphql(
    'mutation($expires_in: Int) { requestAccessToken(expires_in: $expires_in) { token expires_in token_type } }',
    { expires_in: expiresIn }
  )
  return response?.requestAccessToken ?? null
}
