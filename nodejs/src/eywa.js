import { nanoid } from 'nanoid'
import process from 'node:process'


const rpc_callbacks = new Map()
const handlers = new Map()


const handleData = (data) => {
  let { method, id, result, error } = data
  if (method !== undefined) {
    handleRequest(data)
  } else if (result !== undefined && id !== undefined) {
    handleResponse(data)
  } else if (error !== undefined && id !== undefined) {
    handleResponse(data)
  } else {
    console.error('Received invalid JSON-RPC:\n' + data)
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
    //console.log('Calling callback with data: ' + data)
    callback(data)
  } else {
    console.error('RPC callback not registered for request with id = ' + id)
  }
}


let writing = false;
const queue = [];


const write_stdout = async () => {
  if (writing) return;
  writing = true;

  while (queue.length > 0) {
    const data = queue.shift();
    process.stdout.write(JSON.stringify(data) + "\n");
    await new Promise((resolve) => setImmediate(resolve));
  }

  writing = false;
};


export const send_request = (data) => {
  let id = nanoid();
  data['jsonrpc'] = "2.0";
  data['id'] = id;

  const promise = new Promise((resolve, reject) => {
    rpc_callbacks.set(id, ({ result, error }) => {
      if (result !== undefined) {
        resolve(result);
      } else {
        reject(error);
      }
    });
  });

  // Push the data to the queue and trigger the writer
  queue.push(data);
  write_stdout();

  return promise;
};




export const send_notification = (data) => {
  // console.assert(id !== undefined, "Notification doesn't support waiting for response")
  data['jsonrpc'] = "2.0"
  process.stdout.write(JSON.stringify(data) + "\n")
}


export const register_handler = (method, f) => {
  handlers.set(method, f)
}


export let open_pipe = () => {
  let buffer = '';

  // TODO - This is quick and dirty way to read large
  // JSON responses
  process.stdin.on('data', (chunk) => {
    buffer += chunk.toString();

    try {
      const json = JSON.parse(buffer);
      handleData(json);
      buffer = '';
    } catch (error) {
      if (error.name !== 'SyntaxError') {
        console.error("Couldn't parse remote response", error);
        buffer = '';
      }
    }
  });

  process.stdin.on('end', () => {
    // Handle the end of the input stream
    if (buffer) {
      try {
        const json = JSON.parse(buffer);
        handleData(json);
      } catch (error) {
        console.error("Couldn't parse remote response at end of stream", error);
      }
    }
  });
};


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
  const { data, image, metadata } = options;

  // Get current task UUID
  let currentTaskUuid;
  try {
    const task = await get_task();
    currentTaskUuid = task.euuid || task.id;
  } catch (error) {
    throw new Error('Cannot create report: No active task found');
  }

  // Build report data structure
  const reportData = {
    message,
    task: { euuid: currentTaskUuid }
  };

  // Process data and set flags
  if (data) {
    reportData.data = data;
    reportData.has_card = !!(data.card && data.card.trim().length > 0);
    reportData.has_table = !!(data.tables && Object.keys(data.tables).length > 0);
  } else {
    reportData.has_card = false;
    reportData.has_table = false;
  }

  // Process image and validate
  if (image) {
    if (!isValidBase64(image)) {
      throw new Error('Invalid base64 image data');
    }
    reportData.image = image;
    reportData.has_image = true;
  } else {
    reportData.has_image = false;
  }

  // Validate table structure if present
  if (data && data.tables) {
    validateTables(data.tables);
  }

  // Include metadata if provided
  if (metadata && typeof metadata === 'object') {
    reportData.metadata = metadata;
  }

  // Create report using GraphQL mutation
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
  `;

  try {
    const result = await graphql(mutation, { report: reportData });
    return result.data?.stackTaskReport || null;
  } catch (error) {
    throw new Error(`Failed to create report: ${error.message}`);
  }
};

// Helper function to validate base64 data
const isValidBase64 = (str) => {
  if (!str || typeof str !== 'string') return false;
  try {
    // Check if it's valid base64
    const decoded = atob(str);
    const reencoded = btoa(decoded);
    return reencoded === str;
  } catch (err) {
    return false;
  }
};

// Helper function to validate table structure
const validateTables = (tables) => {
  if (!tables || typeof tables !== 'object') {
    throw new Error('Tables must be an object with named table entries');
  }

  for (const [tableName, tableData] of Object.entries(tables)) {
    if (!tableData || typeof tableData !== 'object') {
      throw new Error(`Table '${tableName}' must be an object`);
    }

    if (!Array.isArray(tableData.headers)) {
      throw new Error(`Table '${tableName}' must have a 'headers' array`);
    }

    if (!Array.isArray(tableData.rows)) {
      throw new Error(`Table '${tableName}' must have a 'rows' array`);
    }

    // Validate each row has same number of columns as headers
    const headerCount = tableData.headers.length;
    for (let i = 0; i < tableData.rows.length; i++) {
      const row = tableData.rows[i];
      if (!Array.isArray(row)) {
        throw new Error(`Table '${tableName}' row ${i} must be an array`);
      }
      if (row.length !== headerCount) {
        throw new Error(`Table '${tableName}' row ${i} has ${row.length} columns but headers specify ${headerCount}`);
      }
    }
  }
};

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
  return send_request({ 'method': 'task.get' });
}


export const return_task = () => {
  send_notification({
    'method': 'task.return'
  })
  process.exit(0)
}

export const graphql = (query, variables = null) => {
  return send_request({
    'method': 'eywa.datasets.graphql',
    'params': {
      'query': query,
      'variables': variables
    }
  })
}
