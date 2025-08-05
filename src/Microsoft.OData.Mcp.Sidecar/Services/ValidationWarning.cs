namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Validation warning that should be addressed but doesn't prevent startup.
    /// </summary>
    /// <remarks>
    /// Represents a configuration issue that should be addressed for optimal
    /// operation but doesn't prevent the MCP server from starting. Warnings
    /// typically indicate potential problems, suboptimal configurations, or
    /// deprecated settings.
    /// </remarks>
    public sealed class ValidationWarning : ValidationMessage
    {
        #region Properties

        /// <summary>
        /// Gets or sets the warning code for programmatic handling.
        /// </summary>
        /// <value>A unique warning code identifying the type of validation warning.</value>
        /// <remarks>
        /// Warning codes provide a structured way to identify and handle specific
        /// types of validation warnings programmatically. They should be consistent
        /// and well-documented for integration purposes.
        /// </remarks>
        public string WarningCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the severity level of the warning.
        /// </summary>
        /// <value>The severity level indicating the importance of addressing this warning.</value>
        /// <remarks>
        /// Severity levels help prioritize which warnings should be addressed first.
        /// High severity warnings may indicate potential security or performance issues,
        /// while low severity warnings might be minor configuration improvements.
        /// </remarks>
        public WarningSeverity Severity { get; set; } = WarningSeverity.Medium;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationWarning"/> class.
        /// </summary>
        public ValidationWarning()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationWarning"/> class with the specified message.
        /// </summary>
        /// <param name="message">The warning message.</param>
        public ValidationWarning(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationWarning"/> class with the specified message and path.
        /// </summary>
        /// <param name="message">The warning message.</param>
        /// <param name="path">The configuration path where the warning was found.</param>
        public ValidationWarning(string message, string path) : base(message, path)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationWarning"/> class with complete information.
        /// </summary>
        /// <param name="message">The warning message.</param>
        /// <param name="path">The configuration path where the warning was found.</param>
        /// <param name="section">The configuration section name.</param>
        public ValidationWarning(string message, string path, string section) : base(message, path, section)
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationWarning"/> class with complete information.
        /// </summary>
        /// <param name="message">The warning message.</param>
        /// <param name="path">The configuration path where the warning was found.</param>
        /// <param name="section">The configuration section name.</param>
        /// <param name="warningCode">The warning code for programmatic handling.</param>
        /// <param name="severity">The severity level of the warning.</param>
        public ValidationWarning(string message, string path, string section, string warningCode, WarningSeverity severity = WarningSeverity.Medium) 
            : base(message, path, section)
        {
            WarningCode = warningCode ?? string.Empty;
            Severity = severity;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a high severity validation warning.
        /// </summary>
        /// <param name="message">The warning message.</param>
        /// <param name="path">The configuration path where the warning was found.</param>
        /// <param name="warningCode">The warning code for programmatic handling.</param>
        /// <returns>A new high severity validation warning.</returns>
        public static ValidationWarning High(string message, string path, string warningCode)
        {
            return new ValidationWarning(message, path)
            {
                WarningCode = warningCode ?? string.Empty,
                Severity = WarningSeverity.High
            };
        }

        /// <summary>
        /// Creates a medium severity validation warning.
        /// </summary>
        /// <param name="message">The warning message.</param>
        /// <param name="path">The configuration path where the warning was found.</param>
        /// <param name="warningCode">The warning code for programmatic handling.</param>
        /// <returns>A new medium severity validation warning.</returns>
        public static ValidationWarning Medium(string message, string path, string warningCode)
        {
            return new ValidationWarning(message, path)
            {
                WarningCode = warningCode ?? string.Empty,
                Severity = WarningSeverity.Medium
            };
        }

        /// <summary>
        /// Creates a low severity validation warning.
        /// </summary>
        /// <param name="message">The warning message.</param>
        /// <param name="path">The configuration path where the warning was found.</param>
        /// <param name="warningCode">The warning code for programmatic handling.</param>
        /// <returns>A new low severity validation warning.</returns>
        public static ValidationWarning Low(string message, string path, string warningCode)
        {
            return new ValidationWarning(message, path)
            {
                WarningCode = warningCode ?? string.Empty,
                Severity = WarningSeverity.Low
            };
        }

        /// <summary>
        /// Returns a string representation of the validation warning.
        /// </summary>
        /// <returns>A formatted string containing the warning information.</returns>
        public override string ToString()
        {
            var baseString = base.ToString();
            var severityIndicator = Severity switch
            {
                WarningSeverity.High => "[HIGH] ",
                WarningSeverity.Low => "[LOW] ",
                _ => ""
            };
            var warningCodePart = !string.IsNullOrWhiteSpace(WarningCode) ? $" ({WarningCode})" : "";
            
            return $"{severityIndicator}{baseString}{warningCodePart}";
        }

        #endregion
    }
}