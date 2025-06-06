#!/usr/bin/env node
import eywa from '../src/eywa.js'

// Test script for EYWA JavaScript client
async function testClient() {
    console.log('Starting EYWA JavaScript client test...\n')
    
    // Initialize the pipe
    eywa.open_pipe()
    
    try {
        // Test 1: Logging functions
        eywa.info('Testing info logging', { test: 'info' })
        eywa.warn('Testing warning logging', { test: 'warn' })
        eywa.error('Testing error logging (not a real error)', { test: 'error' })
        eywa.debug('Testing debug logging', { test: 'debug' })
        eywa.trace('Testing trace logging', { test: 'trace' })
        eywa.exception('Testing exception logging', { test: 'exception' })
        
        // Test 2: Custom log with all parameters
        eywa.log({
            event: 'INFO',
            message: 'Custom log with all parameters',
            data: { custom: true },
            duration: 1234,
            coordinates: { x: 10, y: 20 }
        })
        
        // Test 3: Report
        eywa.report('Test report message', { reportData: 'test' })
        
        // Test 4: Task management
        eywa.update_task(eywa.PROCESSING)
        eywa.info('Updated task status to PROCESSING')
        
        // Test 5: Get current task
        try {
            const task = await eywa.get_task()
            eywa.info('Retrieved task:', task)
        } catch (err) {
            eywa.warn('Could not get task (normal if not in task context)', { error: err.message })
        }
        
        // Test 6: GraphQL query
        eywa.info('Testing GraphQL query...')
        try {
            const result = await eywa.graphql(`
                {
                    searchUser(_limit: 2) {
                        euuid
                        name
                        type
                        active
                    }
                }
            `)
            eywa.info('GraphQL query successful', { resultCount: result.data?.searchUser?.length })
            
            // Show first user if available
            if (result.data?.searchUser?.[0]) {
                eywa.info('First user:', result.data.searchUser[0])
            }
        } catch (err) {
            eywa.error('GraphQL query failed', { error: err.message || err })
        }
        
        // Test 7: Constants
        eywa.info('Testing constants', {
            SUCCESS: eywa.SUCCESS,
            ERROR: eywa.ERROR,
            PROCESSING: eywa.PROCESSING,
            EXCEPTION: eywa.EXCEPTION
        })
        
        // Test complete
        eywa.info('All tests completed successfully!')
        eywa.close_task(eywa.SUCCESS)
        
    } catch (error) {
        eywa.error('Test failed with unexpected error', { 
            error: error.message, 
            stack: error.stack 
        })
        eywa.close_task(eywa.ERROR)
    }
}

// Run the test
testClient().catch(err => {
    console.error('Unhandled error:', err)
    process.exit(1)
})
