import eywa


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


eywa.info('hfoiqfioq')
response = eywa.graphql({'query': query, 'variables': {'a': 10, 'b':20}})

print('Response:\n' + str(response))


# {"jsonrpc":"2.0","id":0,"result":100} 
# {"jsonrpc":"2.0","id":0,"error": {"code": -32602, "message": "Fucker"}} 
