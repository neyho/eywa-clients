#!/usr/bin/env ruby

require_relative '../lib/eywa'

def process_task
  # Get current task
  task_thread = get_task
  task = task_thread.value
  
  info("Processing task", {
    task_id: task["euuid"],
    message: task["message"]
  })
  
  # Update status
  update_task(PROCESSING)
  
  # Simulate some work
  info("Starting data processing...")
  sleep(1)
  
  # Report progress
  report("Processed 50% of data", {
    progress: 0.5,
    items_processed: 50,
    items_total: 100
  })
  
  # More work
  sleep(1)
  
  # Example GraphQL query
  result_thread = graphql('
    {
      searchUser(_limit: 5, _where: {active: {_eq: true}}) {
        euuid
        name
        type
      }
    }
  ')
  
  result = result_thread.value
  users = result.dig("data", "searchUser") || []
  info("Found active users", { count: users.length })
  
  # Final report
  report("Task completed successfully", {
    items_processed: 100,
    duration_seconds: 2,
    status: "complete"
  })
rescue => e
  error("Task processing failed", { error: e.message })
  raise
end

def main
  # Initialize EYWA client
  open_pipe
  sleep(0.1)
  
  info("Robot started")
  
  begin
    process_task
    info("All operations completed")
    close_task(SUCCESS)
  rescue => e
    error("Task failed", {
      error: e.message,
      type: e.class.name
    })
    close_task(ERROR)
  end
end

# Run the robot
main
