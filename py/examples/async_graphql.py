#!/usr/bin/env python3
"""
EYWA Async GraphQL Demo

Demonstrates comprehensive GraphQL operations with EYWA:
- Async GraphQL queries and mutations
- User management operations
- Task management queries
- Error handling patterns
- Structured data operations

Usage: eywa run -c "python examples/async_graphql.py"
"""

import sys
import os

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa
import asyncio
import json

async def search_tasks():
    """Search for tasks in the system"""
    return await eywa.graphql("""{
        searchTask(_limit: 10, _order_by: {created_at: desc}) {
            euuid
            status
            finished
            started
            message
            type
            priority
        }
    }""")

async def search_users():
    """Search for users in the system"""
    return await eywa.graphql("""{
        searchUser(_limit: 10) {
            euuid
            name
            type
            email
            created_at
        }
    }""")

async def get_user_by_name(name: str):
    """Get a specific user by name"""
    return await eywa.graphql("""
        query GetUser($name: String!) {
            searchUser(_where: {name: {_eq: $name}}) {
                euuid
                name
                type
                email
                groups {
                    euuid
                    name
                }
                roles {
                    euuid
                    name
                }
            }
        }
    """, {"name": name})

async def create_test_user(username: str, password: str):
    """Create a test user"""
    return await eywa.graphql(
        """
        mutation CreateUser($user: UserInput!) {
            syncUser(data: $user) {
                euuid
                name
                type
                email
                created_at
            }
        }
        """,
        {
            "user": {
                "name": username,
                "password": password,
                "type": "HUMAN",
                "email": f"{username}@example.com"
            }
        }
    )

async def demo_user_operations():
    """Demonstrate user management operations"""
    eywa.info("🔍 DEMO: User Operations")
    
    # Search existing users
    try:
        users_result = await search_users()
        users = users_result.get("data", {}).get("searchUser", [])
        eywa.info(f"Found {len(users)} users in system")
        
        for user in users[:3]:  # Show first 3
            eywa.info(f"  - {user['name']} ({user['type']})")
            
    except Exception as e:
        eywa.error(f"Failed to search users: {e}")

    # Create a test user (only if it doesn't exist)
    test_username = "demo-user"
    try:
        existing_user = await get_user_by_name(test_username)
        existing_users = existing_user.get("data", {}).get("searchUser", [])
        
        if not existing_users:
            eywa.info(f"Creating test user: {test_username}")
            create_result = await create_test_user(test_username, "demo-password")
            if create_result.get("data", {}).get("syncUser"):
                eywa.info(f"✅ Created user: {test_username}")
            else:
                eywa.warn("Failed to create test user")
        else:
            eywa.info(f"Test user {test_username} already exists")
            
    except Exception as e:
        eywa.error(f"User creation failed: {e}")

async def demo_task_operations():
    """Demonstrate task management operations"""
    eywa.info("📋 DEMO: Task Operations")
    
    try:
        tasks_result = await search_tasks()
        tasks = tasks_result.get("data", {}).get("searchTask", [])
        
        eywa.info(f"Found {len(tasks)} recent tasks")
        
        # Analyze task statistics
        status_counts = {}
        for task in tasks:
            status = task["status"]
            status_counts[status] = status_counts.get(status, 0) + 1
        
        eywa.info("Task status breakdown:")
        for status, count in status_counts.items():
            eywa.info(f"  - {status}: {count}")
            
        # Show recent tasks
        if tasks:
            eywa.info("Recent tasks:")
            for task in tasks[:5]:
                message = task["message"][:50] + "..." if len(task["message"]) > 50 else task["message"]
                eywa.info(f"  - {task['status']}: {message}")
                
    except Exception as e:
        eywa.error(f"Failed to search tasks: {e}")

async def demo_error_handling():
    """Demonstrate GraphQL error handling"""
    eywa.info("⚠️ DEMO: Error Handling")
    
    # Test malformed query
    try:
        bad_result = await eywa.graphql("{ badQuery }")
        eywa.warn("Expected error but got result")
    except Exception as e:
        eywa.info(f"✅ Correctly caught GraphQL error: {type(e).__name__}")
    
    # Test query with invalid variables
    try:
        bad_vars_result = await eywa.graphql("""
            query GetUser($invalidVar: NonExistentType!) {
                searchUser(_where: {euuid: {_eq: $invalidVar}}) {
                    name
                }
            }
        """, {"invalidVar": "not-a-uuid"})
        
        errors = bad_vars_result.get("errors", [])
        if errors:
            eywa.info(f"✅ GraphQL returned errors as expected: {len(errors)} error(s)")
        else:
            eywa.warn("Expected GraphQL errors but got none")
            
    except Exception as e:
        eywa.info(f"✅ Correctly caught variable error: {type(e).__name__}")

async def demo_concurrent_operations():
    """Demonstrate concurrent GraphQL operations"""
    eywa.info("🔄 DEMO: Concurrent Operations")
    
    try:
        # Run multiple queries concurrently
        eywa.info("Running concurrent queries...")
        results = await asyncio.gather(
            search_tasks(),
            search_users(),
            return_exceptions=True
        )
        
        tasks_result, users_result = results
        
        if isinstance(tasks_result, Exception):
            eywa.error(f"Tasks query failed: {tasks_result}")
        else:
            tasks_count = len(tasks_result.get("data", {}).get("searchTask", []))
            eywa.info(f"✅ Concurrent tasks query: {tasks_count} results")
        
        if isinstance(users_result, Exception):
            eywa.error(f"Users query failed: {users_result}")
        else:
            users_count = len(users_result.get("data", {}).get("searchUser", []))
            eywa.info(f"✅ Concurrent users query: {users_count} results")
            
    except Exception as e:
        eywa.error(f"Concurrent operations failed: {e}")

async def main():
    eywa.open_pipe()
    
    try:
        eywa.info("🚀 EYWA Async GraphQL Demo")
        eywa.info("=" * 40)
        
        # Run all demo sections
        await demo_user_operations()
        await demo_task_operations()
        await demo_concurrent_operations()
        await demo_error_handling()
        
        eywa.info("\n🎉 GraphQL demo completed successfully!")
        eywa.close_task(eywa.SUCCESS)
        
    except Exception as e:
        eywa.error(f"💥 Demo failed: {e}")
        import traceback
        eywa.debug(traceback.format_exc())
        eywa.close_task(eywa.ERROR)

if __name__ == "__main__":
    asyncio.run(main())
