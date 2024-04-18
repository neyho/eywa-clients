import eywa from 'eywa-reacher-client'


// let query = ` {
//     searchUser {
//         euuid
//         name
//         type
//         modified_on
//         modified_by {
//             name
//         }
//     }
//   }`


let query = `
{
  searchTask (_limit:5, id:{_neq:null}) {
    id
    description
    assignee {
      name
    }
  }
}
`





let execute = async() => {
    eywa.open_pipe()
    eywa.info('Sending GraphQL query to EYWA')
    let response =  await eywa.graphql(query)
    console.log(JSON.stringify(response, null, 2))
    process.exit(0)
}


// let mutation = `
// mutation bilosta($example:TaskInput!) {
//   syncTask(task:$example) {
//   euuid
//   }
// }`
 

// let execute = async() => {
//     eywa.open_pipe()
//     eywa.info('Sending GraphQL query to EYWA')
//     let response =  await eywa.graphql(
//         mutation,
//         {
//             example: {
//                 euuid: "ff78873b-15dc-43e1-b845-93064bdeccc1",
//                 message: "Testing Python reacher client",
//                 data: {a: 100,
//                     drvo: "hrast",
//                     kamen: "bacim"
//                 }
//             }
//         }
//     )
//     console.log(response)
//     process.exit(0)
// }

execute()
