#!/usr/bin/env ruby

require_relative '../lib/eywa'

def test_client
  puts "Starting EYWA Ruby client test...\n"
  
  # Initialize the pipe
  open_pipe
  
  begin
    # Test 1: Logging functions
    info("Testing info logging", { test: "info" })
    warn("Testing warning logging", { test: "warn" })
    error("Testing error logging (not a real error)", { test: "error" })
    debug("Testing debug logging", { test: "debug" })
    trace("Testing trace logging", { test: "trace" })
    exception("Testing exception logging", { test: "exception" })
    
    # Test 2: Custom log with all parameters
    log(
      event: "INFO",
      message: "Custom log with all parameters",
      data: { custom: true },
      duration: 1234,
      coordinates: { x: 10, y: 20 },
      time: Time.now
    )
    
    # Test 3: Report
    report("Test report message", { reportData: "test" })
    
    # Test 4: Task management
    update_task(PROCESSING)
    info("Updated task status to PROCESSING")
    
    # Test 5: Get current task
    task_thread = get_task
    begin
      task = task_thread.value
      info("Retrieved task:", task)
    rescue => e
      warn("Could not get task (normal if not in task context)", { error: e.message })
    end
    
    # Test 6: GraphQL query
    info("Testing GraphQL query...")
    gql_thread = graphql('
      {
        searchUser(_limit: 2) {
          euuid
          name
          type
          active
        }
      }
    ')
    
    begin
      result = gql_thread.value
      info("GraphQL query successful", { 
        resultCount: result.dig("data", "searchUser")&.length || 0 
      })
      
      # Show first user if available
      if result["data"] && result["data"]["searchUser"] && result["data"]["searchUser"][0]
        info("First user:", result["data"]["searchUser"][0])
      end
    rescue => e
      error("GraphQL query failed", { error: e.message })
    end
    
    # Test 7: Constants
    info("Testing constants", {
      SUCCESS: SUCCESS,
      ERROR: ERROR,
      PROCESSING: PROCESSING,
      EXCEPTION: EXCEPTION
    })
    
    # Test 8: GraphQL with variables
    info("Testing GraphQL with variables...")
    query = '
      query GetUsers($limit: Int) {
        searchUser(_limit: $limit) {
          name
          type
        }
      }
    '
    variables = { limit: 1 }
    
    gql_thread2 = graphql(query, variables)
    begin
      result2 = gql_thread2.value
      info("GraphQL with variables successful", { result: result2 })
    rescue => e
      error("GraphQL with variables failed", { error: e.message })
    end
    
    # Test 9: Handler registration
    register_handler("test.echo") do |data|
      info("Received echo request", data)
    end
    
    # Give time for async operations
    sleep(2)
    
    # Test complete
    info("All tests completed successfully!")
    close_task(SUCCESS)
    
  rescue => e
    error("Test failed with unexpected error", {
      error: e.message,
      backtrace: e.backtrace[0..5]
    })
    close_task(ERROR)
  end
end

# Run the test
test_client
