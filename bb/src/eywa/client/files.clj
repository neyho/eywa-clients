(ns eywa.client.files
  "EYWA File Operations for Babashka/Clojure

  This namespace provides comprehensive file upload/download capabilities
  for the EYWA Clojure client, handling the complete file lifecycle:
  - Upload files with automatic URL generation and S3 upload
  - Download files with automatic URL generation
  - List and manage uploaded files with filtering
  - Progress tracking for uploads and downloads"
  (:require
   [clojure.core.async :as async :refer [go <!]]
   [clojure.java.io :as io]
   [clojure.string :as str]
   [clojure.data.json :as json]
   [eywa.client :as client])
  (:import
   [java.io File FileInputStream FileOutputStream]
   [java.net URI URL HttpURLConnection]
   [java.nio.file Files Paths]
   [java.security MessageDigest]
   [java.util UUID]))

;; Exception types for file operations
(defn file-upload-error [msg]
  (ex-info (str "File upload error: " msg) {:type :file-upload-error}))

(defn file-download-error [msg]  
  (ex-info (str "File download error: " msg) {:type :file-download-error}))

;; Utility functions

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
  "Perform HTTP PUT request to upload data"
  [url data content-type progress-fn]
  (let [connection (.openConnection (URL. url))
        _ (.setRequestMethod connection "PUT")
        _ (.setDoOutput connection true)
        _ (.setRequestProperty connection "Content-Type" content-type)
        content-length (if (string? data) (.length (.getBytes data "UTF-8")) (count data))
        _ (.setRequestProperty connection "Content-Length" (str content-length))]
    
    (when progress-fn (progress-fn 0 content-length))
    
    (with-open [out (.getOutputStream connection)]
      (if (string? data)
        (.write out (.getBytes data "UTF-8"))
        (.write out data)))
    
    (when progress-fn (progress-fn content-length content-length))
    
    (let [response-code (.getResponseCode connection)]
      (if (= 200 response-code)
        {:status :success :code response-code}
        (let [error-stream (.getErrorStream connection)
              error-msg (when error-stream 
                         (slurp error-stream))]
          {:status :error :code response-code :message error-msg})))))

(defn- http-get!
  "Perform HTTP GET request to download data"
  [url progress-fn]
  (let [connection (.openConnection (URL. url))
        _ (.setRequestMethod connection "GET")
        response-code (.getResponseCode connection)]
    
    (if (= 200 response-code)
      (let [content-length (try 
                            (Long/parseLong (.getHeaderField connection "Content-Length"))
                            (catch Exception _ 0))
            input-stream (.getInputStream connection)]
        
        (when (and progress-fn (> content-length 0))
          (progress-fn 0 content-length))
        
        (let [baos (java.io.ByteArrayOutputStream.)
              buffer (byte-array 8192)
              total-read (atom 0)]
          
          (loop []
            (let [bytes-read (.read input-stream buffer)]
              (when (> bytes-read 0)
                (.write baos buffer 0 bytes-read)
                (swap! total-read + bytes-read)
                (when (and progress-fn (> content-length 0))
                  (progress-fn @total-read content-length))
                (recur))))
          
          (.close input-stream)
          {:status :success :data (.toByteArray baos)}))
      {:status :error :code response-code})))

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

(defn upload-file
  "Upload a file to EYWA file service.
  
  Args:
    filepath - Path to the file to upload (string or File)
    options - Map of options:
      :name - Custom filename (defaults to file basename)
      :content-type - MIME type (auto-detected if not provided)
      :folder-uuid - UUID of parent folder (optional)
      :progress-fn - Function called with (bytes-uploaded, total-bytes)
  
  Returns:
    Core.async channel that will contain file information map or exception"
  [filepath & {:keys [name content-type folder-uuid progress-fn]}]
  (go
    (try
      (let [file (if (instance? File filepath) filepath (File. filepath))
            _ (when-not (.exists file)
                (throw (file-upload-error (str "File not found: " filepath))))
            _ (when-not (.isFile file)
                (throw (file-upload-error (str "Path is not a file: " filepath))))
            
            file-size (.length file)
            file-name (or name (.getName file))
            detected-content-type (or content-type (mime-type file-name))
            
            _ (client/info (str "Starting upload: " file-name " (" file-size " bytes)"))
            
            ;; Step 1: Request upload URL
            upload-query "mutation RequestUpload($file: FileInput!) {
                           requestUploadURL(file: $file)
                         }"
            
            variables (cond-> {:file {:name file-name
                                     :content_type detected-content-type
                                     :size file-size}}
                        folder-uuid (assoc-in [:file :folder] {:euuid folder-uuid}))
            
            {:keys [error result]} (<! (client/graphql upload-query variables))
            
            _ (when error (throw (file-upload-error (str "Failed to get upload URL: " error))))
            
            upload-url (get-in result [:data :requestUploadURL])
            _ (when-not upload-url (throw (file-upload-error "No upload URL in response")))
            
            _ (client/debug (str "Upload URL received: " (subs upload-url 0 (min 50 (count upload-url))) "..."))
            
            ;; Step 2: Upload file to S3
            file-data (Files/readAllBytes (.toPath file))
            upload-result (http-put! upload-url file-data detected-content-type progress-fn)
            
            _ (when (= :error (:status upload-result))
                (throw (file-upload-error (str "S3 upload failed (" (:code upload-result) "): " (:message upload-result)))))
            
            _ (client/debug "File uploaded to S3 successfully")
            
            ;; Step 3: Confirm upload
            confirm-query "mutation ConfirmUpload($url: String!) {
                            confirmFileUpload(url: $url)
                          }"
            
            {:keys [error result]} (<! (client/graphql confirm-query {:url upload-url}))
            
            _ (when error (throw (file-upload-error (str "Upload confirmation failed: " error))))
            
            confirmed? (get-in result [:data :confirmFileUpload])
            _ (when-not confirmed? (throw (file-upload-error "Upload confirmation returned false")))
            
            _ (client/debug "Upload confirmed")
            
            ;; Step 4: Get file information
            file-info (<! (get-file-by-name file-name))
            _ (when-not file-info (throw (file-upload-error "Could not retrieve uploaded file information")))]
        
        (client/info (str "Upload completed: " file-name " -> " (:euuid file-info)))
        file-info)
      
      (catch Exception e
        (client/error (str "Upload failed: " (.getMessage e)))
        e))))

(defn upload-content
  "Upload content directly from memory.
  
  Args:
    content - String or byte array content to upload
    name - Filename for the content
    options - Map of options:
      :content-type - MIME type (default: 'text/plain')
      :folder-uuid - UUID of parent folder (optional)
      :progress-fn - Function called with (bytes-uploaded, total-bytes)
  
  Returns:
    Core.async channel that will contain file information map or exception"
  [content name & {:keys [content-type folder-uuid progress-fn]
                   :or {content-type "text/plain"}}]
  (go
    (try
      (let [content-bytes (if (string? content)
                           (.getBytes content "UTF-8")
                           content)
            file-size (count content-bytes)
            
            _ (client/info (str "Starting content upload: " name " (" file-size " bytes)"))
            
            ;; Step 1: Request upload URL
            upload-query "mutation RequestUpload($file: FileInput!) {
                           requestUploadURL(file: $file)
                         }"
            
            variables (cond-> {:file {:name name
                                     :content_type content-type
                                     :size file-size}}
                        folder-uuid (assoc-in [:file :folder] {:euuid folder-uuid}))
            
            {:keys [error result]} (<! (client/graphql upload-query variables))
            
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
            
            {:keys [error result]} (<! (client/graphql confirm-query {:url upload-url}))
            
            _ (when error (throw (file-upload-error (str "Upload confirmation failed: " error))))
            
            confirmed? (get-in result [:data :confirmFileUpload])
            _ (when-not confirmed? (throw (file-upload-error "Upload confirmation returned false")))
            
            ;; Step 4: Get file information
            file-info (<! (get-file-by-name name))
            _ (when-not file-info (throw (file-upload-error "Could not retrieve uploaded file information")))]
        
        (client/info (str "Content upload completed: " name " -> " (:euuid file-info)))
        file-info)
      
      (catch Exception e
        (client/error (str "Content upload failed: " (.getMessage e)))
        e))))

(defn download-file
  "Download a file from EYWA file service.
  
  Args:
    file-uuid - UUID of the file to download
    save-path - Path to save the file (if nil, returns content as bytes)
    progress-fn - Function called with (bytes-downloaded, total-bytes)
  
  Returns:
    Core.async channel that will contain saved path or content bytes, or exception"
  [file-uuid & {:keys [save-path progress-fn]}]
  (go
    (try
      (let [_ (client/info (str "Starting download: " file-uuid))
            
            ;; Step 1: Request download URL
            download-query "query RequestDownload($file: FileInput!) {
                             requestDownloadURL(file: $file)
                           }"
            
            {:keys [error result]} (<! (client/graphql download-query {:file {:euuid file-uuid}}))
            
            _ (when error (throw (file-download-error (str "Failed to get download URL: " error))))
            
            download-url (get-in result [:data :requestDownloadURL])
            _ (when-not download-url (throw (file-download-error "No download URL in response")))
            
            _ (client/debug (str "Download URL received: " (subs download-url 0 (min 50 (count download-url))) "..."))
            
            ;; Step 2: Download file
            download-result (http-get! download-url progress-fn)
            
            _ (when (= :error (:status download-result))
                (throw (file-download-error (str "Download failed (" (:code download-result) ")"))))
            
            content (:data download-result)]
        
        (if save-path
          ;; Save to file
          (let [file (File. save-path)
                parent-dir (.getParentFile file)]
            (when (and parent-dir (not (.exists parent-dir)))
              (.mkdirs parent-dir))
            
            (with-open [fos (FileOutputStream. file)]
              (.write fos content))
            
            (client/info (str "Download completed: " file-uuid " -> " save-path))
            save-path)
          
          ;; Return content
          (do
            (client/info (str "Download completed: " file-uuid " (" (count content) " bytes)"))
            content)))
      
      (catch Exception e
        (client/error (str "Download failed: " (.getMessage e)))
        e))))

(defn list-files
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
            
            {:keys [error result]} (<! (client/graphql query variables))
            
            _ (when error (throw (ex-info (str "Failed to list files: " error) {:error error})))
            
            files (get-in result [:data :searchFile])]
        
        (client/debug (str "Found " (count files) " files"))
        files)
      
      (catch Exception e
        (client/error (str "Failed to list files: " (.getMessage e)))
        e))))

(defn get-file-info
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
            
            {:keys [error result]} (<! (client/graphql query {:uuid file-uuid}))]
        
        (if error
          (do
            (client/debug (str "File not found or error: " error))
            nil)
          (get-in result [:data :getFile])))
      
      (catch Exception e
        (client/debug (str "File not found or error: " (.getMessage e)))
        nil))))

(defn get-file-by-name
  "Get file information by name (returns most recent if multiple).
  
  Args:
    name - File name to search for
  
  Returns:
    Core.async channel that will contain file information map or nil if not found"
  [name]
  (go
    (let [files (<! (list-files :limit 1 :name-pattern name))]
      (if (instance? Exception files)
        files
        (first files)))))

(defn delete-file
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
            
            {:keys [error result]} (<! (client/graphql query {:uuid file-uuid}))
            
            _ (when error (throw (ex-info (str "Failed to delete file: " error) {:error error})))
            
            success? (get-in result [:data :deleteFile])]
        
        (if success?
          (client/info (str "File deleted: " file-uuid))
          (client/warn (str "File deletion failed: " file-uuid)))
        
        success?)
      
      (catch Exception e
        (client/error (str "Failed to delete file: " (.getMessage e)))
        false))))

(defn calculate-file-hash
  "Calculate hash of a file for integrity verification.
  
  Args:
    filepath - Path to the file
    algorithm - Hash algorithm ('MD5', 'SHA-1', 'SHA-256', etc.)
  
  Returns:
    Hex digest of the file hash"
  [filepath & {:keys [algorithm] :or {algorithm "SHA-256"}}]
  (calculate-hash (File. filepath) algorithm))

;; Convenience functions

(defn quick-upload
  "Quick upload with minimal parameters.
  
  Args:
    filepath - Path to file to upload
  
  Returns:
    Core.async channel that will contain file UUID or exception"
  [filepath]
  (go
    (let [result (<! (upload-file filepath))]
      (if (instance? Exception result)
        result
        (:euuid result)))))

(defn quick-download
  "Quick download to current directory.
  
  Args:
    file-uuid - UUID of file to download
    filename - Custom filename (auto-detected if not provided)
  
  Returns:
    Core.async channel that will contain path to downloaded file or exception"
  [file-uuid & {:keys [filename]}]
  (go
    (let [final-filename (or filename
                            (let [file-info (<! (get-file-info file-uuid))]
                              (if (instance? Exception file-info)
                                (str "download_" (subs file-uuid 0 8))
                                (:name file-info))))]
      (<! (download-file file-uuid :save-path final-filename)))))

;; Data processing helpers

(defn upload-json
  "Upload JSON data from a Clojure data structure.
  
  Args:
    data - Clojure data to convert to JSON
    filename - Name for the JSON file
    options - Upload options
  
  Returns:
    Core.async channel that will contain file information or exception"
  [data filename & options]
  (let [json-content (json/write-str data)]
    (apply upload-content json-content filename 
           :content-type "application/json" 
           options)))

(defn download-json
  "Download and parse JSON file.
  
  Args:
    file-uuid - UUID of JSON file to download
  
  Returns:
    Core.async channel that will contain parsed Clojure data or exception"
  [file-uuid]
  (go
    (let [content (<! (download-file file-uuid))]
      (if (instance? Exception content)
        content
        (try
          (json/read-str (String. content "UTF-8") :key-fn keyword)
          (catch Exception e
            (ex-info "Failed to parse JSON content" {:content content :error e})))))))

(defn upload-edn
  "Upload EDN data from a Clojure data structure.
  
  Args:
    data - Clojure data to convert to EDN
    filename - Name for the EDN file
    options - Upload options
  
  Returns:
    Core.async channel that will contain file information or exception"
  [data filename & options]
  (let [edn-content (pr-str data)]
    (apply upload-content edn-content filename 
           :content-type "application/edn" 
           options)))

(defn download-edn
  "Download and parse EDN file.
  
  Args:
    file-uuid - UUID of EDN file to download
  
  Returns:
    Core.async channel that will contain parsed Clojure data or exception"
  [file-uuid]
  (go
    (let [content (<! (download-file file-uuid))]
      (if (instance? Exception content)
        content
        (try
          (read-string (String. content "UTF-8"))
          (catch Exception e
            (ex-info "Failed to parse EDN content" {:content content :error e})))))))
