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

export const report = (message, data = null, image = null) => {
  send_notification({
    'method': 'task.report',
    'params': {
      'message': message,
      'data': data,
      'image': image
    }
  })
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