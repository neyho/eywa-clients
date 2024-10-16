import asyncio
import sys
sys.path.insert(1, ".")

import async_client as eywa



async def search_tasks():
    return await eywa.graphql({"query": """{
    searchTask (_limit:2000) {
        euuid
        status
        finished
        started
    }}""", "variables":{} })


async def search_users():    
    return await eywa.graphql({"query":"""{
    searchUser (_limit:2000) {
      euuid
      name
      type      
    }
    }""",
    "variables": {}})


async def main():
    eywa.open_pipe()
    
    query = """{
    searchTask (_limit:2000) {
        euuid
        status
        finished
        started
    }}
    """

    eywa.info("INFO message")

    result = await eywa.graphql({"query": query, "variables":{}})
    print(result, end="\n")

    eywa.report("REPORT MESSAGE")

    task = await eywa.get_task()
    print(task, end="\n")
    
    result = await asyncio.gather(search_tasks(), search_users())
    print(result)
    print("\n")


asyncio.run(main())