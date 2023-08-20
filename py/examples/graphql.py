import eywa
import datetime


query = """
mutation($example:TaskInput!) {
  syncTask(task:$example) {
    euuid
  }
}
"""


response = eywa.graphql({'query': query, 'variables': {
    "example": {
        "euuid":"5653056c-ef73-4acf-ab03-45cb83d102eb",
        "message":"Testing Python reacher client",
        #"started": datetime.datetime(2000,2,3,4,5,6).isoformat()
        "started": datetime.datetime.utcnow().isoformat()
        }
    }},2)

print('Response:\n' + str(response))

eywa.close();

# {"jsonrpc":"2.0","id":0,"result":100} 
# {"jsonrpc":"2.0","id":0,"error": {"code": -32602, "messagjkkkjjjjkhhjjkdioqje": "Fucker"}} 
