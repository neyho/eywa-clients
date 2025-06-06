(ns eywa.client-test
  (:require
   [eywa.client :as eywa]
   [clojure.core.async :as async]
   [clojure.pprint :refer [pprint]]
   [toddler.graphql :as graphql]))

(comment
  ;; IMPORTANT - Don't forget to run eywa/start
  (eywa/start)
  (async/go
    (pprint
     (time
      (async/<!
       (eywa/graphql
        {:query (graphql/queries
                 [{:query :searchUser
                   :selection {:euuid nil
                               :name nil
                               :roles [{:selections
                                        {:euuid nil
                                         :name nil}}]}}])})))))
  (java.util.Date.)
  (def token *1)
  (= token
     (->
      (clojure.edn/read-string (slurp "eywa.edn"))
      (get-in [:cli :tokens "https://demo.eywaonline.com" :access-token])))
  (async/go
    (def x
      (async/<!
       (eywa/graphql
        (graphql/mutations
         [{:mutation :syncUserList
           :alias :new_users
           :types {:user :UserInput}
           :selection {:euuid nil}
           :variables {:user [{:name "bozo"}
                              {:name "hohn"}
                              {:name "triss"}]}}]))))))
