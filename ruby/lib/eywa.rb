require 'securerandom'
require 'json'
require 'time'

# Load file operations if available
begin
  require_relative 'eywa_files'
rescue LoadError
  # File operations not available
end

module Eywa
  # Version
  VERSION = "0.3.0"
  
  # Task status constants
  SUCCESS = 'SUCCESS'
  ERROR = 'ERROR'
  PROCESSING = 'PROCESSING'
  EXCEPTION = 'EXCEPTION'

  class Client
    def initialize
      @rpc_callbacks = {}
      @handlers = {}
      @mutex = Mutex.new
    end

    def open_pipe
      Thread.new do
        while (line = STDIN.gets)
          begin
            json = JSON.parse(line)
            handle_data(json)
          rescue JSON::ParserError => e
            STDERR.puts("Failed to parse JSON: #{e.message}")
          rescue => e
            STDERR.puts("Error handling data: #{e.message}")
          end
        end
      end
    end

    def send_request(data)
      id = SecureRandom.uuid
      data['jsonrpc'] = '2.0'
      data['id'] = id

      # Create a queue for this request
      queue = Queue.new
      
      @mutex.synchronize do
        @rpc_callbacks[id] = queue
      end

      # Send the request
      STDOUT.puts(data.to_json)
      STDOUT.flush

      # Wait for response in a thread
      Thread.new do
        response = queue.pop
        @mutex.synchronize do
          @rpc_callbacks.delete(id)
        end
        
        if response['error']
          raise StandardError.new(response['error']['message'] || response['error'].to_s)
        else
          response['result']
        end
      end
    end

    def send_notification(data)
      data['jsonrpc'] = '2.0'
      STDOUT.puts(data.to_json)
      STDOUT.flush
    end

    def register_handler(method, &handler)
      @mutex.synchronize do
        @handlers[method] = handler
      end
    end

    def log(event: 'INFO', message:, data: nil, duration: nil, coordinates: nil, time: Time.now)
      send_notification(
        'method' => 'task.log',
        'params' => {
          'time' => time.iso8601,
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
      log(event: 'ERROR', message: message, data: data)
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

    def exception(message, data = nil)
      log(event: 'EXCEPTION', message: message, data: data)
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

    private

    def handle_data(data)
      method = data['method']
      id = data['id']
      result = data['result']
      error = data['error']

      if method
        handle_request(data)
      elsif (result || error) && id
        handle_response(data)
      else
        STDERR.puts("Received invalid JSON-RPC:\n#{data}")
      end
    end

    def handle_request(data)
      method = data['method']
      
      @mutex.synchronize do
        handler = @handlers[method]
        if handler
          handler.call(data)
        else
          STDERR.puts("Method #{method} doesn't have registered handler")
        end
      end
    end

    def handle_response(data)
      id = data['id']
      
      @mutex.synchronize do
        queue = @rpc_callbacks[id]
        if queue
          queue.push(data)
        else
          STDERR.puts("RPC callback not registered for request with id = #{id}")
        end
      end
    end
  end

  # Convenience module for global access
  module GlobalClient
    extend self

    def client
      @client ||= Client.new
    end

    # Delegate all methods to the client instance
    def method_missing(method_name, *args, **kwargs, &block)
      if client.respond_to?(method_name)
        client.send(method_name, *args, **kwargs, &block)
      else
        super
      end
    end

    def respond_to_missing?(method_name, include_private = false)
      client.respond_to?(method_name, include_private) || super
    end
  end
end

# For backward compatibility and ease of use
include Eywa::GlobalClient

# Export constants for easy access
SUCCESS = Eywa::SUCCESS
ERROR = Eywa::ERROR
PROCESSING = Eywa::PROCESSING
EXCEPTION = Eywa::EXCEPTION
