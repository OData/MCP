using Microsoft.OData.Mcp.Core.Configuration;

namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Service for validating MCP server configuration.
    /// </summary>
    /// <remarks>
    /// This service provides comprehensive validation of the MCP server configuration,
    /// ensuring all settings are valid and compatible with the sidecar deployment mode.
    /// It performs multi-level validation including critical startup requirements,
    /// sidecar-specific compatibility checks, and best practice recommendations.
    /// </remarks>
    public interface IConfigurationValidationService
    {
        /// <summary>
        /// Validates the complete MCP server configuration.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <returns>Validation result with any errors, warnings, and informational messages.</returns>
        /// <remarks>
        /// This method performs comprehensive validation of all configuration sections,
        /// including network settings, authentication, security, caching, and OData service
        /// configuration. It checks for both critical errors and potential issues.
        /// </remarks>
        /// <example>
        /// <code>
        /// var result = validationService.ValidateConfiguration(configuration);
        /// if (!result.IsValid)
        /// {
        ///     foreach (var error in result.Errors)
        ///     {
        ///         logger.LogError("Configuration error: {Error}", error.Message);
        ///     }
        /// }
        /// </code>
        /// </example>
        ValidationResult ValidateConfiguration(McpServerConfiguration configuration);

        /// <summary>
        /// Validates the configuration and throws an exception if validation fails.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <exception cref="ConfigurationValidationException">Thrown when validation fails.</exception>
        /// <remarks>
        /// This method is a convenience wrapper around <see cref="ValidateConfiguration"/>
        /// that throws an exception if validation fails. Use this when you want to
        /// fail fast on invalid configuration.
        /// </remarks>
        /// <example>
        /// <code>
        /// try
        /// {
        ///     validationService.ValidateConfigurationOrThrow(configuration);
        /// }
        /// catch (ConfigurationValidationException ex)
        /// {
        ///     logger.LogError("Configuration validation failed: {Details}", ex.GetDetailedErrorMessage());
        ///     throw;
        /// }
        /// </code>
        /// </example>
        void ValidateConfigurationOrThrow(McpServerConfiguration configuration);

        /// <summary>
        /// Validates only critical configuration settings that would prevent startup.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <returns>Validation result for critical settings only.</returns>
        /// <remarks>
        /// This method performs a subset of validation focusing only on settings
        /// that are absolutely required for the service to start. Use this for
        /// fast startup validation when a full validation might be too expensive.
        /// </remarks>
        /// <example>
        /// <code>
        /// var criticalResult = validationService.ValidateCriticalConfiguration(configuration);
        /// if (!criticalResult.IsValid)
        /// {
        ///     throw new InvalidOperationException("Critical configuration errors prevent startup");
        /// }
        /// </code>
        /// </example>
        ValidationResult ValidateCriticalConfiguration(McpServerConfiguration configuration);

        /// <summary>
        /// Validates configuration compatibility for sidecar deployment mode.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <returns>Validation result specific to sidecar deployment.</returns>
        /// <remarks>
        /// This method validates configuration settings that are specific to sidecar
        /// deployment mode, such as network configuration for co-location with the
        /// main OData service, authentication delegation, and resource sharing.
        /// </remarks>
        /// <example>
        /// <code>
        /// var sidecarResult = validationService.ValidateSidecarConfiguration(configuration);
        /// if (sidecarResult.HasWarnings)
        /// {
        ///     logger.LogWarning("Sidecar configuration has {Count} warnings", sidecarResult.Warnings.Count);
        /// }
        /// </code>
        /// </example>
        ValidationResult ValidateSidecarConfiguration(McpServerConfiguration configuration);
    }
}