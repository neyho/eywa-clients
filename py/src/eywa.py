"""
Eywa: A powerful module for managing async workflows and data processing.

This module provides tools and utilities to streamline asynchronous programming
and integrate with various backend services.

Version with Windows compatibility fixes for STDIO pipe handling.
"""

import asyncio
import sys
import json
import os
import platform
import logging
from datetime import datetime, date
from nanoid import generate as nanoid

# Set up logging
logger = logging.getLogger(__name__)

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
        handle_result(id_, result)
    elif error and id_:
        handle_error(id_, error)
    else:
        print('Received invalid JSON-RPC:\n', data)


def handle_request(data):
    method = data.get("method")
    handler = handlers.get(method)
    if handler:
        handler(data)
    else:
        print(f"Method {method} doesn't have registered handler")


def handle_result(id_, result):
    callback = rpc_callbacks.get(id_)
    if callback is not None:
        callback.set_result(result)
        # print(f'Handling response for {callback}')
    else:
        print(f'RPC callback not registered for request with id = {id_}')


class JSONRPCException(Exception):
    def __init__(self, data):
        super().__init__(data.get("message"))
        self.data = data


def handle_error(id_, error):
    callback = rpc_callbacks.get(id_)
    if callback is not None:
        callback.set_result(JSONRPCException(error))
        # print(f'Handling response for {callback}')
    else:
        print(f'RPC callback not registered for request with id = {id_}')


def custom_serializer(obj):
    if isinstance(obj, (datetime, date)):
        return obj.isoformat()
    return obj


async def send_request(data):
    id_ = nanoid()
    # id_ = 10
    data["jsonrpc"] = "2.0"
    data["id"] = id_
    future = asyncio.Future()
    rpc_callbacks[id_] = future

    try:
        output_line = json.dumps(data, default=custom_serializer) + "\n"
        sys.stdout.write(output_line)
        sys.stdout.flush()
    except Exception as e:
        logger.error(f"Error writing to STDOUT: {e}")
        del rpc_callbacks[id_]
        raise

    result = await future
    del rpc_callbacks[id_]
    if isinstance(result, BaseException):
        raise result
    else:
        return result


def send_notification(data):
    data["jsonrpc"] = "2.0"
    try:
        output_line = json.dumps(data, default=custom_serializer) + "\n"
        sys.stdout.write(output_line)
        sys.stdout.flush()
    except Exception as e:
        logger.error(f"Error writing notification to STDOUT: {e}")


def register_handler(method, func):
    handlers[method] = func


class LargeBufferStreamReader(asyncio.StreamReader):
    # Default limit set to 10 MB here.
    def __init__(self, limit=1024*1024*10, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._limit = limit


class WindowsStdinReader:
    """Windows-compatible STDIN reader that avoids proactor event loop issues."""

    def __init__(self, buffer_size: int = 1024 * 1024):
        self.buffer_size = buffer_size
        self.running = False
        self._loop = None
        self._executor = None

    async def start(self, data_handler):
        """Start reading from STDIN in a Windows-compatible way."""
        self.running = True
        self._loop = asyncio.get_event_loop()

        # Use thread pool executor for blocking I/O on Windows
        import concurrent.futures
        self._executor = concurrent.futures.ThreadPoolExecutor(max_workers=1)

        try:
            while self.running:
                # Read from STDIN in a separate thread to avoid blocking
                try:
                    line = await self._loop.run_in_executor(
                        self._executor,
                        self._read_line_blocking
                    )

                    if line and self.running:
                        try:
                            json_data = json.loads(line.strip())
                            data_handler(json_data)
                        except json.JSONDecodeError as e:
                            logger.error(f"JSON decode error: {
                                         e}, line: {line}")
                        except Exception as e:
                            logger.error(f"Error handling data: {e}")

                    # Small delay to prevent busy waiting
                    await asyncio.sleep(0.01)

                except Exception as e:
                    if self.running:
                        logger.error(f"Error reading from STDIN: {e}")
                        await asyncio.sleep(0.5)

        except asyncio.CancelledError:
            logger.info("STDIN reader task cancelled")
        finally:
            if self._executor:
                self._executor.shutdown(wait=False)

    def _read_line_blocking(self):
        """Read a line from STDIN in a blocking manner."""
        try:
            # For Windows, we'll use a simple readline with error handling
            line = sys.stdin.readline()
            return line if line else None
        except (OSError, ValueError) as e:
            logger.debug(f"STDIN read error (expected on shutdown): {e}")
            return None
        except Exception as e:
            logger.debug(f"Blocking read error: {e}")
            return None

    def stop(self):
        """Stop the STDIN reader."""
        self.running = False


async def read_stdin():
    """Cross-platform STDIN reader with Windows compatibility."""

    if platform.system() == 'Windows':
        # Use Windows-specific reader
        reader = WindowsStdinReader()
        await reader.start(handle_data)
    else:
        # Use original Unix-style reader for other platforms
        reader = LargeBufferStreamReader()
        protocol = asyncio.StreamReaderProtocol(reader)

        try:
            await asyncio.get_event_loop().connect_read_pipe(lambda: protocol, sys.stdin)
        except Exception as e:
            logger.error(f"Failed to connect read pipe: {e}")
            # Fallback to Windows-style reader even on Unix if pipe connection fails
            fallback_reader = WindowsStdinReader()
            await fallback_reader.start(handle_data)
            return

        while True:
            try:
                raw_json = await asyncio.wait_for(reader.readline(), timeout=2)
                if raw_json:
                    json_data = json.loads(raw_json.decode().strip())
                    handle_data(json_data)
                await asyncio.sleep(0.01)
            except asyncio.TimeoutError:
                await asyncio.sleep(0.5)
            except Exception as e:
                logger.error(f"Unix STDIN reader error: {e}")
                await asyncio.sleep(0.5)


# Additional functions
SUCCESS = "SUCCESS"
ERROR = "ERROR"
PROCESSING = "PROCESSING"
EXCEPTION = "EXCEPTION"


class Sheet():
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
        return json.dumps(self, default=lambda o: o.__dict__)


class Table():
    def __init__(self, name='Table'):
        self.name = name
        self.sheets = []

    def add_sheet(self, sheet):
        self.sheets.append(sheet)

    def remove_sheet(self, idx=0):
        self.sheets.pop(idx)

    def toJSON(self):
        return json.dumps(self, default=lambda o: o.__dict__)


# TODO finish task reporting
class TaskReport():
    def __init__(self, message, data=None, image=None):
        self.message = message
        self.data = data
        self.image = image


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


def exception(message, data=None):
    log(event="EXCEPTION", message=message, data=data)


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
        exit(0)
    else:
        exit(1)


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
    exit(0)


async def graphql(query, variables=None):
    return await send_request({
        'method': 'eywa.datasets.graphql',
        'params': {
            'query': query,
            'variables': variables
        }
    })


# File operations - import from eywa_files module
try:
    from .eywa_files import (
        upload_file,
        upload_content,
        download_file,
        list_files,
        get_file_info,
        get_file_by_name,
        delete_file,
        calculate_file_hash,
        quick_upload,
        quick_download,
        FileUploadError,
        FileDownloadError
    )
except ImportError:
    # If eywa_files module is not available, provide stub functions
    async def upload_file(*args, **kwargs):
        raise NotImplementedError(
            "File operations require aiohttp. Install with: pip install aiohttp")

    async def download_file(*args, **kwargs):
        raise NotImplementedError(
            "File operations require aiohttp. Install with: pip install aiohttp")

    async def upload_content(*args, **kwargs):
        raise NotImplementedError(
            "File operations require aiohttp. Install with: pip install aiohttp")

    async def list_files(*args, **kwargs):
        raise NotImplementedError(
            "File operations require aiohttp. Install with: pip install aiohttp")

    async def get_file_info(*args, **kwargs):
        raise NotImplementedError(
            "File operations require aiohttp. Install with: pip install aiohttp")

    async def get_file_by_name(*args, **kwargs):
        raise NotImplementedError(
            "File operations require aiohttp. Install with: pip install aiohttp")

    async def delete_file(*args, **kwargs):
        raise NotImplementedError(
            "File operations require aiohttp. Install with: pip install aiohttp")

    def calculate_file_hash(*args, **kwargs):
        raise NotImplementedError(
            "File operations require additional dependencies")

    async def quick_upload(*args, **kwargs):
        raise NotImplementedError(
            "File operations require aiohttp. Install with: pip install aiohttp")

    async def quick_download(*args, **kwargs):
        raise NotImplementedError(
            "File operations require aiohttp. Install with: pip install aiohttp")

    class FileUploadError(Exception):
        pass

    class FileDownloadError(Exception):
        pass


__stdin__task__ = None


def open_pipe():
    global __stdin__task__
    try:
        __stdin__task__ = asyncio.create_task(read_stdin())
    except Exception as e:
        logger.error(f"Failed to open pipe: {e}")
        # Try setting up Windows event loop if needed
        if platform.system() == 'Windows':
            try:
                asyncio.set_event_loop_policy(
                    asyncio.WindowsProactorEventLoopPolicy())
                __stdin__task__ = asyncio.create_task(read_stdin())
            except Exception as e2:
                logger.error(f"Failed to open pipe with Windows policy: {e2}")
                raise e2
        else:
            raise e


def exit(status=0):
    global __stdin__task__

    if __stdin__task__ is not None:
        __stdin__task__.cancel()

    try:
        # Try to reset STDIN to blocking mode
        if hasattr(os, 'set_blocking'):
            os.set_blocking(sys.stdin.fileno(), True)
    except (AttributeError, OSError) as e:
        logger.debug(f"Could not reset STDIN blocking mode: {e}")

    # Clean shutdown
    try:
        # Cancel any remaining RPC callbacks
        for callback in rpc_callbacks.values():
            if not callback.done():
                callback.cancel()
        rpc_callbacks.clear()
    except Exception as e:
        logger.debug(f"Error during cleanup: {e}")

    sys.exit(status)
