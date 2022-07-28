"""EYWA Reacher client"""

__version__ = "0.0.1"


import jsonrpyc
import time
import datetime
import json
import sys


class Sheet ():
    def __init__(self, name = 'Sheet'):
        self.name = name
        self.rows = []
        self.columns = []
    def add_row(self,row):
        self.rows.append(row)
    def remove_row(self,row):
        self.rows.remove(row)
    def set_columns(self, columns):
        self.columns = columns
    def toJSON(self):
        return json.dumps(self, default=lambda o:o.__dict__)


class Table ():
    def __init__(self, name = 'Table'):
        self.name = name
        self.sheets= []
    def add_sheet(self,sheet):
        self.sheets.append(sheet)
    def remove_sheet(self,idx=0):
        self.sheets.pop(idx)
    def toJSON(self):
        return json.dumps(self, default=lambda o:o.__dict__)


# TODO finish task reporting
class TaskReport():
    def __init__(self,message, data=None, image=None):
        self.message = message
        self.data = data
        self.image = image

# ws1 = Sheet('miroslav')
# ws1.add_row({'slaven':1,'belupo':2})
# ws1.add_row({'slaven':30,'belupo':0})


# t1 = Table('TEST')
# t1.add_sheet(ws1)

# print(t1.toJSON())
# print(json.dumps({'a':2,'b':'4444'}))


class EYWARPC(jsonrpyc.RPC):
    def call(self, method, args=(), kwargs=None, callback=None, block=0):
        """
        Performs an actual remote procedure call by writing a request representation (a string) to
        the output stream. The remote RPC instance uses *method* to route to the actual method to
        call with *args* and *kwargs*. When *callback* is set, it will be called with the result of
        the remote call. When *block* is larger than *0*, the calling thread is blocked until the
        result is received. In this case, *block* will be the poll interval, emulating synchronuous
        return value behavior. When both *callback* is *None* and *block* is *0* or smaller, the
        request is considered a notification and the remote RPC instance will not send a response.
        """
        # default kwargs
        if kwargs is None:
            kwargs = {}

        # check if the call is a notification
        is_notification = callback is None and block <= 0

        # create a new id for requests expecting a response
        id = None
        if not is_notification:
            self._i += 1
            id = self._i

        # register the callback
        if callback is not None:
            self._callbacks[id] = callback

        # store an empty result for the meantime
        if block > 0:
            self._results[id] = self.EMPTY_RESULT

        # create the request
        params = args
        req = jsonrpyc.Spec.request(method, id=id, params=params)
        self._write(req)

        # blocking return value behavior
        if block > 0:
            while True:
                if self._results[id] != self.EMPTY_RESULT:
                    result = self._results[id]
                    del self._results[id]
                    if isinstance(result, Exception):
                        raise result
                    else:
                        return result
                time.sleep(block)


rpc = EYWARPC(stdout=sys.stdout,stdin=sys.stdin)

class Task():
    SUCCESS = "SUCCESS"
    ERROR = "ERROR"
    PROCESSING = "PROCESSING"
    EXCEPTION = "EXCEPTION"
    def log(self,event="INFO",
            message=None,
            data=None,
            duration=None,
            coordinates=None,
            time=None):

        if (time == None):
            time= datetime.datetime.utcnow().isoformat()

        rpc.call("task.log", {"time": time, "event":event,"message":message,
            "data":data,"coordinates":coordinates,"duration":duration})

    def info(self,message,data=None):
        self.log("INFO", message, data)

    def error(self,message,data=None):
        self.log("ERROR", message, data)

    def warn(self,message,data=None):
        self.log("WARN",message,data)

    def debug(self,message,data=None):
        self.log("DEBUG",message,data)

    def trace(self,message,data=None):
        self.log("TRACE",message,data)

    def report(self,message,data=None,image=None):
        rpc.call("task.report", {"message":message,"data": data,
                                      "image":image})

    def close(self,status=SUCCESS):
        rpc.call("task.close", {"status":status})
        if (status == self.ERROR):
            exit_status=1
        else:
            exit_status=0
        sys.exit(exit_status)

    def update_task(self, status=PROCESSING):
        rpc.call("task.update",{"status":status})

    def return_task(self):
        rpc.call("task.return")
        sys.exit(0)
