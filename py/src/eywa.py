"""EYWA Reacher client"""

__author__ = "Robert Gersak"
__email__ = "r.gersak@gmail.com"
__license__ = "MIT"
__status__ = "Development"
__version__ = "0.0.8"


import time
import datetime
import json
import sys
import threading
import traceback


def check_id(id, allow_empty=False):
    if (id is not None or not allow_empty) and not isinstance(id, (int, str)):
        raise TypeError("id must be an integer or string, got {} ({})".format(id, type(id)))

def check_method(method):
    if not isinstance(method, str):
        raise TypeError("method must be a string, got {} ({})".format(method, type(method)))

def check_code(code):
        if not isinstance(code, int):
            raise TypeError("code must be an integer, got {} ({})".format(id, type(id)))

        if not get_error(code):
            raise ValueError("unknown code, got {} ({})".format(code, type(code)))

def generate_request(method, id=None, params=None):
        try:
            check_method(method)
            check_id(id, allow_empty=True)
        except Exception as e:
            raise RPCInvalidRequest(str(e))

        req = "{{\"jsonrpc\":\"2.0\",\"method\":\"{}\"".format(method)

        if id is not None:
            if isinstance(id, str):
                id = json.dumps(id)
            req += ",\"id\":{}".format(id)

        if params is not None:
            try:
                req += ",\"params\":{}".format(json.dumps(params))
            except Exception as e:
                raise RPCParseError(str(e))

        req += "}"

        return req


def generate_response(id, result):
        try:
            check_id(id)
        except Exception as e:
            raise RPCInvalidRequest(str(e))

        # encode string ids
        if isinstance(id, str):
            id = json.dumps(id)

        # build the response string
        try:
            res = "{{\"jsonrpc\":\"2.0\",\"id\":{},\"result\":{}}}".format(id, json.dumps(result))
        except Exception as e:
            raise RPCParseError(str(e))

        return res

def generate_error(id, code, data=None):
        try:
            check_id(id)
            check_code(code)
        except Exception as e:
            raise RPCInvalidRequest(str(e))

        # build the inner error data
        if (get_error(code) is not None):
            message = get_error(code).title
        else:
            message = 'Unknown error code'
        err_data = "{{\"code\":{},\"message\":\"{}\"".format(code, message)

        # insert data when given
        if data is not None:
            try:
                err_data += ",\"data\":{}}}".format(json.dumps(data))
            except Exception as e:
                raise RPCParseError(str(e))
        else:
            err_data += "}"

        # encode string ids
        if isinstance(id, str):
            id = json.dumps(id)

        # start building the error string
        err = "{{\"jsonrpc\":\"2.0\",\"id\":{},\"error\":{}}}".format(id, err_data)

        return err


EMPTY_RESULT = object()


connection_thread=None

class EYWA():

    def __new__(cls):
        if not hasattr(cls, 'instance'):
            cls.instance = super(EYWA, cls).__new__(cls)
        return cls.instance

    def __init__(self):
        # other attributes
        self._i = -1
        self._callbacks = {}
        self._handlers= {}
        self._results = {}

    def __del__(self):
        if connection_thread:
            connection_thread.stop()

    @classmethod
    def request(cls, method, params, timeout = None):
        # check if the call is a notification
        eywa = EYWA()
        # create a new id for requests expecting a response
        eywa._i += 1
        id = eywa._i

        now = time.time();

        # store an empty result for the meantime
        eywa._results[id] = EMPTY_RESULT

        # create the request
        req = generate_request(method, id=id, params=params)
        eywa._write(req)

        result  = EMPTY_RESULT
        # blocking return value behavior
        while True:
            if eywa._results[id] != EMPTY_RESULT:
                result = eywa._results[id]
                del eywa._results[id]
                if isinstance(result, Exception):
                   raise result
                else:
                    return result
            elif timeout is not None  and time.time() - now > timeout:
                raise ValueError('EYWA silent error. Timeout occured for:\n' + req)
            else:
                time.sleep(0.1)

    @classmethod
    def notify(cls, method, data = None):
        eywa = EYWA()
        req = generate_request(method, None, data)
        eywa._write(req)

    def _handle(self, line):
        """
        Handles an incoming *line* and dispatches the parsed object to the request, response, or
        error handlers.
        """
        obj = None
        try:
            obj = json.loads(line)
        except Exception as e:
            # print('Line:\n' + line, file=sys.stderr)
            traceback.print_exception(e)
            pass

        # dispatch to the correct handler
        if obj is None:
            pass
        elif "method" in obj:
            # request
            self._handle_request(obj)
        elif "error" not in obj:
            # response
            self._handle_response(obj)
        else:
            # error
            self._handle_error(obj)

    def _handle_request(self, req):
        try:
            method = self._route(req["method"])
            result = method(req["params"])
            if "id" in req:
                res = generate_response(req["id"], result)
                self._write(res)
        except Exception as e:
            if "id" in req:
                if isinstance(e, RPCError):
                    err = generate_error(req["id"], e.code, e.data)
                else:
                    err = generate_error(req["id"], -32603, str(e))
                self._write(err)

    def _handle_response(self, res):
        if res["id"] in self._results:
            self._results[res["id"]] = res["result"]

        if res["id"] in self._callbacks:
            callback = self._callbacks[res["id"]]
            del self._callbacks[res["id"]]
            callback(None, res["result"])

    def _handle_error(self, res):
        err = res["error"]
        error = get_error(err["code"])(err.get("data", err["message"]))

        # set the error
        if res["id"] in self._results:
            self._results[res["id"]] = error

        # lookup and invoke the callback
        if res["id"] in self._callbacks:
            callback = self._callbacks[res["id"]]
            del self._callbacks[res["id"]]
            callback(error, None)

    def _route(self, method):
        if method in self._handlers:
            handler = self._handlers[method]
            return handler 
        raise RPCMethodNotFound(data=method)

    def _write(self, s):
        """
        Writes a string *s* to the output stream.
        """
        sys.stdout.write(s + "\n")
        sys.stdout.flush()


class Line(threading.Thread):
    def __init__(self, tap, name="EYWAonline", interval=0.1, start=True):

        super(Line, self).__init__()

        # store attributes
        self.tap = tap
        self.name = name
        self.interval = interval
        self.daemon = True

        # register a stop event
        self._stop = threading.Event()

        if start:
            self.start()

    def start(self):
        super(Line, self).start()

    def stop(self):
        self._stop.set()

    def run(self):
        self._stop.clear()
        # print('starting read loop')
        while not self._stop.is_set():
            line= None
            try:
                # print('Reading sys.stdin.line')
                line = sys.stdin.readline()
                # print('Line length %d', len(line))
            except IOError:
                # prevent residual race conditions occurring when stdin is closed externally
                pass
            if line:
                # line = line.strip()
                self.tap._handle(line)
            else:
                self._stop.wait(self.interval)


class RPCError(Exception):

    """
    Base class for RPC errors.

    .. py:attribute:: message

       The message of this error, i.e., ``"<title> (<code>)[, data: <data>]"``.

    .. py:attribute:: data

       Additional data of this error. Setting the data attribute will also change the message
       attribute.
    """

    def __init__(self, data=None):
        # build the error message
        message = "{} ({})".format(self.title, self.code)
        if data is not None:
            message += ", data: {}".format(data)
        self.message = message

        super(RPCError, self).__init__(message)

        self.data = data

    def __str__(self):
        return self.message


error_map_distinct = {}
error_map_range = {}


def is_range(code):
    return (
        isinstance(code, tuple) and
        len(code) == 2 and
        all(isinstance(i, int) for i in code) and
        code[0] < code[1]
    )


def register_error(cls):
    """
    Decorator that registers a new RPC error derived from :py:class:`RPCError`. The purpose of
    error registration is to have a mapping of error codes/code ranges to error classes for faster
    lookups during error creation.

    .. code-block:: python

       @register_error
       class MyCustomRPCError(RPCError):
           code = ...
           title = "My custom error"
    """
    # it would be much cleaner to add a meta class to RPCError as a registry for codes
    # but in CPython 2 exceptions aren't types, so simply provide a registry mechanism here
    if not issubclass(cls, RPCError):
        raise TypeError("'{}' is not a subclass of RPCError".format(cls))

    code = cls.code

    if isinstance(code, int):
        error_map = error_map_distinct
    elif is_range(code):
        error_map = error_map_range
    else:
        raise TypeError("invalid RPC error code {}".format(code))

    if code in error_map:
        raise AttributeError("duplicate RPC error code {}".format(code))

    error_map[code] = cls

    return cls


def get_error(code):
    """
    Returns the RPC error class that was previously registered to *code*. *None* is returned when no
    class could be found.
    """
    if code in error_map_distinct:
        return error_map_distinct[code]

    for (lower, upper), cls in error_map_range.items():
        if lower <= code <= upper:
            return cls

    return None


@register_error
class RPCParseError(RPCError):

    code = -32700
    title = "Parse error"


@register_error
class RPCInvalidRequest(RPCError):

    code = -32600
    title = "Invalid Request"


@register_error
class RPCMethodNotFound(RPCError):

    code = -32601
    title = "Method not found"


@register_error
class RPCInvalidParams(RPCError):

    code = -32602
    title = "Invalid params"


@register_error
class RPCInternalError(RPCError):

    code = -32603
    title = "Internal error"


@register_error
class RPCServerError(RPCError):

    code = (-32099, -32000)
    title = "Server error"





# EYWA INTERNALS

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


SUCCESS = "SUCCESS"
ERROR = "ERROR"
PROCESSING = "PROCESSING"
EXCEPTION = "EXCEPTION"

def log(event="INFO",
        message=None,
        data=None,
        duration=None,
        coordinates=None,
        time=None):

    if (time == None):
        time= datetime.datetime.utcnow().isoformat()

    if event not in ["INFO", "ERROR", "WARN", "DEBUG", "TRACE"]:
        raise ValueError(f"The event '{event}' is not one of allowed event types [INFO, ERROR, WARN, DEBUG, TRACE].")
    EYWA.notify("task.log", {"time": time, "event":event,"message":message,
        "data":data,"coordinates":coordinates,"duration":duration})

def info(message,data=None):
    log("INFO", message, data)

def error(message,data=None):
    log("ERROR", message, data)

def warn(message,data=None):
    log("WARN",message,data)

def debug(message,data=None):
    log("DEBUG",message,data)

def trace(message,data=None):
    log("TRACE",message,data)

def report(message,data=None,image=None):
    EYWA.notify("task.report",
                {"message":message,
                 "data": data,
                 "image":image})

def close(status=SUCCESS):
    EYWA.notify("task.close", {"status":status})
    if (status == ERROR):
        exit_status=1
    else:
        exit_status=0
    sys.exit(exit_status)


def get_task():
    return EYWA.request("task.get", {})


def update_task(status=PROCESSING):
    if status not in ["SUCCESS", "ERROR", "PROCESSING", "EXCEPTION"]:
        raise ValueError(f"The status {status} is not one of allowed status types [SUCCESS, ERROR, PROCESSING, EXCEPTION]")
    EYWA.notify("task.update",{"status":status})


def return_task():
    EYWA.notify("task.return")
    sys.exit(0)

def graphql(query, timeout = None):
    return EYWA.request("eywa.datasets.graphql", query, timeout)

eywa = EYWA()
connection_thread=Line(eywa)
