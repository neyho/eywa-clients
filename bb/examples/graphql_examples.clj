#!/usr/bin/env bb

(require '[babashka.deps :as deps])
(deps/add-classpath "src")

(require '[eywa.client :as eywa]
         '[clojure.core.async :as async])

(defn -main []
  (eywa/open-pipe)

  (eywa/info "GraphQL Examples")

  ;; Example 1: Simple query (new API)
  (async/go
    (eywa/info "Example 1: Simple query")
    (let [result (async/<! (eywa/graphql "
      {
        searchUser(_limit: 3) {
          euuid
          name
          type
          active
        }
      }"))]
      (if (instance? Exception result)
        (eywa/error "Query failed" {:error (.getMessage result)})
        (eywa/info "Users found" {:count (count (get-in result ["data" "searchUser"]))}))))

  (Thread/sleep 500)

  ;; Example 2: Query with variables (new API)
  (async/go
    (eywa/info "Example 2: Query with variables")
    (let [query "
      query GetUsers($limit: Int!, $active: Boolean) {
        searchUser(_limit: $limit, _where: {active: {_eq: $active}}) {
          euuid
          name
          active
        }
      }"
          variables {:limit 2 :active true}
          result (async/<! (eywa/graphql query variables))]
      (if (instance? Exception result)
        (eywa/error "Query with variables failed" {:error (.getMessage result)})
        (eywa/info "Active users" {:users (get-in result ["data" "searchUser"])}))))

  (Thread/sleep 500)

  ;; Example 3: Old API style (still supported)
  (async/go
    (eywa/info "Example 3: Old API style")
    (let [result (async/<! (eywa/graphql {:query "{ searchUserRole { name description } }"
                                          :variables nil}))]
      (if (instance? Exception result)
        (eywa/error "Old API query failed" {:error (.getMessage result)})
        (eywa/info "Roles found" {:count (count (get-in result ["data" "searchUserRole"]))}))))

  (Thread/sleep 500)

  ;; Example 4: Mutation
  (async/go
    (eywa/info "Example 4: Mutation example")
    (let [mutation "
      mutation CreateTask($data: TaskInput!) {
        syncTask(data: $data) {
          euuid
          message
          status
        }
      }"
          variables {:data {:message "Test task from Babashka"
                            :status "QUEUED"
                            :type "TEST"}}
          result (async/<! (eywa/graphql mutation variables))]
      (if (instance? Exception result)
        (eywa/error "Mutation failed" {:error (.getMessage result)})
        (eywa/info "Task created" {:task (get-in result ["data" "syncTask"])}))))

  ;; Wait for all async operations
  (Thread/sleep 2000)

  (eywa/info "All examples completed")
  (eywa/close-task eywa/SUCCESS))

(-main)
