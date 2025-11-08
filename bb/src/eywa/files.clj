(ns eywa.files
  "EYWA File Operations for Babashka/Clojure

  This namespace provides comprehensive file upload/download capabilities
  for the EYWA Clojure client, handling the complete file lifecycle:
  - Upload files with automatic URL generation and S3 upload
  - Download files with automatic URL generation
  - List and manage uploaded files with filtering
  - Progress tracking for uploads and downloads"
  (:require
    [babashka.http-client :as http]
    [clojure.core.async :as async :refer [go <!]]
    [clojure.string :as str]
    [eywa.client :as client])
  (:import
    [java.io File FileInputStream FileOutputStream]
    [java.nio.file Files]
    [java.security MessageDigest]))

;; Forward declarations for functions defined later in this namespace
(declare info list download-stream)

;; Exception types for file operations

(defn file-upload-error [msg & {:keys [type code]
                                :or {type :upload-error}}]
  (ex-info (str "File upload error: " msg)
           (cond-> {:type type}
             code (assoc :http-code code))))

(defn file-download-error [msg & {:keys [type code]
                                  :or {type :download-error}}]
  (ex-info (str "File download error: " msg)
           (cond-> {:type type}
             code (assoc :http-code code))))

(defn- mime-type
  "Detect MIME type from file extension"
  [filename]
  (let [ext (-> filename
                (str/split #"\.")
                last
                str/lower-case)]
    (case ext
      "txt" "text/plain"
      "html" "text/html"
      "css" "text/css"
      "js" "application/javascript"
      "json" "application/json"
      "xml" "application/xml"
      "pdf" "application/pdf"
      "png" "image/png"
      "jpg" "image/jpeg"
      "jpeg" "image/jpeg"
      "gif" "image/gif"
      "svg" "image/svg+xml"
      "zip" "application/zip"
      "csv" "text/csv"
      "doc" "application/msword"
      "docx" "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
      "xls" "application/vnd.ms-excel"
      "xlsx" "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
      "ppt" "application/vnd.ms-powerpoint"
      "pptx" "application/vnd.openxmlformats-officedocument.presentationml.presentation"
      "application/octet-stream")))

(defn- http-put!
  "Perform HTTP PUT request to upload data using babashka.http-client.
  
  Note: Content-Length is automatically set by the HTTP client."
  [url data content-type progress-fn]
  (try
    (let [content-length (if (string? data)
                           (.length (.getBytes data "UTF-8"))
                           (count data))
          _ (when progress-fn (progress-fn 0 content-length))

          response (http/put url
                             {:headers {"Content-Type" content-type}
                              :body data
                              :throw false})

          _ (when progress-fn (progress-fn content-length content-length))]

      (if (= 200 (:status response))
        {:status :success
         :code (:status response)}
        {:status :error
         :code (:status response)
         :message (:body response)}))
    (catch Exception e
      {:status :error
       :code 0
       :message (.getMessage e)})))

(defn- http-put-stream!
  "Perform HTTP PUT request from an InputStream using babashka.http-client.
  
  IMPORTANT: S3 does not support Transfer-Encoding: chunked. This function
  reads the entire stream into memory first to avoid chunked encoding.
  
  For truly large files (>100MB), consider using S3 multipart upload instead.
  
  Args:
    url - URL to PUT to
    input-stream - InputStream to read from (caller must close!)
    content-length - Expected bytes (for progress tracking)
    content-type - MIME type
    progress-fn - Optional function called with (bytes-read, total-bytes)
    opts - Optional map (currently unused)
  
  Returns:
    Map with :status :success or :error, :code response-code"
  [url input-stream content-length content-type progress-fn]
  (try
    (when progress-fn (progress-fn 0 content-length))

    ;; Read entire stream into byte array to avoid chunked transfer encoding
    ;; S3 requires Content-Length and rejects Transfer-Encoding: chunked
    (let [baos (java.io.ByteArrayOutputStream.)
          buffer (byte-array 8192)
          bytes-read (atom 0)]

      ;; Read stream with progress tracking
      (loop []
        (let [n (.read input-stream buffer)]
          (when (> n 0)
            (.write baos buffer 0 n)
            (swap! bytes-read + n)
            (when progress-fn
              (progress-fn @bytes-read content-length))
            (recur))))

      ;; Now send as byte array (not stream)
      (let [data (.toByteArray baos)
            response (http/put url
                               {:headers {"Content-Type" content-type}
                                :body data
                                :throw false})]

        (if (= 200 (:status response))
          {:status :success
           :code (:status response)}
          {:status :error
           :code (:status response)
           :message (:body response)})))
    (catch Exception e
      {:status :error
       :code 0
       :message (.getMessage e)})))

(defn- http-get-stream!
  "Perform HTTP GET request and return InputStream for streaming using babashka.http-client.
  
  Returns map with:
  - :status :success or :error
  - :stream InputStream (caller must close!)
  - :content-length Long (0 if unknown)
  - :code HTTP response code (on error)
  
  Args:
    url - URL to GET from
    opts - Optional map with :connect-timeout-ms and :read-timeout-ms"
  [url]
  (try
    (let [response (http/get url {:as :stream
                                  :throw false})
          status (:status response)]

      (if (= 200 status)
        (let [headers (:headers response)
              content-length (try
                               (Long/parseLong (get headers "content-length" "0"))
                               (catch Exception _ 0))
              input-stream (:body response)]
          {:status :success
           :stream input-stream
           :content-length content-length})
        {:status :error
         :code status}))
    (catch Exception e
      {:status :error
       :code 0
       :message (.getMessage e)})))

(defn- calculate-hash
  "Calculate hash of file or data"
  [file-or-data algorithm]
  (let [md (MessageDigest/getInstance algorithm)
        data (if (instance? File file-or-data)
               (Files/readAllBytes (.toPath file-or-data))
               (if (string? file-or-data)
                 (.getBytes file-or-data "UTF-8")
                 file-or-data))]
    (.update md data)
    (let [digest (.digest md)]
      (apply str (map #(format "%02x" (bit-and % 0xff)) digest)))))

;; Core file operations

(defn upload
  "Upload a file to EYWA file service using streaming (memory efficient).
  
  Args:
    filepath - Path to the file to upload (string or File)
    file-data - Map matching FileInput GraphQL type:
      :name - Filename (optional, defaults to file basename)
      :euuid - UUID of existing file to replace (optional)
      :folder - Parent folder as {:euuid ...} (optional)
      :content_type - MIME type (optional, auto-detected if not provided)
      :size - File size (optional, auto-calculated)
      :progress-fn - Progress callback (optional, not sent to GraphQL)
  
  Returns:
    Core.async channel that will contain nil on success or exception on failure
    
  Examples:
    ;; Upload new file to root
    (<! (upload \"file.txt\" {:name \"file.txt\"}))
    
    ;; Upload to folder
    (<! (upload \"file.txt\" {:name \"file.txt\" :folder {:euuid folder-id}}))
    
    ;; Replace existing file
    (<! (upload \"file.txt\" {:euuid existing-file-id}))"
  [filepath file-data]
  (go
    (try
      (let [progress-fn (:progress-fn file-data)

            file (if (instance? File filepath) filepath (File. filepath))
            _ (when-not (.exists file)
                (throw (file-upload-error (str "File not found: " filepath))))
            _ (when-not (.isFile file)
                (throw (file-upload-error (str "Path is not a file: " filepath))))

            file-size (.length file)
            file-name (or (:name file-data) (.getName file))
            detected-content-type (or (:content_type file-data) (mime-type file-name))

            ;; Step 1: Request upload URL
            upload-query "mutation RequestUpload($file: FileInput!) { requestUploadURL(file: $file)}"

            ;; Build file input - use file-data directly, just fill in computed values
            file-input (-> file-data
                           (dissoc :progress-fn) ; Remove non-GraphQL field
                           (assoc :name file-name
                                  :content_type detected-content-type
                                  :size file-size))

            {:keys [error]
             :as result} (<! (client/graphql upload-query {:file file-input}))

            _ (when error (throw (file-upload-error (str "Failed to get upload URL: " error))))

            upload-url (get-in result [:data :requestUploadURL])
            _ (when-not upload-url (throw (file-upload-error "No upload URL in response")))

            ;; Step 2: Stream file to S3
            upload-result (with-open [fis (FileInputStream. file)]
                            (http-put-stream! upload-url fis file-size detected-content-type progress-fn))

            _ (when (= :error (:status upload-result))
                (throw (file-upload-error (str "S3 upload failed (" (:code upload-result) "): " (:message upload-result)))))

            ;; Step 3: Confirm upload
            confirm-query "mutation ConfirmUpload($url: String!) {
                          confirmFileUpload(url: $url)
                          }"

            {:keys [error]
             :as result} (<! (client/graphql confirm-query {:url upload-url}))

            _ (when error (throw (file-upload-error (str "Upload confirmation failed: " error))))

            confirmed? (get-in result [:data :confirmFileUpload])
            _ (when-not confirmed? (throw (file-upload-error "Upload confirmation returned false")))]
        nil)

      (catch Exception e e))))

(defn upload-stream
  "Upload data from an InputStream to EYWA file service.
  
  This allows uploading from any source that provides an InputStream.
  Useful for piping data, network streams, or generated content.
  
  Args:
    input-stream - InputStream to upload from (will be closed by this function)
    file-data - Map matching FileInput GraphQL type:
      :name - Filename (required)
      :size - Content length in bytes (required)
      :euuid - UUID of existing file to replace (optional)
      :folder - Parent folder as {:euuid ...} (optional)
      :content_type - MIME type (optional, default: application/octet-stream)
      :progress-fn - Progress callback (optional, not sent to GraphQL)
  
  Returns:
    Core.async channel that will contain nil on success or exception on failure
  
  Examples:
    ;; Upload new file from stream to root
    (with-open [is (io/input-stream source)]
      (<! (upload-stream is {:name \"data.bin\" :size file-size})))
    
    ;; Upload to folder
    (with-open [is (io/input-stream source)]
      (<! (upload-stream is {:name \"data.bin\" :size file-size :folder {:euuid folder-id}})))
    
    ;; Replace existing file from stream
    (with-open [is (io/input-stream source)]
      (<! (upload-stream is {:euuid existing-file-id :name \"data.bin\" :size file-size})))"
  [input-stream file-data]
  (go
    (try
      (let [content-type (or (:content_type file-data) "application/octet-stream")
            content-length (:size file-data)
            progress-fn (:progress-fn file-data)

            ;; Step 1: Request upload URL
            upload-query "mutation RequestUpload($file: FileInput!) {
                           requestUploadURL(file: $file)
                         }"

            ;; Build file input - use file-data directly, just fill in defaults
            file-input (-> file-data
                           (dissoc :progress-fn) ; Remove non-GraphQL field
                           (assoc :content_type content-type))

            {:keys [error]
             :as result} (<! (client/graphql upload-query {:file file-input}))

            _ (when error (throw (file-upload-error (str "Failed to get upload URL: " error))))

            upload-url (get-in result [:data :requestUploadURL])
            _ (when-not upload-url (throw (file-upload-error "No upload URL in response")))

            ;; Step 2: Stream to S3
            upload-result (try
                            (http-put-stream! upload-url input-stream content-length content-type progress-fn)
                            (finally
                              (try (.close input-stream) (catch Exception _))))

            _ (when (= :error (:status upload-result))
                (throw (file-upload-error (str "S3 upload failed (" (:code upload-result) "): " (:message upload-result)))))

            ;; Step 3: Confirm upload
            confirm-query "mutation ConfirmUpload($url: String!) {
                            confirmFileUpload(url: $url)
                          }"

            {:keys [error]
             :as result} (<! (client/graphql confirm-query {:url upload-url}))

            _ (when error (throw (file-upload-error (str "Upload confirmation failed: " error))))

            confirmed? (get-in result [:data :confirmFileUpload])
            _ (when-not confirmed? (throw (file-upload-error "Upload confirmation returned false")))]

        nil)

      (catch Exception e e))))

(defn upload-content
  "Upload content directly from memory.
  
  Args:
    content - String or byte array content to upload
    file-data - Map matching FileInput GraphQL type:
      :name - Filename (required)
      :euuid - UUID of existing file to replace (optional)
      :folder - Parent folder as {:euuid ...} (optional)
      :content_type - MIME type (optional, default: text/plain)
      :size - Content size (optional, auto-calculated)
      :progress-fn - Progress callback (optional, not sent to GraphQL)
  
  Returns:
    Core.async channel that will contain nil on success or exception on failure
    
  Examples:
    ;; Upload new content to root
    (<! (upload-content \"Hello World\" {:name \"greeting.txt\"}))
    
    ;; Upload to folder
    (<! (upload-content \"Hello World\" {:name \"greeting.txt\" :folder {:euuid folder-id}}))
    
    ;; Replace existing file content
    (<! (upload-content \"Updated content\" {:name \"greeting.txt\" :euuid existing-file-id}))"
  [content file-data]
  (go
    (try
      (let [content-bytes (if (string? content)
                            (.getBytes content "UTF-8")
                            content)
            file-size (count content-bytes)
            content-type (or (:content_type file-data) "text/plain")
            progress-fn (:progress-fn file-data)

            ;; Step 1: Request upload URL
            upload-query "mutation RequestUpload($file: FileInput!) {
                           requestUploadURL(file: $file)
                         }"

            ;; Build file input - use file-data directly, just fill in computed values
            file-input (-> file-data
                           (dissoc :progress-fn) ; Remove non-GraphQL field
                           (assoc :content_type content-type
                                  :size file-size))

            {:keys [error]
             :as result} (<! (client/graphql upload-query {:file file-input}))

            _ (when error (throw (file-upload-error (str "Failed to get upload URL: " error))))

            upload-url (get-in result [:data :requestUploadURL])

            ;; Step 2: Upload content to S3
            upload-result (http-put! upload-url content-bytes content-type progress-fn)

            _ (when (= :error (:status upload-result))
                (throw (file-upload-error (str "S3 upload failed (" (:code upload-result) "): " (:message upload-result)))))

            ;; Step 3: Confirm upload
            confirm-query "mutation ConfirmUpload($url: String!) {
                            confirmFileUpload(url: $url)
                          }"

            {:keys [error]
             :as result} (<! (client/graphql confirm-query {:url upload-url}))

            _ (when error (throw (file-upload-error (str "Upload confirmation failed: " error))))

            confirmed? (get-in result [:data :confirmFileUpload])
            _ (when-not confirmed? (throw (file-upload-error "Upload confirmation returned false")))]
        nil)

      (catch Exception e e))))

(defn download-stream
  "Download a file from EYWA and return an InputStream for streaming.
  
  This is the recommended approach for large files as it doesn't buffer
  the entire file in memory. The returned InputStream can be used with
  clojure.java.io functions.
  
  Args:
    file-uuid - UUID of the file to download
  
  Returns:
    Core.async channel containing:
    - On success: map with :stream (InputStream - MUST be closed by caller!)
                  and :content-length (bytes, 0 if unknown)
    - On error: Exception
  
  Example:
    (async/go
      (let [result (async/<! (download-stream file-uuid))]
        (if (instance? Exception result)
          (handle-error result)
          (with-open [stream (:stream result)]
            (io/copy stream output-stream)))))"
  [file-uuid]
  (go
    (try
      (let [;; Step 1: Request download URL
            download-query "query RequestDownload($file: FileInput!) {
                             requestDownloadURL(file: $file)
                           }"

            {:keys [error]
             :as result} (<! (client/graphql download-query {:file {:euuid file-uuid}}))

            _ (when error (throw (file-download-error (str "Failed to get download URL: " error))))

            download-url (get-in result [:data :requestDownloadURL])
            _ (when-not download-url (throw (file-download-error "No download URL in response")))

            ;; Step 2: Get InputStream from S3
            stream-result (http-get-stream! download-url)

            _ (when (= :error (:status stream-result))
                (throw (file-download-error (str "Download failed (" (:code stream-result) ")"))))]

        {:stream (:stream stream-result)
         :content-length (:content-length stream-result)})

      (catch Exception e e))))

(defn download
  "Download a file from EYWA file service (convenience function for small files).
  
  For large files, use download-stream instead to avoid buffering entire file in memory.
  
  Args:
    file-uuid - UUID of the file to download
    save-path - Path to save the file (if nil, returns content as bytes)
    progress-fn - Function called with (bytes-downloaded, total-bytes)
  
  Returns:
    Core.async channel that will contain saved path or content bytes, or exception"
  [file-uuid & {:keys [save-path progress-fn]}]
  (go
    (try
      (let [stream-result (<! (download-stream file-uuid))]
        (if (instance? Exception stream-result)
          stream-result
          (let [{:keys [stream content-length]} stream-result]
            (try
              (when progress-fn (progress-fn 0 content-length))

              (if save-path
                ;; Save to file
                (let [file (File. save-path)
                      parent-dir (.getParentFile file)]
                  (when (and parent-dir (not (.exists parent-dir)))
                    (.mkdirs parent-dir))

                  (with-open [is stream
                              fos (FileOutputStream. file)]
                    (let [buffer (byte-array 8192)
                          total-read (atom 0)]
                      (loop []
                        (let [bytes-read (.read is buffer)]
                          (when (> bytes-read 0)
                            (.write fos buffer 0 bytes-read)
                            (swap! total-read + bytes-read)
                            (when (and progress-fn (> content-length 0))
                              (progress-fn @total-read content-length))
                            (recur))))))

                  save-path)

                ;; Return content as bytes
                (let [baos (java.io.ByteArrayOutputStream.)
                      buffer (byte-array 8192)
                      total-read (atom 0)]
                  (with-open [is stream]
                    (loop []
                      (let [bytes-read (.read is buffer)]
                        (when (> bytes-read 0)
                          (.write baos buffer 0 bytes-read)
                          (swap! total-read + bytes-read)
                          (when (and progress-fn (> content-length 0))
                            (progress-fn @total-read content-length))
                          (recur)))))

                  (let [content (.toByteArray baos)]
                    content)))

              (catch Exception e
                (try (.close stream) (catch Exception _))
                (throw e))))))

      (catch Exception e
        e))))

(defn list
  "List files in EYWA file service.
  
  Args:
    filters - Map of filter options:
      :limit - Maximum number of files to return
      :status - Filter by status (PENDING, UPLOADED, etc.)
      :name - Filter by name pattern (SQL LIKE)
      :folder - Folder filter as map:
        {:euuid ...} - Filter by folder UUID
        {:path ...} - Filter by folder path
  
  Returns:
    Core.async channel that will contain list of file maps or exception
    
  Examples:
    ;; List all files
    (<! (list {}))
    
    ;; List files with name pattern
    (<! (list {:name \"test\"}))
    
    ;; List files by folder UUID
    (<! (list {:folder {:euuid folder-uuid}}))
    
    ;; List files by folder path
    (<! (list {:folder {:path \"/documents\"}}))
    
    ;; List files with status filter
    (<! (list {:status \"UPLOADED\"}))
    
    ;; Combined filters
    (<! (list {:limit 10 :name \"test\" :folder {:euuid folder-uuid}}))"
  [filters]
  (go
    (try
      (let [folder-filter (get filters :folder)

            ;; Build folder relationship filter using new approach
            folder-where-clause (when folder-filter
                                  (cond
                                    (:euuid folder-filter)
                                    (format "(_where: {euuid: {_eq: \"%s\"}})" (:euuid folder-filter))

                                    (:path folder-filter)
                                    (format "(_where: {path: {_eq: \"%s\"}})" (:path folder-filter))

                                    :else
                                    (throw (ex-info "Invalid folder filter - must be {:euuid ...} or {:path ...}"
                                                    {:folder folder-filter}))))

            ;; Build query with dynamic folder filtering
            query (format "query ListFiles($limit: Int, $where: searchFileOperator) {
                            searchFile(_limit: $limit, _where: $where, _order_by: {uploaded_at: desc}) {
                              euuid
                              name
                              status
                              content_type
                              size
                              uploaded_at
                              uploaded_by {
                                name
                              }
                              folder%s {
                                euuid
                                name
                                path
                              }
                            }
                          }" (or folder-where-clause ""))

            ;; Build WHERE conditions for file-level filters only
            where-conditions (cond-> []
                               (:status filters)
                               (conj {:status {:_eq (:status filters)}})

                               (:name filters)
                               (conj {:name {:_ilike (str "%" (:name filters) "%")}}))

            variables (cond-> {}
                        (:limit filters) (assoc :limit (:limit filters))
                        (seq where-conditions) (assoc :where
                                                 (if (= 1 (count where-conditions))
                                                   (first where-conditions)
                                                   {:_and where-conditions})))

            {:keys [error]
             :as result} (<! (client/graphql query variables))

            _ (when error (throw (ex-info (str "Failed to list files: " error) {:error error})))

            ;; Handle null result when folder filtering returns no matches
            files (get-in result [:data :searchFile])]

        (if (nil? files) [] files))

      (catch Exception e
        e))))

(defn info
  "Get information about a specific file.
  
  Args:
    file-uuid - UUID of the file
  
  Returns:
    Core.async channel containing:
    - File information map if found
    - nil if file not found (valid case)
    - Exception if error occurred (GraphQL failure, network error, etc.)"
  [file-uuid]
  (go
    (try
      (let [query "query GetFile($uuid: UUID!) {
                     getFile(euuid: $uuid) {
                       euuid
                       name
                       status
                       content_type
                       size
                       uploaded_at
                       uploaded_by {
                         name
                       }
                       folder {
                         euuid
                         name
                       }
                     }
                   }"

            {:keys [error]
             :as result} (<! (client/graphql query {:uuid file-uuid}))]

        (if error
          (ex-info (str "Failed to get file info: " error) {:error error})
          (get-in result [:data :getFile])))

      (catch Exception e e))))

(defn delete
  "Delete a file from EYWA file service.
  
  Args:
    file-uuid - UUID of the file to delete
  
  Returns:
    Core.async channel that will contain true if deletion successful, false otherwise"
  [file-uuid]
  (go
    (try
      (let [query "mutation DeleteFile($uuid: UUID!) {
                     deleteFile(euuid: $uuid)
                   }"

            {:keys [error]
             :as result} (<! (client/graphql query {:uuid file-uuid}))

            _ (when error (throw (ex-info (str "Failed to delete file: " error) {:error error})))

            success? (get-in result [:data :deleteFile])]
        success?)

      (catch Exception e e))))

(defn hash
  "Calculate hash of a file for integrity verification.
  
  Args:
    filepath - Path to the file
    algorithm - Hash algorithm ('MD5', 'SHA-1', 'SHA-256', etc.)
  
  Returns:
    Hex digest of the file hash"
  [filepath & {:keys [algorithm]
               :or {algorithm "SHA-256"}}]
  (calculate-hash (File. filepath) algorithm))

(def root-euuid #uuid "87ce50d8-5dfa-4008-a265-053e727ab793")
(def root-folder {:euuid root-euuid})

;; Folder operations
(defn create-folder
  "Create a new folder in EYWA file service.
  
  Args:
    folder-data - Map matching FolderInput GraphQL type:
      :name - Folder name (required)
      :euuid - Custom UUID for folder (optional, EYWA generates if not provided)
      :parent - Parent folder as {:euuid ...} (optional, nil/missing = root)
  
  Returns:
    Core.async channel containing folder information map or exception
    
  Examples:
    ;; Create folder in root
    (<! (create-folder {:name \"my-folder\"}))
    
    ;; Create folder with parent
    (<! (create-folder {:name \"subfolder\" :parent {:euuid parent-uuid}}))
    
    ;; Create folder with custom UUID and parent
    (<! (create-folder {:name \"my-folder\" :euuid custom-uuid :parent {:euuid parent-uuid}}))"
  [folder-data]
  (go
    (try
      (let [mutation "mutation CreateFolder($folder: FolderInput!) {
                       stackFolder(data: $folder) {
                         euuid
                         name
                         path
                         modified_on
                         parent {
                           euuid
                           name
                         }
                       }
                     }"

            variables {:folder folder-data}

            {:keys [error]
             :as result} (<! (client/graphql mutation variables))

            _ (when error (throw (ex-info (str "Failed to create folder: " error) {:error error})))

            folder (get-in result [:data :stackFolder])]

        folder)

      (catch Exception e
        e))))

(defn list-folders
  "List folders in EYWA file service.
  
  Args:
    filters - Map of filter options:
      :limit - Maximum number of folders to return
      :name - Filter by name pattern (SQL LIKE)
      :parent - Parent folder filter as map:
        {:euuid ...} - Filter by parent UUID
        {:path ...} - Filter by parent path
        nil or missing - Filter for root folders only
  
  Returns:
    Core.async channel containing list of folder maps or exception
    
  Examples:
    ;; List all folders
    (<! (list-folders {}))
    
    ;; List root folders only
    (<! (list-folders {:parent nil}))
    
    ;; List folders with name pattern
    (<! (list-folders {:name \"test\"}))
    
    ;; List folders by parent UUID
    (<! (list-folders {:parent {:euuid parent-uuid}}))
    
    ;; List folders by parent path
    (<! (list-folders {:parent {:path \"/documents\"}}))
    
    ;; Combined filters
    (<! (list-folders {:limit 10 :name \"test\" :parent {:euuid parent-uuid}}))"
  [filters]
  (go
    (try
      (let [query "query ListFolders($limit: Int, $where: searchFolderOperator) {
                     searchFolder(_limit: $limit, _where: $where, _order_by: {name: asc}) {
                       euuid
                       name
                       modified_on
                       parent {
                         euuid
                         name
                       }
                     }
                   }"

            parent-filter (get filters :parent ::not-provided)

            where-conditions (cond-> []
                               (:name filters)
                               (conj {:name {:_ilike (str "%" (:name filters) "%")}})

                               ;; Handle parent filter
                               (not= parent-filter ::not-provided)
                               (conj (cond
                                       ;; nil means root folders only
                                       (nil? parent-filter)
                                       {:parent {:_is_null true}}

                                       ;; Parent by UUID
                                       (:euuid parent-filter)
                                       {:parent {:euuid {:_eq (:euuid parent-filter)}}}

                                       ;; Parent by path
                                       (:path parent-filter)
                                       {:parent {:path {:_eq (:path parent-filter)}}}

                                       :else
                                       (throw (ex-info "Invalid parent filter - must be nil, {:euuid ...}, or {:path ...}"
                                                       {:parent parent-filter})))))

            variables (cond-> {}
                        (:limit filters) (assoc :limit (:limit filters))
                        (seq where-conditions) (assoc :where
                                                 (if (= 1 (count where-conditions))
                                                   (first where-conditions)
                                                   {:_and where-conditions})))

            {:keys [error]
             :as result} (<! (client/graphql query variables))

            _ (when error (throw (ex-info (str "Failed to list folders: " error) {:error error})))

            folders (get-in result [:data :searchFolder])]

        folders)

      (catch Exception e e))))

(defn get-folder-info
  "Get information about a specific folder.
  
  Args:
    data - Map containing either :euuid or :path
  
  Returns:
    Core.async channel containing:
    - Folder information map if found
    - nil if folder not found (valid case)
    - Exception if error occurred (GraphQL failure, network error, etc.)"
  [data]
  (go
    (try
      (let [query (if (contains? data :euuid)
                    "query GetFolder($euuid: UUID!) {
                       getFolder(euuid: $euuid) {
                         euuid
                         name
                         modified_on
                         parent {
                           euuid
                           name
                         }
                       }
                     }"
                    "query GetFolder($path: String!) {
                       getFolder(path: $path) {
                         euuid
                         name
                         modified_on
                         parent {
                           euuid
                           name
                         }
                       }
                     }")

            {:keys [error]
             :as result} (<! (client/graphql query data))]

        (if error
          (ex-info
            "GraphQL query failed!"
            {:query query
             :variables data
             :error error})
          (get-in result [:data :getFolder])))

      (catch Exception e e))))

(defn delete-folder
  "Delete a folder from EYWA file service.
  
  Note: Folder must be empty (no files or subfolders) to be deleted.
  
  Args:
    folder-uuid - UUID of the folder to delete
  
  Returns:
    Core.async channel containing true if deletion successful, false otherwise"
  [folder-uuid]
  (go
    (try
      (let [mutation "mutation DeleteFolder($uuid: UUID!) {
                       deleteFolder(euuid: $uuid)
                     }"

            {:keys [error]
             :as result} (<! (client/graphql mutation {:uuid folder-uuid}))

            _ (when error (throw (ex-info (str "Failed to delete folder: " error) {:error error})))

            success? (get-in result [:data :deleteFolder])]
        success?)

      (catch Exception e e))))

(comment
  (client/open-pipe)

  (async/go
    (println
      (async/<!
        (get-folder-info
          {:path "/"}))))
  (async/go
    (println
      (async/<!
        (get-folder-info
          {:euuid root-euuid}))))
  (async/go
    (def result
      (async/<!
        (upload "examples/resources/sample.txt"
                {:euuid #uuid "210d5d37-1f96-4f1f-aa45-6cd4bed0a3b0"
                 :name "robi_test1"
                 :content-type "txt"}))))
  (delete #uuid "210d5d37-1f96-4f1f-aa45-6cd4bed0a3b0")
  result
  (async/go
    (def result
      (async/<!
        (create-folder
          {:name "TEST_ROBI_2"
           :euuid #uuid "210d5d37-1f96-4f1f-aa45-6cd4bed0a3b0"
           :parent root-folder}))))
  (async/go
    (def result
      (async/<!
        (download #uuid "210d5d37-1f96-4f1f-aa45-6cd4bed0a3b0"))))

  (count result))
