using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for input validation and sanitization.
    /// </summary>
    /// <remarks>
    /// Input validation configuration controls how user-provided data is validated
    /// and sanitized before processing. This helps prevent injection attacks,
    /// data corruption, and ensures data integrity throughout the application.
    /// </remarks>
    public sealed class InputValidationConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether strict validation is enabled.
        /// </summary>
        /// <value><c>true</c> to enable strict validation; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Strict validation applies more rigorous rules to input data, rejecting
        /// potentially dangerous content. This provides better security but may
        /// be more restrictive for legitimate use cases.
        /// </remarks>
        public bool EnableStrictValidation { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether special characters are allowed in input.
        /// </summary>
        /// <value><c>true</c> to allow special characters; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Special characters can be used in injection attacks but may also be
        /// legitimate parts of user data. This setting controls the balance
        /// between security and functionality.
        /// </remarks>
        public bool AllowSpecialCharacters { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum allowed length for string inputs.
        /// </summary>
        /// <value>The maximum string length in characters.</value>
        /// <remarks>
        /// String length limits prevent buffer overflow attacks and ensure
        /// predictable resource usage. This applies to all string inputs
        /// unless overridden by specific field validation rules.
        /// </remarks>
        public int MaxStringLength { get; set; } = 1000;

        /// <summary>
        /// Creates an input validation configuration with lenient rules.
        /// </summary>
        /// <returns>A validation configuration suitable for environments requiring flexible input handling.</returns>
        public static InputValidationConfiguration Lenient() => new() { EnableStrictValidation = false, AllowSpecialCharacters = true };

        /// <summary>
        /// Creates an input validation configuration with strict rules.
        /// </summary>
        /// <returns>A validation configuration suitable for high-security environments.</returns>
        public static InputValidationConfiguration Strict() => new();

        /// <summary>
        /// Validates the input validation configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate() => Enumerable.Empty<string>();

        /// <summary>
        /// Creates a copy of this input validation configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public InputValidationConfiguration Clone() => new() { EnableStrictValidation = EnableStrictValidation, AllowSpecialCharacters = AllowSpecialCharacters, MaxStringLength = MaxStringLength };

        /// <summary>
        /// Merges another input validation configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        public void MergeWith(InputValidationConfiguration other) { if (other != null) { EnableStrictValidation = other.EnableStrictValidation; AllowSpecialCharacters = other.AllowSpecialCharacters; MaxStringLength = other.MaxStringLength; } }
    }
}
