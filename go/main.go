package main

import (
	"fmt"
	"log"
	"time"

	"github.com/neyho/eywa-client"
)

func main() {
	// Register a handler for a custom method
	eywa.RegisterHandler("custom.ping", func(req eywa.Request) {
		log.Println("Received ping request:", req)
		
		// Send a response notification
		eywa.SendNotification(map[string]interface{}{
			"method": "custom.pong",
			"params": map[string]interface{}{
				"message": "Pong!",
				"timestamp": time.Now().Unix(),
			},
		})
	})

	// Start the pipe listener
	go eywa.OpenPipe()

	// Give it time to start
	time.Sleep(100 * time.Millisecond)

	// Example: Various logging levels
	eywa.Info("Application started", map[string]interface{}{
		"version": "1.0.0",
		"environment": "development",
	})

	// Example: Task status update
	eywa.UpdateTask(eywa.PROCESSING)

	// Example: Simple GraphQL query
	result, err := eywa.GraphQL(`
		{
			searchUserRole {
				euuid
				name
				description
			}
		}
	`, nil)

	if err != nil {
		eywa.Error("Failed to fetch user roles", map[string]interface{}{
			"error": err.Error(),
		})
	} else {
		eywa.Info("User roles fetched", map[string]interface{}{
			"roles": result["data"],
		})
	}

	// Example: Report with data
	eywa.Report("System check complete", map[string]interface{}{
		"status": "healthy",
		"checks": map[string]interface{}{
			"database": "connected",
			"api": "responsive",
			"cache": "ready",
		},
	}, nil)

	// Example: Using all log levels
	eywa.Debug("Debug information", map[string]interface{}{"debug_mode": true})
	eywa.Trace("Trace information", map[string]interface{}{"trace_id": "123"})
	eywa.Warn("Warning message", map[string]interface{}{"threshold": 80})
	eywa.Exception("Exception occurred", map[string]interface{}{"type": "test_exception"})

	// Complete the task
	fmt.Println("Demo completed!")
	eywa.CloseTask(eywa.SUCCESS)
}
