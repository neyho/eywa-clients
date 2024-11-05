// handler.go
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

// Structs for JSON-RPC messages
type Request struct {
    JsonRPC string      `json:"jsonrpc"`
    Method  string      `json:"method"`
    Params  interface{} `json:"params,omitempty"`
    ID      string      `json:"id,omitempty"`
}

type Response struct {
    JsonRPC string      `json:"jsonrpc"`
    Result  interface{} `json:"result,omitempty"`
    Error   interface{} `json:"error,omitempty"`
    ID      string      `json:"id,omitempty"`
}

// Global maps and mutex for concurrency
var rpcCallbacks = make(map[string]chan Response)
var handlers = make(map[string]func(Request))
var mu sync.Mutex

func init() {
    rand.Seed(time.Now().UnixNano())
}

// Register a handler for a specific method
func RegisterHandler(method string, handler func(Request)) {
    handlers[method] = handler
}

// Send a JSON-RPC request with a callback
func SendRequest(method string, params interface{}) chan Response {
    id := generateID()
    data := Request{JsonRPC: "2.0", Method: method, Params: params, ID: id}

    // Create a channel for the response and store it
    responseChan := make(chan Response, 1)
    mu.Lock()
    rpcCallbacks[id] = responseChan
    mu.Unlock()

    // Write request to stdout
    sendJSON(data)
    return responseChan
}

// Send a JSON-RPC notification (without expecting a response)
func SendNotification(method string, params interface{}) {
    data := Request{JsonRPC: "2.0", Method: method, Params: params}
    sendJSON(data)
}

// Listen for incoming data on stdin
func OpenPipe() {
    scanner := bufio.NewScanner(os.Stdin)
    for scanner.Scan() {
        var data map[string]interface{}
        if err := json.Unmarshal(scanner.Bytes(), &data); err != nil {
            log.Printf("Received invalid JSON: %v", err)
            continue
        }
        handleData(data)
    }
}

// Generate a unique ID for each request
func generateID() string {
    return fmt.Sprintf("%d", rand.Int63())
}

// Handle incoming JSON-RPC data
func handleData(data map[string]interface{}) {
    if method, ok := data["method"].(string); ok {
        handleRequest(data)
    } else if _, ok := data["id"]; ok {
        handleResponse(data)
    } else {
        log.Println("Received invalid JSON-RPC:", data)
    }
}

// Process a JSON-RPC request
func handleRequest(data map[string]interface{}) {
    method := data["method"].(string)
    request := Request{
        JsonRPC: "2.0",
        Method:  method,
        Params:  data["params"],
        ID:      data["id"].(string),
    }
    if handler, exists := handlers[method]; exists {
        handler(request)
    } else {
        log.Printf("Method %s doesn't have a registered handler", method)
    }
}

// Process a JSON-RPC response
func handleResponse(data map[string]interface{}) {
    id := data["id"].(string)
    response := Response{
        JsonRPC: "2.0",
        Result:  data["result"],
        Error:   data["error"],
        ID:      id,
    }

    mu.Lock()
    if callback, exists := rpcCallbacks[id]; exists {
        delete(rpcCallbacks, id)
        callback <- response
        close(callback)
    } else {
        log.Printf("RPC callback not registered for request with id = %s", id)
    }
    mu.Unlock()
}

// Send JSON-encoded data to stdout
func sendJSON(data interface{}) {
    encoded, err := json.Marshal(data)
    if err != nil {
        log.Printf("Failed to encode JSON: %v", err)
        return
    }
    fmt.Println(string(encoded))
}
