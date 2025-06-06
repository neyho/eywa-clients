package main

import (
	"encoding/json"
	"fmt"
	"log"
	"time"

	"github.com/neyho/eywa-client"
)

func main() {
	fmt.Println("Starting EYWA Go client test...\n")

	// Start the pipe listener in a goroutine
	go eywa.OpenPipe()

	// Give it a moment to start
	time.Sleep(100 * time.Millisecond)

	// Test 1: Logging functions
	eywa.Info("Testing info logging", map[string]interface{}{"test": "info"})
	eywa.Warn("Testing warning logging", map[string]interface{}{"test": "warn"})
	eywa.Error("Testing error logging (not a real error)", map[string]interface{}{"test": "error"})
	eywa.Debug("Testing debug logging", map[string]interface{}{"test": "debug"})
	eywa.Trace("Testing trace logging", map[string]interface{}{"test": "trace"})
	eywa.Exception("Testing exception logging", map[string]interface{}{"test": "exception"})

	// Test 2: Custom log with all parameters
	duration := 1234
	now := time.Now()
	eywa.Log(eywa.INFO, "Custom log with all parameters",
		map[string]interface{}{"custom": true},
		&duration,
		map[string]interface{}{"x": 10, "y": 20},
		&now)

	// Test 3: Report
	eywa.Report("Test report message",
		map[string]interface{}{"reportData": "test"},
		nil)

	// Test 4: Task management
	eywa.UpdateTask(eywa.PROCESSING)
	eywa.Info("Updated task status to PROCESSING", nil)

	// Test 5: Get current task
	task, err := eywa.GetTask()
	if err != nil {
		eywa.Warn("Could not get task (normal if not in task context)",
			map[string]interface{}{"error": err.Error()})
	} else {
		eywa.Info("Retrieved task:", task)
	}

	// Test 6: GraphQL query
	eywa.Info("Testing GraphQL query...", nil)
	result, err := eywa.GraphQL(`
		{
			searchUser(_limit: 2) {
				euuid
				name
				type
				active
			}
		}
	`, nil)

	if err != nil {
		eywa.Error("GraphQL query failed", map[string]interface{}{"error": err.Error()})
	} else {
		eywa.Info("GraphQL query successful", map[string]interface{}{
			"resultCount": len(result["data"].(map[string]interface{})["searchUser"].([]interface{})),
		})

		// Show first user if available
		if users, ok := result["data"].(map[string]interface{})["searchUser"].([]interface{}); ok && len(users) > 0 {
			if firstUser, ok := users[0].(map[string]interface{}); ok {
				eywa.Info("First user:", firstUser)
			}
		}
	}

	// Test 7: Constants
	eywa.Info("Testing constants", map[string]interface{}{
		"SUCCESS":    eywa.SUCCESS,
		"ERROR":      eywa.ERROR,
		"PROCESSING": eywa.PROCESSING,
		"EXCEPTION":  eywa.EXCEPTION,
	})

	// Test 8: GraphQL with variables
	eywa.Info("Testing GraphQL with variables...", nil)
	query := `
		query GetUsers($limit: Int) {
			searchUser(_limit: $limit) {
				name
				type
			}
		}
	`
	variables := map[string]interface{}{
		"limit": 1,
	}

	result2, err := eywa.GraphQL(query, variables)
	if err != nil {
		eywa.Error("GraphQL with variables failed", map[string]interface{}{"error": err.Error()})
	} else {
		// Pretty print the result
		jsonBytes, _ := json.MarshalIndent(result2, "", "  ")
		eywa.Info("GraphQL with variables successful", map[string]interface{}{
			"result": string(jsonBytes),
		})
	}

	// Test complete
	eywa.Info("All tests completed successfully!", nil)
	eywa.CloseTask(eywa.SUCCESS)
}
