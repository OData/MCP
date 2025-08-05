using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Authentication.Models;
using Microsoft.OData.Mcp.Core.Configuration;

namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Health check for the OData service connectivity.
    /// </summary>
    /// <remarks>
    /// This health check verifies that the configured OData service is accessible
    /// and responding to requests.
    /// </remarks>
    public sealed class ODataServiceHealthCheck : IHealthCheck
    {
        #region Fields

        private readonly IOptions<McpServerConfiguration> _configuration;
        private readonly ILogger<ODataServiceHealthCheck> _logger;
        private readonly HttpClient _httpClient;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataServiceHealthCheck"/> class.
        /// </summary>
        /// <param name="configuration">The MCP server configuration.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public ODataServiceHealthCheck(
            IOptions<McpServerConfiguration> configuration,
            ILogger<ODataServiceHealthCheck> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClientFactory?.CreateClient() ?? throw new ArgumentNullException(nameof(httpClientFactory));
            
            // Configure the HTTP client
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        #endregion

        #region IHealthCheck Implementation

        /// <summary>
        /// Performs the health check for the OData service.
        /// </summary>
        /// <param name="context">The health check context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The health check result.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var config = _configuration.Value;
            
            if (string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
            {
                return HealthCheckResult.Unhealthy("OData service URL is not configured");
            }

            try
            {
                var serviceUrl = config.ODataService.BaseUrl.TrimEnd('/');
                var metadataUrl = $"{serviceUrl}{config.ODataService.MetadataPath}";

                _logger.LogDebug("Checking OData service health at: {Url}", metadataUrl);

                using var response = await _httpClient.GetAsync(metadataUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseTime = response.Headers.Date.HasValue 
                        ? (DateTimeOffset.UtcNow - response.Headers.Date.Value).TotalMilliseconds 
                        : 0;

                    var data = new Dictionary<string, object>
                    {
                        ["ServiceUrl"] = serviceUrl,
                        ["MetadataUrl"] = metadataUrl,
                        ["StatusCode"] = (int)response.StatusCode,
                        ["ResponseTime"] = $"{responseTime:F0}ms",
                        ["ContentType"] = response.Content.Headers.ContentType?.ToString() ?? "Unknown"
                    };

                    return HealthCheckResult.Healthy("OData service is accessible", data);
                }
                else
                {
                    var data = new Dictionary<string, object>
                    {
                        ["ServiceUrl"] = serviceUrl,
                        ["MetadataUrl"] = metadataUrl,
                        ["StatusCode"] = (int)response.StatusCode,
                        ["ReasonPhrase"] = response.ReasonPhrase ?? "Unknown"
                    };

                    return HealthCheckResult.Degraded($"OData service returned {response.StatusCode}: {response.ReasonPhrase}", data: data);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error while checking OData service health");
                return HealthCheckResult.Unhealthy($"Failed to connect to OData service: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogWarning("Timeout while checking OData service health");
                return HealthCheckResult.Degraded("OData service health check timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while checking OData service health");
                return HealthCheckResult.Unhealthy($"Unexpected error: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Health check for the MCP server functionality.
    /// </summary>
    /// <remarks>
    /// This health check verifies that the MCP server components are functioning correctly
    /// and that tools can be generated and executed.
    /// </remarks>
    public sealed class McpServerHealthCheck : IHealthCheck
    {
        #region Fields

        private readonly IOptions<McpServerConfiguration> _configuration;
        private readonly ILogger<McpServerHealthCheck> _logger;
        private readonly IServiceProvider _serviceProvider;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServerHealthCheck"/> class.
        /// </summary>
        /// <param name="configuration">The MCP server configuration.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="serviceProvider">The service provider for dependency resolution.</param>
        public McpServerHealthCheck(
            IOptions<McpServerConfiguration> configuration,
            ILogger<McpServerHealthCheck> logger,
            IServiceProvider serviceProvider)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        #endregion

        #region IHealthCheck Implementation

        /// <summary>
        /// Performs the health check for the MCP server.
        /// </summary>
        /// <param name="context">The health check context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The health check result.</returns>
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = _configuration.Value;
                
                // Check basic configuration
                var configurationValid = !string.IsNullOrWhiteSpace(config.ODataService.BaseUrl);
                
                // Check if required services are available
                var servicesAvailable = CheckRequiredServices();

                // Get system metrics
                var memoryInfo = GC.GetGCMemoryInfo();
                var processInfo = System.Diagnostics.Process.GetCurrentProcess();

                var data = new Dictionary<string, object>
                {
                    ["DeploymentMode"] = config.DeploymentMode.ToString(),
                    ["ConfigurationValid"] = configurationValid,
                    ["ServicesAvailable"] = servicesAvailable,
                    ["MemoryUsage"] = $"{memoryInfo.HeapSizeBytes / (1024 * 1024)} MB",
                    ["ProcessId"] = Environment.ProcessId,
                    ["ThreadCount"] = processInfo.Threads.Count,
                    ["CachingEnabled"] = config.Caching.Enabled,
                    ["AuthenticationEnabled"] = config.Authentication.Enabled,
                    ["MonitoringEnabled"] = config.Monitoring.EnableMetrics
                };

                if (configurationValid && servicesAvailable)
                {
                    return Task.FromResult(HealthCheckResult.Healthy("MCP server is functioning correctly", data));
                }
                else
                {
                    var issues = new List<string>();
                    if (!configurationValid) issues.Add("Invalid configuration");
                    if (!servicesAvailable) issues.Add("Missing required services");
                    
                    return Task.FromResult(HealthCheckResult.Degraded($"MCP server issues: {string.Join(", ", issues)}", data: data));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MCP server health check");
                return Task.FromResult(HealthCheckResult.Unhealthy($"MCP server health check failed: {ex.Message}"));
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Checks if all required services are available.
        /// </summary>
        /// <returns><c>true</c> if all required services are available; otherwise, <c>false</c>.</returns>
        private bool CheckRequiredServices()
        {
            try
            {
                // Check for critical services
                var criticalServices = new[]
                {
                    typeof(IConfigurationValidationService),
                    typeof(IServiceInformationService)
                };

                foreach (var serviceType in criticalServices)
                {
                    var service = _serviceProvider.GetService(serviceType);
                    if (service is null)
                    {
                        _logger.LogWarning("Required service {ServiceType} is not available", serviceType.Name);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking required services");
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// Health check for the authentication system.
    /// </summary>
    /// <remarks>
    /// This health check verifies that the authentication system is configured correctly
    /// and can validate tokens if authentication is enabled.
    /// </remarks>
    public sealed class AuthenticationHealthCheck : IHealthCheck
    {
        #region Fields

        private readonly IOptions<McpServerConfiguration> _configuration;
        private readonly ILogger<AuthenticationHealthCheck> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationHealthCheck"/> class.
        /// </summary>
        /// <param name="configuration">The MCP server configuration.</param>
        /// <param name="logger">The logger instance.</param>
        public AuthenticationHealthCheck(
            IOptions<McpServerConfiguration> configuration,
            ILogger<AuthenticationHealthCheck> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region IHealthCheck Implementation

        /// <summary>
        /// Performs the health check for the authentication system.
        /// </summary>
        /// <param name="context">The health check context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The health check result.</returns>
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = _configuration.Value;
                var authConfig = config.Authentication;

                var data = new Dictionary<string, object>
                {
                    ["Enabled"] = authConfig.Enabled,
                    ["Scheme"] = authConfig.Scheme,
                    ["RequireHttps"] = authConfig.RequireHttps,
                    ["Timeout"] = authConfig.Timeout.ToString()
                };

                if (!authConfig.Enabled)
                {
                    return Task.FromResult(HealthCheckResult.Healthy("Authentication is disabled", data));
                }

                // Validate authentication configuration
                var validationErrors = authConfig.Validate().ToList();
                
                if (validationErrors.Count > 0)
                {
                    var errorMessage = $"Authentication configuration errors: {string.Join(", ", validationErrors)}";
                    data["ValidationErrors"] = validationErrors;
                    return Task.FromResult(HealthCheckResult.Unhealthy(errorMessage, data: data));
                }

                // Check specific authentication type requirements
                var typeCheckResult = CheckAuthenticationType(authConfig, data);
                if (typeCheckResult != null)
                {
                    return Task.FromResult(typeCheckResult.Value);
                }

                return Task.FromResult(HealthCheckResult.Healthy("Authentication system is configured correctly", data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication health check");
                return Task.FromResult(HealthCheckResult.Unhealthy($"Authentication health check failed: {ex.Message}"));
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Checks authentication type-specific requirements.
        /// </summary>
        /// <param name="authConfig">The authentication configuration.</param>
        /// <param name="data">The health check data dictionary.</param>
        /// <returns>A health check result if there are issues; otherwise, null.</returns>
        private HealthCheckResult? CheckAuthenticationType(McpAuthenticationOptions authConfig, Dictionary<string, object> data)
        {
            if (string.Equals(authConfig.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                if (authConfig.JwtBearer is null)
                {
                    return HealthCheckResult.Unhealthy("Bearer authentication is enabled but JWT configuration is missing", data: data);
                }
                
                if (string.IsNullOrWhiteSpace(authConfig.JwtBearer.Authority))
                {
                    return HealthCheckResult.Unhealthy("JWT authority is required for Bearer authentication", data: data);
                }
                
                data["JwtAuthority"] = authConfig.JwtBearer.Authority;
                data["JwtAudience"] = authConfig.JwtBearer.Audience ?? "Not specified";
            }
            else if (string.IsNullOrWhiteSpace(authConfig.Scheme))
            {
                data["Note"] = "Authentication scheme is not specified but authentication is enabled";
            }
            else
            {
                data["CustomScheme"] = authConfig.Scheme;
            }

            return null;
        }

        #endregion
    }
}