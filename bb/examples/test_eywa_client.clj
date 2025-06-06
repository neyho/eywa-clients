#!/usr/bin/env bb

(require '[babashka.deps :as deps])

;; Add the local source to classpath
(deps/add-classpath "src")

(require '[eywa.client :as eywa]
         '[clojure.core.async :as async])

(defn test-client []
  (println "Starting EYWA Babashka client test...\n")

  ;; Initialize the pipe
  (eywa/start) ; or (eywa/open-pipe)

  (try
    ;; Test 1: Logging functions
    (eywa/info "Testing info logging" {:test "info"})
    (eywa/warn "Testing warning logging" {:test "warn"})
    (eywa/error "Testing error logging (not a real error)" {:test "error"})
    (eywa/debug "Testing debug logging" {:test "debug"})
    (eywa/trace "Testing trace logging" {:test "trace"})
    (eywa/exception "Testing exception logging" {:test "exception"})

    ;; Test 2: Custom log with all parameters
    (eywa/log "INFO" "Custom log with all parameters"
              :data {:custom true}
              :duration 1234
              :coordinates {:x 10 :y 20})

    ;; Test 3: Report
    (eywa/report "Test report message"
                 :data {:reportData "test"})

    ;; Test 4: Task management
    (eywa/update-task eywa/PROCESSING)
    (eywa/info "Updated task status to PROCESSING")

    ;; Test 5: Get current task
    (let [task-chan (eywa/get-task)]
      (async/go
        (try
          (let [task (async/<! task-chan)]
            (if (:error task)
              (eywa/warn "Could not get task (normal if not in task context)"
                         {:error (:error task)})
              (eywa/info "Retrieved task:" (:result task))))
          (catch Exception e
            (eywa/warn "Task retrieval failed" {:error (.getMessage e)})))))

    ;; Give async operations time to complete
    (Thread/sleep 500)

    ;; Test 6: GraphQL query
    (eywa/info "Testing GraphQL query...")
    (let [gql-chan (eywa/graphql "
      {
        searchUser(_limit: 2) {
          euuid
          name
          type
          active
        }
      }")]
      (async/go
        (try
          (let [result (async/<! gql-chan)]
            (if (instance? Exception result)
              (eywa/error "GraphQL query failed" {:error (.getMessage result)})
              (do
                (eywa/info "GraphQL query successful"
                           {:resultCount (count (get-in result ["data" "searchUser"]))})
                ;; Show first user if available
                (when-let [first-user (first (get-in result ["data" "searchUser"]))]
                  (eywa/info "First user:" first-user)))))
          (catch Exception e
            (eywa/error "GraphQL execution failed" {:error (.getMessage e)})))))

    ;; Give async operations more time
    (Thread/sleep 1000)

    ;; Test 7: Constants
    (eywa/info "Testing constants" {:SUCCESS eywa/SUCCESS
                                    :ERROR eywa/ERROR
                                    :PROCESSING eywa/PROCESSING
                                    :EXCEPTION eywa/EXCEPTION})

    ;; Test 8: New graphql API (with separate args)
    (eywa/info "Testing new GraphQL API...")
    (let [variables {:limit 1}
          query "query($limit: Int) { searchUser(_limit: $limit) { name } }"
          gql-chan2 (eywa/graphql query variables)]
      (async/go
        (try
          (let [result (async/<! gql-chan2)]
            (if (instance? Exception result)
              (eywa/error "GraphQL with variables failed" {:error (.getMessage result)})
              (eywa/info "GraphQL with variables successful" {:result result})))
          (catch Exception e
            (eywa/error "GraphQL with variables execution failed"
                        {:error (.getMessage e)})))))

    ;; Final wait for async operations
    (Thread/sleep 1000)

    ;; Test complete
    (eywa/info "All tests completed successfully!")
    (eywa/close-task eywa/SUCCESS)

    (catch Exception e
      (eywa/error "Test failed with unexpected error"
                  {:error (.getMessage e)
                   :stack (with-out-str (.printStackTrace e))})
      (eywa/close-task eywa/ERROR))))

;; Run the test
(test-client)
