#!/usr/bin/env ruby

require_relative '../lib/eywa'

def demonstrate_features
  open_pipe
  
  info("EYWA Ruby Client - Feature Demonstration")
  
  # Demonstrate all logging levels
  info("=== Logging Levels ===")
  info("Info message", { level: "info" })
  warn("Warning message", { level: "warn" })
  error("Error message (not real)", { level: "error" })
  debug("Debug message", { level: "debug" })
  trace("Trace message", { level: "trace" })
  exception("Exception message", { level: "exception" }) # NEW!
  
  # Custom log with all parameters
  log(
    event: "INFO",
    message: "Custom log entry",
    data: { custom: true, ruby_version: RUBY_VERSION },
    duration: 1500,
    coordinates: { lat: 45.5017, lon: -73.5673 },
    time: Time.now
  )
  
  # Demonstrate reporting
  info("=== Report Function ===")
  report("Processing started") # Simple report
  report("Progress update", { completed: 25, total: 100 }) # With data
  report("Chart generated", { type: "bar-chart" }, "data:image/png;base64,iVBORw0KG...") # With image
  
  # Task management
  info("=== Task Management ===")
  info("Task status constants", {
    SUCCESS: SUCCESS,
    ERROR: ERROR,
    PROCESSING: PROCESSING,
    EXCEPTION: EXCEPTION
  })
  
  update_task(PROCESSING)
  
  # Try to get task
  task_thread = get_task
  begin
    task = task_thread.value
    info("Current task", { task: task })
  rescue => e
    warn("No task context", { error: e.message })
  end
  
  # GraphQL examples
  info("=== GraphQL Examples ===")
  
  # Simple query
  thread1 = graphql('{ searchUser(_limit: 1) { name type } }')
  begin
    result1 = thread1.value
    info("Simple GraphQL query", { result: result1 })
  rescue => e
    error("Simple query failed", { error: e.message })
  end
  
  # Query with variables
  thread2 = graphql(
    'query($limit: Int) { searchUser(_limit: $limit) { name } }',
    { limit: 2 }
  )
  begin
    result2 = thread2.value
    info("GraphQL with variables", { result: result2 })
  rescue => e
    error("Query with variables failed", { error: e.message })
  end
  
  # Handler registration
  info("=== Handler Registration ===")
  register_handler("custom.event") do |request|
    info("Received custom event", request["params"])
  end
  
  # Wait for async operations
  sleep(2)
  
  info("Demo completed!")
  close_task(SUCCESS)
end

# Run the demonstration
demonstrate_features
