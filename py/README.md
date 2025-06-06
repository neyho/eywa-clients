# EYWA Client for Python

[![PyPI version](https://badge.fury.io/py/eywa-client.svg)](https://badge.fury.io/py/eywa-client)
[![Python Versions](https://img.shields.io/pypi/pyversions/eywa-client.svg)](https://pypi.org/project/eywa-client/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

EYWA client library for Python providing JSON-RPC communication, GraphQL queries, and task management for EYWA robots.

## Installation

```bash
pip install eywa-client
```

## Quick Start

```python
import asyncio
import eywa

async def main():
    # Initialize the client
    eywa.open_pipe()
    
    # Log messages
    eywa.info("Robot started")
    
    # Execute GraphQL queries
    result = await eywa.graphql("""
        query {
            searchUser(_limit: 10) {
                euuid
                name
                type
            }
        }
    """)
    
    # Update task status
    eywa.update_task(eywa.PROCESSING)
    
    # Complete the task
    eywa.close_task(eywa.SUCCESS)

asyncio.run(main())
```

## Features

- 🚀 **Async/Await Support** - Modern Python async programming
- 📊 **GraphQL Integration** - Execute queries and mutations against EYWA datasets
- 📝 **Comprehensive Logging** - Multiple log levels with metadata support
- 🔄 **Task Management** - Update status, report progress, handle task lifecycle
- 🎯 **Type Hints** - Full type annotations for better IDE support
- 📋 **Table/Sheet Classes** - Built-in data structures for reports

## API Reference

### Initialization

#### `open_pipe()`
Initialize stdin/stdout communication with EYWA runtime. Must be called before using other functions.

```python
eywa.open_pipe()
```

### Logging Functions

#### `log(event="INFO", message="", data=None, duration=None, coordinates=None, time=None)`
Log a message with full control over all parameters.

```python
eywa.log(
    event="INFO",
    message="Processing item",
    data={"itemId": 123},
    duration=1500,
    coordinates={"x": 10, "y": 20}
)
```

#### `info()`, `error()`, `warn()`, `debug()`, `trace()`, `exception()`
Convenience methods for different log levels.

```python
eywa.info("User logged in", {"userId": "abc123"})
eywa.error("Failed to process", {"error": str(e)})
eywa.exception("Unhandled error", {"stack": traceback.format_exc()})
```

### Task Management

#### `async get_task()`
Get current task information. Returns a coroutine.

```python
task = await eywa.get_task()
print(f"Processing: {task['message']}")
```

#### `update_task(status="PROCESSING")`
Update the current task status.

```python
eywa.update_task(eywa.PROCESSING)
```

#### `close_task(status="SUCCESS")`
Close the task with a final status and exit the process.

```python
try:
    # Do work...
    eywa.close_task(eywa.SUCCESS)
except Exception as e:
    eywa.error("Task failed", {"error": str(e)})
    eywa.close_task(eywa.ERROR)
```

#### `return_task()`
Return control to EYWA without closing the task.

```python
eywa.return_task()
```

### Reporting

#### `report(message, data=None, image=None)`
Send a task report with optional data and image.

```python
eywa.report("Analysis complete", {
    "accuracy": 0.95,
    "processed": 1000
}, chart_image_base64)
```

### GraphQL

#### `async graphql(query, variables=None)`
Execute a GraphQL query against the EYWA server.

```python
result = await eywa.graphql("""
    mutation CreateUser($input: UserInput!) {
        syncUser(data: $input) {
            euuid
            name
        }
    }
""", {
    "input": {
        "name": "John Doe",
        "active": True
    }
})
```

### JSON-RPC

#### `async send_request(data)`
Send a JSON-RPC request and wait for response.

```python
result = await eywa.send_request({
    "method": "custom.method",
    "params": {"foo": "bar"}
})
```

#### `send_notification(data)`
Send a JSON-RPC notification without expecting a response.

```python
eywa.send_notification({
    "method": "custom.event",
    "params": {"status": "ready"}
})
```

#### `register_handler(method, func)`
Register a handler for incoming JSON-RPC method calls.

```python
def handle_ping(data):
    print(f"Received ping: {data['params']}")
    eywa.send_notification({
        "method": "custom.pong",
        "params": {"timestamp": time.time()}
    })

eywa.register_handler("custom.ping", handle_ping)
```

## Data Structures

### Sheet Class
For creating structured tabular data:

```python
sheet = eywa.Sheet("UserReport")
sheet.set_columns(["Name", "Email", "Status"])
sheet.add_row({"Name": "John", "Email": "john@example.com", "Status": "Active"})
sheet.add_row({"Name": "Jane", "Email": "jane@example.com", "Status": "Active"})
```

### Table Class
For creating multi-sheet reports:

```python
table = eywa.Table("MonthlyReport")
table.add_sheet(users_sheet)
table.add_sheet(stats_sheet)

# Convert to JSON for reporting
eywa.report("Monthly report", {"table": json.loads(table.toJSON())})
```

## Constants

- `SUCCESS` - Task completed successfully
- `ERROR` - Task failed with error
- `PROCESSING` - Task is currently processing
- `EXCEPTION` - Task failed with exception

## Complete Example

```python
import asyncio
import eywa
import traceback

async def process_data():
    # Initialize
    eywa.open_pipe()
    
    try:
        # Get task info
        task = await eywa.get_task()
        eywa.info("Starting task", {"taskId": task["euuid"]})
        
        # Update status
        eywa.update_task(eywa.PROCESSING)
        
        # Query data with GraphQL
        result = await eywa.graphql("""
            query GetActiveUsers {
                searchUser(_where: {active: {_eq: true}}) {
                    euuid
                    name
                    email
                }
            }
        """)
        
        users = result["data"]["searchUser"]
        
        # Create report
        sheet = eywa.Sheet("ActiveUsers")
        sheet.set_columns(["ID", "Name", "Email"])
        
        for user in users:
            eywa.debug("Processing user", {"userId": user["euuid"]})
            sheet.add_row({
                "ID": user["euuid"],
                "Name": user["name"],
                "Email": user.get("email", "N/A")
            })
        
        # Report results
        eywa.report("Found active users", {
            "count": len(users),
            "sheet": sheet.__dict__
        })
        
        # Success!
        eywa.info("Task completed")
        eywa.close_task(eywa.SUCCESS)
        
    except Exception as e:
        eywa.error("Task failed", {
            "error": str(e),
            "traceback": traceback.format_exc()
        })
        eywa.close_task(eywa.ERROR)

if __name__ == "__main__":
    asyncio.run(process_data())
```

## Type Hints

The library includes comprehensive type hints via `.pyi` file:

```python
from typing import Dict, Any, Optional
import eywa

async def process() -> None:
    task: Dict[str, Any] = await eywa.get_task()
    result: Dict[str, Any] = await eywa.graphql(
        "query { searchUser { name } }", 
        variables={"limit": 10}
    )
```

## Error Handling

The client includes custom exception handling:

```python
try:
    result = await eywa.graphql("{ invalid }")
except eywa.JSONRPCException as e:
    eywa.error(f"GraphQL failed: {e.message}", {"error": e.data})
```

## Testing

Test your robot locally using the EYWA CLI:

```bash
eywa run -c 'python my_robot.py'
```

## Examples

To run examples, position terminal to root project folder and run:

```bash
# Test all features
python -m examples.test_eywa_client

# Run a simple GraphQL query
python -m examples.raw_graphql

# WebDriver example
python -m examples.webdriver
```

## Requirements

- Python 3.7+
- No external dependencies (uses only standard library)

## License

MIT

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues and questions, please visit the [EYWA repository](https://github.com/neyho/eywa).
