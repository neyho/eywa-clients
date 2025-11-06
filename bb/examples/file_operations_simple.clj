#!/usr/bin/env bb

;; EYWA File Operations - Simple Examples
;; Run with: eywa run -c 'bb examples/file_operations_simple.clj'

(require '[eywa.client :as client]
         '[eywa.files :as files]
         '[clojure.core.async :as async])

(import '[java.util UUID])

;; Start the EYWA client (handles stdin/stdout RPC)
(client/start)
(Thread/sleep 500) ; Give client time to initialize

(println "\n=== EYWA File Operations - Simple Examples ===\n")

;; Helper to run async operations
(defn run! [label async-chan]
  (println (str "\n" label))
  (let [result (async/<!! async-chan)]
    (if (instance? Exception result)
      (do
        (println "❌ FAILED:" (.getMessage result))
        (throw result))
      (do
        (println "✅ SUCCESS")
        result))))

;; ============================================
;; TEST DATA - Pre-generated UUIDs and structure
;; ============================================

(def root-euuid #uuid "87ce50d8-5dfa-4008-a265-053e727ab793")
(def demo-euuid #uuid "9bd6fe99-7540-4a54-9998-138405ea8d2c")
(def reports-euuid #uuid "4e2dfc2f-d46e-499a-b008-2104b9214aa1")
(def archive-euuid #uuid "16e65f24-b051-4fe6-8171-058747ec6241")

(def test-data
  {:folders
   {:root {:euuid demo-euuid
           :name "demo-files"
           :parent {:euuid root-euuid}}
    :reports {:euuid reports-euuid
              :name "reports"
              :parent {:euuid demo-euuid}}
    :archive {:euuid archive-euuid
              :name "archive"
              :parent {:euuid reports-euuid}}}
   :files
   {:sample-root {:euuid (str #uuid "3f0f4173-4ef7-4499-857e-37568adeab48")
                  :name "sample.txt"
                  :folder nil
                  :source "examples/resources/sample.txt"}
    :report-data {:euuid #uuid "ea0fee9a-30d9-4aae-b087-10bce969af57"
                  :name "report-data.json"
                  :folder {:euuid reports-euuid}
                  :source "examples/resources/test-data.json"}
    :report-txt {:euuid #uuid "25ad4327-926a-4862-b825-978c2249201e"
                 :name "report.txt"
                 :folder {:euuid reports-euuid}
                 :source "examples/resources/sample.txt"}
    :generated {:euuid #uuid "b986f49c-b91b-48fb-b4df-e749f6ca735a"
                :name "generated.txt"
                :folder {:euuid demo-euuid}
                :content "Hello from EYWA!\nGenerated content.\n"}
    :updated {:euuid #uuid "c1b982b3-bd15-4f5e-8eae-71f388e31bab"
              :name "updated.txt"
              :folder {:euuid archive-euuid}
              :content "Initial content"}}})

;; Store resolved paths for verification
(def resolved-data (atom {:folders {}
                          :files {}}))

(println "📋 Test Data Generated:")
(println "\nFolders:")
(doseq [[k v] (:folders test-data)]
  (println (str "  " k ": " (:name v) " -> " (:euuid v))))
(println "\nFiles:")
(doseq [[k v] (:files test-data)]
  (println (str "  " k ": " (:name v) " -> " (:euuid v))))

;; ============================================
;; EXAMPLE 1: Create Folder Structure
;; ============================================

(println "\n\n📁 EXAMPLE 1: Create Folder Structure")

;; Create demo-files folder under system root
(let [folder-def (get-in test-data [:folders :root])]
  (run! (str "Creating folder: " (:name folder-def))
        (files/create-folder folder-def))

  ;; Verify by getting folder info
  (when-let [info (run! "Verifying demo-files folder"
                        (files/get-folder-info (:euuid folder-def)))]
    (swap! resolved-data assoc-in [:folders :root] info)
    (println "  UUID:" (:euuid info))
    (println "  Name:" (:name info))
    (println "  Path:" (:path info))))

;; Create reports subfolder
(let [folder-def (get-in test-data [:folders :reports])]
  (run! (str "Creating folder: " (:name folder-def))
        (files/create-folder folder-def))

  ;; Verify
  (when-let [info (run! "Verifying reports folder"
                        (files/get-folder-info (:euuid folder-def)))]
    (swap! resolved-data assoc-in [:folders :reports] info)
    (println "  UUID:" (:euuid info))
    (println "  Path:" (:path info))
    (println "  Parent:" (get-in info [:parent :name]))))

;; Create archive subfolder
(let [folder-def (get-in test-data [:folders :archive])]
  (run! (str "Creating folder: " (:name folder-def))
        (files/create-folder folder-def))

  ;; Verify
  (when-let [info (run! "Verifying archive folder"
                        (files/get-folder-info (:euuid folder-def)))]
    (swap! resolved-data assoc-in [:folders :archive] info)
    (println "  UUID:" (:euuid info))
    (println "  Path:" (:path info))
    (println "  Parent:" (get-in info [:parent :name]))))

;; ============================================
;; EXAMPLE 2: Upload File to Root
;; ============================================

(println "\n\n📤 EXAMPLE 2: Upload File to Root")

(let [file-def (get-in test-data [:files :sample-root])]
  (run! (str "Uploading " (:name file-def) " to root")
        (files/upload (:source file-def)
                      {:name (:name file-def)
                       :folder {:euuid demo-euuid}
                       :euuid (:euuid file-def)}))

  ;; Verify
  (when-let [info (run! "Verifying uploaded file"
                        (files/info (:euuid file-def)))]
    (swap! resolved-data assoc-in [:files :sample-root] info)
    (println "  UUID:" (:euuid info))
    (println "  Name:" (:name info))
    (println "  Size:" (:size info) "bytes")
    (println "  Path:" (:path info))))

;; ============================================
;; EXAMPLE 3: Upload to Folder by UUID (String)
;; ============================================

(println "\n\n📤 EXAMPLE 3: Upload to Folder by UUID (String)")

(let [file-def (get-in test-data [:files :report-data])
      folder-uuid (get-in test-data [:folders :reports :euuid])]
  (run! (str "Uploading " (:name file-def) " by folder UUID (string)")
        (files/upload (:source file-def)
                      {:name (:name file-def)
                       :euuid (:euuid file-def)
                       :folder {:euuid folder-uuid}}))

  ;; Verify
  (when-let [info (run! "Verifying uploaded file"
                        (files/info (:euuid file-def)))]
    (swap! resolved-data assoc-in [:files :report-data] info)
    (println "  UUID:" (:euuid info))
    (println "  Name:" (:name info))
    (println "  Path:" (:path info))
    (println "  Folder:" (get-in info [:folder :name]))))

;; ============================================
;; EXAMPLE 4: Upload to Folder by UUID (Map)
;; ============================================

(println "\n\n📤 EXAMPLE 4: Upload to Folder by UUID (Map)")

(let [file-def (get-in test-data [:files :report-txt])
      folder-uuid (get-in test-data [:folders :reports :euuid])]
  (run! (str "Uploading " (:name file-def) " by folder UUID (map)")
        (files/upload (:source file-def)
                      {:name (:name file-def)
                       :euuid (:euuid file-def)
                       :folder {:euuid folder-uuid}}))

  ;; Verify
  (when-let [info (run! "Verifying uploaded file"
                        (files/info (:euuid file-def)))]
    (swap! resolved-data assoc-in [:files :report-txt] info)
    (println "  UUID:" (:euuid info))
    (println "  Name:" (:name info))
    (println "  Path:" (:path info))))

;; ============================================
;; EXAMPLE 5: Upload String Content
;; ============================================

(println "\n\n📤 EXAMPLE 5: Upload String Content")

(let [file-def (get-in test-data [:files :generated])
      folder-uuid (get-in test-data [:folders :root :euuid])]
  (run! (str "Uploading generated content as " (:name file-def))
        (files/upload-content (:content file-def)
                              {:name (:name file-def)
                               :euuid (:euuid file-def)
                               :folder {:euuid folder-uuid}}))

  ;; Verify
  (when-let [info (run! "Verifying uploaded content"
                        (files/info (:euuid file-def)))]
    (swap! resolved-data assoc-in [:files :generated] info)
    (println "  UUID:" (:euuid info))
    (println "  Size:" (:size info) "bytes")
    (println "  Path:" (:path info))))

;; ============================================
;; EXAMPLE 6: Replace/Update File Content
;; ============================================

(println "\n\n📤 EXAMPLE 6: Replace File Content")

(let [file-def (get-in test-data [:files :updated])
      folder-uuid (get-in test-data [:folders :archive :euuid])]

  ;; First upload
  (run! (str "Initial upload: " (:name file-def))
        (files/upload-content (:content file-def)
                              {:name (:name file-def)
                               :euuid (:euuid file-def)
                               :folder {:euuid folder-uuid}}))

  ;; Get initial info
  (when-let [info-before (run! "Getting initial file info"
                               (files/info (:euuid file-def)))]
    (println "  Initial size:" (:size info-before) "bytes"))

  ;; Update content
  (let [new-content "UPDATED CONTENT\nThis file has been replaced!\nNew timestamp: "
        new-content (str new-content (java.time.Instant/now))]
    (run! "Replacing file content with new data"
          (files/upload-content new-content
                                {:name (:name file-def)
                                 :euuid (:euuid file-def)}))

    ;; Verify update
    (when-let [info-after (run! "Verifying updated file"
                                (files/info (:euuid file-def)))]
      (swap! resolved-data assoc-in [:files :updated] info-after)
      (println "  Updated size:" (:size info-after) "bytes")
      (println "  Same UUID:" (= (:euuid file-def) (:euuid info-after))))))

;; ============================================
;; EXAMPLE 7: List Files in Folder
;; ============================================

(println "\n\n📋 EXAMPLE 7: List Files in Folder")

(let [reports-uuid (get-in test-data [:folders :reports :euuid])]
  (when-let [files-list (run! "Listing files in reports folder"
                              (files/list :folder-uuid reports-uuid))]
    (println "  Found" (count files-list) "file(s):")
    (doseq [f files-list]
      (println (str "    - " (:name f) " (" (:size f) " bytes)")))))

;; ============================================
;; EXAMPLE 8: Download and Verify Content
;; ============================================

(println "\n\n📥 EXAMPLE 8: Download File")

(let [file-uuid (get-in test-data [:files :generated :euuid])]
  (when-let [content (run! "Downloading generated.txt"
                           (files/download file-uuid))]
    (let [text (String. content "UTF-8")]
      (println "  Downloaded" (count content) "bytes")
      (println "  Content:" text))))

;; ============================================
;; EXAMPLE 9: List All Folders
;; ============================================

(println "\n\n📋 EXAMPLE 9: List All Demo Folders")

(when-let [folders (run! "Listing folders"
                         (files/list-folders :name-pattern "demo"))]
  (println "  Found" (count folders) "folder(s):")
  (doseq [f folders]
    (println (str "    - " (:path f)))))

;; ============================================
;; VERIFICATION SUMMARY
;; ============================================

(println "\n\n✅ VERIFICATION SUMMARY")
(println "======================================")

(println "\n📁 Folders Created:")
(doseq [[k v] (:folders @resolved-data)]
  (println (str "  " k ": " (:path v))))

(println "\n📄 Files Uploaded:")
(doseq [[k v] (:files @resolved-data)]
  (println (str "  " k ": " (:path v) " (" (:size v) " bytes)")))

(println "\n💡 Key Takeaways:")
(println "  • Pre-generate UUIDs and pass complete folder/file definitions")
(println "  • create-folder accepts a map with :name, :euuid, :parent")
(println "  • Folder can be string UUID or {:euuid \"...\"} map")
(println "  • EYWA automatically computes file paths based on folder hierarchy")
(println "  • Uploads return nil on success - verify with info/list")
(println "  • Same UUID can be used to replace/update file content")

(println "\n👋 Demo finished!")
(client/close-task client/SUCCESS)
