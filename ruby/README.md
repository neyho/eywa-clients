# EYWA Client for Ruby

[![Gem Version](https://badge.fury.io/rb/eywa-client.svg)](https://badge.fury.io/rb/eywa-client)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

EYWA client library for Ruby providing JSON-RPC communication, GraphQL queries, and task management for EYWA robots.

## Installation

Add this line to your application's Gemfile:

```ruby
gem 'eywa-client'
```

And then execute:

```bash
bundle install
```

Or install it yourself as:

```bash
gem install eywa-client
```

## Quick Start

```ruby
require 'eywa'

# Initialize the client
open_pipe

# Log messages
info("Robot started")

# Execute GraphQL queries
result_thread = graphql('
  {
    searchUser(_limit: 10) {
      euuid
      name
      type
    }
  }
')

result = result_thread.value
info("Users found", result)

# Update task status
update_task(PROCESSING)

# Complete the task
close_task(SUCCESS)
```

## Features

- 🚀 **Thread-Based Async** - Ruby native threading for async operations
- 📊 **GraphQL Integration** - Execute queries and mutations against EYWA datasets
- 📝 **Comprehensive Logging** - Multiple log levels with metadata support
- 🔄 **Task Management** - Update status, report progress, handle task lifecycle
- 🎯 **Thread-Safe** - Mutex protection for concurrent operations
- 💎 **Ruby Idioms** - Keyword arguments and blocks for handlers

## API Reference

### Initialization

#### `open_pipe`
Initialize stdin/stdout communication with EYWA runtime. Must be called before using other functions.

```ruby
open_pipe
```

### Logging Functions

#### `log(event: "INFO", message:, data: nil, duration: nil, coordinates: nil, time: Time.now)`
Log a message with full control over all parameters.

```ruby
log(
  event: "INFO",
  message: "Processing item",
  data: { item_id: 123 },
  duration: 1500,
  coordinates: { x: 10, y: 20 }
)
```

#### `info()`, `error()`, `warn()`, `debug()`, `trace()`, `exception()`
Convenience methods for different log levels.

```ruby
info("User logged in", { user_id: "abc123" })
error("Failed to process", { error: e.message })
exception("Unhandled error", { stack: e.backtrace[0..5] })
```

### Task Management

#### `get_task`
Get current task information. Returns a Thread.

```ruby
task_thread = get_task
begin
  task = task_thread.value
  info("Processing task", { task_id: task["euuid"] })
rescue => e
  warn("Could not get task", { error: e.message })
end
```

#### `update_task(status = PROCESSING)`
Update the current task status.

```ruby
update_task(PROCESSING)
```

#### `close_task(status = SUCCESS)`
Close the task with a final status and exit the process.

```ruby
begin
  # Do work...
  close_task(SUCCESS)
rescue => e
  error("Task failed", { error: e.message })
  close_task(ERROR)
end
```

#### `return_task`
Return control to EYWA without closing the task.

```ruby
return_task
```

### Reporting

#### `report(message, data = nil, image = nil)`
Send a task report with optional data and image.

```ruby
report("Analysis complete", {
  accuracy: 0.95,
  processed: 1000
}, chart_image_base64)
```

### GraphQL

#### `graphql(query, variables = nil)`
Execute a GraphQL query against the EYWA server. Returns a Thread.

```ruby
# Simple query
thread = graphql('{ searchUser { name email } }')
result = thread.value

# Query with variables
thread = graphql('
  mutation CreateUser($input: UserInput!) {
    syncUser(data: $input) {
      euuid
      name
    }
  }
', {
  input: {
    name: "John Doe",
    active: true
  }
})

begin
  result = thread.value
  info("User created", result)
rescue => e
  error("Creation failed", { error: e.message })
end
```

### JSON-RPC

#### `send_request(data)`
Send a JSON-RPC request and get a Thread for the response.

```ruby
thread = send_request({
  "method" => "custom.method",
  "params" => { "foo" => "bar" }
})

begin
  result = thread.value
  info("Response received", result)
rescue => e
  error("Request failed", { error: e.message })
end
```

#### `send_notification(data)`
Send a JSON-RPC notification without expecting a response.

```ruby
send_notification({
  "method" => "custom.event",
  "params" => { "status" => "ready" }
})
```

#### `register_handler(method, &handler)`
Register a handler for incoming JSON-RPC method calls.

```ruby
register_handler("custom.ping") do |request|
  info("Received ping", request["params"])
  send_notification({
    "method" => "custom.pong",
    "params" => { "timestamp" => Time.now.to_i }
  })
end
```

## Module Structure

The library uses a modular structure with a global client for ease of use:

```ruby
# Direct usage (recommended)
info("Hello")

# Or use the client instance
Eywa.client.info("Hello")

# Access constants
SUCCESS    # => "SUCCESS"
ERROR      # => "ERROR"
PROCESSING # => "PROCESSING"
EXCEPTION  # => "EXCEPTION"
```

## Complete Example

```ruby
#!/usr/bin/env ruby

require 'eywa'

def process_data
  # Get task
  task_thread = get_task
  task = task_thread.value
  
  info("Starting task", {
    task_id: task["euuid"],
    message: task["message"]
  })
  
  # Update status
  update_task(PROCESSING)
  
  # Query data
  result_thread = graphql('
    query GetActiveUsers {
      searchUser(_where: {active: {_eq: true}}) {
        euuid
        name
        email
      }
    }
  ')
  
  result = result_thread.value
  users = result.dig("data", "searchUser") || []
  
  info("Found users", { count: users.length })
  
  # Process users
  users.each do |user|
    debug("Processing user", { user_id: user["euuid"] })
    # ... do something
  end
  
  # Report results
  report("Found active users", {
    count: users.length,
    user_names: users.map { |u| u["name"] }
  })
  
  info("Task completed")
rescue => e
  error("Task processing failed", {
    error: e.message,
    backtrace: e.backtrace[0..5]
  })
  raise
end

def main
  # Initialize
  open_pipe
  sleep(0.1)
  
  info("Robot started")
  
  begin
    process_data
    close_task(SUCCESS)
  rescue => e
    error("Task failed", { error: e.message })
    close_task(ERROR)
  end
end

main
```

## Error Handling

Threads return exceptions that can be caught:

```ruby
thread = graphql("{ invalid }")
begin
  result = thread.value
rescue => e
  error("GraphQL failed", { error: e.message })
end
```

## Thread Safety

All operations are thread-safe:
- Mutex protection for callbacks and handlers
- Thread-safe Queue for request/response correlation
- Safe concurrent access to shared state

## Testing

Test your robot locally using the EYWA CLI:

```bash
eywa run -c 'ruby my_robot.rb'
```

## Requirements

- Ruby 2.5+
- No external gem dependencies (uses only standard library)

## License

MIT

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues and questions, please visit the [EYWA repository](https://github.com/neyho/eywa).
