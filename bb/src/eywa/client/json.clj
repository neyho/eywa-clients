(ns eywa.client.json
  (:require
   [cheshire.core :as json]
   [clojure.instant :refer [read-instant-date]]
   [clojure.string :as str]))

(def uuid-pattern #"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")

(def date-pattern #"^\d\d{3}-(0[1-9]|1[0-2])-[0-3]\dT[0-2]\d:[0-5]\d:[0-5]\d")

(defn- snake-case [data]
  (str/replace (name data) #"[\-\s]+" "_"))

(defn eywa-val-fn
  "Helper function for transforming dates and other objects to Clojure data
  objects"
  [_ data]
  (letfn [(cast-date [date]
            (try
              (read-instant-date date)
              (catch Exception _ nil)))]
    (cond
      (and (string? data) (re-find date-pattern data)) (cast-date data)
      (and (string? data) (= (count data) 36) (re-find uuid-pattern data)) (java.util.UUID/fromString data)
      (vector? data) (mapv #(eywa-val-fn nil %) data)
      (map? data) (reduce
                   (fn [r [k v]] (assoc r k (eywa-val-fn k v)))
                   {}
                   data)
      :else data)))

(defn <-json
  ([v]
   (eywa-val-fn nil (json/parse-string v keyword))))

(defn ->json [data]
  (json/generate-string
   data
   {:key-fn (fn [data]
              (if (keyword? data)
                (if-let [n (namespace data)]
                  (str n "/" (snake-case data))
                  (snake-case data))
                data))}))
