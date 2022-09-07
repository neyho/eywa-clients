import eywa

task=eywa.Task()

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


print(eywa.rpc.watchdog)

task.info('hfoiqfioq')
response = eywa.graphql({'query': query, 'variables': {'a': 10, 'b':20}})

print('Response:\n' + response)


# {"jsonrpc":"2.0","id":0,"result":100} 
# {"jsonrpc":"2.0","id":0,"error": {"code": -32602, "message": "Fucker"}} 
