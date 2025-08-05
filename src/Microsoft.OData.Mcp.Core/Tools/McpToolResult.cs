using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Microsoft.OData.Mcp.Core.Tools
{
    /// <summary>
    /// Represents the result of an MCP tool execution.
    /// </summary>
    /// <remarks>
    /// This class standardizes the format of tool execution results, providing
    /// consistent error handling, data formatting, and metadata across all tools.
    /// </remarks>
    public sealed class McpToolResult
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the tool execution was successful.
        /// </summary>
        /// <value><c>true</c> if the execution was successful; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Success indicates that the tool executed without errors and completed
        /// its intended operation, regardless of whether data was found or modified.
        /// </remarks>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the result data as a JSON document.
        /// </summary>
        /// <value>The structured result data, or null if no data is available.</value>
        /// <remarks>
        /// The data format depends on the specific tool but should always be
        /// valid JSON that can be consumed by MCP clients.
        /// </remarks>
        public JsonDocument? Data { get; set; }

        /// <summary>
        /// Gets or sets the error message for failed executions.
        /// </summary>
        /// <value>A human-readable error message, or null if execution was successful.</value>
        /// <remarks>
        /// Error messages should be clear and actionable, helping users understand
        /// what went wrong and how to fix it.
        /// </remarks>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the error code for failed executions.
        /// </summary>
        /// <value>A machine-readable error code, or null if execution was successful.</value>
        /// <remarks>
        /// Error codes provide a standardized way for clients to handle specific
        /// error conditions programmatically.
        /// </remarks>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the execution.
        /// </summary>
        /// <value>A dictionary of metadata key-value pairs.</value>
        /// <remarks>
        /// Metadata can include information such as execution time, record counts,
        /// caching status, or other operational details.
        /// </remarks>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the execution duration.
        /// </summary>
        /// <value>The time taken to execute the tool.</value>
        /// <remarks>
        /// This information is useful for performance monitoring and optimization.
        /// </remarks>
        public TimeSpan ExecutionDuration { get; set; }

        /// <summary>
        /// Gets or sets the correlation ID for this execution.
        /// </summary>
        /// <value>The correlation ID that can be used to trace this execution across systems.</value>
        /// <remarks>
        /// This should match the correlation ID from the tool context for end-to-end tracing.
        /// </remarks>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code equivalent for this result.
        /// </summary>
        /// <value>The HTTP status code that best represents this result.</value>
        /// <remarks>
        /// This helps clients understand the result in terms of standard HTTP semantics,
        /// even when the tool isn't directly related to HTTP operations.
        /// </remarks>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Gets or sets warnings that occurred during execution.
        /// </summary>
        /// <value>A collection of warning messages.</value>
        /// <remarks>
        /// Warnings indicate potential issues or notable conditions that didn't
        /// prevent successful execution but may be of interest to users.
        /// </remarks>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets the timestamp when the execution completed.
        /// </summary>
        /// <value>The UTC timestamp when the tool finished executing.</value>
        /// <remarks>
        /// This timestamp can be used for auditing and troubleshooting purposes.
        /// </remarks>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolResult"/> class.
        /// </summary>
        public McpToolResult()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolResult"/> class with success status.
        /// </summary>
        /// <param name="data">The result data.</param>
        /// <param name="correlationId">The correlation ID.</param>
        public McpToolResult(JsonDocument? data, string? correlationId = null)
        {
            IsSuccess = true;
            Data = data;
            CorrelationId = correlationId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolResult"/> class with error status.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="correlationId">The correlation ID.</param>
        public McpToolResult(string errorMessage, string? errorCode = null, string? correlationId = null)
        {
            IsSuccess = false;
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
            CorrelationId = correlationId;
            StatusCode = 500;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a successful result with the specified data.
        /// </summary>
        /// <param name="data">The result data.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <returns>A successful MCP tool result.</returns>
        public static McpToolResult Success(JsonDocument? data = null, string? correlationId = null)
        {
            return new McpToolResult(data, correlationId);
        }

        /// <summary>
        /// Creates a successful result with the specified object data.
        /// </summary>
        /// <param name="data">The data object to serialize to JSON.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <returns>A successful MCP tool result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public static McpToolResult Success(object data, string? correlationId = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(data);
#else
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }
#endif

            var json = JsonSerializer.Serialize(data);
            var document = JsonDocument.Parse(json);
            return new McpToolResult(document, correlationId);
        }

        /// <summary>
        /// Creates an error result with the specified message and code.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <returns>An error MCP tool result.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="errorMessage"/> is null or whitespace.</exception>
        public static McpToolResult Error(string errorMessage, string? errorCode = null, string? correlationId = null)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
#else
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Error message cannot be null or whitespace.", nameof(errorMessage));
            }
#endif

            return new McpToolResult(errorMessage, errorCode, correlationId);
        }

        /// <summary>
        /// Creates an error result from an exception.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <returns>An error MCP tool result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
        public static McpToolResult Error(Exception exception, string? correlationId = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(exception);
#else
            if (exception is null)
            {
                throw new ArgumentNullException(nameof(exception));
            }
#endif

            var errorCode = exception.GetType().Name;
            var result = new McpToolResult(exception.Message, errorCode, correlationId);
            
            // Set appropriate status codes based on exception type
            result.StatusCode = exception switch
            {
                ArgumentException => 400,
                UnauthorizedAccessException => 401,
                InvalidOperationException => 409,
                NotSupportedException => 501,
                TimeoutException => 408,
                _ => 500
            };

            return result;
        }

        /// <summary>
        /// Creates a not found result.
        /// </summary>
        /// <param name="message">The not found message.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <returns>A not found MCP tool result.</returns>
        public static McpToolResult NotFound(string? message = null, string? correlationId = null)
        {
            var result = new McpToolResult(message ?? "Resource not found.", "NOT_FOUND", correlationId);
            result.StatusCode = 404;
            return result;
        }

        /// <summary>
        /// Creates an unauthorized result.
        /// </summary>
        /// <param name="message">The unauthorized message.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <returns>An unauthorized MCP tool result.</returns>
        public static McpToolResult Unauthorized(string? message = null, string? correlationId = null)
        {
            var result = new McpToolResult(message ?? "Access denied.", "UNAUTHORIZED", correlationId);
            result.StatusCode = 401;
            return result;
        }

        /// <summary>
        /// Creates a validation error result.
        /// </summary>
        /// <param name="message">The validation error message.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <returns>A validation error MCP tool result.</returns>
        public static McpToolResult ValidationError(string message, string? correlationId = null)
        {
            var result = new McpToolResult(message, "VALIDATION_ERROR", correlationId);
            result.StatusCode = 400;
            return result;
        }

        /// <summary>
        /// Adds metadata to the result.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
        public void AddMetadata(string key, object value)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
#else
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Metadata key cannot be null or whitespace.", nameof(key));
            }
#endif

            Metadata[key] = value;
        }

        /// <summary>
        /// Gets metadata value by key.
        /// </summary>
        /// <typeparam name="T">The type of the metadata value.</typeparam>
        /// <param name="key">The metadata key.</param>
        /// <returns>The metadata value if found and of the correct type; otherwise, the default value.</returns>
        public T? GetMetadata<T>(string key)
        {
            if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Adds a warning to the result.
        /// </summary>
        /// <param name="warning">The warning message.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="warning"/> is null or whitespace.</exception>
        public void AddWarning(string warning)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(warning);
#else
            if (string.IsNullOrWhiteSpace(warning))
            {
                throw new ArgumentException("Warning message cannot be null or whitespace.", nameof(warning));
            }
#endif

            Warnings.Add(warning);
        }

        /// <summary>
        /// Sets the execution duration based on a start time.
        /// </summary>
        /// <param name="startTime">The execution start time.</param>
        public void SetExecutionDuration(DateTime startTime)
        {
            ExecutionDuration = DateTime.UtcNow - startTime;
        }

        /// <summary>
        /// Converts the result to a dictionary for JSON serialization.
        /// </summary>
        /// <returns>A dictionary representation of the result.</returns>
        public Dictionary<string, object?> ToDictionary()
        {
            var result = new Dictionary<string, object?>
            {
                ["isSuccess"] = IsSuccess,
                ["statusCode"] = StatusCode,
                ["correlationId"] = CorrelationId,
                ["completedAt"] = CompletedAt,
                ["executionDuration"] = ExecutionDuration.TotalMilliseconds
            };

            if (Data is not null)
            {
                var dataJson = Data.RootElement.GetRawText();
                result["data"] = JsonDocument.Parse(dataJson).RootElement;
            }

            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                result["errorMessage"] = ErrorMessage;
            }

            if (!string.IsNullOrWhiteSpace(ErrorCode))
            {
                result["errorCode"] = ErrorCode;
            }

            if (Metadata.Count > 0)
            {
                result["metadata"] = Metadata;
            }

            if (Warnings.Count > 0)
            {
                result["warnings"] = Warnings;
            }

            return result;
        }

        /// <summary>
        /// Returns a JSON string representation of the result.
        /// </summary>
        /// <returns>A JSON string representation of the result.</returns>
        public override string ToString()
        {
            var dictionary = ToDictionary();
            return JsonSerializer.Serialize(dictionary);
        }

        #endregion
    }
}