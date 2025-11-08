# EYWA Client for Python

[![PyPI version](https://badge.fury.io/py/eywa-client.svg)](https://badge.fury.io/py/eywa-client)
[![Python Versions](https://img.shields.io/pypi/pyversions/eywa-client.svg)](https://pypi.org/project/eywa-client/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**MODERNIZED** EYWA client library for Python providing JSON-RPC communication, GraphQL queries, and **comprehensive file operations** for EYWA robots.

## 🚀 Version 2.0 - Modernized Architecture

This version has been completely modernized to match the Babashka and Node.js clients:

- ✅ **Single Map Arguments** - API functions use single dict arguments that mirror GraphQL schema
- ✅ **Client UUID Management** - Full control over file and folder UUIDs for deduplication
- ✅ **Modern GraphQL Patterns** - Relationship filtering instead of broken WHERE clause patterns
- ✅ **Complete Folder Operations** - Full folder hierarchy support (create, list, delete, info)
- ✅ **Streaming Operations** - Memory-efficient uploads/downloads with progress tracking
- ✅ **FILES_SPEC.md Compliance** - Implements all 12 core functions per specification

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

### Core Features
- 🚀 **Async/Await Support** - Modern Python async programming
- 📡 **JSON-RPC Communication** - Seamless communication with EYWA server
- 🗃️ **GraphQL Integration** - Execute queries and mutations
- 📝 **Task Management** - Status updates, logging, and reporting

### File Operations (NEW - v2.0)
- 📤 **Modern Upload API** - Single map arguments matching GraphQL schema
- 📥 **Streaming Downloads** - Memory-efficient downloads with progress tracking
- 📁 **Complete Folder Support** - Create, list, delete, and manage folder hierarchies
- 🔧 **Client UUID Control** - Pre-generate UUIDs for deterministic file management
- 🚀 **S3 Integration** - 3-step upload protocol (request → upload → confirm)
- 📊 **Progress Callbacks** - Track upload/download progress in real-time
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

## 📁 File Operations (v2.0 - Modernized)

The modernized Python client provides comprehensive file and folder operations that match the Babashka and Node.js implementations.

### Key Concepts

- **Single Map Arguments** - All functions use single dict arguments that mirror GraphQL schema
- **Client UUID Control** - You generate and manage UUIDs for deterministic operations
- **Modern GraphQL Filtering** - Uses relationship filtering instead of broken WHERE patterns
- **3-Step Upload Protocol** - Request URL → S3 Upload → Confirm
- **Complete Folder Support** - Full hierarchy management

### Constants

```python
# Root folder for file operations
print(eywa.ROOT_UUID)    # "87ce50d8-5dfa-4008-a265-053e727ab793"
print(eywa.ROOT_FOLDER)  # {"euuid": "87ce50d8-5dfa-4008-a265-053e727ab793"}
```

### Upload Operations

#### Upload Content from Memory

```python
import uuid

# Upload string content with client UUID
file_uuid = str(uuid.uuid4())

await eywa.upload_content("Hello EYWA!", {
    "name": "greeting.txt",
    "euuid": file_uuid,  # Client controls UUID
    "folder": {"euuid": folder_uuid},
    "content_type": "text/plain"
})

# Upload JSON data
import json
data = {"message": "Hello", "timestamp": "2024-01-01"}

await eywa.upload_content(json.dumps(data), {
    "name": "data.json", 
    "euuid": str(uuid.uuid4()),
    "content_type": "application/json"
})
```

#### Upload File from Disk

```python
# Upload with progress tracking
def progress_callback(current, total):
    percentage = (current / total) * 100
    print(f"Upload: {percentage:.1f}% ({current}/{total} bytes)")

await eywa.upload("local_file.pdf", {
    "name": "document.pdf",
    "euuid": str(uuid.uuid4()),
    "folder": {"euuid": reports_folder_uuid},
    "progress_fn": progress_callback
})
```

#### Upload from Stream

```python
# Upload from async iterator
async def data_generator():
    for i in range(1000):
        yield f"Line {i}\n".encode()

await eywa.upload_stream(data_generator(), {
    "name": "generated.txt",
    "size": 8000,  # Must calculate size beforehand
    "euuid": str(uuid.uuid4())
})
```

### Download Operations

#### Download to Memory

```python
# Download with progress tracking
def download_progress(current, total):
    print(f"Downloaded: {current}/{total} bytes")

content = await eywa.download(file_uuid, progress_fn=download_progress)
text = content.decode('utf-8')
```

#### Download to File

```python
# Download and save to disk
saved_path = await eywa.download(file_uuid, save_path="local_copy.txt")
print(f"File saved to: {saved_path}")
```

#### Stream Download (Memory Efficient)

```python
# For large files - process in chunks
stream_result = await eywa.download_stream(file_uuid)

with open("large_file.dat", "wb") as f:
    async for chunk in stream_result["stream"]:
        f.write(chunk)
```

### File Management

#### List Files with Modern Filtering

```python
# List all files
files = await eywa.list_files({})

# List files by folder UUID (modern GraphQL pattern)
folder_files = await eywa.list_files({
    "folder": {"euuid": folder_uuid}
})

# List files by folder path
path_files = await eywa.list_files({
    "folder": {"path": "/documents/reports"}
})

# Combined filters
filtered_files = await eywa.list_files({
    "folder": {"euuid": folder_uuid},
    "name": "report",  # SQL LIKE pattern
    "status": "UPLOADED",
    "limit": 10
})
```

#### File Information

```python
# Get detailed file info
file_info = await eywa.file_info(file_uuid)
if file_info:
    print(f"Name: {file_info['name']}")
    print(f"Size: {file_info['size']} bytes")
    print(f"Path: {file_info['folder']['path']}")
    print(f"Uploaded: {file_info['uploaded_at']}")
```

#### Delete Files

```python
# Delete file
success = await eywa.delete_file(file_uuid)
if success:
    print("File deleted successfully")
```

### Folder Operations

#### Create Folders

```python
# Create folder in root
folder = await eywa.create_folder({
    "name": "my-documents",
    "euuid": str(uuid.uuid4()),
    "parent": {"euuid": eywa.ROOT_UUID}
})

# Create subfolder
subfolder = await eywa.create_folder({
    "name": "reports",
    "euuid": str(uuid.uuid4()),
    "parent": {"euuid": folder["euuid"]}
})

print(f"Created: {subfolder['path']}")
```

#### List Folders

```python
# List all folders
folders = await eywa.list_folders({})

# List root folders only
root_folders = await eywa.list_folders({"parent": None})

# List subfolders by parent UUID
subfolders = await eywa.list_folders({
    "parent": {"euuid": parent_folder_uuid}
})

# List folders with name pattern
report_folders = await eywa.list_folders({"name": "report"})
```

#### Folder Information

```python
# Get folder by UUID
folder = await eywa.get_folder_info({"euuid": folder_uuid})

# Get folder by path
folder = await eywa.get_folder_info({"path": "/documents/reports"})

if folder:
    print(f"Folder: {folder['name']} -> {folder['path']}")
```

#### Delete Folders

```python
# Delete empty folder
success = await eywa.delete_folder(folder_uuid)
if not success:
    print("Folder deletion failed - may contain files")
```

### Complete Example

```python
import asyncio
import eywa
import uuid
import json

async def file_operations_example():
    eywa.open_pipe()
    
    try:
        # Create folder structure
        project_uuid = str(uuid.uuid4())
        project_folder = await eywa.create_folder({
            "name": "my-project",
            "euuid": project_uuid,
            "parent": {"euuid": eywa.ROOT_UUID}
        })
        
        # Upload files
        readme_uuid = str(uuid.uuid4())
        await eywa.upload_content("# My Project\nThis is a demo", {
            "name": "README.md",
            "euuid": readme_uuid,
            "folder": {"euuid": project_uuid},
            "content_type": "text/markdown"
        })
        
        # Upload JSON config
        config_data = {"version": "1.0", "debug": True}
        config_uuid = str(uuid.uuid4())
        await eywa.upload_content(json.dumps(config_data), {
            "name": "config.json",
            "euuid": config_uuid,
            "folder": {"euuid": project_uuid},
            "content_type": "application/json"
        })
        
        # List project files
        project_files = await eywa.list_files({
            "folder": {"euuid": project_uuid}
        })
        
        print(f"Project files ({len(project_files)}):")
        for file in project_files:
            print(f"  - {file['name']} ({file['size']} bytes)")
        
        # Download and verify
        config_content = await eywa.download(config_uuid)
        config_json = json.loads(config_content.decode('utf-8'))
        print(f"Config version: {config_json['version']}")
        
        eywa.info("File operations completed successfully")
        eywa.close_task(eywa.SUCCESS)
        
    except Exception as e:
        eywa.error(f"File operations failed: {e}")
        eywa.close_task(eywa.ERROR)

asyncio.run(file_operations_example())
```

### Error Handling

```python
try:
    await eywa.upload_content("test", {"name": "test.txt"})
except eywa.FileUploadError as e:
    print(f"Upload failed: {e}")
    print(f"Error type: {e.type}")
    if e.code:
        print(f"HTTP code: {e.code}")

try:
    content = await eywa.download("non-existent-uuid")
except eywa.FileDownloadError as e:
    print(f"Download failed: {e}")
```

### Utility Functions

```python
# Calculate file hash
hash_value = eywa.calculate_file_hash("local_file.txt", "sha256")
print(f"SHA256: {hash_value}")

# Quick operations (convenience functions)
file_uuid = await eywa.quick_upload("document.pdf")
saved_path = await eywa.quick_download(file_uuid, "downloaded.pdf")
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