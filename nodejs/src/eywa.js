import {nanoid} from 'nanoid'
import process from 'node:process'

const rpc_callbacks = new Map()
const handlers = new Map()


const handleData = (data) => {
  let {method, id, result, error} = data
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
  let {method} = data
  let handler = handlers.get(method)
  if (handler !== undefined) {
    handler(data)
  } else {
    console.error('Method ' + method + ' doesn\'t have registered handler')
  }
}


const handleResponse = (data) => {
  let {id} = data
  let callback = rpc_callbacks.get(id)
  if (callback !== undefined) {
    rpc_callbacks.delete(id)
    console.log('Calling callback with data: ' + data)
    callback(data)
  } else {
    console.error('RPC callback not registered for request with id = ' + id)
  }
}


export const send_request = (data) => {
  let id = nanoid()
  data['jsonrpc'] = "2.0"
  data['id'] = id
  let promise = new Promise((resolve, reject) => {
    rpc_callbacks.set(id, ({result, error}) => {
      if (result!== undefined) {
        console.log('Returning result: ' + result)
        resolve(result)
      } else {
        console.log('Returning error: ' + error)
        reject(error)
      }
    })
  })
  process.stdout.write(JSON.stringify(data) + "\n")
  return promise
}


export const send_notification = (data) => {
  // console.assert(id !== undefined, "Notification doesn't support waiting for response")
  data['jsonrpc'] = "2.0"
  process.stdout.write(JSON.stringify(data) + "\n")
}


export const register_handler = (method,f) => {
  handlers.set(method) = f
}


let open_pipe = () => {
  process.stdin.on('data', (data) => {
    let raw_json = data.toString()
    let json = JSON.parse(raw_json)
    handleData(json)
  })
}

export const SUCCESS = "SUCCESS"
export const ERROR = "ERROR"
export const PROCESSING = "PROCESSING"
export const EXCEPTION = "EXCEPTION"

export const log = ({event = "INFO", message, data, duration, coordinates, time=new Date()}) => {
  send_notification(
    {'method': 'task.log',
      'params': {
        'time': time,
        'event': event,
        'message': message,
        'data': data,
        'coordinates':coordinates,
        'duration':duration
      }
    })
}


export const info = (message, data=null) => {
  log({'event': 'INFO','message': message, 'data':data})
}

export const error = (message, data=null) => {
  log({'event': ERROR,'message': message, 'data':data})
}

export const warn = (message, data=null) => {
  log({'event': 'WARN','message': message, 'data':data})
}

export const debug = (message, data=null) => {
  log({'event': 'DEBUG','message': message, 'data':data})
}

export const trace = (message, data=null) => {
  log({'event': 'TRACE','message': message, 'data':data})
}

export const report = (message, data=null, image=null) => {
  send_notification({
    'method': 'task.report',
    'params': {
      'message':message,
      'data':data,
      'image':image
    }
  })
}

export const close_task = (status=SUCCESS) => {
  send_notification({
    'method': 'task.close',
    'params': {
      'status':status
    }
  })

  if (status == SUCCESS) {
    process.exit(0)
  } else {
    process.exit(1)
  }
}

export const update_task = (status=PROCESSING) => {
  send_notification({
    'method': 'task.update',
    'params': {
      'status':status
    }
  })
}


export const get_task = () => {
  return send_request({'method':'task.get'});
}


export const return_task = () => {
  send_notification({
    'method': 'task.return'
  })
  process.exit(0)
}

export const graphql = (query, variables=null) => {
  return send_request({
    'method':'eywa.datasets.graphql',
    'params': {
      'query':query,
      'variables':variables
    }
  })
}


export default {
  send_request: send_request,
  send_notification: send_notification,
  register_handler: register_handler,
  open_pipe: open_pipe,
  get_task: get_task,
  log: log,
  info: info,
  warn: warn,
  update_task: update_task,
  return_task: return_task,
  close_task: close_task,
  graphql: graphql
}


// info('hello from nodejs')
// close()
// openPipe()
