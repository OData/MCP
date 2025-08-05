using System;

namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Exception thrown when configuration validation fails.
    /// </summary>
    /// <remarks>
    /// This exception is thrown when configuration validation encounters
    /// errors that prevent the MCP server from starting successfully.
    /// It contains the complete validation result to provide detailed
    /// information about what went wrong.
    /// </remarks>
    public sealed class ConfigurationValidationException : Exception
    {
        #region Properties

        /// <summary>
        /// Gets the validation result that caused the exception.
        /// </summary>
        /// <value>The validation result containing errors, warnings, and information.</value>
        /// <remarks>
        /// This property provides access to the complete validation result,
        /// including all errors, warnings, and informational messages that
        /// were generated during the validation process.
        /// </remarks>
        public ValidationResult ValidationResult { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationValidationException"/> class.
        /// </summary>
        /// <param name="validationResult">The validation result that caused the exception.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="validationResult"/> is null.</exception>
        public ConfigurationValidationException(ValidationResult validationResult)
            : base($"Configuration validation failed: {validationResult?.Summary ?? "Unknown validation failure"}")
        {
            ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationValidationException"/> class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="validationResult">The validation result that caused the exception.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="validationResult"/> is null.</exception>
        public ConfigurationValidationException(string message, ValidationResult validationResult)
            : base(message)
        {
            ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationValidationException"/> class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="validationResult">The validation result that caused the exception.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="validationResult"/> is null.</exception>
        public ConfigurationValidationException(string message, Exception innerException, ValidationResult validationResult)
            : base(message, innerException)
        {
            ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a configuration validation exception from a validation result.
        /// </summary>
        /// <param name="validationResult">The validation result that failed.</param>
        /// <returns>A new configuration validation exception.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="validationResult"/> is null.</exception>
        public static ConfigurationValidationException FromValidationResult(ValidationResult validationResult)
        {
            if (validationResult is null)
            {
                throw new ArgumentNullException(nameof(validationResult));
            }

            return new ConfigurationValidationException(validationResult);
        }

        /// <summary>
        /// Creates a configuration validation exception with a custom message.
        /// </summary>
        /// <param name="message">The custom exception message.</param>
        /// <param name="validationResult">The validation result that failed.</param>
        /// <returns>A new configuration validation exception.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="validationResult"/> is null.</exception>
        public static ConfigurationValidationException WithMessage(string message, ValidationResult validationResult)
        {
            if (validationResult is null)
            {
                throw new ArgumentNullException(nameof(validationResult));
            }

            return new ConfigurationValidationException(message, validationResult);
        }

        /// <summary>
        /// Gets a detailed error message including all validation errors.
        /// </summary>
        /// <returns>A formatted string containing all validation errors.</returns>
        public string GetDetailedErrorMessage()
        {
            if (ValidationResult.Errors.Count == 0)
            {
                return Message;
            }

            var details = new System.Text.StringBuilder();
            details.AppendLine(Message);
            details.AppendLine();
            details.AppendLine("Validation Errors:");

            foreach (var error in ValidationResult.Errors)
            {
                details.AppendLine($"- {error}");
            }

            if (ValidationResult.HasWarnings)
            {
                details.AppendLine();
                details.AppendLine("Validation Warnings:");

                foreach (var warning in ValidationResult.Warnings)
                {
                    details.AppendLine($"- {warning}");
                }
            }

            return details.ToString();
        }

        /// <summary>
        /// Returns a string representation of the exception.
        /// </summary>
        /// <returns>A string containing the exception message and validation summary.</returns>
        public override string ToString()
        {
            return $"{base.ToString()}\n\nValidation Result: {ValidationResult.Summary}";
        }

        #endregion
    }
}