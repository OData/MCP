using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;

namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Hosted service that performs configuration validation during application startup.
    /// </summary>
    /// <remarks>
    /// This service validates the configuration early in the startup process and can prevent
    /// the application from starting if critical configuration errors are detected.
    /// </remarks>
    public sealed class StartupValidationService : IHostedService
    {
        #region Fields

        private readonly IOptions<McpServerConfiguration> _configuration;
        private readonly IConfigurationValidationService _validationService;
        private readonly ILogger<StartupValidationService> _logger;
        private readonly IHostApplicationLifetime _applicationLifetime;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="StartupValidationService"/> class.
        /// </summary>
        /// <param name="configuration">The MCP server configuration.</param>
        /// <param name="validationService">The configuration validation service.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="applicationLifetime">The application lifetime service.</param>
        public StartupValidationService(
            IOptions<McpServerConfiguration> configuration,
            IConfigurationValidationService validationService,
            ILogger<StartupValidationService> logger,
            IHostApplicationLifetime applicationLifetime)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        }

        #endregion

        #region IHostedService Implementation

        /// <summary>
        /// Starts the startup validation service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting configuration validation");

            try
            {
                // Perform critical configuration validation
                var criticalResult = _validationService.ValidateCriticalConfiguration(_configuration.Value);
                
                if (!criticalResult.IsValid)
                {
                    _logger.LogCritical("Critical configuration validation failed - application cannot start");
                    
                    foreach (var error in criticalResult.Errors)
                    {
                        _logger.LogCritical("Critical configuration error in {Section}: {Message}", 
                            error.Section, error.Message);
                    }

                    // Stop the application
                    _applicationLifetime.StopApplication();
                    return Task.CompletedTask;
                }

                _logger.LogInformation("Critical configuration validation passed");

                // Perform comprehensive validation
                var fullResult = _validationService.ValidateConfiguration(_configuration.Value);

                // Log all validation results
                LogValidationResults(fullResult);

                // If there are non-critical errors, log them but don't stop the application
                if (fullResult.HasErrors)
                {
                    var nonCriticalErrors = fullResult.Errors.Where(e => !e.IsCritical).ToList();
                    if (nonCriticalErrors.Count > 0)
                    {
                        _logger.LogWarning("Configuration has {Count} non-critical errors that should be addressed", 
                            nonCriticalErrors.Count);
                    }
                }

                _logger.LogInformation("Startup validation completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Startup validation failed with exception - application cannot start");
                _applicationLifetime.StopApplication();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the startup validation service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Startup validation service stopped");
            return Task.CompletedTask;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Logs the validation results in a structured format.
        /// </summary>
        /// <param name="result">The validation result to log.</param>
        private void LogValidationResults(ValidationResult result)
        {
            _logger.LogInformation("Configuration validation summary: {Summary}", result.Summary);

            // Log errors
            foreach (var error in result.Errors)
            {
                var logLevel = error.IsCritical ? LogLevel.Critical : LogLevel.Error;
                _logger.Log(logLevel, "Configuration error [{ErrorCode}] in {Section}: {Message}", 
                    error.ErrorCode, error.Section, error.Message);
                
                if (!string.IsNullOrWhiteSpace(error.Remediation))
                {
                    _logger.Log(logLevel, "Remediation for {ErrorCode}: {Remediation}", 
                        error.ErrorCode, error.Remediation);
                }
            }

            // Log warnings
            foreach (var warning in result.Warnings)
            {
                var logLevel = warning.Severity switch
                {
                    WarningSeverity.High => LogLevel.Warning,
                    WarningSeverity.Medium => LogLevel.Warning,
                    WarningSeverity.Low => LogLevel.Information,
                    _ => LogLevel.Information
                };

                _logger.Log(logLevel, "Configuration warning [{WarningCode}] in {Section}: {Message}", 
                    warning.WarningCode, warning.Section, warning.Message);
                
                if (!string.IsNullOrWhiteSpace(warning.Remediation))
                {
                    _logger.Log(logLevel, "Recommendation for {WarningCode}: {Remediation}", 
                        warning.WarningCode, warning.Remediation);
                }
            }

            // Log informational messages
            foreach (var info in result.Information)
            {
                var logLevel = info.Type switch
                {
                    InfoType.Recommendation => LogLevel.Information,
                    InfoType.BestPractice => LogLevel.Information,
                    InfoType.Feature => LogLevel.Debug,
                    _ => LogLevel.Debug
                };

                _logger.Log(logLevel, "Configuration info in {Section}: {Message}", 
                    info.Section, info.Message);
            }

            // Log configuration summary
            LogConfigurationSummary();
        }

        /// <summary>
        /// Logs a summary of the current configuration.
        /// </summary>
        private void LogConfigurationSummary()
        {
            var config = _configuration.Value;

            _logger.LogInformation("Configuration Summary:");
            _logger.LogInformation("  Deployment Mode: {DeploymentMode}", config.DeploymentMode);
            _logger.LogInformation("  OData Service: {ServiceUrl}", config.ODataService.BaseUrl ?? "Not configured");
            _logger.LogInformation("  Authentication: {AuthEnabled} ({AuthScheme})", 
                config.Authentication.Enabled ? "Enabled" : "Disabled", 
                config.Authentication.Scheme);
            _logger.LogInformation("  Network: {Host}:{Port} (HTTPS: {Https})", 
                config.Network.Host, 
                config.Network.Port?.ToString() ?? "Auto",
                config.Network.EnableHttps ? "Enabled" : "Disabled");
            _logger.LogInformation("  Caching: {CacheEnabled} ({CacheProvider})", 
                config.Caching.Enabled ? "Enabled" : "Disabled",
                config.Caching.ProviderType);
            _logger.LogInformation("  Monitoring: Metrics={Metrics}, Tracing={Tracing}, HealthChecks={Health}", 
                config.Monitoring.EnableMetrics,
                config.Monitoring.EnableTracing,
                config.Monitoring.EnableHealthChecks);
            
            var enabledFeatures = config.FeatureFlags.GetEnabledFlags().ToList();
            if (enabledFeatures.Count > 0)
            {
                _logger.LogInformation("  Enabled Features: {Features}", string.Join(", ", enabledFeatures));
            }
        }

        #endregion
    }
}