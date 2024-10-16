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
import asyncio
import traceback
from nanoid import generate as nanoid
import logging



callbacks = {}
handlers = {}


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


class LargeBufferStreamReader(asyncio.StreamReader):
    # Default limit set to 1 MB here.
    def __init__(self, limit=1024*1024*10, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._limit = limit


async def read_stdin():
    reader = LargeBufferStreamReader()
    protocol = asyncio.StreamReaderProtocol(reader)
    
    await asyncio.get_event_loop().connect_read_pipe(lambda: protocol, sys.stdin)
    while True:
        try:
            raw_json = await asyncio.wait_for(reader.readline(), timeout=2)
            if raw_json:      
                _handle(raw_json)            

            await asyncio.sleep(0.5)
        # except asyncio.CancelledError:
        #     raise
                        
        except asyncio.TimeoutError:
            await asyncio.sleep(0.5)
        
        


async def request(method, params, timeout = None):    
    # create a new id for requests expecting a response
    id = nanoid()
    
    # creaete a future that will be response once fulfilled
    future = asyncio.Future()
    callbacks[id] = future    

    # create the request
    req = generate_request(method, id=id, params=params)
    
    _write(req)
    
    result = await future    
    return result
    

def notify(method, data = None):
    req = generate_request(method, None, data)
    _write(req)


def _handle(line):
    """
    Handles an incoming *line* and dispatches the parsed object to the request, response, or
    error handlers.
    """    
    
    obj = None
    try:
        obj = json.loads(line)
    except Exception as e:

        traceback.print_exception(e)

    # dispatch to the correct handler
    if obj is None:
        pass
    elif "method" in obj:
        # request
        _handle_request(obj)
    elif "error" not in obj:
        # response
        _handle_response(obj)
    else:
        # error
        _handle_error(obj)


def _handle_request(req):
    try:
        method = _route(req["method"])
        result = method(req["params"])
        if "id" in req:
            res = generate_response(req["id"], result)
            _write(res)
    except Exception as e:
        if "id" in req:
            if isinstance(e, RPCError):
                err = generate_error(req["id"], e.code, e.data)
            else:
                err = generate_error(req["id"], -32603, str(e))
            _write(err)


def _handle_response(res):
    if res["id"] in callbacks:
        callback = callbacks[res["id"]]
        callback.set_result(res)
 


def _handle_error(res):
    err = res["error"]
    error = get_error(err["code"])(err.get("data", err["message"]))


    # lookup and invoke the callback
    if res["id"] in callbacks:
        callback = callbacks.pop(res["id"])
        callback.set_exception(error)
                


def _route(method):
    if method in handlers:
        handler = handlers[method]
        return handler 
    raise RPCMethodNotFound(data=method)


def _write(s):
    """
    Writes a string *s* to the output stream.
    """
    sys.stdout.write(s + "\n")
    sys.stdout.flush()


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
        message = message

        super(RPCError, self).__init__(message)

        data = data

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
    notify("task.log", {"time": time, "event":event,"message":message,
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
    notify("task.report",
                {"message":message,
                 "data": data,
                 "image":image})

def close(status=SUCCESS):
    notify("task.close", {"status":status})
    if (status == ERROR):
        exit_status=1
    else:
        exit_status=0
    sys.exit(exit_status)


async def get_task():
    return await request("task.get", {})


def update_task(status=PROCESSING):
    if status not in ["SUCCESS", "ERROR", "PROCESSING", "EXCEPTION"]:
        raise ValueError(f"The status {status} is not one of allowed status types [SUCCESS, ERROR, PROCESSING, EXCEPTION]")
    notify("task.update",{"status":status})


def return_task():
    notify("task.return")
    sys.exit(0)


async def graphql(query, timeout = None):
    return await request("eywa.datasets.graphql", query, timeout)


__stdin__task__ = None


def open_pipe():
    global __stdin__task__
    __stdin__task__ = asyncio.create_task(read_stdin())


def exit():
    if __stdin__task__ is not None:        
        __stdin__task__.cancel()        
    sys.exit(0)


