package eywa

import (
	"bufio"
	"encoding/json"
	"fmt"
	"log"
	"math/rand"
	"os"
	"sync"
	"time"
)

// Task status constants
const (
	SUCCESS    = "SUCCESS"
	ERROR      = "ERROR"
	PROCESSING = "PROCESSING"
	EXCEPTION  = "EXCEPTION"
)

// Log event types
const (
	INFO      = "INFO"
	WARN      = "WARN"
	DEBUG     = "DEBUG"
	TRACE     = "TRACE"
	LOG_ERROR = "ERROR"
	LOG_EXCEPTION = "EXCEPTION"
)

// Request represents a JSON-RPC request
type Request struct {
	JsonRPC string      `json:"jsonrpc"`
	Method  string      `json:"method"`
	Params  interface{} `json:"params,omitempty"`
	ID      string      `json:"id,omitempty"`
}

// Response represents a JSON-RPC response
type Response struct {
	JsonRPC string      `json:"jsonrpc"`
	Result  interface{} `json:"result,omitempty"`
	Error   interface{} `json:"error,omitempty"`
	ID      string      `json:"id,omitempty"`
}

// LogParams represents parameters for logging
type LogParams struct {
	Time        *time.Time  `json:"time,omitempty"`
	Event       string      `json:"event"`
	Message     string      `json:"message"`
	Data        interface{} `json:"data,omitempty"`
	Coordinates interface{} `json:"coordinates,omitempty"`
	Duration    *int        `json:"duration,omitempty"`
}

// ReportParams represents parameters for reporting
type ReportParams struct {
	Message string      `json:"message"`
	Data    interface{} `json:"data,omitempty"`
	Image   interface{} `json:"image,omitempty"`
}

// TaskParams represents task-related parameters
type TaskParams struct {
	Status string `json:"status"`
}

// GraphQLParams represents GraphQL query parameters
type GraphQLParams struct {
	Query     string                 `json:"query"`
	Variables map[string]interface{} `json:"variables,omitempty"`
}

// Global maps and mutex for concurrency
var (
	rpcCallbacks = make(map[string]chan Response)
	handlers     = make(map[string]func(Request))
	mu           sync.Mutex
)

func init() {
	rand.Seed(time.Now().UnixNano())
}

// RegisterHandler registers a handler for a specific method
func RegisterHandler(method string, handler func(Request)) {
	mu.Lock()
	defer mu.Unlock()
	handlers[method] = handler
}

// SendRequest sends a JSON-RPC request and returns a channel for the response
func SendRequest(data map[string]interface{}) chan Response {
	id := generateID()
	data["jsonrpc"] = "2.0"
	data["id"] = id

	// Create a channel for the response and store it
	responseChan := make(chan Response, 1)
	mu.Lock()
	rpcCallbacks[id] = responseChan
	mu.Unlock()

	// Write request to stdout
	sendJSON(data)
	return responseChan
}

// SendNotification sends a JSON-RPC notification (no response expected)
func SendNotification(data map[string]interface{}) {
	data["jsonrpc"] = "2.0"
	sendJSON(data)
}

// OpenPipe starts listening for incoming JSON-RPC messages on stdin
func OpenPipe() {
	scanner := bufio.NewScanner(os.Stdin)
	// Increase buffer size for large JSON responses
	buf := make([]byte, 0, 64*1024)
	scanner.Buffer(buf, 1024*1024)
	
	for scanner.Scan() {
		var data map[string]interface{}
		if err := json.Unmarshal(scanner.Bytes(), &data); err != nil {
			log.Printf("Received invalid JSON: %v", err)
			continue
		}
		handleData(data)
	}
	
	if err := scanner.Err(); err != nil {
		log.Printf("Error reading stdin: %v", err)
	}
}

// Log sends a log message with full control over parameters
func Log(event, message string, data interface{}, duration *int, coordinates interface{}, logTime *time.Time) {
	params := LogParams{
		Event:       event,
		Message:     message,
		Data:        data,
		Duration:    duration,
		Coordinates: coordinates,
	}
	
	if logTime != nil {
		params.Time = logTime
	} else {
		now := time.Now()
		params.Time = &now
	}
	
	SendNotification(map[string]interface{}{
		"method": "task.log",
		"params": params,
	})
}

// Info logs an info message
func Info(message string, data interface{}) {
	Log(INFO, message, data, nil, nil, nil)
}

// Error logs an error message
func Error(message string, data interface{}) {
	Log(LOG_ERROR, message, data, nil, nil, nil)
}

// Warn logs a warning message
func Warn(message string, data interface{}) {
	Log(WARN, message, data, nil, nil, nil)
}

// Debug logs a debug message
func Debug(message string, data interface{}) {
	Log(DEBUG, message, data, nil, nil, nil)
}

// Trace logs a trace message
func Trace(message string, data interface{}) {
	Log(TRACE, message, data, nil, nil, nil)
}

// Exception logs an exception message
func Exception(message string, data interface{}) {
	Log(LOG_EXCEPTION, message, data, nil, nil, nil)
}

// Report sends a task report
func Report(message string, data interface{}, image interface{}) {
	SendNotification(map[string]interface{}{
		"method": "task.report",
		"params": ReportParams{
			Message: message,
			Data:    data,
			Image:   image,
		},
	})
}

// UpdateTask updates the current task status
func UpdateTask(status string) {
	SendNotification(map[string]interface{}{
		"method": "task.update",
		"params": TaskParams{
			Status: status,
		},
	})
}

// GetTask retrieves the current task information
func GetTask() (interface{}, error) {
	responseChan := SendRequest(map[string]interface{}{
		"method": "task.get",
	})
	
	response := <-responseChan
	
	if response.Error != nil {
		return nil, fmt.Errorf("task.get error: %v", response.Error)
	}
	
	return response.Result, nil
}

// ReturnTask returns control to EYWA without closing the task
func ReturnTask() {
	SendNotification(map[string]interface{}{
		"method": "task.return",
	})
	os.Exit(0)
}

// CloseTask closes the current task with a status
func CloseTask(status string) {
	SendNotification(map[string]interface{}{
		"method": "task.close",
		"params": TaskParams{
			Status: status,
		},
	})
	
	if status == SUCCESS {
		os.Exit(0)
	} else {
		os.Exit(1)
	}
}

// GraphQL executes a GraphQL query
func GraphQL(query string, variables map[string]interface{}) (map[string]interface{}, error) {
	responseChan := SendRequest(map[string]interface{}{
		"method": "eywa.datasets.graphql",
		"params": GraphQLParams{
			Query:     query,
			Variables: variables,
		},
	})
	
	response := <-responseChan
	
	if response.Error != nil {
		return nil, fmt.Errorf("GraphQL error: %v", response.Error)
	}
	
	// Convert result to map
	if result, ok := response.Result.(map[string]interface{}); ok {
		return result, nil
	}
	
	return nil, fmt.Errorf("unexpected GraphQL response format")
}

// Helper functions (internal)

func generateID() string {
	return fmt.Sprintf("%d", rand.Int63())
}

func handleData(data map[string]interface{}) {
	if _, ok := data["method"].(string); ok {
		handleRequest(data)
	} else if _, ok := data["id"]; ok {
		handleResponse(data)
	} else {
		log.Println("Received invalid JSON-RPC:", data)
	}
}

func handleRequest(data map[string]interface{}) {
	method, _ := data["method"].(string)
	
	request := Request{
		JsonRPC: "2.0",
		Method:  method,
		Params:  data["params"],
	}
	
	if id, ok := data["id"]; ok {
		request.ID = fmt.Sprintf("%v", id)
	}
	
	mu.Lock()
	handler, exists := handlers[method]
	mu.Unlock()
	
	if exists {
		handler(request)
	} else {
		log.Printf("Method %s doesn't have a registered handler", method)
	}
}

func handleResponse(data map[string]interface{}) {
	id := fmt.Sprintf("%v", data["id"])
	
	response := Response{
		JsonRPC: "2.0",
		Result:  data["result"],
		Error:   data["error"],
		ID:      id,
	}

	mu.Lock()
	if callback, exists := rpcCallbacks[id]; exists {
		delete(rpcCallbacks, id)
		mu.Unlock()
		callback <- response
		close(callback)
	} else {
		mu.Unlock()
		log.Printf("RPC callback not registered for request with id = %s", id)
	}
}

func sendJSON(data interface{}) {
	encoded, err := json.Marshal(data)
	if err != nil {
		log.Printf("Failed to encode JSON: %v", err)
		return
	}
	fmt.Println(string(encoded))
}
