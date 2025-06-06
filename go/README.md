# EYWA Client for Go

[![Go Reference](https://pkg.go.dev/badge/github.com/neyho/eywa-client.svg)](https://pkg.go.dev/github.com/neyho/eywa-client)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

EYWA client library for Go providing JSON-RPC communication, GraphQL queries, and task management for EYWA robots.

## Installation

```bash
go get github.com/neyho/eywa-client
```

## Quick Start

```go
package main

import (
    "log"
    "time"
    
    "github.com/neyho/eywa-client"
)

func main() {
    // Initialize the client
    go eywa.OpenPipe()
    time.Sleep(100 * time.Millisecond)
    
    // Log messages
    eywa.Info("Robot started", nil)
    
    // Execute GraphQL queries
    result, err := eywa.GraphQL(`
        {
            searchUser(_limit: 10) {
                euuid
                name
                type
            }
        }
    `, nil)
    
    if err != nil {
        eywa.Error("GraphQL failed", map[string]interface{}{
            "error": err.Error(),
        })
    }
    
    // Update task status
    eywa.UpdateTask(eywa.PROCESSING)
    
    // Complete the task
    eywa.CloseTask(eywa.SUCCESS)
}
```

## Features

- 🚀 **Channel-Based Concurrency** - Idiomatic Go async patterns
- 📊 **GraphQL Integration** - Execute queries and mutations against EYWA datasets
- 📝 **Comprehensive Logging** - Multiple log levels with metadata support
- 🔄 **Task Management** - Update status, report progress, handle task lifecycle
- 🎯 **Type Safety** - Strongly typed with proper error handling
- 🔒 **Thread-Safe** - Concurrent operations with mutexes

## API Reference

### Initialization

#### `OpenPipe()`
Initialize stdin/stdout communication with EYWA runtime. Should be called in a goroutine.

```go
go eywa.OpenPipe()
time.Sleep(100 * time.Millisecond) // Give it time to start
```

### Logging Functions

#### `Log(event, message string, data interface{}, duration *int, coordinates interface{}, logTime *time.Time)`
Log a message with full control over all parameters.

```go
duration := 1500
now := time.Now()
eywa.Log(eywa.INFO, "Processing item",
    map[string]interface{}{"itemId": 123},
    &duration,
    map[string]interface{}{"x": 10, "y": 20},
    &now)
```

#### `Info()`, `Error()`, `Warn()`, `Debug()`, `Trace()`, `Exception()`
Convenience methods for different log levels.

```go
eywa.Info("User logged in", map[string]interface{}{"userId": "abc123"})
eywa.Error("Failed to process", map[string]interface{}{"error": err.Error()})
eywa.Exception("Unhandled error", map[string]interface{}{"stack": fmt.Sprintf("%+v", err)})
```

### Task Management

#### `GetTask() (interface{}, error)`
Get current task information.

```go
task, err := eywa.GetTask()
if err != nil {
    eywa.Warn("Could not get task", map[string]interface{}{
        "error": err.Error(),
    })
} else {
    eywa.Info("Processing task", task)
}
```

#### `UpdateTask(status string)`
Update the current task status.

```go
eywa.UpdateTask(eywa.PROCESSING)
```

#### `CloseTask(status string)`
Close the task with a final status and exit the process.

```go
err := processData()
if err != nil {
    eywa.Error("Task failed", map[string]interface{}{
        "error": err.Error(),
    })
    eywa.CloseTask(eywa.ERROR)
} else {
    eywa.CloseTask(eywa.SUCCESS)
}
```

#### `ReturnTask()`
Return control to EYWA without closing the task.

```go
eywa.ReturnTask()
```

### Reporting

#### `Report(message string, data interface{}, image interface{})`
Send a task report with optional data and image.

```go
eywa.Report("Analysis complete", map[string]interface{}{
    "accuracy": 0.95,
    "processed": 1000,
}, chartImageBase64)
```

### GraphQL

#### `GraphQL(query string, variables map[string]interface{}) (map[string]interface{}, error)`
Execute a GraphQL query against the EYWA server.

```go
// Simple query
result, err := eywa.GraphQL(`
    {
        searchUser {
            name
            email
        }
    }
`, nil)

// Query with variables
result, err := eywa.GraphQL(`
    mutation CreateUser($input: UserInput!) {
        syncUser(data: $input) {
            euuid
            name
        }
    }
`, map[string]interface{}{
    "input": map[string]interface{}{
        "name": "John Doe",
        "active": true,
    },
})
```

### JSON-RPC

#### `SendRequest(data map[string]interface{}) chan Response`
Send a JSON-RPC request and get a channel for the response.

```go
responseChan := eywa.SendRequest(map[string]interface{}{
    "method": "custom.method",
    "params": map[string]interface{}{"foo": "bar"},
})

response := <-responseChan
if response.Error != nil {
    log.Printf("Error: %v", response.Error)
} else {
    log.Printf("Result: %v", response.Result)
}
```

#### `SendNotification(data map[string]interface{})`
Send a JSON-RPC notification without expecting a response.

```go
eywa.SendNotification(map[string]interface{}{
    "method": "custom.event",
    "params": map[string]interface{}{"status": "ready"},
})
```

#### `RegisterHandler(method string, handler func(Request))`
Register a handler for incoming JSON-RPC method calls.

```go
eywa.RegisterHandler("custom.ping", func(req eywa.Request) {
    log.Printf("Received ping: %v", req.Params)
    eywa.SendNotification(map[string]interface{}{
        "method": "custom.pong",
        "params": map[string]interface{}{
            "timestamp": time.Now().Unix(),
        },
    })
})
```

## Constants

```go
const (
    SUCCESS    = "SUCCESS"
    ERROR      = "ERROR"
    PROCESSING = "PROCESSING"
    EXCEPTION  = "EXCEPTION"
)

const (
    INFO          = "INFO"
    WARN          = "WARN"
    DEBUG         = "DEBUG"
    TRACE         = "TRACE"
    LOG_ERROR     = "ERROR"
    LOG_EXCEPTION = "EXCEPTION"
)
```

## Complete Example

```go
package main

import (
    "fmt"
    "log"
    "time"
    
    "github.com/neyho/eywa-client"
)

func processData() error {
    // Get task
    task, err := eywa.GetTask()
    if err != nil {
        return fmt.Errorf("failed to get task: %w", err)
    }
    
    taskData, ok := task.(map[string]interface{})
    if !ok {
        return fmt.Errorf("unexpected task format")
    }
    
    eywa.Info("Starting task", map[string]interface{}{
        "taskId": taskData["euuid"],
    })
    
    // Update status
    eywa.UpdateTask(eywa.PROCESSING)
    
    // Query data
    result, err := eywa.GraphQL(`
        query GetActiveUsers {
            searchUser(_where: {active: {_eq: true}}) {
                euuid
                name
                email
            }
        }
    `, nil)
    
    if err != nil {
        return fmt.Errorf("GraphQL query failed: %w", err)
    }
    
    // Process results
    if data, ok := result["data"].(map[string]interface{}); ok {
        if users, ok := data["searchUser"].([]interface{}); ok {
            eywa.Info("Found users", map[string]interface{}{
                "count": len(users),
            })
            
            for _, user := range users {
                if u, ok := user.(map[string]interface{}); ok {
                    eywa.Debug("Processing user", map[string]interface{}{
                        "userId": u["euuid"],
                    })
                }
            }
            
            // Report results
            eywa.Report("Found active users", map[string]interface{}{
                "count": len(users),
                "timestamp": time.Now().Unix(),
            }, nil)
        }
    }
    
    return nil
}

func main() {
    // Initialize
    go eywa.OpenPipe()
    time.Sleep(100 * time.Millisecond)
    
    eywa.Info("Robot started", nil)
    
    // Process
    err := processData()
    if err != nil {
        eywa.Error("Task failed", map[string]interface{}{
            "error": err.Error(),
        })
        eywa.CloseTask(eywa.ERROR)
        return
    }
    
    // Success
    eywa.Info("Task completed", nil)
    eywa.CloseTask(eywa.SUCCESS)
}
```

## Error Handling

All functions that can fail return proper Go errors:

```go
result, err := eywa.GraphQL("{ invalid }")
if err != nil {
    eywa.Error("GraphQL failed", map[string]interface{}{
        "error": err.Error(),
        "query": "{ invalid }",
    })
    return err
}
```

## Buffer Management

The client handles large JSON responses with increased buffer sizes:

```go
// Automatically configured in OpenPipe()
scanner.Buffer(buf, 1024*1024) // 1MB max
```

## Testing

Test your robot locally using the EYWA CLI:

```bash
eywa run -c 'go run my-robot.go'
```

## Thread Safety

All operations are thread-safe:
- Mutex protection for callbacks and handlers
- Channel-based communication
- Safe concurrent access

## Requirements

- Go 1.19+
- No external dependencies (uses only standard library)

## License

MIT

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues and questions, please visit the [EYWA repository](https://github.com/neyho/eywa).
