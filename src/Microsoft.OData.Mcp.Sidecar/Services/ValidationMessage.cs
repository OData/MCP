namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Base class for validation messages.
    /// </summary>
    /// <remarks>
    /// This abstract base class provides common properties for all types of
    /// validation messages, including errors, warnings, and informational messages.
    /// It contains contextual information to help users understand and resolve
    /// validation issues.
    /// </remarks>
    public abstract class ValidationMessage
    {
        #region Properties

        /// <summary>
        /// Gets or sets the validation message.
        /// </summary>
        /// <value>A human-readable description of the validation issue or information.</value>
        /// <remarks>
        /// This should be a clear, concise message that explains what was found
        /// during validation. For errors and warnings, it should describe the
        /// problem. For informational messages, it should describe what was detected.
        /// </remarks>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the configuration path where the issue was found.
        /// </summary>
        /// <value>A dot-separated path indicating the location in the configuration.</value>
        /// <remarks>
        /// This path helps users locate the specific configuration setting that
        /// caused the validation issue. For example: "Network.Port" or "Authentication.Jwt.SecretKey".
        /// </remarks>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the configuration section name.
        /// </summary>
        /// <value>The name of the configuration section containing the issue.</value>
        /// <remarks>
        /// This provides a high-level grouping of the configuration issue,
        /// such as "Network", "Authentication", "Security", etc.
        /// </remarks>
        public string Section { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional context about the validation issue.
        /// </summary>
        /// <value>Additional context information, or <c>null</c> if not applicable.</value>
        /// <remarks>
        /// This can provide additional background information about why the
        /// validation issue occurred or what conditions led to it.
        /// </remarks>
        public string? Context { get; set; }

        /// <summary>
        /// Gets or sets suggested remediation steps.
        /// </summary>
        /// <value>Suggested steps to resolve the issue, or <c>null</c> if not applicable.</value>
        /// <remarks>
        /// This should provide actionable guidance on how to resolve the validation
        /// issue, including specific configuration changes or prerequisites.
        /// </remarks>
        public string? Remediation { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationMessage"/> class.
        /// </summary>
        protected ValidationMessage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationMessage"/> class with the specified message.
        /// </summary>
        /// <param name="message">The validation message.</param>
        protected ValidationMessage(string message)
        {
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationMessage"/> class with the specified message and path.
        /// </summary>
        /// <param name="message">The validation message.</param>
        /// <param name="path">The configuration path where the issue was found.</param>
        protected ValidationMessage(string message, string path)
        {
            Message = message ?? string.Empty;
            Path = path ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationMessage"/> class with complete information.
        /// </summary>
        /// <param name="message">The validation message.</param>
        /// <param name="path">The configuration path where the issue was found.</param>
        /// <param name="section">The configuration section name.</param>
        protected ValidationMessage(string message, string path, string section)
        {
            Message = message ?? string.Empty;
            Path = path ?? string.Empty;
            Section = section ?? string.Empty;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a string representation of the validation message.
        /// </summary>
        /// <returns>A formatted string containing the message and path information.</returns>
        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Path))
            {
                return $"{Path}: {Message}";
            }
            return Message;
        }

        #endregion
    }
}