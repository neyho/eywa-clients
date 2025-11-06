#!/usr/bin/env bb

;; EYWA File Operations - Cleanup Script
;; Run with: eywa run -c 'bb examples/file_operations_cleanup.clj'
;;
;; This script deletes all test files and folders created by file_operations_simple.clj

(require '[eywa.client :as client]
         '[eywa.files :as files]
         '[clojure.core.async :as async])

(import '[java.util UUID])

;; Start the EYWA client (handles stdin/stdout RPC)
(client/start)
(Thread/sleep 500) ; Give client time to initialize

(println "\n=== EYWA File Operations - Cleanup ===\n")

;; Helper to run async operations
(defn run! [label async-chan]
  (println (str "\n" label))
  (let [result (async/<!! async-chan)]
    (if (instance? Exception result)
      (do
        (println "❌ FAILED:" (.getMessage result))
        nil) ; Continue on error
      (do
        (println "✅ SUCCESS")
        result))))

;; ============================================
;; TEST DATA - Same UUIDs from file_operations_simple.clj
;; ============================================

(def demo-euuid #uuid "9bd6fe99-7540-4a54-9998-138405ea8d2c")
(def reports-euuid #uuid "4e2dfc2f-d46e-499a-b008-2104b9214aa1")
(def archive-euuid #uuid "16e65f24-b051-4fe6-8171-058747ec6241")

(def test-data
  {:files
   [#uuid "3f0f4173-4ef7-4499-857e-37568adeab48" ; sample-root
    #uuid "ea0fee9a-30d9-4aae-b087-10bce969af57" ; report-data
    #uuid "25ad4327-926a-4862-b825-978c2249201e" ; report-txt
    #uuid "b986f49c-b91b-48fb-b4df-e749f6ca735a" ; generated
    #uuid "c1b982b3-bd15-4f5e-8eae-71f388e31bab"] ; updated

   :folders
   [archive-euuid ; Delete deepest first
    reports-euuid
    demo-euuid]})

(println "📋 Cleanup Plan:")
(println "\nFiles to delete:" (count (:files test-data)))
(doseq [file-uuid (:files test-data)]
  (println (str "  - " file-uuid)))
(println "\nFolders to delete:" (count (:folders test-data)))
(doseq [folder-uuid (:folders test-data)]
  (println (str "  - " folder-uuid)))

;; ============================================
;; STEP 1: Delete all files
;; ============================================

(println "\n\n🗑️  STEP 1: Deleting Files")

(doseq [file-uuid (:files test-data)]
  (run! (str "Deleting file: " file-uuid)
        (files/delete file-uuid)))

;; ============================================
;; STEP 2: Delete all folders (deepest first)
;; ============================================

(println "\n\n🗑️  STEP 2: Deleting Folders")

(doseq [folder-uuid (:folders test-data)]
  (run! (str "Deleting folder: " folder-uuid)
        (files/delete-folder folder-uuid)))

;; ============================================
;; VERIFICATION
;; ============================================

(println "\n\n✅ CLEANUP COMPLETE")
(println "======================================")
(println "\n💡 All test files and folders have been deleted.")
(println "   You can now run file_operations_simple.clj again.")

(println "\n👋 Cleanup finished!")
(client/close-task client/SUCCESS)
