require 'securerandom'
require 'json'

RPC_CALLBACKS = {}
HANDLERS = {}

def handle_data(data)
  method = data['method']
  id = data['id']
  result = data['result']
  error = data['error']

  if method
    handle_request(data)
  elsif result && id
    handle_response(data)
  elsif error && id
    handle_response(data)
  else
    STDERR.puts("Received invalid JSON-RPC:\n#{data}")
  end
end

def handle_request(data)
  method = data['method']
  handler = HANDLERS[method]

  if handler
    handler.call(data)
  else
    STDERR.puts("Method #{method} doesn't have registered handler")
  end
end

def handle_response(data)
  id = data['id']
  callback = RPC_CALLBACKS.delete(id)

  if callback
    puts("Calling callback with data: #{data}")
    callback.call(data)
  else
    STDERR.puts("RPC callback not registered for request with id = #{id}")
  end
end

def send_request(data)
  id = SecureRandom.uuid
  data['jsonrpc'] = '2.0'
  data['id'] = id

  promise = Concurrent::Promises.future do
    RPC_CALLBACKS[id] = lambda do |response|
      result = response['result']
      error = response['error']

      if result
        puts("Returning result: #{result}")
        Concurrent::Promises.fulfill(result)
      else
        puts("Returning error: #{error}")
        Concurrent::Promises.reject(error)
      end
    end
  end

  STDOUT.puts(data.to_json)
  promise
end

def send_notification(data)
  data['jsonrpc'] = '2.0'
  STDOUT.puts(data.to_json)
end

def register_handler(method, handler)
  HANDLERS[method] = handler
end

def open_pipe
  Thread.new do
    while (line = STDIN.gets)
      json = JSON.parse(line)
      handle_data(json)
    end
  end
end

SUCCESS = 'SUCCESS'
ERROR = 'ERROR'
PROCESSING = 'PROCESSING'
EXCEPTION = 'EXCEPTION'

def log(event: 'INFO', message:, data: nil, duration: nil, coordinates: nil, time: Time.now)
  send_notification(
    'method' => 'task.log',
    'params' => {
      'time' => time,
      'event' => event,
      'message' => message,
      'data' => data,
      'coordinates' => coordinates,
      'duration' => duration
    }
  )
end

def info(message, data = nil)
  log(event: 'INFO', message: message, data: data)
end

def error(message, data = nil)
  log(event: ERROR, message: message, data: data)
end

def warn(message, data = nil)
  log(event: 'WARN', message: message, data: data)
end

def debug(message, data = nil)
  log(event: 'DEBUG', message: message, data: data)
end

def trace(message, data = nil)
  log(event: 'TRACE', message: message, data: data)
end

def report(message, data = nil, image = nil)
  send_notification(
    'method' => 'task.report',
    'params' => {
      'message' => message,
      'data' => data,
      'image' => image
    }
  )
end

def close_task(status = SUCCESS)
  send_notification(
    'method' => 'task.close',
    'params' => {
      'status' => status
    }
  )

  exit(status == SUCCESS ? 0 : 1)
end

def update_task(status = PROCESSING)
  send_notification(
    'method' => 'task.update',
    'params' => {
      'status' => status
    }
  )
end

def get_task
  send_request('method' => 'task.get')
end

def return_task
  send_notification('method' => 'task.return')
  exit(0)
end

def graphql(query, variables = nil)
  send_request(
    'method' => 'eywa.datasets.graphql',
    'params' => {
      'query' => query,
      'variables' => variables
    }
  )
end

# Usage example (uncomment to run):
# info('hello from ruby')
# close_task()
# open_pipe()

