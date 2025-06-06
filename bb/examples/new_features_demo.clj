#!/usr/bin/env bb

(require '[babashka.deps :as deps])
(deps/add-classpath "src")

(require '[eywa.client :as eywa]
         '[clojure.core.async :as async])

(defn demonstrate-logging []
  (eywa/info "=== Demonstrating all logging levels ===")

  ;; All logging levels
  (eywa/info "Info level message" {:data "info"})
  (eywa/warn "Warning level message" {:data "warn"})
  (eywa/error "Error level message (not real)" {:data "error"})
  (eywa/debug "Debug level message" {:data "debug"})
  (eywa/trace "Trace level message" {:data "trace"})
  (eywa/exception "Exception level message" {:data "exception"}) ; NEW!

  ;; Custom log with all parameters
  (eywa/log "INFO" "Custom log entry"
            :data {:custom true}
            :duration 1500
            :coordinates {:lat 45.5017 :lon -73.5673}
            :time (java.time.LocalDateTime/now)))

(defn demonstrate-reporting []
  (eywa/info "=== Demonstrating report function ===")

  ;; Simple report
  (eywa/report "Processing started") ; NEW!

  ;; Report with data
  (eywa/report "Progress update"
               :data {:completed 25 :total 100})

  ;; Report with image (base64 or URL)
  (eywa/report "Chart generated"
               :data {:type "bar-chart"}
               :image "data:image/png;base64,iVBORw0KG..."))

(defn demonstrate-task-management []
  (eywa/info "=== Demonstrating task management ===")

  ;; Show constants
  (eywa/info "Task status constants" {:SUCCESS eywa/SUCCESS ; NEW!
                                      :ERROR eywa/ERROR ; NEW!
                                      :PROCESSING eywa/PROCESSING ; NEW!
                                      :EXCEPTION eywa/EXCEPTION}) ; NEW!

  ;; Get task (NEW!)
  (async/go
    (let [task-result (async/<! (eywa/get-task))]
      (if (:error task-result)
        (eywa/warn "No task context" {:error (:error task-result)})
        (eywa/info "Current task" {:task (:result task-result)}))))

  ;; Update task status
  (eywa/update-task eywa/PROCESSING))

(defn demonstrate-graphql []
  (eywa/info "=== Demonstrating GraphQL APIs ===")

  ;; New API - query only
  (async/go
    (let [result (async/<! (eywa/graphql "{ searchUser(_limit: 1) { name } }"))]
      (eywa/info "GraphQL (new API, query only)" {:result result})))

  ;; New API - query with variables
  (async/go
    (let [result (async/<! (eywa/graphql
                            "query($limit: Int) { searchUser(_limit: $limit) { name } }"
                            {:limit 2}))]
      (eywa/info "GraphQL (new API, with vars)" {:result result})))

  ;; Old API - still works
  (async/go
    (let [result (async/<! (eywa/graphql
                            {:query "{ searchUserGroup { name type } }"}))]
      (eywa/info "GraphQL (old API)" {:result result}))))

(defn -main []
  ;; Start using either function
  (eywa/open-pipe) ; NEW alias! Same as (eywa/start)

  (eywa/info "EYWA Babashka Client - New Features Demo")

  (demonstrate-logging)
  (Thread/sleep 500)

  (demonstrate-reporting)
  (Thread/sleep 500)

  (demonstrate-task-management)
  (Thread/sleep 1000)

  (demonstrate-graphql)
  (Thread/sleep 2000)

  (eywa/info "Demo completed!")
  (eywa/close-task eywa/SUCCESS))

(-main)
