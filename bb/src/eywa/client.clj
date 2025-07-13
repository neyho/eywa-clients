(ns eywa.client
  (:require
   [clojure.pprint :refer [pprint]]
   [clojure.core.async :as async]
   [clojure.java.io :as io]
   [eywa.client.json :refer [->json <-json]])
  ;; Try to load file operations, ignore if not available
  (:require [eywa.client.files :as files] :reload))

(def pending-rpcs (atom {}))
(def handlers (atom {}))

(defn ->out [text]
  (.println System/out text))

(defn ->err [text]
  (.println System/err text))

(comment
  (pprint @pending-rpcs))

;; Function to handle responses
(defn handle-response [data]
  (let [id (:id data)
        callback (get @pending-rpcs id)]
    (if callback
      (do
        (async/put! callback data)
        (swap! pending-rpcs dissoc id))
      (->err (str "RPC callback not registered for request with id = " id)))))

;; Function to handle requests
(defn handle-request [data]
  (let [method (:method data)
        handler (get @handlers method)]
    (if handler
      (handler data)
      (->err (str "Method " method " doesn't have registered handler")))))

;; Function to handle incoming JSON-RPC data
(defn handle-data [data]
  (let [{:keys [method id result error]} data]
    (cond
      method (handle-request data)
      (and result id) (handle-response data)
      (and error id) (handle-response data)
      :else (->err "Received invalid JSON-RPC:" data))))

;; Function to send a request
(defn send-request
  "Will send json rpc request upstream. Promise of result is returned.
  Promise is registered at pending-rpcs."
  [data]
  (let [id (java.util.UUID/randomUUID)
        promise (async/promise-chan)]
    (swap! pending-rpcs assoc id promise)
    (let [request (assoc data :jsonrpc "2.0" :id id)]
      (->out (->json request))
      (flush))
    promise))

;; Function to send a notification
(defn send-notification [data]
  (let [notification (assoc data :jsonrpc "2.0")]
    (->out (->json notification))
    (flush)))

;; Function to register a handler
(defn register-handler [method func]
  (swap! handlers assoc method func))

;; Async reading from stdin
(defn read-stdin []
  (let [reader (io/reader *in*)]
    (loop []
      (when-let [line (try (.readLine reader) (catch Exception _))]
        (when-some [data (try
                           (<-json line)
                           (catch Throwable _
                             (->err "Couldn't parse: " (pr-str line))))]
          (try
            (handle-data data)
            (catch Throwable ex
              (.println System/err
                        (str "[READ Thread] Couldn't handle incomming JSON-RPC: '" (ex-message ex) " '\n"
                             (with-out-str (pprint data)))))))
        (recur)))))

;; Log utility functions
(defn log [event message & {:keys [data duration coordinates time]}]
  (send-notification {:method "task.log" :params {:time (or time (str (java.time.LocalDateTime/now)))
                                                  :event event
                                                  :message message
                                                  :data data
                                                  :coordinates coordinates
                                                  :duration duration}}))

(defn info [message & [data]] (log "INFO" message :data data))
(defn error [message & [data]] (log "ERROR" message :data data))
(defn warn [message & [data]] (log "WARN" message :data data))
(defn debug [message & [data]] (log "DEBUG" message :data data))
(defn trace [message & [data]] (log "TRACE" message :data data))
(defn exception [message & [data]] (log "EXCEPTION" message :data data))

;; Report function
(defn report
  "Send a task report with optional data and image"
  [message & {:keys [data image]}]
  (send-notification {:method "task.report"
                      :params {:message message
                               :data data
                               :image image}}))

;; Task status constants
(def SUCCESS "SUCCESS")
(def ERROR "ERROR")
(def PROCESSING "PROCESSING")
(def EXCEPTION "EXCEPTION")

;; Task management
(defn close-task [status]
  (send-notification {:method "task.close"
                      :params {:status status}})
  (if (= status SUCCESS)
    (System/exit 0)
    (System/exit 1)))

(defn update-task [status]
  (send-notification {:method "task.update"
                      :params {:status status}}))

(defn get-task
  "Get current task information. Returns a promise channel."
  []
  (send-request {:method "task.get"}))

(defn return-task []
  (send-notification {:method "task.return"})
  (System/exit 0))

(defn graphql
  "Execute a GraphQL query. Can be called as:
   (graphql query)
   (graphql query variables)
   (graphql {:query query :variables variables})"
  ([query]
   (graphql query nil))
  ([query variables]
   (if (map? query)
     ;; Old API - called with a map
     (let [{:keys [query variables]} query]
       (graphql query variables))
     ;; New API - called with separate args
     (async/go
       (let [{:keys [error result]}
             (async/<!
              (send-request
               {:method "eywa.datasets.graphql"
                :params {:query query
                         :variables variables}}))]
         (if-not error result
                 (ex-info
                  "GraphQL error"
                  error)))))))

;; Main loop
(defn start []
  (async/thread (read-stdin)))

;; Alias for compatibility with other clients
(def open-pipe start)

;; File operations - delegates to eywa.client.files namespace
;; These functions return core.async channels

(defn upload-file
  "Upload a file to EYWA file service.
  
  Args:
    filepath - Path to the file to upload (string or File)
    options - Keyword args:
      :name - Custom filename (defaults to file basename)
      :content-type - MIME type (auto-detected if not provided)
      :folder-uuid - UUID of parent folder (optional)
      :progress-fn - Function called with (bytes-uploaded, total-bytes)
  
  Returns:
    Core.async channel containing file information map"
  [filepath & options]
  (apply files/upload-file filepath options))

(defn upload-content
  "Upload content directly from memory.
  
  Args:
    content - String or byte array content to upload
    name - Filename for the content
    options - Keyword args same as upload-file
  
  Returns:
    Core.async channel containing file information map"
  [content name & options]
  (apply files/upload-content content name options))

(defn download-file
  "Download a file from EYWA file service.
  
  Args:
    file-uuid - UUID of the file to download
    options - Keyword args:
      :save-path - Path to save file (if nil, returns content as bytes)
      :progress-fn - Function called with (bytes-downloaded, total-bytes)
  
  Returns:
    Core.async channel containing saved path or content bytes"
  [file-uuid & options]
  (apply files/download-file file-uuid options))

(defn list-files
  "List files in EYWA file service.
  
  Args:
    options - Keyword args:
      :limit - Maximum number of files to return
      :status - Filter by status (PENDING, UPLOADED, etc.)
      :name-pattern - Filter by name pattern (SQL LIKE)
      :folder-uuid - Filter by folder UUID
  
  Returns:
    Core.async channel containing list of file maps"
  [& options]
  (apply files/list-files options))

(defn get-file-info
  "Get information about a specific file.
  
  Args:
    file-uuid - UUID of the file
  
  Returns:
    Core.async channel containing file information map or nil"
  [file-uuid]
  (files/get-file-info file-uuid))

(defn get-file-by-name
  "Get file information by name (returns most recent if multiple).
  
  Args:
    name - File name to search for
  
  Returns:
    Core.async channel containing file information map or nil"
  [name]
  (files/get-file-by-name name))

(defn delete-file
  "Delete a file from EYWA file service.
  
  Args:
    file-uuid - UUID of the file to delete
  
  Returns:
    Core.async channel containing true if successful"
  [file-uuid]
  (files/delete-file file-uuid))

(defn calculate-file-hash
  "Calculate hash of a file for integrity verification.
  
  Args:
    filepath - Path to the file
    options - Keyword args:
      :algorithm - Hash algorithm ('MD5', 'SHA-1', 'SHA-256', etc.)
  
  Returns:
    Hex digest of the file hash"
  [filepath & options]
  (apply files/calculate-file-hash filepath options))

;; Convenience functions
(defn quick-upload
  "Quick upload with minimal parameters.
  Returns core.async channel with file UUID"
  [filepath]
  (files/quick-upload filepath))

(defn quick-download
  "Quick download to current directory.
  Returns core.async channel with downloaded file path"
  [file-uuid & options]
  (apply files/quick-download file-uuid options))

;; Data processing helpers
(defn upload-json
  "Upload JSON data from Clojure data structure.
  Returns core.async channel with file information"
  [data filename & options]
  (apply files/upload-json data filename options))

(defn download-json
  "Download and parse JSON file.
  Returns core.async channel with parsed Clojure data"
  [file-uuid]
  (files/download-json file-uuid))

(defn upload-edn
  "Upload EDN data from Clojure data structure.
  Returns core.async channel with file information"
  [data filename & options]
  (apply files/upload-edn data filename options))

(defn download-edn
  "Download and parse EDN file.
  Returns core.async channel with parsed Clojure data"
  [file-uuid]
  (files/download-edn file-uuid))
