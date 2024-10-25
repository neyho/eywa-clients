import asyncio
import sys
import json
import os
from nanoid import generate as nanoid


rpc_callbacks = {}
handlers = {}


def handle_data(data):
    method = data.get("method")
    id_ = data.get("id")
    result = data.get("result")
    error = data.get("error")
    if method:
        handle_request(data)
    elif result and id_:
        handle_response(data)
    elif error and id_:
        handle_response(data)
    else:
        print('Received invalid JSON-RPC:\n', data)


def handle_request(data):
    method = data.get("method")
    handler = handlers.get(method)
    if handler:
        handler(data)
    else:
        print(f"Method {method} doesn't have registered handler")


def handle_response(data):
    id_ = data.get("id")
    callback = rpc_callbacks.get(id_)
    if callback is not None:
        callback.set_result(data)
        # print(f'Handling response for {callback}')
    else:
        print(f'RPC callback not registered for request with id = {id_}')


async def send_request(data):
    id_ = nanoid()
    # id_ = 10
    data["jsonrpc"] = "2.0"
    data["id"] = id_
    future = asyncio.Future()
    rpc_callbacks[id_] = future
    sys.stdout.write(json.dumps(data) + "\n")
    sys.stdout.flush()
    result = await future
    del rpc_callbacks[id_]
    return result


def send_notification(data):
    data["jsonrpc"] = "2.0"
    sys.stdout.write(json.dumps(data) + "\n")
    sys.stdout.flush()


def register_handler(method, func):
    handlers[method] = func


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
            json_data = json.loads(raw_json.decode().strip())
            handle_data(json_data)
            await asyncio.sleep(0.5)
        except asyncio.TimeoutError:
            await asyncio.sleep(0.5)


# Additional functions

SUCCESS = "SUCCESS"
ERROR = "ERROR"
PROCESSING = "PROCESSING"
EXCEPTION = "EXCEPTION"


class Sheet ():
    def __init__(self, name='Sheet'):
        self.name = name
        self.rows = []
        self.columns = []

    def add_row(self, row):
        self.rows.append(row)

    def remove_row(self, row):
        self.rows.remove(row)

    def set_columns(self, columns):
        self.columns = columns

    def toJSON(self):
        return json.dumps(self, default=lambda o:o.__dict__)


class Table ():
    def __init__(self, name='Table'):
        self.name = name
        self.sheets = []

    def add_sheet(self, sheet):
        self.sheets.append(sheet)

    def remove_sheet(self, idx=0):
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


def log(event="INFO", message="", data=None, duration=None, coordinates=None, time=None):
    if time is None:
        from datetime import datetime
        time = datetime.now()

    send_notification({
        "method": "task.log",
        "params": {
            "time": time,
            "event": event,
            "message": message,
            "data": data,
            "coordinates": coordinates,
            "duration": duration
        }
    })


def info(message, data=None):
    log(event="INFO", message=message, data=data)


def error(message, data=None):
    log(event="ERROR", message=message, data=data)


def warn(message, data=None):
    log(event="WARN", message=message, data=data)


def debug(message, data=None):
    log(event="DEBUG", message=message, data=data)


def trace(message, data=None):
    log(event="TRACE", message=message, data=data)


def report(message, data=None, image=None):
    send_notification({
        'method': 'task.report',
        'params': {
            'message': message,
            'data': data,
            'image': image
        }
    })


def close_task(status="SUCCESS"):
    send_notification({
        'method': 'task.close',
        'params': {
            'status': status
        }
    })

    if status == "SUCCESS":
        sys.exit(0)
    else:
        sys.exit(1)


def update_task(status="PROCESSING"):
    send_notification({
        'method': 'task.update',
        'params': {
            'status': status
        }
    })


async def get_task():
    return await send_request({'method': 'task.get'})


def return_task():
    send_notification({
        'method': 'task.return'
    })
    sys.exit(0)


async def graphql(query, variables=None):
    return await send_request({
        'method': 'eywa.datasets.graphql',
        'params': {
            'query': query,
            'variables': variables
        }
    })


__stdin__task__ = None


def open_pipe():
    global __stdin__task__
    __stdin__task__ = asyncio.create_task(read_stdin())


def exit():
    if __stdin__task__ is not None:        
        __stdin__task__.cancel()
    os.set_blocking(sys.stdin.fileno(), True)
    sys.exit(0)
