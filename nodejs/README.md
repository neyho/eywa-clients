# EYWA Client for Node.js

[![npm version](https://badge.fury.io/js/eywa-client.svg)](https://badge.fury.io/js/eywa-client)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

EYWA client library for Node.js providing JSON-RPC communication, GraphQL queries, and task management for EYWA robots.

## Installation

```bash
npm install eywa-client
```

## Quick Start

```javascript
import eywa from 'eywa-client'

// Initialize the client
eywa.open_pipe()

// Log messages
eywa.info('Robot started')

// Execute GraphQL queries
const result = await eywa.graphql(`
  query {
    searchUser(_limit: 10) {
      euuid
      name
      type
    }
  }
`)

// Update task status
eywa.update_task(eywa.PROCESSING)

// Complete the task
eywa.close_task(eywa.SUCCESS)
```

## Features

- 🚀 **JSON-RPC Communication** - Bidirectional communication with EYWA runtime
- 📊 **GraphQL Integration** - Execute queries and mutations against EYWA datasets
- 📝 **Comprehensive Logging** - Multiple log levels with metadata support
- 🔄 **Task Management** - Update status, report progress, handle task lifecycle
- 🎯 **Type Safety** - Full TypeScript definitions included
- ⚡ **Async/Promise Based** - Modern async/await support

## API Reference

### Initialization

#### `open_pipe()`
Initialize stdin/stdout communication with EYWA runtime. Must be called before using other functions.

```javascript
eywa.open_pipe()
```

### Logging Functions

#### `log(record)`
Log a message with full control over all parameters.

```javascript
eywa.log({
  event: 'INFO',
  message: 'Processing item',
  data: { itemId: 123 },
  duration: 1500,
  coordinates: { x: 10, y: 20 }
})
```

#### `info(message, data?)`, `error(message, data?)`, `warn(message, data?)`, `debug(message, data?)`, `trace(message, data?)`, `exception(message, data?)`
Convenience methods for different log levels.

```javascript
eywa.info('User logged in', { userId: 'abc123' })
eywa.error('Failed to process', { error: err.message })
```

### Task Management

#### `get_task()`
Get current task information. Returns a promise.

```javascript
const task = await eywa.get_task()
console.log('Processing:', task.message)
```

#### `update_task(status?)`
Update the current task status.

```javascript
eywa.update_task(eywa.PROCESSING)
```

#### `close_task(status?)`
Close the task with a final status and exit the process.

```javascript
try {
  // Do work...
  eywa.close_task(eywa.SUCCESS)
} catch (err) {
  eywa.error('Task failed', err)
  eywa.close_task(eywa.ERROR)
}
```

#### `return_task()`
Return control to EYWA without closing the task.

```javascript
eywa.return_task()
```

### Reporting

#### `report(message, data?, image?)`
Send a task report with optional data and image.

```javascript
eywa.report('Analysis complete', {
  accuracy: 0.95,
  processed: 1000
}, chartImageBase64)
```

### GraphQL

#### `graphql(query, variables?)`
Execute a GraphQL query against the EYWA server.

```javascript
const result = await eywa.graphql(`
  mutation CreateUser($input: UserInput!) {
    syncUser(data: $input) {
      euuid
      name
    }
  }
`, {
  input: {
    name: 'John Doe',
    active: true
  }
})
```

### JSON-RPC

#### `send_request(data)`
Send a JSON-RPC request and wait for response.

```javascript
const result = await eywa.send_request({
  method: 'custom.method',
  params: { foo: 'bar' }
})
```

#### `send_notification(data)`
Send a JSON-RPC notification without expecting a response.

```javascript
eywa.send_notification({
  method: 'custom.event',
  params: { status: 'ready' }
})
```

#### `register_handler(method, handler)`
Register a handler for incoming JSON-RPC method calls.

```javascript
eywa.register_handler('custom.ping', (data) => {
  console.log('Received ping:', data.params)
  eywa.send_notification({
    method: 'custom.pong',
    params: { timestamp: Date.now() }
  })
})
```

## Constants

- `SUCCESS` - Task completed successfully
- `ERROR` - Task failed with error
- `PROCESSING` - Task is currently processing
- `EXCEPTION` - Task failed with exception

## Complete Example

```javascript
import eywa from 'eywa-client'

async function processData() {
  // Initialize
  eywa.open_pipe()
  
  try {
    // Get task info
    const task = await eywa.get_task()
    eywa.info('Starting task', { taskId: task.euuid })
    
    // Update status
    eywa.update_task(eywa.PROCESSING)
    
    // Do work with GraphQL
    const users = await eywa.graphql(`
      query GetActiveUsers {
        searchUser(_where: {active: {_eq: true}}) {
          euuid
          name
          email
        }
      }
    `)
    
    // Report progress
    eywa.report('Found users', {
      count: users.data.searchUser.length
    })
    
    // Process users...
    for (const user of users.data.searchUser) {
      eywa.debug('Processing user', { userId: user.euuid })
      // ... do something
    }
    
    // Success!
    eywa.info('Task completed')
    eywa.close_task(eywa.SUCCESS)
    
  } catch (error) {
    eywa.error('Task failed', {
      error: error.message,
      stack: error.stack
    })
    eywa.close_task(eywa.ERROR)
  }
}

processData()
```

## TypeScript Support

Full TypeScript definitions are included. The client exports interfaces for all data structures:

```typescript
import eywa, { LogRecord, TaskStatus, GraphQLVariables } from 'eywa-client'

const logData: LogRecord = {
  event: 'INFO',
  message: 'Test',
  data: { custom: true }
}

const status: TaskStatus = eywa.SUCCESS
```

## Testing

Test your robot locally using the EYWA CLI:

```bash
eywa run -c 'node my-robot.js'
```

## License

MIT

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues and questions, please visit the [EYWA repository](https://github.com/neyho/eywa).
