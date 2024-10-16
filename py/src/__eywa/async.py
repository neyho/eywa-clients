import asyncio
import sys
import json
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
    sys.exit(0)


async def search_tasks():
    return await graphql("""{
    searchTask (_limit:2000) {
      euuid
      status
      finished
      started
    }
    }""")


async def search_users():
    return await graphql("""{
    searchUser (_limit:2000) {
      euuid
      name
      type      
    }
    }""")


async def main():        
    open_pipe()

    result = await asyncio.gather(search_tasks(), search_users())
    search_tasks_result, search_users_result = result
    print(search_tasks_result)
    print()
    print(search_users_result)

    search_tasks_result = await search_tasks()
    search_users_result = await search_users()
    print()
    print(search_tasks_result)
    print()
    print(search_users_result)

    print(f'Exiting!')
    exit()

asyncio.run(main())

# {"jsonrpc":"2.0","id":10,"result":{"searchTask":[{"euuid":"ba67467d-574e-4fdd-8f28-41e4c4937bc1","status":"EXCEPTION","data":{"input":"yum install epel-release -y"},"finished":null,"started":null},{"euuid":"878796bf-4cab-431c-a48f-c831348a49b8","status":"EXCEPTION","data":null,"finished":null,"started":null}]}}
