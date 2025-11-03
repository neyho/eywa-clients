using EywaClient.Models;

namespace EywaClient.Core;

/// <summary>
/// Manages EYWA task lifecycle operations.
/// </summary>
public class TaskManager
{
    private readonly JsonRpcClient _rpcClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskManager"/> class.
    /// </summary>
    /// <param name="rpcClient">The JSON-RPC client.</param>
    public TaskManager(JsonRpcClient rpcClient)
    {
        _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
    }

    /// <summary>
    /// Gets the current task information from the EYWA server.
    /// </summary>
    /// <returns>The task information.</returns>
    /// <example>
    /// <code>
    /// var task = await taskManager.GetTaskAsync();
    /// Console.WriteLine($"Task: {task.Message}");
    /// Console.WriteLine($"Status: {task.Status}");
    /// </code>
    /// </example>
    public async Task<TaskInfo> GetTaskAsync()
    {
        var result = await _rpcClient.SendRequestAsync<TaskInfo>("task.get", null)
            .ConfigureAwait(false);
        
        return result ?? new TaskInfo();
    }

    /// <summary>
    /// Updates the current task status.
    /// </summary>
    /// <param name="status">The new task status.</param>
    /// <example>
    /// <code>
    /// taskManager.UpdateTask(TaskStatus.Processing);
    /// // Do work...
    /// taskManager.UpdateTask(TaskStatus.Success);
    /// </code>
    /// </example>
    public void UpdateTask(Models.TaskStatus status)
    {
        _rpcClient.SendNotification("task.update", new
        {
            status = status.ToString().ToUpperInvariant()
        });
    }

    /// <summary>
    /// Closes the current task with a final status and exits the process.
    /// </summary>
    /// <param name="status">The final task status (default: Success).</param>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     // Do work...
    ///     taskManager.CloseTask(TaskStatus.Success);
    /// }
    /// catch (Exception ex)
    /// {
    ///     Console.Error.WriteLine($"Error: {ex.Message}");
    ///     taskManager.CloseTask(TaskStatus.Error);
    /// }
    /// </code>
    /// </example>
    public void CloseTask(Models.TaskStatus status = Models.TaskStatus.Success)
    {
        _rpcClient.SendNotification("task.close", new
        {
            status = status.ToString().ToUpperInvariant()
        });

        // Exit with appropriate code
        var exitCode = status == Models.TaskStatus.Success ? 0 : 1;
        Environment.Exit(exitCode);
    }

    /// <summary>
    /// Returns control to EYWA without closing the task.
    /// Exits the process with code 0.
    /// </summary>
    /// <example>
    /// <code>
    /// // Hand back control to EYWA for later continuation
    /// taskManager.ReturnTask();
    /// </code>
    /// </example>
    public void ReturnTask()
    {
        _rpcClient.SendNotification("task.return", null);
        Environment.Exit(0);
    }
}