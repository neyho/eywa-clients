using EywaClient.Models;

namespace EywaClient.Core;

/// <summary>
/// Provides logging functionality for EYWA robots.
/// </summary>
public class Logger
{
    private readonly JsonRpcClient _rpcClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class.
    /// </summary>
    /// <param name="rpcClient">The JSON-RPC client.</param>
    public Logger(JsonRpcClient rpcClient)
    {
        _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
    }

    /// <summary>
    /// Logs a message with full control over all parameters.
    /// </summary>
    /// <param name="record">The log record containing event type, message, and optional metadata.</param>
    /// <example>
    /// <code>
    /// logger.Log(new LogRecord
    /// {
    ///     Event = "INFO",
    ///     Message = "Processing item",
    ///     Data = new { itemId = 123 },
    ///     Duration = 1500
    /// });
    /// </code>
    /// </example>
    public void Log(LogRecord record)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));

        _rpcClient.SendNotification("task.log", new
        {
            time = record.Time,
            @event = record.Event,
            message = record.Message,
            data = record.Data,
            coordinates = record.Coordinates,
            duration = record.Duration
        });
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="data">Optional structured data to include.</param>
    /// <example>
    /// <code>
    /// logger.Info("User logged in", new { userId = "abc123" });
    /// </code>
    /// </example>
    public void Info(string message, object? data = null)
    {
        Log(new LogRecord
        {
            Event = "INFO",
            Message = message,
            Data = data
        });
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="data">Optional error details or context.</param>
    /// <example>
    /// <code>
    /// logger.Error("Failed to process file", new 
    /// { 
    ///     filename = "data.csv", 
    ///     error = ex.Message 
    /// });
    /// </code>
    /// </example>
    public void Error(string message, object? data = null)
    {
        Log(new LogRecord
        {
            Event = "ERROR",
            Message = message,
            Data = data
        });
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    /// <param name="data">Optional warning context.</param>
    /// <example>
    /// <code>
    /// logger.Warn("API rate limit approaching", new { remaining = 10 });
    /// </code>
    /// </example>
    public void Warn(string message, object? data = null)
    {
        Log(new LogRecord
        {
            Event = "WARN",
            Message = message,
            Data = data
        });
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    /// <param name="data">Optional debug data.</param>
    /// <example>
    /// <code>
    /// logger.Debug("Cache hit", new { key = "user:123" });
    /// </code>
    /// </example>
    public void Debug(string message, object? data = null)
    {
        Log(new LogRecord
        {
            Event = "DEBUG",
            Message = message,
            Data = data
        });
    }

    /// <summary>
    /// Logs a trace message (most verbose level).
    /// </summary>
    /// <param name="message">The trace message to log.</param>
    /// <param name="data">Optional trace data.</param>
    /// <example>
    /// <code>
    /// logger.Trace("Entering function processData", new { args = new[] {1, 2, 3} });
    /// </code>
    /// </example>
    public void Trace(string message, object? data = null)
    {
        Log(new LogRecord
        {
            Event = "TRACE",
            Message = message,
            Data = data
        });
    }

    /// <summary>
    /// Logs an exception message.
    /// </summary>
    /// <param name="message">The exception message to log.</param>
    /// <param name="data">Optional exception details.</param>
    /// <example>
    /// <code>
    /// logger.Exception("Unhandled error in worker", new { stack = ex.StackTrace });
    /// </code>
    /// </example>
    public void Exception(string message, object? data = null)
    {
        Log(new LogRecord
        {
            Event = "EXCEPTION",
            Message = message,
            Data = data
        });
    }

    /// <summary>
    /// Sends a task report with optional data and image.
    /// </summary>
    /// <param name="message">The report message.</param>
    /// <param name="data">Optional structured data for the report.</param>
    /// <param name="image">Optional image data (base64 encoded or URL).</param>
    /// <example>
    /// <code>
    /// logger.Report("Analysis complete", new 
    /// { 
    ///     accuracy = 0.95,
    ///     processed = 1000
    /// }, 
    /// chartImageBase64);
    /// </code>
    /// </example>
    public void Report(string message, object? data = null, string? image = null)
    {
        _rpcClient.SendNotification("task.report", new
        {
            message,
            data,
            image
        });
    }
}
