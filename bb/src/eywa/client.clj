(ns eywa.client
  (:require
   [clojure.pprint :refer [pprint]]
   [clojure.core.async :as async]
   [clojure.java.io :as io]
   [eywa.client.json :refer [->json <-json]]))

(def pending-rpcs (atom {}))
(def handlers (atom {}))

(defn ->out [text]
  (.println System/out text))

(defn ->err [text]
  (.println System/err text))

(comment
  (pprint @pending-rpcs))

;; Function to handle responses
(defn handle-response [data]
  (let [id (:id data)
        callback (get @pending-rpcs id)]
    (if callback
      (do
        (async/put! callback data)
        (swap! pending-rpcs dissoc id))
      (->err (str "RPC callback not registered for request with id = " id)))))

;; Function to handle requests
(defn handle-request [data]
  (let [method (:method data)
        handler (get @handlers method)]
    (if handler
      (handler data)
      (->err (str "Method " method " doesn't have registered handler")))))

;; Function to handle incoming JSON-RPC data
(defn handle-data [data]
  (let [{:keys [method id result error]} data]
    (cond
      method (handle-request data)
      (and result id) (handle-response data)
      (and error id) (handle-response data)
      :else (->err "Received invalid JSON-RPC:" data))))

;; Function to send a request
(defn send-request
  "Will send json rpc request upstream. Promise of result is returned.
  Promise is registered at pending-rpcs."
  [data]
  (let [id (java.util.UUID/randomUUID)
        promise (async/promise-chan)]
    (swap! pending-rpcs assoc id promise)
    (let [request (assoc data :jsonrpc "2.0" :id id)]
      (->out (->json request))
      (flush))
    promise))

;; Function to send a notification
(defn send-notification [data]
  (let [notification (assoc data :jsonrpc "2.0")]
    (->out (->json notification))
    (flush)))

;; Function to register a handler
(defn register-handler [method func]
  (swap! handlers assoc method func))

;; Async reading from stdin
(defn read-stdin []
  (let [reader (io/reader *in*)]
    (loop []
      (when-let [line (try (.readLine reader) (catch Exception _))]
        (when-some [data (try
                           (<-json line)
                           (catch Throwable _
                             (->err "Couldn't parse: " (pr-str line))))]
          (try
            (handle-data data)
            (catch Throwable ex
              (.println System/err
                        (str "[READ Thread] Couldn't handle incomming JSON-RPC: '" (ex-message ex) " '\n"
                             (with-out-str (pprint data)))))))
        (recur)))))

;; Log utility functions
(defn log [event message & {:keys [data duration coordinates time]}]
  (send-notification {:method "task.log" :params {:time (or time (str (java.time.LocalDateTime/now)))
                                                  :event event
                                                  :message message
                                                  :data data
                                                  :coordinates coordinates
                                                  :duration duration}}))

(defn info [message & [data]] (log "INFO" message :data data))
(defn error [message & [data]] (log "ERROR" message :data data))
(defn warn [message & [data]] (log "WARN" message :data data))
(defn debug [message & [data]] (log "DEBUG" message :data data))
(defn trace [message & [data]] (log "TRACE" message :data data))

;; Task management
(defn close-task [status]
  (send-notification {:method "task.close"
                      :params {:status status}})
  (if (= status "SUCCESS")
    (System/exit 0)
    (System/exit 1)))

(defn update-task [status]
  (send-notification {:method "task.update"
                      :params {:status status}}))

(defn return-task []
  (send-notification {:method "task.return"})
  (System/exit 0))

(defn graphql
  ([{:keys [query variables]}]
   (async/go
     (let [{:keys [error result]}
           (async/<!
            (send-request
             {:method "eywa.datasets.graphql"
              :params {:query query
                       :variables variables}}))]
       (if-not error result
               (ex-info
                "GraphQL error"
                error))))))

;; Main loop
(defn start []
  (async/thread (read-stdin)))
