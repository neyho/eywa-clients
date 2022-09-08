import sys
import json

query = """
{
    searchUser {
        euuid
        name
        type
        modified_on
        modified_by {
            name
        }
    }
}"""


print(str(json.dumps({
    'jsonrpc': "2.0",
    'id':0,
    'method': 'eywa.datasets.graphql',
    'params': {
        'query': query,
        'variables': {
            'a': 10,
            'b':20
            }
        }})) +
    "\n")


print(sys.stdin.readline())


# {"jsonrpc":"2.0","id":0,"result":100} 
# {"jsonrpc":"2.0","id":0,"error": {"code": -32602, "message": "Fucker"}} 
