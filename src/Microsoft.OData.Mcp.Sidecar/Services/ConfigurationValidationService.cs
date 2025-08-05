using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Authentication.Models;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Implementation of configuration validation service for MCP sidecar.
    /// </summary>
    /// <remarks>
    /// This service provides comprehensive validation of the MCP server configuration,
    /// with specific validation rules for sidecar deployment mode.
    /// </remarks>
    public sealed class ConfigurationValidationService : IConfigurationValidationService
    {
        #region Fields

        private readonly ILogger<ConfigurationValidationService> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationValidationService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public ConfigurationValidationService(ILogger<ConfigurationValidationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the complete MCP server configuration.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <returns>Validation result with any errors or warnings.</returns>
        public ValidationResult ValidateConfiguration(McpServerConfiguration configuration)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(configuration);
#else
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
#endif

            _logger.LogDebug("Starting comprehensive configuration validation");

            var result = new ValidationResult();

            try
            {
                // Validate each configuration section
                ValidateServerInfo(configuration.ServerInfo, result);
                ValidateODataService(configuration.ODataService, result);
                ValidateAuthentication(configuration.Authentication, result);
                ValidateNetwork(configuration.Network, result);
                ValidateCaching(configuration.Caching, result);
                ValidateMonitoring(configuration.Monitoring, result);
                ValidateSecurity(configuration.Security, result);
                ValidateFeatureFlags(configuration.FeatureFlags, result);
                ValidateToolGeneration(configuration.ToolGeneration, result);

                // Validate sidecar-specific requirements
                ValidateSidecarSpecific(configuration, result);

                // Cross-section validation
                ValidateConfigurationCompatibility(configuration, result);

                result.IsValid = !result.HasErrors;

                _logger.LogInformation("Configuration validation completed: {Summary}", result.Summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during configuration validation");
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "VALIDATION_FAILED",
                    Message = $"Configuration validation failed with exception: {ex.Message}",
                    Section = "General",
                    Path = "Configuration",
                    Context = ex.GetType().Name,
                    Remediation = "Check the configuration format and ensure all required settings are provided"
                });
                result.IsValid = false;
            }

            return result;
        }

        /// <summary>
        /// Validates the configuration and throws an exception if validation fails.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <exception cref="ConfigurationValidationException">Thrown when validation fails.</exception>
        public void ValidateConfigurationOrThrow(McpServerConfiguration configuration)
        {
            var result = ValidateConfiguration(configuration);
            
            if (!result.IsValid)
            {
                throw new ConfigurationValidationException(result);
            }
        }

        /// <summary>
        /// Validates only critical configuration settings that would prevent startup.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <returns>Validation result for critical settings only.</returns>
        public ValidationResult ValidateCriticalConfiguration(McpServerConfiguration configuration)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(configuration);
#else
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
#endif

            _logger.LogDebug("Starting critical configuration validation");

            var result = new ValidationResult();

            try
            {
                // Only validate settings that are critical for startup
                ValidateCriticalODataService(configuration.ODataService, result);
                ValidateCriticalNetwork(configuration.Network, result);
                ValidateCriticalAuthentication(configuration.Authentication, result);

                result.IsValid = !result.HasErrors;

                _logger.LogInformation("Critical configuration validation completed: {Summary}", result.Summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during critical configuration validation");
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "CRITICAL_VALIDATION_FAILED",
                    Message = $"Critical configuration validation failed: {ex.Message}",
                    Section = "General",
                    Path = "Configuration",
                    IsCritical = true
                });
                result.IsValid = false;
            }

            return result;
        }

        /// <summary>
        /// Validates configuration compatibility for sidecar deployment mode.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <returns>Validation result specific to sidecar deployment.</returns>
        public ValidationResult ValidateSidecarConfiguration(McpServerConfiguration configuration)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(configuration);
#else
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
#endif

            var result = new ValidationResult();

            ValidateSidecarSpecific(configuration, result);

            result.IsValid = !result.HasErrors;
            return result;
        }

        #endregion

        #region Private Validation Methods

        /// <summary>
        /// Validates server information configuration.
        /// </summary>
        /// <param name="serverInfo">The server info configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateServerInfo(McpServerInfo serverInfo, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(serverInfo.Name))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WarningCode = "MISSING_SERVER_NAME",
                    Message = "Server name is not specified",
                    Section = "ServerInfo",
                    Path = "ServerInfo.Name",
                    Severity = WarningSeverity.Low,
                    Remediation = "Consider setting a descriptive server name for identification"
                });
            }

            if (string.IsNullOrWhiteSpace(serverInfo.Version))
            {
                result.Information.Add(new ValidationInfo
                {
                    Message = "Server version will be auto-detected from assembly",
                    Section = "ServerInfo",
                    Path = "ServerInfo.Version",
                    Type = InfoType.General
                });
            }
        }

        /// <summary>
        /// Validates OData service configuration.
        /// </summary>
        /// <param name="odataService">The OData service configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateODataService(ODataServiceConfiguration odataService, ValidationResult result)
        {
            var validationErrors = odataService.Validate(McpDeploymentMode.Sidecar).ToList();
            foreach (var error in validationErrors)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "ODATA_CONFIG_ERROR",
                    Message = error,
                    Section = "ODataService",
                    Path = "ODataService",
                    IsCritical = true
                });
            }

            if (!string.IsNullOrWhiteSpace(odataService.BaseUrl))
            {
                try
                {
                    var uri = new Uri(odataService.BaseUrl);
                    if (uri.Scheme != "https")
                    {
                        result.Warnings.Add(new ValidationWarning
                        {
                            WarningCode = "INSECURE_ODATA_URL",
                            Message = "OData service URL is not using HTTPS",
                            Section = "ODataService",
                            Path = "ODataService.BaseUrl",
                            Severity = WarningSeverity.High,
                            Remediation = "Use HTTPS for secure communication with the OData service"
                        });
                    }
                }
                catch (UriFormatException)
                {
                    result.Errors.Add(new ValidationError
                    {
                        ErrorCode = "INVALID_ODATA_URL",
                        Message = "OData service URL is not a valid URI",
                        Section = "ODataService",
                        Path = "ODataService.BaseUrl",
                        IsCritical = true
                    });
                }
            }
        }

        /// <summary>
        /// Validates authentication configuration.
        /// </summary>
        /// <param name="authentication">The authentication configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateAuthentication(McpAuthenticationOptions authentication, ValidationResult result)
        {
            var validationErrors = authentication.Validate().ToList();
            foreach (var error in validationErrors)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "AUTH_CONFIG_ERROR",
                    Message = error,
                    Section = "Authentication",
                    Path = "Authentication"
                });
            }

            if (authentication.Enabled && 
                string.IsNullOrWhiteSpace(authentication.Scheme))
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "AUTH_ENABLED_BUT_NO_SCHEME",
                    Message = "Authentication is enabled but no authentication scheme is specified",
                    Section = "Authentication",
                    Path = "Authentication.Scheme",
                    Remediation = "Specify an authentication scheme (Bearer, JWT, etc.) or disable authentication"
                });
            }
        }

        /// <summary>
        /// Validates network configuration.
        /// </summary>
        /// <param name="network">The network configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateNetwork(NetworkConfiguration network, ValidationResult result)
        {
            var validationErrors = network.Validate(McpDeploymentMode.Sidecar).ToList();
            foreach (var error in validationErrors)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "NETWORK_CONFIG_ERROR",
                    Message = error,
                    Section = "Network",
                    Path = "Network"
                });
            }

            if (network.Port.HasValue && (network.Port.Value == 80 || network.Port.Value == 443))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WarningCode = "STANDARD_PORT_USAGE",
                    Message = $"Using standard port {network.Port.Value} may require elevated privileges",
                    Section = "Network",
                    Path = "Network.Port",
                    Severity = WarningSeverity.Medium,
                    Remediation = "Consider using a non-standard port (e.g., 8080, 8443) or ensure the service runs with appropriate privileges"
                });
            }
        }

        /// <summary>
        /// Validates caching configuration.
        /// </summary>
        /// <param name="caching">The caching configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateCaching(CachingConfiguration caching, ValidationResult result)
        {
            var validationErrors = caching.Validate().ToList();
            foreach (var error in validationErrors)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "CACHE_CONFIG_ERROR",
                    Message = error,
                    Section = "Caching",
                    Path = "Caching"
                });
            }

            if (caching.Enabled && caching.ProviderType == CacheProviderType.Distributed)
            {
                if (string.IsNullOrWhiteSpace(caching.DistributedCache.ConnectionString))
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        WarningCode = "DISTRIBUTED_CACHE_NO_CONNECTION",
                        Message = "Distributed cache is enabled but no connection string is provided",
                        Section = "Caching",
                        Path = "Caching.DistributedCache.ConnectionString",
                        Severity = WarningSeverity.High,
                        Remediation = "Provide a connection string for the distributed cache or use memory caching"
                    });
                }
            }
        }

        /// <summary>
        /// Validates monitoring configuration.
        /// </summary>
        /// <param name="monitoring">The monitoring configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateMonitoring(MonitoringConfiguration monitoring, ValidationResult result)
        {
            var validationErrors = monitoring.Validate().ToList();
            foreach (var error in validationErrors)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "MONITORING_CONFIG_ERROR",
                    Message = error,
                    Section = "Monitoring",
                    Path = "Monitoring"
                });
            }

            if (monitoring.EnableTracing && string.IsNullOrWhiteSpace(monitoring.OpenTelemetry.OtlpEndpoint))
            {
                result.Information.Add(new ValidationInfo
                {
                    Message = "Tracing is enabled but no OTLP endpoint is configured - traces will be sent to console",
                    Section = "Monitoring",
                    Path = "Monitoring.OpenTelemetry.OtlpEndpoint",
                    Type = InfoType.General
                });
            }
        }

        /// <summary>
        /// Validates security configuration.
        /// </summary>
        /// <param name="security">The security configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateSecurity(SecurityConfiguration security, ValidationResult result)
        {
            var validationErrors = security.Validate().ToList();
            foreach (var error in validationErrors)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "SECURITY_CONFIG_ERROR",
                    Message = error,
                    Section = "Security",
                    Path = "Security"
                });
            }

            if (!security.RequireHttps)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WarningCode = "HTTPS_NOT_REQUIRED",
                    Message = "HTTPS is not required - this may expose sensitive data",
                    Section = "Security",
                    Path = "Security.RequireHttps",
                    Severity = WarningSeverity.High,
                    Remediation = "Enable HTTPS requirement for production deployments"
                });
            }

            if (security.EnableDetailedErrors)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WarningCode = "DETAILED_ERRORS_ENABLED",
                    Message = "Detailed error responses are enabled - this may expose sensitive information",
                    Section = "Security",
                    Path = "Security.EnableDetailedErrors",
                    Severity = WarningSeverity.Medium,
                    Remediation = "Disable detailed errors in production environments"
                });
            }
        }

        /// <summary>
        /// Validates feature flags configuration.
        /// </summary>
        /// <param name="featureFlags">The feature flags configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateFeatureFlags(FeatureFlagsConfiguration featureFlags, ValidationResult result)
        {
            var validationWarnings = featureFlags.Validate().ToList();
            foreach (var warning in validationWarnings)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WarningCode = "FEATURE_FLAG_WARNING",
                    Message = warning,
                    Section = "FeatureFlags",
                    Path = "FeatureFlags",
                    Severity = WarningSeverity.Low
                });
            }

            if (featureFlags.EnableExperimentalFeatures)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WarningCode = "EXPERIMENTAL_FEATURES_ENABLED",
                    Message = "Experimental features are enabled - these may be unstable",
                    Section = "FeatureFlags",
                    Path = "FeatureFlags.EnableExperimentalFeatures",
                    Severity = WarningSeverity.Medium,
                    Remediation = "Disable experimental features in production environments"
                });
            }
        }

        /// <summary>
        /// Validates tool generation configuration.
        /// </summary>
        /// <param name="toolGeneration">The tool generation configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateToolGeneration(McpToolGenerationOptions toolGeneration, ValidationResult result)
        {
            var validationErrors = toolGeneration.Validate().ToList();
            foreach (var error in validationErrors)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "TOOL_GEN_CONFIG_ERROR",
                    Message = error,
                    Section = "ToolGeneration",
                    Path = "ToolGeneration"
                });
            }
        }

        /// <summary>
        /// Validates sidecar-specific configuration requirements.
        /// </summary>
        /// <param name="configuration">The complete configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateSidecarSpecific(McpServerConfiguration configuration, ValidationResult result)
        {
            if (configuration.DeploymentMode != McpDeploymentMode.Sidecar)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "INVALID_DEPLOYMENT_MODE",
                    Message = $"Configuration deployment mode is {configuration.DeploymentMode} but must be Sidecar for sidecar deployment",
                    Section = "General",
                    Path = "DeploymentMode",
                    IsCritical = true,
                    Remediation = "Set DeploymentMode to Sidecar"
                });
            }

            if (configuration.Network.UseHostConfiguration)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WarningCode = "USE_HOST_CONFIG_IN_SIDECAR",
                    Message = "UseHostConfiguration is enabled in sidecar mode - this may cause issues",
                    Section = "Network",
                    Path = "Network.UseHostConfiguration",
                    Severity = WarningSeverity.Medium,
                    Remediation = "Set UseHostConfiguration to false for sidecar deployment"
                });
            }

            if (!configuration.Network.Port.HasValue)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "MISSING_PORT_SIDECAR",
                    Message = "Port must be specified for sidecar deployment",
                    Section = "Network",
                    Path = "Network.Port",
                    IsCritical = true,
                    Remediation = "Specify a port number for the sidecar service"
                });
            }
        }

        /// <summary>
        /// Validates configuration compatibility across sections.
        /// </summary>
        /// <param name="configuration">The complete configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateConfigurationCompatibility(McpServerConfiguration configuration, ValidationResult result)
        {
            // Check if authentication is required but OData service doesn't have auth configured
            if (configuration.Authentication.Enabled && 
                configuration.ODataService.Authentication.Type == ODataAuthenticationType.None)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WarningCode = "AUTH_MISMATCH",
                    Message = "MCP authentication is enabled but OData service has no authentication",
                    Section = "Authentication",
                    Path = "Authentication vs ODataService.Authentication",
                    Severity = WarningSeverity.Medium,
                    Context = "This may cause authentication token forwarding issues",
                    Remediation = "Configure OData service authentication or consider disabling MCP authentication"
                });
            }

            // Check if HTTPS is required but network doesn't enable HTTPS
            if (configuration.Security.RequireHttps && !configuration.Network.EnableHttps)
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "HTTPS_REQUIRED_BUT_NOT_ENABLED",
                    Message = "HTTPS is required by security configuration but not enabled in network configuration",
                    Section = "Security vs Network",
                    Path = "Security.RequireHttps vs Network.EnableHttps",
                    IsCritical = true,
                    Remediation = "Enable HTTPS in network configuration or disable HTTPS requirement"
                });
            }

            // Check if caching is enabled but cache TTL values are very short
            if (configuration.Caching.Enabled && 
                configuration.Caching.MetadataTtl < TimeSpan.FromMinutes(1))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WarningCode = "SHORT_CACHE_TTL",
                    Message = "Metadata cache TTL is very short which may impact performance",
                    Section = "Caching",
                    Path = "Caching.MetadataTtl",
                    Severity = WarningSeverity.Low,
                    Remediation = "Consider increasing cache TTL for better performance"
                });
            }
        }

        /// <summary>
        /// Validates critical OData service settings only.
        /// </summary>
        /// <param name="odataService">The OData service configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateCriticalODataService(ODataServiceConfiguration odataService, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(odataService.BaseUrl))
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "MISSING_ODATA_URL",
                    Message = "OData service base URL is required",
                    Section = "ODataService",
                    Path = "ODataService.BaseUrl",
                    IsCritical = true,
                    Remediation = "Provide a valid OData service URL"
                });
            }
        }

        /// <summary>
        /// Validates critical network settings only.
        /// </summary>
        /// <param name="network">The network configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateCriticalNetwork(NetworkConfiguration network, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(network.Host))
            {
                result.Errors.Add(new ValidationError
                {
                    ErrorCode = "MISSING_HOST",
                    Message = "Network host is required",
                    Section = "Network",
                    Path = "Network.Host",
                    IsCritical = true
                });
            }
        }

        /// <summary>
        /// Validates critical authentication settings only.
        /// </summary>
        /// <param name="authentication">The authentication configuration.</param>
        /// <param name="result">The validation result to update.</param>
        private static void ValidateCriticalAuthentication(McpAuthenticationOptions authentication, ValidationResult result)
        {
            if (authentication.Enabled && string.Equals(authentication.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                if (authentication.JwtBearer?.Authority is null || 
                    string.IsNullOrWhiteSpace(authentication.JwtBearer.Authority))
                {
                    result.Errors.Add(new ValidationError
                    {
                        ErrorCode = "MISSING_JWT_AUTHORITY",
                        Message = "JWT authority is required when Bearer authentication is enabled",
                        Section = "Authentication",
                        Path = "Authentication.JwtBearer.Authority",
                        IsCritical = true
                    });
                }
            }
        }

        #endregion
    }
}