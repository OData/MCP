namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Validation error that prevents the service from starting.
    /// </summary>
    /// <remarks>
    /// Represents a critical configuration issue that must be resolved before
    /// the MCP server can start successfully. These errors indicate problems
    /// that would cause the service to fail during startup or operation.
    /// </remarks>
    public sealed class ValidationError : ValidationMessage
    {
        #region Properties

        /// <summary>
        /// Gets or sets the error code for programmatic handling.
        /// </summary>
        /// <value>A unique error code identifying the type of validation error.</value>
        /// <remarks>
        /// Error codes provide a structured way to identify and handle specific
        /// types of validation errors programmatically. They should be consistent
        /// and well-documented for integration purposes.
        /// </remarks>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this error is critical for startup.
        /// </summary>
        /// <value><c>true</c> if the error prevents startup; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Critical errors must be resolved before the service can start.
        /// Non-critical errors may allow the service to start but could cause
        /// issues during operation.
        /// </remarks>
        public bool IsCritical { get; set; } = true;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class.
        /// </summary>
        public ValidationError()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class with the specified message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public ValidationError(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class with the specified message and path.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="path">The configuration path where the error was found.</param>
        public ValidationError(string message, string path) : base(message, path)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class with complete information.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="path">The configuration path where the error was found.</param>
        /// <param name="section">The configuration section name.</param>
        public ValidationError(string message, string path, string section) : base(message, path, section)
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class with complete information.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="path">The configuration path where the error was found.</param>
        /// <param name="section">The configuration section name.</param>
        /// <param name="errorCode">The error code for programmatic handling.</param>
        /// <param name="isCritical">Whether this error is critical for startup.</param>
        public ValidationError(string message, string path, string section, string errorCode, bool isCritical = true) 
            : base(message, path, section)
        {
            ErrorCode = errorCode ?? string.Empty;
            IsCritical = isCritical;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a critical validation error.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="path">The configuration path where the error was found.</param>
        /// <param name="errorCode">The error code for programmatic handling.</param>
        /// <returns>A new critical validation error.</returns>
        public static ValidationError Critical(string message, string path, string errorCode)
        {
            return new ValidationError(message, path)
            {
                ErrorCode = errorCode ?? string.Empty,
                IsCritical = true
            };
        }

        /// <summary>
        /// Creates a non-critical validation error.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="path">The configuration path where the error was found.</param>
        /// <param name="errorCode">The error code for programmatic handling.</param>
        /// <returns>A new non-critical validation error.</returns>
        public static ValidationError NonCritical(string message, string path, string errorCode)
        {
            return new ValidationError(message, path)
            {
                ErrorCode = errorCode ?? string.Empty,
                IsCritical = false
            };
        }

        /// <summary>
        /// Returns a string representation of the validation error.
        /// </summary>
        /// <returns>A formatted string containing the error information.</returns>
        public override string ToString()
        {
            var baseString = base.ToString();
            var criticalIndicator = IsCritical ? "[CRITICAL] " : "";
            var errorCodePart = !string.IsNullOrWhiteSpace(ErrorCode) ? $" ({ErrorCode})" : "";
            
            return $"{criticalIndicator}{baseString}{errorCodePart}";
        }

        #endregion
    }
}