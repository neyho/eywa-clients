#!/usr/bin/env bb

(require '[babashka.deps :as deps])
(deps/add-classpath "src")

(require '[eywa.client :as eywa]
         '[clojure.core.async :as async])

(defn -main []
  ;; Start the EYWA client
  (eywa/open-pipe) ; or (eywa/start)

  (try
    ;; Simple info logging
    (eywa/info "Robot started")

    ;; Get current task (if running in task context)
    (async/go
      (let [task-result (async/<! (eywa/get-task))]
        (if-let [task (:result task-result)]
          (do
            (eywa/info "Processing task" {:task-id (:euuid task)})
            ;; Update status
            (eywa/update-task eywa/PROCESSING)

            ;; Do some work...
            (Thread/sleep 1000)

            ;; Report progress
            (eywa/report "Processed 50%" :data {:progress 0.5})

            ;; Do more work...
            (Thread/sleep 1000)

            ;; Success!
            (eywa/report "Task completed" :data {:items-processed 100})
            (eywa/close-task eywa/SUCCESS))
          (do
            ;; Not in task context, just demonstrate features
            (eywa/warn "No task context, running demo mode")

            ;; Show GraphQL usage
            (let [result (async/<! (eywa/graphql "{ searchUser(_limit: 2) { name type } }"))]
              (if (instance? Exception result)
                (eywa/error "GraphQL failed" {:error (.getMessage result)})
                (eywa/info "Found users" {:users (get-in result ["data" "searchUser"])})))

            (eywa/close-task eywa/SUCCESS)))))

    ;; Keep the process alive for async operations
    (Thread/sleep 3000)

    (catch Exception e
      (eywa/exception "Unhandled error" {:error (.getMessage e)
                                         :type (type e)})
      (eywa/close-task eywa/ERROR))))

(-main)
