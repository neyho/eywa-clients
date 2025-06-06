package main

import (
	"fmt"
	"time"

	"github.com/neyho/eywa-client"
)

func processTask() error {
	// Get current task
	task, err := eywa.GetTask()
	if err != nil {
		return fmt.Errorf("failed to get task: %w", err)
	}

	// Extract task info
	taskData, ok := task.(map[string]interface{})
	if !ok {
		return fmt.Errorf("unexpected task format")
	}

	eywa.Info("Processing task", map[string]interface{}{
		"task_id": taskData["euuid"],
		"message": taskData["message"],
	})

	// Update status to processing
	eywa.UpdateTask(eywa.PROCESSING)

	// Simulate some work
	eywa.Info("Starting data processing...", nil)
	time.Sleep(1 * time.Second)

	// Report progress
	eywa.Report("Processed 50% of data", map[string]interface{}{
		"progress": 0.5,
		"items_processed": 50,
		"items_total": 100,
	}, nil)

	// More work
	time.Sleep(1 * time.Second)

	// Example GraphQL query
	result, err := eywa.GraphQL(`
		{
			searchUser(_limit: 5, _where: {active: {_eq: true}}) {
				euuid
				name
				type
			}
		}
	`, nil)

	if err != nil {
		return fmt.Errorf("GraphQL query failed: %w", err)
	}

	// Process results
	if data, ok := result["data"].(map[string]interface{}); ok {
		if users, ok := data["searchUser"].([]interface{}); ok {
			eywa.Info("Found active users", map[string]interface{}{
				"count": len(users),
			})
		}
	}

	// Final report
	eywa.Report("Task completed successfully", map[string]interface{}{
		"items_processed": 100,
		"duration_seconds": 2,
		"status": "complete",
	}, nil)

	return nil
}

func main() {
	// Initialize EYWA client
	go eywa.OpenPipe()
	time.Sleep(100 * time.Millisecond)

	eywa.Info("Robot started", nil)

	// Try to process task
	err := processTask()
	if err != nil {
		eywa.Error("Task failed", map[string]interface{}{
			"error": err.Error(),
		})
		eywa.CloseTask(eywa.ERROR)
		return
	}

	// Success
	eywa.Info("All operations completed", nil)
	eywa.CloseTask(eywa.SUCCESS)
}
