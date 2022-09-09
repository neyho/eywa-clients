import {nanoid} from 'nanoid'
import process from 'node:process'


var rpc_callbacks = new Map()
var handlers = new Map()


let handleData = ({method, id, response, error} = data) => {
  if (method !== undefined) {
    handleRequest(data)
  } else if (response !== undefined && id !== undefined) {
    handleResponse(data)
  } else if (error !== undefined && id !== undefined) {
    handleResponse(data)
  } else {
    console.error('Received invalid JSON-RPC:\n' + data)
  }
}


let handleRequest = ({method} = data) => {
  let handler = handlers.get(method)
  if (handler !== undefined) {
    handler(data)
  } else {
    console.error('Method ' + method + ' doesn\'t have registered handler')
  }
}


let handleResponse = ({id} = data) => {
  callback = rpc_callbacks.get(id)
  if (callback !== undefined) {
    rpc_callbacks.delete(id)
    callback(data)
  } else {
    console.error('RPC callback not registered for request with id = ' + id)
  }
}


export let sendRequest = (data) => {
  id = nanoid()
  data['jsonrpc'] = "2.0"
  data['id'] = id
  promise = new Promise((resolve, reject) => {
    rpc_callbacks.set(id) = ({response, error}) => {
      if (response !== undefined) {
        resolve(response)
      } else {
        reject(error)
      }
    }
  })
  process.stdout.write(JSON.write(data))
  return promise
}


export let sendNotification = (data) => {
  console.assert(id !== undefined, "Notification doesn't support waiting for response")
  data['jsonrpc'] = "2.0"
  process.stdout.write(JSON.write(data))
}


export let registerHandler = (method,f) => {
  handlers.set(method) = f
}


let openPipe = () => {
  process.stdin.on('data', (data) => {
    let raw_json = data.toString()
    let json = JSON.parse(raw_json)
    handleData(json)
  })
}


openPipe()
