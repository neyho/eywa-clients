/// <summary>
/// EYWA Task Manager - Simple task lifecycle management and reporting
/// 
/// Handles task status updates and structured reporting using dynamic data structures
/// to stay GraphQL-aligned.
/// </summary>

using System;
using System.Text.Json;
using EywaClient.Core;

namespace EywaClient.Core;

/// <summary>
/// EYWA execution status
/// </summary>
/// <summary>
/// Task status values for EYWA operations
/// </summary>
public enum Status
{
    /// <summary>Task completed successfully</summary>
    Success,
    /// <summary>Task failed with error</summary>
    Error,
    /// <summary>Task is currently processing</summary>
    Processing,
    /// <summary>Task encountered an exception</summary>
    Exception
}

/// <summary>
/// Data structure for task reports - supports markdown cards and named tables
/// </summary>
public class ReportData
{
    /// <summary>Markdown-formatted content for cards</summary>
    public string? Card { get; set; }
    
    /// <summary>Named tables with headers and rows</summary>
    public Dictionary<string, TableData>? Tables { get; set; }
}

/// <summary>
/// Table structure for reports
/// </summary>
public class TableData
{
    /// <summary>Column headers</summary>
    public string[] Headers { get; set; } = Array.Empty<string>();
    
    /// <summary>Data rows - each row must match headers count</summary>
    public object[][] Rows { get; set; } = Array.Empty<object[]>();
}

/// <summary>
/// Options for task reports - matches EYWA Task Report schema exactly
/// </summary>
public class ReportOptions
{
    /// <summary>Structured report data with card and tables</summary>
    public ReportData? Data { get; set; }
    
    /// <summary>Base64-encoded image data for visualizations</summary>
    public string? Image { get; set; }
}

/// <summary>
/// Simple task lifecycle manager with reporting capabilities
/// </summary>
public class TaskManager
{
    private readonly JsonRpcClient _client;
    
    /// <summary>
    /// Creates a new TaskManager instance
    /// </summary>
    /// <param name="client">The JSON-RPC client to use for task operations</param>
    public TaskManager(JsonRpcClient client)
    {
        _client = client;
    }
    
    /// <summary>
    /// Get current task information
    /// </summary>
    public async Task<Dictionary<string, object>> GetTaskAsync()
    {
        var response = await _client.SendRequestAsync("task.get");
        
        // Extract result from JSON-RPC response
        var resultObj = response.GetValueOrDefault("result");
        Dictionary<string, object> result;
        
        if (resultObj is JsonElement jsonElement)
        {
            // Convert JsonElement to Dictionary
            var resultJson = jsonElement.GetRawText();
            result = JsonSerializer.Deserialize<Dictionary<string, object>>(resultJson) ?? new Dictionary<string, object>();
        }
        else if (resultObj is Dictionary<string, object> dict)
        {
            result = dict;
        }
        else
        {
            result = new Dictionary<string, object>();
        }
        
        return result;
    }
    
    /// <summary>
    /// Update task status
    /// </summary>
    public async Task UpdateTaskAsync(Status status)
    {
        var statusString = status switch
        {
            Status.Success => "SUCCESS",
            Status.Error => "ERROR", 
            Status.Processing => "PROCESSING",
            Status.Exception => "EXCEPTION",
            _ => "PROCESSING"
        };
        
        await _client.SendNotificationAsync("task.update", new Dictionary<string, object>
        {
            ["status"] = statusString
        });
    }
    
    /// <summary>
    /// Return task (without closing)
    /// </summary>
    public async Task ReturnTaskAsync()
    {
        await _client.SendNotificationAsync("task.return");
    }
    
    /// <summary>
    /// Close task with final status
    /// </summary>
    public async Task CloseTaskAsync(Status status)
    {
        var statusString = status switch
        {
            Status.Success => "SUCCESS",
            Status.Error => "ERROR",
            Status.Processing => "PROCESSING", 
            Status.Exception => "EXCEPTION",
            _ => "SUCCESS"
        };
        
        await _client.SendNotificationAsync("task.close", new Dictionary<string, object>
        {
            ["status"] = statusString
        });
    }

    /// <summary>
    /// Create a structured task report with markdown cards, tables, and optional images
    /// Matches EYWA Task Report schema exactly: message, data, image, has_* flags
    /// </summary>
    /// <param name="message">Report title/description</param>
    /// <param name="options">Optional report data including card content, tables, and images</param>
    /// <returns>Created report data from GraphQL response</returns>
    /// <exception cref="InvalidOperationException">Thrown when no active task found</exception>
    /// <exception cref="ArgumentException">Thrown when image data is invalid base64</exception>
    /// <exception cref="Exception">Thrown when GraphQL mutation fails</exception>
    public async Task<Dictionary<string, object>?> ReportAsync(string message, ReportOptions? options = null)
    {
        // Get current task UUID
        string currentTaskUuid;
        try
        {
            var task = await GetTaskAsync();
            
            // Debug: Log the task structure to understand what we're getting
            Console.WriteLine($"DEBUG: Task structure: {JsonSerializer.Serialize(task)}");
            
            currentTaskUuid = task.GetValueOrDefault("euuid")?.ToString() ?? 
                            task.GetValueOrDefault("id")?.ToString() ?? 
                            task.GetValueOrDefault("uuid")?.ToString() ?? 
                            throw new InvalidOperationException($"Task UUID not found in task data: {JsonSerializer.Serialize(task)}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot create report: No active task found. Error: {ex.Message}");
        }

        // Build report data structure
        var reportData = new Dictionary<string, object>
        {
            ["message"] = message,
            ["task"] = new Dictionary<string, object> { ["euuid"] = currentTaskUuid }
        };

        // Process data and set flags
        if (options?.Data != null)
        {
            reportData["data"] = ConvertToJsonCompatible(options.Data);
            reportData["has_card"] = !string.IsNullOrWhiteSpace(options.Data.Card);
            reportData["has_table"] = options.Data.Tables != null && options.Data.Tables.Count > 0;
        }
        else
        {
            reportData["has_card"] = false;
            reportData["has_table"] = false;
        }

        // Process image and validate
        if (!string.IsNullOrEmpty(options?.Image))
        {
            if (!IsValidBase64(options.Image))
            {
                throw new ArgumentException("Invalid base64 image data");
            }
            reportData["image"] = options.Image;
            reportData["has_image"] = true;
        }
        else
        {
            reportData["has_image"] = false;
        }

        // Validate table structure if present
        if (options?.Data?.Tables != null)
        {
            ValidateTables(options.Data.Tables);
        }

        // Note: metadata is not supported by EYWA Task Report schema
        // The Task Report entity only supports: message, data, image, has_* flags
        // Removed metadata to match actual schema

        // Use task.report JSON-RPC method (works with basic EYWA)
        try
        {
            await _client.SendNotificationAsync("task.report", reportData);
            
            // Return a basic success response since task.report is a notification
            return new Dictionary<string, object>
            {
                ["euuid"] = Guid.NewGuid().ToString(),
                ["message"] = message,
                ["has_table"] = reportData.GetValueOrDefault("has_table", false),
                ["has_card"] = reportData.GetValueOrDefault("has_card", false),
                ["has_image"] = reportData.GetValueOrDefault("has_image", false),
                ["success"] = true
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create report: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Helper method to validate base64 data
    /// </summary>
    private static bool IsValidBase64(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;

        try
        {
            // Check if it's valid base64
            var bytes = Convert.FromBase64String(str);
            var reencoded = Convert.ToBase64String(bytes);
            return reencoded == str;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Helper method to validate table structure
    /// </summary>
    private static void ValidateTables(Dictionary<string, TableData> tables)
    {
        foreach (var (tableName, tableData) in tables)
        {
            if (tableData.Headers == null)
            {
                throw new ArgumentException($"Table '{tableName}' must have headers array");
            }

            if (tableData.Rows == null)
            {
                throw new ArgumentException($"Table '{tableName}' must have rows array");
            }

            // Validate each row has same number of columns as headers
            var headerCount = tableData.Headers.Length;
            for (int i = 0; i < tableData.Rows.Length; i++)
            {
                var row = tableData.Rows[i];
                if (row == null)
                {
                    throw new ArgumentException($"Table '{tableName}' row {i} cannot be null");
                }

                if (row.Length != headerCount)
                {
                    throw new ArgumentException(
                        $"Table '{tableName}' row {i} has {row.Length} columns but headers specify {headerCount}");
                }
            }
        }
    }

    /// <summary>
    /// Convert ReportData and nested objects to JSON-compatible dictionary structure
    /// </summary>
    private static Dictionary<string, object> ConvertToJsonCompatible(ReportData data)
    {
        var result = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(data.Card))
        {
            result["card"] = data.Card;
        }

        if (data.Tables != null && data.Tables.Count > 0)
        {
            var tables = new Dictionary<string, object>();
            foreach (var (name, table) in data.Tables)
            {
                tables[name] = new Dictionary<string, object>
                {
                    ["headers"] = table.Headers,
                    ["rows"] = table.Rows
                };
            }
            result["tables"] = tables;
        }

        return result;
    }

    /// <summary>
    /// Convert JsonElement to appropriate .NET object
    /// </summary>
    private static object? ConvertJsonElement(object? value)
    {
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText()),
                JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(jsonElement.GetRawText()),
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Number => jsonElement.TryGetInt64(out var longValue) ? longValue : jsonElement.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => jsonElement.GetRawText()
            };
        }
        return value;
    }
}