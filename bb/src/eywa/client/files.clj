(ns eywa.client.files
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
    opts - Map of options matching FileInput fields:
      :name - Custom filename (defaults to file basename)
      :content-type - MIME type (auto-detected if not provided)
      :folder-uuid - UUID of parent folder (optional)
      :euuid - UUID of existing file to replace (optional)
      :progress-fn - Function called with (bytes-uploaded, total-bytes)
  
  Returns:
    Core.async channel that will contain file information map or exception
    
  Examples:
    ;; Upload new file
    (<! (upload \"file.txt\" {:folder-uuid folder-id}))
    
    ;; Replace existing file
    (<! (upload \"file.txt\" {:euuid existing-file-id}))"
  [filepath opts]
  (go
    (try
      (let [{:keys [name content-type folder-uuid euuid progress-fn]} opts

            file (if (instance? File filepath) filepath (File. filepath))
            _ (when-not (.exists file)
                (throw (file-upload-error (str "File not found: " filepath))))
            _ (when-not (.isFile file)
                (throw (file-upload-error (str "Path is not a file: " filepath))))

            file-size (.length file)
            file-name (or name (.getName file))
            detected-content-type (or content-type (mime-type file-name))

            ;; Step 1: Request upload URL
            upload-query "mutation RequestUpload($file: FileInput!) { requestUploadURL(file: $file)}"

            file-input (cond-> {:name file-name
                                :content_type detected-content-type
                                :size file-size}
                         folder-uuid (assoc :folder {:euuid folder-uuid})
                         euuid (assoc :euuid euuid))

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

      (catch Exception e
        e))))

(defn upload-stream
  "Upload data from an InputStream to EYWA file service.
  
  This allows uploading from any source that provides an InputStream.
  Useful for piping data, network streams, or generated content.
  
  Args:
    input-stream - InputStream to upload from (will be closed by this function)
    file-name - Name for the uploaded file
    content-length - Total bytes to upload (must be known upfront)
    opts - Map of options matching FileInput fields:
      :content-type - MIME type (default: application/octet-stream)
      :folder-uuid - UUID of parent folder (optional)
      :euuid - UUID of existing file to replace (optional)
      :progress-fn - Function called with (bytes-uploaded, total-bytes)
  
  Returns:
    Core.async channel that will contain file information map or exception
  
  Examples:
    ;; Upload new file from stream
    (with-open [is (io/input-stream source)]
      (<! (upload-stream is \"data.bin\" file-size {:content-type \"application/octet-stream\"})))
    
    ;; Replace existing file from stream
    (with-open [is (io/input-stream source)]
      (<! (upload-stream is \"data.bin\" file-size {:euuid existing-file-id})))"
  [input-stream file-name content-length opts]
  (go
    (try
      (let [{:keys [content-type folder-uuid euuid progress-fn]
             :or {content-type "application/octet-stream"}} opts

            _ (client/info (str "Starting stream upload: " file-name " (" content-length " bytes)"))

            ;; Step 1: Request upload URL
            upload-query "mutation RequestUpload($file: FileInput!) {
                           requestUploadURL(file: $file)
                         }"

            file-input (cond-> {:name file-name
                                :content_type content-type
                                :size content-length}
                         folder-uuid (assoc :folder {:euuid folder-uuid})
                         euuid (assoc :euuid euuid))

            {:keys [error]
             :as result} (<! (client/graphql upload-query {:file file-input}))

            _ (when error (throw (file-upload-error (str "Failed to get upload URL: " error))))

            upload-url (get-in result [:data :requestUploadURL])
            _ (when-not upload-url (throw (file-upload-error "No upload URL in response")))

            _ (client/debug (str "Upload URL received: " (subs upload-url 0 (min 50 (count upload-url))) "..."))

            ;; Step 2: Stream to S3
            upload-result (try
                            (http-put-stream! upload-url input-stream content-length content-type progress-fn)
                            (finally
                              (try (.close input-stream) (catch Exception _))))

            _ (when (= :error (:status upload-result))
                (throw (file-upload-error (str "S3 upload failed (" (:code upload-result) "): " (:message upload-result)))))

            _ (client/debug "Stream uploaded to S3 successfully")

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

      (catch Exception e
        (client/error (str "Stream upload failed: " (.getMessage e)))
        e))))

(defn upload-content
  "Upload content directly from memory.
  
  Args:
    content - String or byte array content to upload
    name - Filename for the content
    opts - Map of options matching FileInput fields:
      :content-type - MIME type (default: 'text/plain')
      :folder-uuid - UUID of parent folder (optional)
      :euuid - UUID of existing file to replace (optional)
      :progress-fn - Function called with (bytes-uploaded, total-bytes)
  
  Returns:
    Core.async channel that will contain file information map or exception
    
  Examples:
    ;; Upload new content
    (<! (upload-content \"Hello World\" \"greeting.txt\" {:content-type \"text/plain\"}))
    
    ;; Replace existing file content
    (<! (upload-content \"Updated content\" \"greeting.txt\" {:euuid existing-file-id}))"
  [content name opts]
  (go
    (try
      (let [{:keys [content-type folder-uuid euuid progress-fn]
             :or {content-type "text/plain"}} opts

            content-bytes (if (string? content)
                            (.getBytes content "UTF-8")
                            content)
            file-size (count content-bytes)

            _ (client/info (str "Starting content upload: " name " (" file-size " bytes)"))

            ;; Step 1: Request upload URL
            upload-query "mutation RequestUpload($file: FileInput!) {
                           requestUploadURL(file: $file)
                         }"

            file-input (cond-> {:name name
                                :content_type content-type
                                :size file-size}
                         folder-uuid (assoc :folder {:euuid folder-uuid})
                         euuid (assoc :euuid euuid))

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

      (catch Exception e
        (client/error (str "Content upload failed: " (.getMessage e)))
        e))))

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
      (let [_ (client/info (str "Starting streaming download: " file-uuid))

            ;; Step 1: Request download URL
            download-query "query RequestDownload($file: FileInput!) {
                             requestDownloadURL(file: $file)
                           }"

            {:keys [error]
             :as result} (<! (client/graphql download-query {:file {:euuid file-uuid}}))

            _ (when error (throw (file-download-error (str "Failed to get download URL: " error))))

            download-url (get-in result [:data :requestDownloadURL])
            _ (when-not download-url (throw (file-download-error "No download URL in response")))

            _ (client/debug (str "Download URL received: " (subs download-url 0 (min 50 (count download-url))) "..."))

            ;; Step 2: Get InputStream from S3
            stream-result (http-get-stream! download-url)

            _ (when (= :error (:status stream-result))
                (throw (file-download-error (str "Download failed (" (:code stream-result) ")"))))]

        (client/info (str "Stream ready for: " file-uuid " (" (:content-length stream-result) " bytes)"))
        {:stream (:stream stream-result)
         :content-length (:content-length stream-result)})

      (catch Exception e
        (client/error (str "Stream download failed: " (.getMessage e)))
        e))))

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

                  (client/info (str "Download completed: " file-uuid " -> " save-path))
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
                    (client/info (str "Download completed: " file-uuid " (" (count content) " bytes)"))
                    content)))

              (catch Exception e
                (try (.close stream) (catch Exception _))
                (throw e))))))

      (catch Exception e
        (client/error (str "Download failed: " (.getMessage e)))
        e))))

(defn list
  "List files in EYWA file service.
  
  Args:
    options - Map of filter options:
      :limit - Maximum number of files to return
      :status - Filter by status (PENDING, UPLOADED, etc.)
      :name-pattern - Filter by name pattern (SQL LIKE)
      :folder-uuid - Filter by folder UUID
  
  Returns:
    Core.async channel that will contain list of file maps or exception"
  [& {:keys [limit status name-pattern folder-uuid]}]
  (go
    (try
      (let [_ (client/debug (str "Listing files (limit=" limit ", status=" status ")"))

            query "query ListFiles($limit: Int, $where: FileWhereInput) {
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
                       folder {
                         euuid
                         name
                       }
                     }
                   }"

            where-conditions (cond-> []
                               status (conj {:status {:_eq status}})
                               name-pattern (conj {:name {:_ilike (str "%" name-pattern "%")}})
                               folder-uuid (conj {:folder {:euuid {:_eq folder-uuid}}}))

            variables (cond-> {}
                        limit (assoc :limit limit)
                        (seq where-conditions) (assoc :where
                                                 (if (= 1 (count where-conditions))
                                                   (first where-conditions)
                                                   {:_and where-conditions})))

            {:keys [error]
             :as result} (<! (client/graphql query variables))

            _ (when error (throw (ex-info (str "Failed to list files: " error) {:error error})))

            files (get-in result [:data :searchFile])]

        (client/debug (str "Found " (count files) " files"))
        files)

      (catch Exception e
        (client/error (str "Failed to list files: " (.getMessage e)))
        e))))

(defn info
  "Get information about a specific file.
  
  Args:
    file-uuid - UUID of the file
  
  Returns:
    Core.async channel that will contain file information map or nil if not found"
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
          (do
            (client/debug (str "File not found or error: " error))
            nil)
          (get-in result [:data :getFile])))

      (catch Exception e
        (client/debug (str "File not found or error: " (.getMessage e)))
        nil))))


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

        (if success?
          (client/info (str "File deleted: " file-uuid))
          (client/warn (str "File deletion failed: " file-uuid)))

        success?)

      (catch Exception e
        (client/error (str "Failed to delete file: " (.getMessage e)))
        false))))

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

;; Folder operations

(defn create-folder
  "Create a new folder in EYWA file service.
  
  Args:
    name - Folder name
    parent-folder-uuid - UUID of parent folder (optional, nil for root)
  
  Returns:
    Core.async channel containing folder information map or exception"
  [name & {:keys [parent-folder-uuid]}]
  (go
    (try
      (let [_ (client/info (str "Creating folder: " name))

            mutation "mutation CreateFolder($folder: FolderInput!) {
                       syncFolder(data: $folder) {
                         euuid
                         name
                         created_at
                         parent {
                           euuid
                           name
                         }
                       }
                     }"

            variables (cond-> {:folder {:name name}}
                        parent-folder-uuid (assoc-in [:folder :parent :euuid] parent-folder-uuid))

            {:keys [error]
             :as result} (<! (client/graphql mutation variables))

            _ (when error (throw (ex-info (str "Failed to create folder: " error) {:error error})))

            folder (get-in result [:data :syncFolder])]

        (client/info (str "Folder created: " name " -> " (:euuid folder)))
        folder)

      (catch Exception e
        (client/error (str "Failed to create folder: " (.getMessage e)))
        e))))

(defn list-folders
  "List folders in EYWA file service.
  
  Args:
    options - Map of filter options:
      :limit - Maximum number of folders to return
      :name-pattern - Filter by name pattern (SQL LIKE)
      :parent-folder-uuid - Filter by parent folder UUID (nil for root folders)
  
  Returns:
    Core.async channel containing list of folder maps or exception"
  [& {:keys [limit name-pattern parent-folder-uuid]}]
  (go
    (try
      (let [_ (client/debug (str "Listing folders (limit=" limit ")"))

            query "query ListFolders($limit: Int, $where: FolderWhereInput) {
                     searchFolder(_limit: $limit, _where: $where, _order_by: {name: asc}) {
                       euuid
                       name
                       created_at
                       parent {
                         euuid
                         name
                       }
                     }
                   }"

            where-conditions (cond-> []
                               name-pattern (conj {:name {:_ilike (str "%" name-pattern "%")}})
                               (some? parent-folder-uuid)
                               (conj (if parent-folder-uuid
                                       {:parent {:euuid {:_eq parent-folder-uuid}}}
                                       {:parent {:_is_null true}})))

            variables (cond-> {}
                        limit (assoc :limit limit)
                        (seq where-conditions) (assoc :where
                                                 (if (= 1 (count where-conditions))
                                                   (first where-conditions)
                                                   {:_and where-conditions})))

            {:keys [error]
             :as result} (<! (client/graphql query variables))

            _ (when error (throw (ex-info (str "Failed to list folders: " error) {:error error})))

            folders (get-in result [:data :searchFolder])]

        (client/debug (str "Found " (count folders) " folders"))
        folders)

      (catch Exception e
        (client/error (str "Failed to list folders: " (.getMessage e)))
        e))))

(defn get-folder-info
  "Get information about a specific folder.
  
  Args:
    folder-uuid - UUID of the folder
  
  Returns:
    Core.async channel containing folder information map or nil if not found"
  [folder-uuid]
  (go
    (try
      (let [query "query GetFolder($uuid: UUID!) {
                     getFolder(euuid: $uuid) {
                       euuid
                       name
                       created_at
                       parent {
                         euuid
                         name
                       }
                     }
                   }"

            {:keys [error]
             :as result} (<! (client/graphql query {:uuid folder-uuid}))]

        (if error
          (do
            (client/debug (str "Folder not found or error: " error))
            nil)
          (get-in result [:data :getFolder])))

      (catch Exception e
        (client/debug (str "Folder not found or error: " (.getMessage e)))
        nil))))

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

        (if success?
          (client/info (str "Folder deleted: " folder-uuid))
          (client/warn (str "Folder deletion failed: " folder-uuid)))

        success?)

      (catch Exception e
        (client/error (str "Failed to delete folder: " (.getMessage e)))
        false))))

(comment
  (client/open-pipe)
  (async/go
    (def result
      (async/<!
        (upload "examples/resources/sample.txt"
                {:euuid #uuid "210d5d37-1f96-4f1f-aa45-6cd4bed0a3b0"
                 :name "robi_test1"
                 :content-type "txt"}))))
  result
  (delete #uuid "210d5d37-1f96-4f1f-aa45-6cd4bed0a3b0")
  (async/go
    (def result
      (async/<!
        (download #uuid "210d5d37-1f96-4f1f-aa45-6cd4bed0a3b0"))))

  (count result))
