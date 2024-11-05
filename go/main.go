// main.go
package main

import (
    "fmt"
    "log"
)

func main() {
    // Register a handler for a test method
    RegisterHandler("example.method", func(req Request) {
        log.Println("Handling request:", req)
        SendNotification("example.response", map[string]interface{}{"message": "Handled example.method"})
    })

    // Example: Send a JSON-RPC request
    responseChan := SendRequest("example.method", map[string]interface{}{"param1": "value1"})

    // Start the pipe listener
    go OpenPipe()

    // Wait for the response
    response := <-responseChan
    fmt.Println("Received response:", response)

    // Example: Log an event
    SendNotification("task.log", map[string]interface{}{
        "event": "INFO",
        "message": "This is a log message.",
    })
}
