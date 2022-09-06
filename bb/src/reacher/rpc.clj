(ns reacher.rpc
  (:require
    [cheshire.core :as json]
    [clojure.core.async :as async]))




(defn show-users
  []
  (println
    (json/generate-string
      {:id "109201"
       :jsonrpc "2.0"
       :method "eywa.datasets.graphql"
       :params {:query "{
                       searchUser {
                       name
                       euuid
                       type
                       }
                       }"
                :variables {:a 10 :b 20}}})))


(defn read-output
  []
  (read-line))



(defn -main [& _]
  (show-users)
  (let [in (read-output)]
    (println "RECEIVED OUTPUT: " in)
    (spit "eywa_response.json" in)))
