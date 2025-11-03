namespace EywaClient.Exceptions;

/// <summary>
/// Exception thrown when a JSON-RPC call fails.
/// </summary>
public class JsonRpcException : Exception
{
    /// <summary>
    /// The JSON-RPC error object.
    /// </summary>
    public Models.JsonRpcError Error { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcException"/> class.
    /// </summary>
    /// <param name="error">The JSON-RPC error object.</param>
    public JsonRpcException(Models.JsonRpcError error)
        : base(error.Message)
    {
        Error = error;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcException"/> class.
    /// </summary>
    /// <param name="error">The JSON-RPC error object.</param>
    /// <param name="innerException">The inner exception.</param>
    public JsonRpcException(Models.JsonRpcError error, Exception innerException)
        : base(error.Message, innerException)
    {
        Error = error;
    }
}

/// <summary>
/// Exception thrown when a GraphQL operation fails.
/// </summary>
public class GraphQLException : Exception
{
    /// <summary>
    /// The list of GraphQL errors.
    /// </summary>
    public IReadOnlyList<Models.GraphQLError> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLException"/> class.
    /// </summary>
    /// <param name="errors">The list of GraphQL errors.</param>
    public GraphQLException(IEnumerable<Models.GraphQLError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors.ToList();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLException"/> class.
    /// </summary>
    /// <param name="errors">The list of GraphQL errors.</param>
    /// <param name="innerException">The inner exception.</param>
    public GraphQLException(IEnumerable<Models.GraphQLError> errors, Exception innerException)
        : base(BuildMessage(errors), innerException)
    {
        Errors = errors.ToList();
    }

    private static string BuildMessage(IEnumerable<Models.GraphQLError> errors)
    {
        var errorList = errors.ToList();
        if (errorList.Count == 0)
            return "GraphQL operation failed";
        if (errorList.Count == 1)
            return $"GraphQL error: {errorList[0].Message}";
        return $"GraphQL operation failed with {errorList.Count} errors: {string.Join("; ", errorList.Select(e => e.Message))}";
    }
}

/// <summary>
/// Exception thrown when a file upload operation fails.
/// </summary>
public class FileUploadException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public FileUploadException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public FileUploadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a file download operation fails.
/// </summary>
public class FileDownloadException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileDownloadException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public FileDownloadException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDownloadException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public FileDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
