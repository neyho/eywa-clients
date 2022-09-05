import eywa

task=eywa.Task()

query = """
{
    searchUser (name:{_ilike:}) {
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
