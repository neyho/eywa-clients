/**
 * Basic GraphQL Query
 * 
 * Shows how to make direct GraphQL queries to EYWA
 * Perfect for understanding the underlying API
 */

import eywa from '../../src/index.js';

async function main() {
  try {
    eywa.open_pipe();
    
    const task = await eywa.get_task();
    console.log('🔍 Running basic GraphQL examples...');
    
    eywa.update_task('PROCESSING');
    
    // Example 1: Simple query
    console.log('1. Simple query example:');
    const users = await eywa.graphql(`
      query {
        searchUser(_limit: 3) {
          euuid
          name
        }
      }
    `);
    console.log('Users:', users.data.searchUser);
    
    // Example 2: Query with variables
    console.log('\\n2. Query with variables:');
    const userById = await eywa.graphql(`
      query GetUser($id: String!) {
        getUser(euuid: $id) {
          euuid
          name
        }
      }
    `, { id: 'some-user-id' });
    console.log('User by ID:', userById);
    
    eywa.info('GraphQL examples completed');
    eywa.close_task('SUCCESS');
    
  } catch (error) {
    console.error('❌ GraphQL error:', error.message);
    eywa.error(`GraphQL example failed: ${error.message}`);
    eywa.close_task('ERROR');
  }
}

main();
