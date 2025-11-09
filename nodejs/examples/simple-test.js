#!/usr/bin/env node

/**
 * Simple GraphQL test with the new module
 */

import eywa from '../src/index.js'

eywa.open_pipe()

async function main() {
  try {
    eywa.info('Testing GraphQL with new module')
    
    // Simple GraphQL query test
    const query = `{
      searchTask(_limit: 5) {
        euuid
        message
        assignee {
          name
        }
      }
    }`
    
    eywa.info('Sending GraphQL query to EYWA')
    const response = await eywa.graphql(query)
    console.log('Response:', JSON.stringify(response, null, 2))
    
    eywa.close_task(eywa.SUCCESS)
    
  } catch (error) {
    eywa.error('Test failed', { message: error.message })
    console.error(error.stack)
    eywa.close_task(eywa.ERROR)
  }
}

main()
