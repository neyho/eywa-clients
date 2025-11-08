import sys
import os

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import eywa
import asyncio


async def search_tasks():
    return await eywa.graphql("""{
    searchTask (_limit:2000) {
      euuid
      status
      finished
      started
    }
    }""")


async def search_users():
    return await eywa.graphql("""{
    searchUser (_limit:2000) {
      euuid
      name
      type      
    }
    }""")


async def add_user(username, password):
    return await eywa.graphql(
        """
    mutation($user:UserInput) {
        syncUser(data:$user) {
            euuid
            name
            password
        }
    }
    """,
        {"user": {"name": username, "password": password}},
    )


async def main():
    eywa.open_pipe()
    await add_user("marijan", "komunista")

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

    print(await eywa.graphql("Bad Query"))

    print(f"Exiting!")
    eywa.exit()


asyncio.run(main())

