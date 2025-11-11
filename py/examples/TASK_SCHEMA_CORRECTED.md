# Task File Requirements - CORRECTED VERSION

For backend persistence, your task file must include a valid UUID and should only use fields that exist in the Task entity schema.

## Task Entity Schema (Task Workflow 1.0.3)

**Valid Task Entity Fields:**
Based on the actual EYWA Task entity from Task Workflow model 1.0.3:

- `euuid` (string, unique) - Task identifier (required for persistence) 
- `message` (string, optional) - Task message or status description
- `status` (enum, optional) - UNKNOWN, QUEUED, PROCESSING, SUCCESS, ERROR, CANCELED, EXCEPTION
- `type` (enum, optional) - TEST, PROCESS, USER_ACTION, ROBOT
- `data` (json, optional) - **Primary field for robot input data**
- `priority` (int, optional) - Task priority level
- `exception` (string, optional) - Exception details
- `decision` (string, optional) - Task decision or outcome
- `started` (timestamp, optional) - Task start time
- `finished` (timestamp, optional) - Task completion time
- `created_by` (user, optional) - Creator user reference
- `created_on` (timestamp, optional) - Creation timestamp
- `assignee` (user, optional) - Assigned user reference
- `assignee_group` (group, optional) - Assigned group reference
- `assigned_by` (user, optional) - User who assigned the task
- `assigned_on` (timestamp, optional) - Assignment timestamp
- `resolved_by` (user, optional) - User who resolved the task
- `resolved_on` (timestamp, optional) - Resolution timestamp

## Task File Examples

**Minimum Required for Persistence:**
```json
{
  "euuid": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Robot Task with Input Data:**
```json
{
  "euuid": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Processing user data in batches",
  "type": "ROBOT",
  "priority": 1,
  "data": {
    "targetModel": "User",
    "batchSize": 50,
    "config": {
      "dryRun": false,
      "validateData": true
    }
  }
}
```

**Simple Input Data Only:**
```json
{
  "euuid": "550e8400-e29b-41d4-a716-446655440000",
  "data": {
    "targetModel": "User",
    "batchSize": 50
  }
}
```

## ❌ Invalid Fields - Do NOT Use

These fields do NOT exist in the Task entity schema:
- `subject` - Does not exist
- `description` - Does not exist
- `metadata` - Does not exist
- `tags` - Does not exist
- `scheduling` - Does not exist
- `department` - Does not exist
- `cost_center` - Does not exist

## ✅ Key Points

1. **`data` field is where all robot input goes** - Use this for parameters, configuration, filters, etc.
2. **`message` field is for human-readable descriptions** - Use this for task titles or status messages
3. **Only include fields that exist in the schema** - Unknown fields are ignored
4. **`euuid` is required for backend persistence** - Without it, logs only print to console

## Generate UUIDs

```bash
# Linux/macOS
uuidgen

# Python
python -c "import uuid; print(uuid.uuid4())"

# Node.js
node -e "console.log(require('crypto').randomUUID())"
```
