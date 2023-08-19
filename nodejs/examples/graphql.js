import eywa from 'eywa-reacher-client'


let query = ` {
    searchUser {
        euuid
        name
        type
        modified_on
        modified_by {
            name
        }
    }
  }`


let execute = async() => {
    eywa.open_pipe()
    eywa.info('Sending GraphQL query to EYWA')
    let response =  await eywa.graphql(query)
    console.log('Success!!!')
    console.log(response)
    process.exit(0)
}

execute()
