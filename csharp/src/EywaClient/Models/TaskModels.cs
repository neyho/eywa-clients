using System.Text.Json.Serialization;

namespace EywaClient.Models;

/// <summary>
/// Task status values for EYWA robots.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Task completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Task failed with an error.
    /// </summary>
    Error,

    /// <summary>
    /// Task is currently processing.
    /// </summary>
    Processing,

    /// <summary>
    /// Task encountered an exception.
    /// </summary>
    Exception
}

/// <summary>
/// Log event levels.
/// </summary>
public enum LogEvent
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning message.
    /// </summary>
    Warn,

    /// <summary>
    /// Error message.
    /// </summary>
    Error,

    /// <summary>
    /// Debug message.
    /// </summary>
    Debug,

    /// <summary>
    /// Trace message (most verbose).
    /// </summary>
    Trace,

    /// <summary>
    /// Exception message.
    /// </summary>
    Exception
}

/// <summary>
/// Represents information about an EYWA task.
/// </summary>
public class TaskInfo
{
    /// <summary>
    /// The task UUID.
    /// </summary>
    [JsonPropertyName("euuid")]
    public string? Euuid { get; set; }

    /// <summary>
    /// The task message or description.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// The current task status.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Additional task data.
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

/// <summary>
/// Represents a log record for task logging.
/// </summary>
public class LogRecord
{
    /// <summary>
    /// The log event type. Defaults to INFO.
    /// </summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = "INFO";

    /// <summary>
    /// The log message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional structured data to log. Optional.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    /// <summary>
    /// Duration in milliseconds. Optional.
    /// </summary>
    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Duration { get; set; }

    /// <summary>
    /// Coordinates or location information. Optional.
    /// </summary>
    [JsonPropertyName("coordinates")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Coordinates { get; set; }

    /// <summary>
    /// The timestamp of the log entry. Defaults to current time.
    /// </summary>
    [JsonPropertyName("time")]
    public DateTime Time { get; set; } = DateTime.Now;
}
