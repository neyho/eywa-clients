# EYWA Client for Babashka/Clojure

[![Clojars Project](https://img.shields.io/clojars/v/org.neyho/eywa-client.svg)](https://clojars.org/org.neyho/eywa-client)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

EYWA client library for Babashka and Clojure providing JSON-RPC communication, GraphQL queries, comprehensive file operations, and task management for EYWA robots.

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
         '[eywa.files :as files]
         '[clojure.core.async :as async])

(defn -main []
  ;; Initialize the client
  (eywa/start) ; or (eywa/open-pipe)
  
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
  
  ;; Upload a file
  (async/go
    (async/<! (files/upload "data.csv" 
                            {:name "data.csv"
                             :folder {:euuid files/root-euuid}}))
    (eywa/info "File uploaded"))
  
  ;; Update task status
  (eywa/update-task eywa/PROCESSING)
  
  ;; Wait for async operations
  (Thread/sleep 2000)
  
  ;; Complete the task
  (eywa/close-task eywa/SUCCESS))
```

## Features

- 🚀 **Core.async Integration** - Idiomatic Clojure async programming
- 📊 **GraphQL Support** - Execute queries and mutations against EYWA datasets
- 📁 **Comprehensive File Operations** - Upload/download with streaming, folders, progress tracking
- 📝 **Multi-Level Logging** - info, warn, error, debug, trace, exception
- 🔄 **Task Management** - Update status, report progress, handle task lifecycle
- 🎯 **Thread-Safe** - Concurrent operations with atoms and channels
- 🔧 **Flexible API** - Both old and new GraphQL API styles supported
- 💾 **Memory Efficient** - Streaming for large file operations
- 🔐 **File Integrity** - Hash calculation (SHA-256, MD5, SHA-512)

## API Reference

### Initialization

#### `(start)` or `(open-pipe)`
Initialize stdin/stdout communication with EYWA runtime. Must be called before using other functions.

```clojure
(eywa/start)
;; or
(eywa/open-pipe) ; alias for compatibility
```

---

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

---

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

**Status Constants:**
```clojure
eywa/SUCCESS    ; "SUCCESS"
eywa/ERROR      ; "ERROR"
eywa/PROCESSING ; "PROCESSING"
eywa/EXCEPTION  ; "EXCEPTION"
```

---

### Reporting

#### `(report message & {:keys [data image]})`
Send a task report with optional data and image.

```clojure
(eywa/report "Analysis complete"
             :data {:accuracy 0.95
                    :processed 1000}
             :image chart-image-base64)
```

---

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

---

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

---

## File Operations

The library includes comprehensive file upload/download capabilities through the `eywa.files` namespace.

### Core File Operations

#### `(upload filepath file-data)`
Upload a file from disk.

```clojure
(require '[eywa.files :as files])

;; Upload to root folder
(async/go
  (async/<! (files/upload "data.csv" 
                          {:name "data.csv"})))

;; Upload to specific folder
(async/go
  (async/<! (files/upload "report.pdf" 
                          {:name "report.pdf"
                           :folder {:euuid folder-uuid}})))

;; Replace existing file (same UUID)
(async/go
  (async/<! (files/upload "updated-data.csv"
                          {:name "data.csv"
                           :euuid existing-file-uuid})))

;; With progress tracking
(async/go
  (async/<! (files/upload "large-file.zip"
                          {:name "large-file.zip"
                           :progress-fn (fn [bytes-sent total]
                                         (println "Progress:" bytes-sent "/" total))})))
```

#### `(upload-content content file-data)`
Upload content directly from memory (string or byte array).

```clojure
;; Upload string content
(async/go
  (async/<! (files/upload-content "Hello World"
                                  {:name "greeting.txt"
                                   :folder {:euuid folder-uuid}})))

;; Upload generated data
(async/go
  (let [data (generate-report)]
    (async/<! (files/upload-content data
                                    {:name "report.txt"
                                     :content_type "text/plain"}))))

;; Replace existing file content
(async/go
  (async/<! (files/upload-content "UPDATED CONTENT"
                                  {:name "data.txt"
                                   :euuid existing-file-uuid})))
```

#### `(upload-stream input-stream file-data)`
Upload from an InputStream (memory efficient for large files).

```clojure
(require '[clojure.java.io :as io])

;; Upload from stream
(let [file (io/file "large-file.zip")
      file-size (.length file)]
  (async/go
    (with-open [stream (io/input-stream file)]
      (async/<! (files/upload-stream stream
                                     {:name "large-file.zip"
                                      :size file-size
                                      :folder {:euuid folder-uuid}})))))
```

#### `(download file-uuid & {:keys [save-path progress-fn]})`
Download a file (convenience function for small files).

```clojure
;; Download to file
(async/go
  (async/<! (files/download file-uuid 
                            :save-path "downloaded.csv")))

;; Download to memory (returns byte array)
(async/go
  (let [content (async/<! (files/download file-uuid))]
    (println "Downloaded" (count content) "bytes")))

;; With progress tracking
(async/go
  (async/<! (files/download file-uuid
                            :save-path "large-file.zip"
                            :progress-fn (fn [bytes total]
                                          (println "Received:" bytes "/" total)))))
```

#### `(download-stream file-uuid)`
Download as InputStream (memory efficient). Returns `{:stream InputStream :content-length Long}`.

```clojure
;; Stream to file
(async/go
  (let [result (async/<! (files/download-stream file-uuid))]
    (when-not (instance? Exception result)
      (with-open [stream (:stream result)
                  output (io/output-stream "saved.zip")]
        (io/copy stream output)
        (eywa/info "Downloaded" {:bytes (:content-length result)})))))

;; Process stream without saving
(async/go
  (let [result (async/<! (files/download-stream file-uuid))]
    (when-not (instance? Exception result)
      (with-open [stream (:stream result)
                  reader (io/reader stream)]
        (doseq [line (line-seq reader)]
          (process-line line))))))
```

#### `(list & {:keys [limit status name-pattern folder-uuid]})`
List files with optional filters.

```clojure
;; List all files
(async/go
  (let [files (async/<! (files/list))]
    (doseq [f files]
      (println (:name f) "-" (:size f) "bytes"))))

;; List files in folder
(async/go
  (let [files (async/<! (files/list :folder-uuid folder-id))]
    (eywa/info "Files in folder" {:count (count files)})))

;; List with filters
(async/go
  (let [files (async/<! (files/list :limit 10
                                    :name-pattern "report"
                                    :status "UPLOADED"))]
    (println "Found" (count files) "files")))
```

#### `(info file-uuid)`
Get file information.

```clojure
(async/go
  (when-let [file-info (async/<! (files/info file-uuid))]
    (eywa/info "File details"
               {:name (:name file-info)
                :size (:size file-info)
                :type (:content_type file-info)
                :uploaded-at (:uploaded_at file-info)
                :folder (get-in file-info [:folder :name])})))
```

#### `(delete file-uuid)`
Delete a file.

```clojure
(async/go
  (let [success? (async/<! (files/delete file-uuid))]
    (if success?
      (eywa/info "File deleted")
      (eywa/error "Delete failed"))))
```

#### `(hash filepath & {:keys [algorithm]})`
Calculate file hash for integrity verification.

```clojure
(files/hash "data.csv") ; SHA-256 (default)
(files/hash "data.csv" :algorithm "MD5")
(files/hash "data.csv" :algorithm "SHA-1")
(files/hash "data.csv" :algorithm "SHA-512")
```

---

### Folder Operations

#### `(create-folder folder-data)`
Create a new folder.

```clojure
;; Create folder in root
(async/go
  (let [folder (async/<! (files/create-folder 
                          {:name "my-folder"}))]
    (eywa/info "Created folder" {:uuid (:euuid folder)
                                 :path (:path folder)})))

;; Create subfolder
(async/go
  (async/<! (files/create-folder 
             {:name "subfolder"
              :parent {:euuid parent-folder-uuid}})))

;; Create with pre-generated UUID (for idempotency)
(async/go
  (async/<! (files/create-folder 
             {:name "reports"
              :euuid #uuid "9bd6fe99-7540-4a54-9998-138405ea8d2c"
              :parent {:euuid files/root-euuid}})))
```

#### `(list-folders & {:keys [limit name-pattern parent-folder-uuid]})`
List folders with filters.

```clojure
;; List all folders
(async/go
  (let [folders (async/<! (files/list-folders))]
    (doseq [f folders]
      (println (:name f) "-" (:path f)))))

;; List root folders
(async/go
  (let [folders (async/<! (files/list-folders :parent-folder-uuid nil))]
    (println "Root folders:" (count folders))))

;; List subfolders
(async/go
  (let [folders (async/<! (files/list-folders 
                           :parent-folder-uuid parent-id))]
    (println "Subfolders:" (count folders))))
```

#### `(get-folder-info folder-uuid)`
Get folder information.

```clojure
(async/go
  (when-let [folder (async/<! (files/get-folder-info folder-uuid))]
    (eywa/info "Folder details"
               {:name (:name folder)
                :path (:path folder)
                :parent (get-in folder [:parent :name])})))
```

#### `(delete-folder folder-uuid)`
Delete a folder (must be empty).

```clojure
(async/go
  (let [success? (async/<! (files/delete-folder folder-uuid))]
    (if success?
      (eywa/info "Folder deleted")
      (eywa/error "Delete failed - folder may not be empty"))))
```

**Root Folder Constants:**
```clojure
files/root-euuid    ; #uuid "87ce50d8-5dfa-4008-a265-053e727ab793"
files/root-folder   ; {:euuid root-euuid} - use for :parent field
```

---

## File Operations Best Practices

### Pre-Generated UUIDs for Idempotency

```clojure
;; ✅ RECOMMENDED: Generate UUIDs ahead of time
(def test-data
  {:folders
   {:reports {:euuid #uuid "4e2dfc2f-d46e-499a-b008-2104b9214aa1"
              :name "reports"
              :parent {:euuid files/root-euuid}}
    :archive {:euuid #uuid "16e65f24-b051-4fe6-8171-058747ec6241"
              :name "archive"
              :parent {:euuid #uuid "4e2dfc2f-d46e-499a-b008-2104b9214aa1"}}}
   
   :files
   {:report-data {:euuid #uuid "ea0fee9a-30d9-4aae-b087-10bce969af57"
                  :name "report.csv"
                  :folder {:euuid #uuid "4e2dfc2f-d46e-499a-b008-2104b9214aa1"}}}})

;; Benefits:
;; - Idempotent operations (safe to run multiple times)
;; - Easy cleanup scripts (know exact UUIDs to delete)
;; - Reproducible test data
;; - Reference folders/files easily
```

### Folder Hierarchy Patterns

```clojure
;; Build nested folder structure
(defn create-folder-hierarchy [folders]
  (async/go
    (doseq [[key folder-def] folders]
      (let [result (async/<! (files/create-folder folder-def))]
        (if (instance? Exception result)
          (eywa/error "Failed to create folder" {:key key :error (.getMessage result)})
          (eywa/info "Created folder" {:key key :path (:path result)}))))))

;; Example usage
(def folder-structure
  {:root    {:euuid #uuid "..." :name "project" :parent files/root-folder}
   :input   {:euuid #uuid "..." :name "input"   :parent {:euuid root-uuid}}
   :output  {:euuid #uuid "..." :name "output"  :parent {:euuid root-uuid}}
   :archive {:euuid #uuid "..." :name "archive" :parent {:euuid output-uuid}}})

(create-folder-hierarchy folder-structure)
```

### Upload Patterns

```clojure
;; Pattern 1: Upload from disk
(files/upload "data/report.csv"
              {:name "report.csv"
               :euuid #uuid "..."  ; Pre-generated for idempotency
               :folder {:euuid folder-uuid}})

;; Pattern 2: Upload content from memory
(files/upload-content (str "Generated at: " (java.time.Instant/now))
                      {:name "timestamp.txt"
                       :euuid #uuid "..."
                       :folder {:euuid folder-uuid}})

;; Pattern 3: Replace existing file (same UUID)
(files/upload-content "UPDATED CONTENT"
                      {:name "report.txt"
                       :euuid existing-file-uuid})  ; Replaces content
```

### Cleanup Pattern

```clojure
;; Always delete files before folders
;; Delete folders deepest-first

(def cleanup-order
  {:files [#uuid "..." #uuid "..." #uuid "..."]
   :folders [#uuid "..."  ; deepest folder
             #uuid "..."  ; parent folder  
             #uuid "..."]}) ; root folder

;; Delete all files first
(async/go
  (doseq [file-uuid (:files cleanup-order)]
    (async/<! (files/delete file-uuid)))
  
  ;; Then delete folders (deepest first)
  (doseq [folder-uuid (:folders cleanup-order)]
    (async/<! (files/delete-folder folder-uuid))))
```

### Error Handling Pattern

```clojure
(async/go
  (let [result (async/<! (files/upload "data.csv" {:name "data.csv"}))]
    (if (instance? Exception result)
      (do
        (eywa/error "Upload failed" {:error (.getMessage result)})
        (eywa/close-task eywa/ERROR))
      (do
        (eywa/info "Upload successful")
        ;; Verify with files/info
        (let [info (async/<! (files/info file-uuid))]
          (eywa/info "File details" {:size (:size info)
                                     :path (:path info)}))))))
```

---

## Streaming Operations

For large files or memory-constrained environments, use streaming operations.

### Upload from Stream

```clojure
;; Upload from any InputStream source
(async/go
  (with-open [input-stream (io/input-stream "large-file.zip")]
    (let [file-size (.length (io/file "large-file.zip"))]
      (async/<! (files/upload-stream input-stream
                                     {:name "large-file.zip"
                                      :size file-size
                                      :folder {:euuid folder-id}})))))

;; Upload from network stream
(async/go
  (with-open [conn (.openConnection (java.net.URL. "https://example.com/data.csv"))
              stream (.getInputStream conn)]
    (async/<! (files/upload-stream stream
                                   {:name "downloaded-data.csv"
                                    :size content-length}))))

;; Upload generated content without intermediate file
(async/go
  (let [content "Generated content..."
        data-stream (io/input-stream (.getBytes content "UTF-8"))]
    (async/<! (files/upload-stream data-stream
                                   {:name "generated.txt"
                                    :size (count (.getBytes content "UTF-8"))}))))
```

### Download to Stream

```clojure
;; Download large file without loading into memory
(async/go
  (let [result (async/<! (files/download-stream file-uuid))]
    (if (instance? Exception result)
      (eywa/error "Download failed" {:error (.getMessage result)})
      (with-open [input-stream (:stream result)
                  output-stream (io/output-stream "saved-file.zip")]
        (io/copy input-stream output-stream)
        (eywa/info "Downloaded" {:bytes (:content-length result)})))))

;; Process stream without saving to disk
(async/go
  (let [result (async/<! (files/download-stream file-uuid))]
    (if (instance? Exception result)
      (eywa/error "Download failed")
      (with-open [input-stream (:stream result)
                  reader (io/reader input-stream)]
        ;; Process line by line
        (doseq [line (line-seq reader)]
          (process-line line))))))
```

### Memory-Efficient Patterns

```clojure
;; ✅ RECOMMENDED: Stream large files
(async/go
  (with-open [is (io/input-stream "10GB-file.zip")]
    (async/<! (files/upload-stream is {:name "big.zip" 
                                       :size file-size}))))

;; ❌ AVOID: Loading large files into memory
(async/go
  (let [content (slurp "10GB-file.txt")] ; Loads entire file into memory!
    (async/<! (files/upload-content content {:name "big.txt"}))))

;; ✅ RECOMMENDED: Download with streaming
(async/go
  (let [result (async/<! (files/download-stream file-uuid))]
    (with-open [stream (:stream result)]
      (process-stream stream)))) ; Process without loading all

;; ❌ AVOID: Loading into memory first
(async/go
  (let [content (async/<! (files/download file-uuid))] ; Loads entire file!
    (process-bytes content)))
```

---

## File Integrity & Security

### Hash Verification

Verify file integrity before and after upload:

```clojure
;; Calculate hash before upload
(let [local-file "important-data.csv"
      local-hash (files/hash local-file :algorithm "SHA-256")]
  
  (eywa/info "Local file hash" {:hash local-hash})
  
  ;; Upload file
  (async/go
    (async/<! (files/upload local-file 
                            {:name "important-data.csv"
                             :euuid file-uuid}))
    
    ;; Download and verify
    (let [downloaded (async/<! (files/download file-uuid))
          temp-file "/tmp/verify.csv"]
      (if (instance? Exception downloaded)
        (eywa/error "Download failed")
        (do
          (spit temp-file downloaded)
          (let [downloaded-hash (files/hash temp-file :algorithm "SHA-256")]
            (if (= local-hash downloaded-hash)
              (eywa/info "✅ File integrity verified" {:hash downloaded-hash})
              (eywa/error "❌ Hash mismatch!" {:local local-hash 
                                               :remote downloaded-hash}))))))))

;; Available hash algorithms
(files/hash "file.txt" :algorithm "MD5")
(files/hash "file.txt" :algorithm "SHA-1")
(files/hash "file.txt" :algorithm "SHA-256") ; Default and recommended
(files/hash "file.txt" :algorithm "SHA-512")
```

### Content Type Detection

```clojure
;; Auto-detection (recommended)
(files/upload "report.pdf" {:name "report.pdf"})
; Content-Type: application/pdf (auto-detected)

;; Manual override
(files/upload "data.bin" {:name "data.bin"
                          :content_type "application/octet-stream"})

;; Supported auto-detected types:
; .txt  → text/plain
; .html → text/html
; .json → application/json
; .csv  → text/csv
; .pdf  → application/pdf
; .png  → image/png
; .jpg  → image/jpeg
; .docx → application/vnd.openxmlformats-officedocument.wordprocessingml.document
; .xlsx → application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
; ... and many more
```

---

## Async/Await Patterns

The EYWA client uses `core.async` for non-blocking operations. Here are common patterns:

### Pattern 1: Fire-and-Forget (Logging, Notifications)

```clojure
;; Logging and notifications don't need async handling
(eywa/info "Processing started")
(eywa/update-task eywa/PROCESSING)
```

### Pattern 2: Single Async Operation

```clojure
;; Use async/go for operations that return channels
(async/go
  (let [result (async/<! (eywa/graphql "{ searchUser { name } }"))]
    (if (instance? Exception result)
      (eywa/error "Failed" {:error (.getMessage result)})
      (eywa/info "Success" {:data result}))))

;; Wait for completion in main thread
(Thread/sleep 1000)
```

### Pattern 3: Sequential Async Operations

```clojure
(async/go
  ;; Each operation waits for the previous to complete
  (let [folder (async/<! (files/create-folder {:name "data"}))
        _      (async/<! (files/upload "file.csv" 
                                       {:folder {:euuid (:euuid folder)}}))
        files  (async/<! (files/list :folder-uuid (:euuid folder)))]
    (eywa/info "Uploaded" {:count (count files)})))
```

### Pattern 4: Blocking Wait (In -main)

```clojure
(defn -main []
  (eywa/start)
  
  ;; Use async/<!! to block until result is ready
  (let [task (async/<!! (eywa/get-task))]
    (eywa/info "Got task" {:task task}))
  
  ;; Or use Thread/sleep to keep process alive for go blocks
  (async/go
    (let [result (async/<! (files/list))]
      (eywa/info "Files" {:count (count result)})))
  
  (Thread/sleep 2000) ; Wait for go block to complete
  (eywa/close-task eywa/SUCCESS))
```

### Pattern 5: Error Handling

```clojure
(async/go
  (try
    (let [result (async/<! (files/upload "data.csv" {:name "data.csv"}))]
      (if (instance? Exception result)
        (throw result)  ; Re-throw for catch block
        (eywa/info "Success")))
    (catch Exception e
      (eywa/error "Operation failed" {:error (.getMessage e)})
      (eywa/close-task eywa/ERROR))))
```

---

## Advanced Patterns

### Batch File Operations

```clojure
;; Upload multiple files to same folder
(defn upload-batch [files folder-uuid]
  (async/go
    (doseq [file files]
      (let [result (async/<! (files/upload (:path file)
                                           {:name (:name file)
                                            :folder {:euuid folder-uuid}}))]
        (if (instance? Exception result)
          (eywa/error "Upload failed" {:file (:name file)
                                       :error (.getMessage result)})
          (eywa/info "Uploaded" {:file (:name file)}))))))

;; Use it
(upload-batch [{:path "data/file1.csv" :name "file1.csv"}
               {:path "data/file2.csv" :name "file2.csv"}
               {:path "data/file3.csv" :name "file3.csv"}]
              folder-uuid)
```

### Conditional File Upload

```clojure
;; Upload only if file doesn't exist or is outdated
(defn upload-if-needed [filepath file-data]
  (async/go
    (let [existing (async/<! (files/info (:euuid file-data)))]
      (if existing
        (let [local-size (.length (io/file filepath))
              remote-size (:size existing)]
          (if (= local-size remote-size)
            (eywa/info "File unchanged, skipping upload" {:name (:name file-data)})
            (do
              (eywa/info "File modified, re-uploading" {:name (:name file-data)})
              (async/<! (files/upload filepath file-data)))))
        (do
          (eywa/info "New file, uploading" {:name (:name file-data)})
          (async/<! (files/upload filepath file-data)))))))
```

### Retry with Exponential Backoff

```clojure
;; Retry failed uploads with exponential backoff
(defn upload-with-retry [filepath file-data & {:keys [max-retries]
                                               :or {max-retries 3}}]
  (async/go
    (loop [attempt 1]
      (let [result (async/<! (files/upload filepath file-data))]
        (if (instance? Exception result)
          (if (< attempt max-retries)
            (do
              (eywa/warn "Upload failed, retrying" 
                        {:attempt attempt
                         :error (.getMessage result)})
              (async/<! (async/timeout (* 1000 (Math/pow 2 attempt)))) ; Exponential backoff
              (recur (inc attempt)))
            (do
              (eywa/error "Upload failed after retries" 
                         {:attempts attempt})
              result))
          (do
            (eywa/info "Upload successful" {:attempts attempt})
            result))))))
```

### Parallel Uploads

```clojure
;; Upload multiple files in parallel (be careful with rate limits)
(defn parallel-upload [files folder-uuid]
  (async/go
    (let [channels (mapv (fn [file]
                          (files/upload (:path file)
                                       {:name (:name file)
                                        :folder {:euuid folder-uuid}}))
                        files)
          results (async/<! (async/map vector channels))]
      
      (doseq [[file result] (map vector files results)]
        (if (instance? Exception result)
          (eywa/error "Failed" {:file (:name file)})
          (eywa/info "Success" {:file (:name file)}))))))
```

### Directory Synchronization

```clojure
;; Sync local directory to EYWA folder
(defn sync-directory [local-dir eywa-folder-uuid]
  (async/go
    (let [local-files (file-seq (io/file local-dir))
          files-only (filter #(.isFile %) local-files)]
      
      (eywa/info "Syncing directory" {:files (count files-only)})
      
      (doseq [file files-only]
        (let [file-uuid (java.util.UUID/randomUUID)]
          (async/<! (files/upload (.getPath file)
                                  {:name (.getName file)
                                   :euuid file-uuid
                                   :folder {:euuid eywa-folder-uuid}}))
          (eywa/info "Synced" {:file (.getName file)})))
      
      (eywa/info "Sync complete"))))
```

---

## Examples

The `examples/` directory contains comprehensive working examples demonstrating all client capabilities.

### Prerequisites

First, connect to your EYWA instance:

```bash
eywa connect http://localhost:8080
# Complete the device code flow authentication
```

### Available Examples

#### 1. Simple Robot (`simple_robot.clj`)
Basic robot demonstrating logging, task management, and GraphQL queries.

```bash
eywa run -c "bb examples/simple_robot.clj"
```

**Demonstrates:**
- Client initialization with `(eywa/start)`
- Task context detection and handling
- Logging at different levels
- GraphQL query execution
- Task status updates and completion

---

#### 2. GraphQL Examples (`graphql_examples.clj`)
Comprehensive GraphQL query and mutation examples.

```bash
eywa run -c "bb examples/graphql_examples.clj"
```

**Demonstrates:**
- Simple queries (new API style)
- Queries with variables
- Old API compatibility
- Mutation execution
- Error handling

---

#### 3. File Operations Demo (`file_operations_simple.clj`)
Complete file and folder management workflow with pre-generated UUIDs.

```bash
eywa run -c "bb examples/file_operations_simple.clj"
```

**Demonstrates:**
- Creating folder hierarchies
- Uploading files to specific folders
- Uploading content from memory (strings)
- Downloading files
- Listing files and folders
- Replacing/updating file content
- Using pre-generated UUIDs for deterministic operations

**Key Pattern:** This example uses **pre-generated UUIDs** to create reproducible test data. This is useful for testing, cleanup scripts, and ensuring idempotent operations.

---

#### 4. Cleanup Script (`file_operations_cleanup.clj`)
Removes all test files and folders created by `file_operations_simple.clj`.

```bash
eywa run -c "bb examples/file_operations_cleanup.clj"
```

**Use this to:**
- Clean up after testing
- Learn how to delete files and folders
- Reset your environment between test runs

---

#### 5. New Features Demo (`new_features_demo.clj`)
Showcases all logging levels, reporting, and task management features.

```bash
eywa run -c "bb examples/new_features_demo.clj"
```

**Demonstrates:**
- All logging levels (info, warn, error, debug, trace, exception)
- Custom log entries with duration and coordinates
- Report function with data and images
- Task status constants
- Multiple GraphQL API styles

---

### Example Workflow

```bash
# 1. Connect to EYWA
eywa connect http://localhost:8080

# 2. Run the file operations example
eywa run -c "bb examples/file_operations_simple.clj"

# 3. Explore what was created (view files in EYWA UI or query via GraphQL)

# 4. Clean up test data
eywa run -c "bb examples/file_operations_cleanup.clj"

# 5. Test other features
eywa run -c "bb examples/graphql_examples.clj"
eywa run -c "bb examples/new_features_demo.clj"
```

---

## Testing

### Local Testing Workflow

```bash
# 1. Connect to EYWA instance
eywa connect http://localhost:8080

# 2. Test your robot
eywa run -c "bb my-robot.clj"

# 3. Test with specific task context (if you have task JSON)
eywa run -c "bb my-robot.clj" --task task.json
```

### Testing Examples

Run all examples to verify your EYWA setup:

```bash
# Test basic functionality
eywa run -c "bb examples/simple_robot.clj"

# Test GraphQL integration
eywa run -c "bb examples/graphql_examples.clj"

# Test file operations (creates test data)
eywa run -c "bb examples/file_operations_simple.clj"

# Clean up test data
eywa run -c "bb examples/file_operations_cleanup.clj"

# Test all features
eywa run -c "bb examples/new_features_demo.clj"
```

### Unit Testing

For unit tests of the client library itself:

```bash
# Run client library tests
bb test
# or
clojure -M:test
```

---

## Common Mistakes to Avoid

### ❌ Mistake 1: Not Waiting for Async Results

```clojure
;; ❌ WRONG - doesn't wait for upload
(async/go
  (files/upload "file.txt" {:name "file.txt"})
  (eywa/info "Upload complete")) ; Runs immediately!

;; ✅ CORRECT - wait for upload
(async/go
  (async/<! (files/upload "file.txt" {:name "file.txt"}))
  (eywa/info "Upload complete")) ; Runs after upload
```

### ❌ Mistake 2: Ignoring Exceptions

```clojure
;; ❌ WRONG - doesn't check for errors
(async/go
  (async/<! (files/upload "file.txt" {:name "file.txt"}))
  (eywa/info "Success")) ; What if upload failed?

;; ✅ CORRECT - always check results
(async/go
  (let [result (async/<! (files/upload "file.txt" {:name "file.txt"}))]
    (if (instance? Exception result)
      (eywa/error "Upload failed" {:error (.getMessage result)})
      (eywa/info "Upload successful"))))
```

### ❌ Mistake 3: Deleting Folders Before Files

```clojure
;; ❌ WRONG - deletes folder before files (will fail)
(async/<!! (files/delete-folder folder-uuid))
(async/<!! (files/delete file-uuid))

;; ✅ CORRECT - delete files first, then folders
(async/<!! (files/delete file-uuid))
(async/<!! (files/delete-folder folder-uuid))
```

### ❌ Mistake 4: Not Closing Streams

```clojure
;; ❌ WRONG - stream leak
(async/go
  (let [result (async/<! (files/download-stream file-uuid))
        stream (:stream result)]
    (process-stream stream))) ; Stream not closed!

;; ✅ CORRECT - always close streams
(async/go
  (let [result (async/<! (files/download-stream file-uuid))]
    (with-open [stream (:stream result)]
      (process-stream stream)))) ; Automatically closed
```

### ❌ Mistake 5: Using Blocking Operations in go Blocks

```clojure
;; ❌ WRONG - Thread/sleep blocks go thread
(async/go
  (async/<! (files/upload "file.txt" {:name "file.txt"}))
  (Thread/sleep 1000) ; Blocks thread pool!
  (async/<! (files/upload "file2.txt" {:name "file2.txt"})))

;; ✅ CORRECT - use async/timeout
(async/go
  (async/<! (files/upload "file.txt" {:name "file.txt"}))
  (async/<! (async/timeout 1000)) ; Non-blocking
  (async/<! (files/upload "file2.txt" {:name "file2.txt"})))

;; ✅ ALSO CORRECT - use Thread/sleep in -main, not go blocks
(defn -main []
  (async/go
    (async/<! (files/upload "file.txt" {:name "file.txt"})))
  (Thread/sleep 1000) ; OK in main thread
  (eywa/close-task eywa/SUCCESS))
```

### ❌ Mistake 6: Forgetting File Size for Streams

```clojure
;; ❌ WRONG - missing required :size parameter
(with-open [stream (io/input-stream "file.txt")]
  (async/<! (files/upload-stream stream {:name "file.txt"})))

;; ✅ CORRECT - always provide size for streams
(let [file (io/file "file.txt")
      size (.length file)]
  (with-open [stream (io/input-stream file)]
    (async/<! (files/upload-stream stream {:name "file.txt" 
                                           :size size}))))
```

---

## Troubleshooting

### Common Issues

#### "No response received" or timeout errors

```clojure
;; Ensure you're using async operations correctly
;; ❌ Wrong - missing async/<! 
(let [result (eywa/graphql "{ searchUser { name } }")])

;; ✅ Correct - wait for channel
(async/go
  (let [result (async/<! (eywa/graphql "{ searchUser { name } }"))]
    ...))
```

#### File upload returns nil but file isn't in EYWA

```clojure
;; Uploads return nil on success - verify with files/info or files/list
(async/go
  (async/<! (files/upload "data.csv" {:name "data.csv" :euuid file-uuid}))
  
  ;; Verify upload succeeded
  (let [info (async/<! (files/info file-uuid))]
    (if info
      (eywa/info "Upload verified" {:size (:size info)})
      (eywa/error "Upload failed - file not found"))))
```

#### GraphQL returns errors

```clojure
;; Check the error message and structure
(async/go
  (let [result (async/<! (eywa/graphql "{ invalid }"))]
    (if (instance? Exception result)
      (do
        (eywa/error "GraphQL error" {:msg (.getMessage result)})
        ;; Check if it's ExceptionInfo with data
        (when (instance? clojure.lang.ExceptionInfo result)
          (eywa/error "Error data" {:data (ex-data result)})))
      (eywa/info "Success" result))))
```

#### Process exits before async operations complete

```clojure
;; Add Thread/sleep to keep main thread alive
(defn -main []
  (eywa/start)
  
  (async/go
    (let [result (async/<! (files/list))]
      (eywa/info "Files" {:count (count result)})))
  
  ;; ✅ Wait for async operations
  (Thread/sleep 2000)
  
  (eywa/close-task eywa/SUCCESS))
```

#### Authentication issues with eywa run

```bash
# Ensure you're connected first
eywa connect http://localhost:8080

# Verify connection status
eywa status

# If disconnected, reconnect
eywa connect http://localhost:8080
```

### Debug Logging

```clojure
;; Use debug and trace for detailed logging
(eywa/debug "Operation details" {:file-uuid file-uuid
                                 :folder folder-structure})

(eywa/trace "Raw data" {:response raw-response})
```

### Getting Help

- Check the [examples directory](examples/) for working code
- Review [API Reference](#api-reference) for correct function signatures
- Visit the [EYWA repository](https://github.com/neyho/eywa) for issues
- Ensure client library version matches EYWA server version

---

## Quick Reference

### Essential Commands

```bash
# Connect to EYWA
eywa connect http://localhost:8080

# Run a robot
eywa run -c "bb my-robot.clj"

# Run examples
eywa run -c "bb examples/file_operations_simple.clj"
eywa run -c "bb examples/file_operations_cleanup.clj"
```

### Common Code Patterns

```clojure
;; Initialize
(require '[eywa.client :as eywa]
         '[eywa.files :as files]
         '[clojure.core.async :as async])
(eywa/start)

;; Logging
(eywa/info "message" {:data "value"})

;; GraphQL
(async/go
  (let [r (async/<! (eywa/graphql "{ searchUser { name } }"))]
    (eywa/info "Result" r)))

;; File upload
(async/go
  (async/<! (files/upload "file.csv" {:name "file.csv"
                                      :folder {:euuid folder-id}})))

;; Task management
(eywa/update-task eywa/PROCESSING)
(eywa/close-task eywa/SUCCESS)
```

### Important Constants

```clojure
;; Task status
eywa/SUCCESS
eywa/ERROR
eywa/PROCESSING
eywa/EXCEPTION

;; File system
files/root-euuid     ; Root folder UUID
files/root-folder    ; {:euuid root-euuid}
```

---

## Thread Safety

All operations are thread-safe:
- Request/response tracking uses atoms
- Handler registration is synchronized
- Channel operations are inherently thread-safe

---

## Dependencies

- Clojure 1.10+
- core.async
- babashka.http-client (for file operations)
- cheshire (JSON parsing)
- camel-snake-kebab (case conversion)

---

## License

MIT

---

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## Support

For issues and questions:
- Visit the [EYWA repository](https://github.com/neyho/eywa)
- Check the [examples directory](examples/) for working code
- Review the [EYWA documentation](https://neyho.github.io/eywa/docs/overview)
