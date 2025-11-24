#!/usr/bin/env python3
"""
EYWA Raw GraphQL Example

Demonstrates raw GraphQL operations without using the reacher library.
Shows direct JSON-RPC communication with EYWA's GraphQL endpoint.

Usage: eywa run -c "python -m examples.raw_graphql"
"""

import sys
import os
import json

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa
import asyncio

async def raw_graphql_query(query: str, variables: dict = None):
    """
    Execute a raw GraphQL query using direct JSON-RPC call
    """
    result = await eywa.graphql(query, variables)
    return result

async def main():
    eywa.open_pipe()

    try:
        eywa.info("🚀 EYWA Raw GraphQL Example")
        eywa.update_task(status=eywa.PROCESSING)

        # Example 1: Simple query without variables
        eywa.info("Example 1: Simple query for users")
        simple_query = """
        {
            searchUser(_limit: 3) {
                euuid
                name
                type
            }
        }
        """

        result1 = await raw_graphql_query(simple_query)
        users = result1.get("data", {}).get("searchUser", [])
        eywa.info(f"Retrieved {len(users)} users")
        for user in users:
            eywa.info(f"  - {user['name']} ({user['type']})")

        # Example 2: Query with variables
        eywa.info("Example 2: Query with variables")
        query_with_vars = """
        query GetUserByType($userType: String!, $limit: Int!) {
            searchUser(_where: {type: {_eq: $userType}}, _limit: $limit) {
                euuid
                name
                type
                email
            }
        }
        """

        variables = {
            "userType": "HUMAN",
            "limit": 5
        }

        result2 = await raw_graphql_query(query_with_vars, variables)
        typed_users = result2.get("data", {}).get("searchUser", [])
        eywa.info(f"Found {len(typed_users)} HUMAN type users")

        # Example 3: Task query
        eywa.info("Example 3: Querying tasks")
        task_query = """
        {
            searchTask(_limit: 3, _order_by: {created_at: desc}) {
                euuid
                status
                type
                message
            }
        }
        """

        result3 = await raw_graphql_query(task_query)
        tasks = result3.get("data", {}).get("searchTask", [])
        eywa.info(f"Retrieved {len(tasks)} recent tasks")

        # Generate comprehensive report
        eywa.report(
            "Raw GraphQL Operations Complete",
            {
                "card": f"""# Raw GraphQL Example Results

## Operations Executed

### 1. Simple User Query
- Retrieved {len(users)} users
- No variables used
- Direct JSON-RPC communication

### 2. Parameterized Query
- Filter: User type = HUMAN
- Limit: 5 results
- Found {len(typed_users)} matching users
- Used GraphQL variables

### 3. Task Query
- Retrieved {len(tasks)} recent tasks
- Ordered by creation date (descending)
- Raw response processing

## Technical Details
✅ All queries executed successfully
✅ Raw JSON-RPC protocol used
✅ No high-level reacher library dependencies
✅ Direct EYWA dataset access

> This example demonstrates low-level GraphQL access patterns
""",
                "SimpleUsers": eywa.create_table(
                    headers=["Name", "Type"],
                    rows=[[u["name"], u["type"]] for u in users]
                ),
                "TypedUsers": eywa.create_table(
                    headers=["Name", "Email", "Type"],
                    rows=[[u["name"], u.get("email", "N/A"), u["type"]] for u in typed_users]
                ),
                "RecentTasks": eywa.create_table(
                    headers=["Status", "Type", "Message"],
                    rows=[[t["status"], t["type"], t["message"][:40] + "..." if len(t["message"]) > 40 else t["message"]] for t in tasks]
                )
            }
        )

        eywa.info("✅ Raw GraphQL example completed successfully!")
        eywa.close_task(eywa.SUCCESS)

    except Exception as e:
        eywa.error(f"❌ Raw GraphQL example failed: {e}")
        import traceback
        eywa.debug(traceback.format_exc())
        eywa.close_task(eywa.ERROR)

if __name__ == "__main__":
    asyncio.run(main())
