# EYWA Client for Babashka/Clojure

[![Clojars Project](https://img.shields.io/clojars/v/org.neyho/eywa-client.svg)](https://clojars.org/org.neyho/eywa-client)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

EYWA client library for Babashka and Clojure providing JSON-RPC communication, GraphQL queries, and task management for EYWA robots.

## Installation

### Babashka

Add to your `bb.edn`:

```clojure
{:deps {org.neyho/eywa-client {:mvn/version "0.2.0"}}}
```

### Clojure (deps.edn)

```clojure
{:deps {org.neyho/eywa-client {:mvn/version "0.2.0"}}}
```

### Leiningen

```clojure
[org.neyho/eywa-client "0.2.0"]
```

## Quick Start

```clojure
(require '[eywa.client :as eywa]
         '[clojure.core.async :as async])

(defn -main []
  ;; Initialize the client
  (eywa/open-pipe) ; or (eywa/start)
  
  ;; Log messages
  (eywa/info "Robot started")
  
  ;; Execute GraphQL queries
  (async/go
    (let [result (async/<! (eywa/graphql "
      {
        searchUser(_limit: 10) {
          euuid
          name
          type
        }
      }"))]
      (eywa/info "Users found" {:users result})))
  
  ;; Update task status
  (eywa/update-task eywa/PROCESSING)
  
  ;; Complete the task
  (Thread/sleep 1000)
  (eywa/close-task eywa/SUCCESS))
```

## Features

- 🚀 **Core.async Integration** - Idiomatic Clojure async programming
- 📊 **GraphQL Support** - Execute queries and mutations against EYWA datasets
- 📝 **Comprehensive Logging** - Multiple log levels with metadata support
- 🔄 **Task Management** - Update status, report progress, handle task lifecycle
- 🎯 **Thread-Safe** - Concurrent operations with atoms and channels
- 🔧 **Flexible API** - Both old and new GraphQL API styles supported

## API Reference

### Initialization

#### `(start)` or `(open-pipe)`
Initialize stdin/stdout communication with EYWA runtime. Must be called before using other functions.

```clojure
(eywa/start)
;; or
(eywa/open-pipe) ; alias for compatibility
```

### Logging Functions

#### `(log event message & {:keys [data duration coordinates time]})`
Log a message with full control over all parameters.

```clojure
(eywa/log "INFO" "Processing item"
          :data {:item-id 123}
          :duration 1500
          :coordinates {:x 10 :y 20})
```

#### `(info)`, `(error)`, `(warn)`, `(debug)`, `(trace)`, `(exception)`
Convenience functions for different log levels.

```clojure
(eywa/info "User logged in" {:user-id "abc123"})
(eywa/error "Failed to process" {:error (.getMessage ex)})
(eywa/exception "Unhandled error" {:stack (with-out-str (.printStackTrace ex))})
```

### Task Management

#### `(get-task)`
Get current task information. Returns a promise channel.

```clojure
(async/go
  (let [task (async/<! (eywa/get-task))]
    (eywa/info "Processing task" {:task-id (:euuid (:result task))})))
```

#### `(update-task status)`
Update the current task status.

```clojure
(eywa/update-task eywa/PROCESSING)
```

#### `(close-task status)`
Close the task with a final status and exit the process.

```clojure
(try
  ;; Do work...
  (eywa/close-task eywa/SUCCESS)
  (catch Exception e
    (eywa/error "Task failed" {:error (.getMessage e)})
    (eywa/close-task eywa/ERROR)))
```

#### `(return-task)`
Return control to EYWA without closing the task.

```clojure
(eywa/return-task)
```

### Reporting

#### `(report message & {:keys [data image]})`
Send a task report with optional data and image.

```clojure
(eywa/report "Analysis complete"
             :data {:accuracy 0.95
                    :processed 1000}
             :image chart-image-base64)
```

### GraphQL

#### `(graphql query)` or `(graphql query variables)`
Execute a GraphQL query against the EYWA server. Returns a go channel.

```clojure
;; Simple query
(async/go
  (let [result (async/<! (eywa/graphql "{ searchUser { name } }"))]
    (println "Result:" result)))

;; Query with variables
(async/go
  (let [result (async/<! (eywa/graphql
                          "mutation CreateUser($input: UserInput!) {
                             syncUser(data: $input) { euuid name }
                           }"
                          {:input {:name "John Doe"
                                  :active true}}))]
    (println "Created user:" result)))

;; Old API style (still supported)
(async/go
  (let [result (async/<! (eywa/graphql
                          {:query "{ searchUser { name } }"
                           :variables nil}))]
    (println "Result:" result)))
```

### JSON-RPC

#### `(send-request data)`
Send a JSON-RPC request and get a promise channel for the response.

```clojure
(async/go
  (let [result (async/<! (eywa/send-request
                          {:method "custom.method"
                           :params {:foo "bar"}}))]
    (println "Response:" result)))
```

#### `(send-notification data)`
Send a JSON-RPC notification without expecting a response.

```clojure
(eywa/send-notification
  {:method "custom.event"
   :params {:status "ready"}})
```

#### `(register-handler method func)`
Register a handler for incoming JSON-RPC method calls.

```clojure
(eywa/register-handler "custom.ping"
  (fn [request]
    (eywa/info "Received ping" (:params request))
    (eywa/send-notification
      {:method "custom.pong"
       :params {:timestamp (System/currentTimeMillis)}})))
```

## Constants

```clojure
eywa/SUCCESS    ; "SUCCESS"
eywa/ERROR      ; "ERROR"
eywa/PROCESSING ; "PROCESSING"
eywa/EXCEPTION  ; "EXCEPTION"
```

## Complete Example

```clojure
#!/usr/bin/env bb

(require '[babashka.deps :as deps])
(deps/add-classpath "src")

(require '[eywa.client :as eywa]
         '[clojure.core.async :as async])

(defn process-data []
  (eywa/open-pipe)
  
  (try
    ;; Get task info
    (async/go
      (when-let [task-result (async/<! (eywa/get-task))]
        (if-let [task (:result task-result)]
          (do
            (eywa/info "Starting task" {:task-id (:euuid task)})
            
            ;; Update status
            (eywa/update-task eywa/PROCESSING)
            
            ;; Query data
            (let [result (async/<! (eywa/graphql "
              query GetActiveUsers {
                searchUser(_where: {active: {_eq: true}}) {
                  euuid
                  name
                  email
                }
              }"))]
              
              (if (instance? Exception result)
                (throw result)
                (let [users (get-in result ["data" "searchUser"])]
                  (eywa/info "Found users" {:count (count users)})
                  
                  ;; Process users
                  (doseq [user users]
                    (eywa/debug "Processing user" {:user-id (get user "euuid")}))
                  
                  ;; Report results
                  (eywa/report "Found active users"
                               :data {:count (count users)
                                     :users (map #(get % "name") users)})
                  
                  ;; Success!
                  (eywa/close-task eywa/SUCCESS))))
          
          ;; No task context
          (do
            (eywa/warn "No task context, running in demo mode")
            (eywa/close-task eywa/SUCCESS)))))
    
    ;; Wait for async operations
    (Thread/sleep 2000)
    
    (catch Exception e
      (eywa/error "Task failed"
                  {:error (.getMessage e)
                   :stack (with-out-str (.printStackTrace e))})
      (eywa/close-task eywa/ERROR))))

(process-data)
```

## Error Handling

GraphQL errors are returned as ExceptionInfo:

```clojure
(async/go
  (let [result (async/<! (eywa/graphql "{ invalid }"))]
    (if (instance? Exception result)
      (eywa/error "GraphQL failed" {:error (.getMessage result)})
      (eywa/info "Success" result))))
```

## Testing

Test your robot locally using the EYWA CLI:

```bash
eywa run -c 'bb my-robot.clj'
```

## Thread Safety

All operations are thread-safe:
- Request/response tracking uses atoms
- Handler registration is synchronized
- Channel operations are inherently thread-safe

## Dependencies

- Clojure 1.10+
- core.async
- cheshire (JSON parsing)
- camel-snake-kebab (case conversion)

## License

MIT

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues and questions, please visit the [EYWA repository](https://github.com/neyho/eywa).
