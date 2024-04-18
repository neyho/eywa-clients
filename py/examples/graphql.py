import eywa
from datetime import datetime


query = """
mutation($example:TaskInput!) {
  syncTask(task:$example) {
    euuid
  }
}
"""


response = eywa.graphql({'query': query, 'variables': {
    "example": {
        "euuid": "ff78873b-15dc-43e1-b845-93064bdeccc1",
        "message": "Testing Python reacher client",
        "data": {"a": 100,
                 "drvo": "hrast",
                 "kamen": "bacim"},
        "started": datetime(2000, 2, 3, 4, 5, 6).isoformat()
        }
    }}, 2)


print('Response:\n' + str(response))

eywa.close()
