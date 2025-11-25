#!/usr/bin/env python3
"""
EYWA GraphQL Example with Reacher Library

Demonstrates GraphQL operations using the EYWA client:
- Querying users and tasks
- Working with GraphQL mutations
- Handling responses and errors

Usage: eywa run -c "python -m examples.graphql"
"""

import sys
import os

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa
import asyncio

async def main():
    eywa.open_pipe()

    try:
        eywa.info("🚀 EYWA GraphQL Example with Reacher")
        eywa.update_task(status=eywa.PROCESSING)

        # Query users
        eywa.info("Querying users from dataset...")
        users_result = await eywa.graphql("""{
            searchUser(_limit: 5) {
                euuid
                name
                type
                email
                created_at
            }
        }""")

        users = users_result.get("searchUser", [])
        eywa.info(f"Found {len(users)} users")

        for user in users:
            eywa.info(f"  - {user['name']} ({user['type']}) - {user['email']}")

        # Query recent tasks
        eywa.info("Querying recent tasks...")
        tasks_result = await eywa.graphql("""{
            searchTask(_limit: 5, _order_by: {created_at: desc}) {
                euuid
                status
                message
                type
                priority
                created_at
            }
        }""")

        tasks = tasks_result.get("searchTask", [])
        eywa.info(f"Found {len(tasks)} recent tasks")

        for task in tasks:
            message = task["message"][:40] + "..." if len(task["message"]) > 40 else task["message"]
            eywa.info(f"  - [{task['status']}] {message}")

        # Generate report
        eywa.report(
            "GraphQL Query Results",
            {
                "card": f"""# GraphQL Example Complete
## Summary
- **Users Found:** {len(users)}
- **Tasks Found:** {len(tasks)}
- **Status:** Success

### Operations Performed
✅ User search query executed
✅ Task search query executed
✅ Data retrieved and processed
""",
                "Users": eywa.create_table(
                    headers=["Name", "Type", "Email"],
                    rows=[[u["name"], u["type"], u["email"]] for u in users]
                ),
                "Tasks": eywa.create_table(
                    headers=["Status", "Priority", "Message"],
                    rows=[[t["status"], str(t.get("priority", "N/A")), t["message"][:50]] for t in tasks]
                )
            }
        )

        eywa.info("✅ GraphQL example completed successfully!")
        eywa.close_task(eywa.SUCCESS)

    except Exception as e:
        eywa.error(f"❌ GraphQL example failed: {e}")
        import traceback
        eywa.debug(traceback.format_exc())
        eywa.close_task(eywa.ERROR)

if __name__ == "__main__":
    asyncio.run(main())
