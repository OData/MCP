using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Result of configuration validation.
    /// </summary>
    /// <remarks>
    /// Contains the complete results of validating a configuration, including
    /// errors that prevent startup, warnings that should be addressed, and
    /// informational messages about the configuration.
    /// </remarks>
    public sealed class ValidationResult
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the validation passed.
        /// </summary>
        /// <value><c>true</c> if validation passed with no blocking errors; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// This property is <c>true</c> when there are no validation errors that would
        /// prevent the service from starting. Warnings do not affect this value.
        /// </remarks>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the validation errors that prevent the service from starting.
        /// </summary>
        /// <value>A collection of validation errors.</value>
        /// <remarks>
        /// These are critical issues that must be resolved before the service
        /// can start successfully. Each error should provide enough information
        /// to help the user resolve the issue.
        /// </remarks>
        public List<ValidationError> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets the validation warnings that don't prevent startup but should be addressed.
        /// </summary>
        /// <value>A collection of validation warnings.</value>
        /// <remarks>
        /// These are issues that should be addressed for optimal operation but
        /// don't prevent the service from starting. They may indicate potential
        /// problems or suboptimal configurations.
        /// </remarks>
        public List<ValidationWarning> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets informational messages about the configuration.
        /// </summary>
        /// <value>A collection of informational messages.</value>
        /// <remarks>
        /// These messages provide helpful information about the configuration,
        /// such as detected features, recommendations, or best practices.
        /// </remarks>
        public List<ValidationInfo> Information { get; set; } = new();

        /// <summary>
        /// Gets a value indicating whether there are any errors.
        /// </summary>
        /// <value><c>true</c> if there are validation errors; otherwise, <c>false</c>.</value>
        public bool HasErrors => Errors.Count > 0;

        /// <summary>
        /// Gets a value indicating whether there are any warnings.
        /// </summary>
        /// <value><c>true</c> if there are validation warnings; otherwise, <c>false</c>.</value>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Gets a value indicating whether there are any informational messages.
        /// </summary>
        /// <value><c>true</c> if there are informational messages; otherwise, <c>false</c>.</value>
        public bool HasInformation => Information.Count > 0;

        /// <summary>
        /// Gets a summary of the validation result.
        /// </summary>
        /// <value>A summary string describing the validation outcome.</value>
        public string Summary => $"Validation {(IsValid ? "passed" : "failed")} with {Errors.Count} errors, {Warnings.Count} warnings";

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> class.
        /// </summary>
        public ValidationResult()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> class with the specified validity.
        /// </summary>
        /// <param name="isValid">Whether the validation passed.</param>
        public ValidationResult(bool isValid)
        {
            IsValid = isValid;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a validation error to the result.
        /// </summary>
        /// <param name="error">The validation error to add.</param>
        /// <remarks>
        /// Adding an error automatically sets <see cref="IsValid"/> to <c>false</c>.
        /// </remarks>
        public void AddError(ValidationError error)
        {
            if (error is not null)
            {
                Errors.Add(error);
                IsValid = false;
            }
        }

        /// <summary>
        /// Adds a validation warning to the result.
        /// </summary>
        /// <param name="warning">The validation warning to add.</param>
        public void AddWarning(ValidationWarning warning)
        {
            if (warning is not null)
            {
                Warnings.Add(warning);
            }
        }

        /// <summary>
        /// Adds informational message to the result.
        /// </summary>
        /// <param name="info">The informational message to add.</param>
        public void AddInformation(ValidationInfo info)
        {
            if (info is not null)
            {
                Information.Add(info);
            }
        }

        /// <summary>
        /// Gets the total number of validation messages (errors + warnings + information).
        /// </summary>
        /// <returns>The total number of validation messages.</returns>
        public int GetTotalMessageCount()
        {
            return Errors.Count + Warnings.Count + Information.Count;
        }

        /// <summary>
        /// Returns a string representation of the validation result.
        /// </summary>
        /// <returns>A string containing the validation summary.</returns>
        public override string ToString()
        {
            return Summary;
        }

        #endregion
    }
}