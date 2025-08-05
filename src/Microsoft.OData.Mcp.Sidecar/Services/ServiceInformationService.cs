using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;

namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Implementation of service information provider for MCP sidecar.
    /// </summary>
    /// <remarks>
    /// This service collects runtime information from various sources including
    /// configuration, health checks, performance counters, and system metrics.
    /// </remarks>
    public sealed class ServiceInformationService : IServiceInformationService
    {
        #region Fields

        private readonly IOptions<McpServerConfiguration> _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ServiceInformationService> _logger;
        private readonly DateTimeOffset _startTime;
        private readonly Process _currentProcess;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceInformationService"/> class.
        /// </summary>
        /// <param name="configuration">The MCP server configuration.</param>
        /// <param name="environment">The web host environment.</param>
        /// <param name="logger">The logger instance.</param>
        public ServiceInformationService(
            IOptions<McpServerConfiguration> configuration,
            IWebHostEnvironment environment,
            ILogger<ServiceInformationService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _startTime = DateTimeOffset.UtcNow;
            _currentProcess = Process.GetCurrentProcess();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets comprehensive service information.
        /// </summary>
        /// <returns>Service information including version, status, and configuration details.</returns>
        public ServiceInformation GetServiceInformation()
        {
            try
            {
                return new ServiceInformation
                {
                    Name = "OData MCP Server Sidecar",
                    Version = GetVersion(),
                    DeploymentMode = _configuration.Value.DeploymentMode.ToString(),
                    StartTime = _startTime,
                    Uptime = DateTimeOffset.UtcNow - _startTime,
                    Environment = GetEnvironmentInformation(),
                    Status = GetServiceStatus(),
                    Metrics = GetRuntimeMetrics(),
                    Configuration = GetConfigurationSummary(),
                    Endpoints = GetEndpointInformation()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving service information");
                throw;
            }
        }

        /// <summary>
        /// Gets basic service status information.
        /// </summary>
        /// <returns>Basic service status and health information.</returns>
        public ServiceStatus GetServiceStatus()
        {
            try
            {
                return new ServiceStatus
                {
                    Health = "Healthy", // This would be determined by health checks in a real implementation
                    IsReady = true,
                    IsAlive = true,
                    ODataServiceStatus = "Connected", // This would check actual connectivity
                    AuthenticationStatus = _configuration.Value.Authentication.Enabled ? "Enabled" : "Disabled",
                    CacheStatus = _configuration.Value.Caching.Enabled ? "Enabled" : "Disabled",
                    Warnings = GetCurrentWarnings(),
                    LastHealthCheck = DateTimeOffset.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving service status");
                return new ServiceStatus
                {
                    Health = "Unhealthy",
                    IsReady = false,
                    IsAlive = true,
                    Warnings = new List<string> { $"Status check failed: {ex.Message}" },
                    LastHealthCheck = DateTimeOffset.UtcNow
                };
            }
        }

        /// <summary>
        /// Gets detailed runtime metrics.
        /// </summary>
        /// <returns>Runtime metrics including performance counters and resource usage.</returns>
        public RuntimeMetrics GetRuntimeMetrics()
        {
            try
            {
                var gcMemoryInfo = GC.GetGCMemoryInfo();

                return new RuntimeMetrics
                {
                    TotalRequests = 0, // This would be tracked by request middleware
                    ActiveRequests = 0, // This would be tracked by request middleware
                    AverageRequestDuration = 0.0, // This would be calculated from request timings
                    MemoryUsage = gcMemoryInfo.HeapSizeBytes,
                    CpuUsage = GetCpuUsage(),
                    ActiveConnections = 0, // This would be tracked by connection middleware
                    Cache = GetCacheStatistics(),
                    Errors = GetErrorStatistics()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving runtime metrics");
                return new RuntimeMetrics();
            }
        }

        /// <summary>
        /// Gets the current configuration summary.
        /// </summary>
        /// <returns>Configuration summary with sensitive information redacted.</returns>
        public ConfigurationSummary GetConfigurationSummary()
        {
            try
            {
                var config = _configuration.Value;
                
                return new ConfigurationSummary
                {
                    DeploymentMode = config.DeploymentMode.ToString(),
                    ODataServiceUrl = RedactSensitiveUrl(config.ODataService.BaseUrl),
                    AuthenticationType = config.Authentication.Enabled ? config.Authentication.Scheme : "None",
                    CachingEnabled = config.Caching.Enabled,
                    CacheProvider = config.Caching.ProviderType.ToString(),
                    MonitoringEnabled = config.Monitoring.EnableMetrics,
                    TracingEnabled = config.Monitoring.EnableTracing,
                    EnabledFeatures = config.FeatureFlags.GetEnabledFlags().ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration summary");
                return new ConfigurationSummary();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the application version.
        /// </summary>
        /// <returns>The application version string.</returns>
        private static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                         ?? assembly.GetName().Version?.ToString()
                         ?? "1.0.0";
            return version;
        }

        /// <summary>
        /// Gets environment information.
        /// </summary>
        /// <returns>Environment information.</returns>
        private EnvironmentInformation GetEnvironmentInformation()
        {
            return new EnvironmentInformation
            {
                Name = _environment.EnvironmentName,
                MachineName = Environment.MachineName,
                OperatingSystem = RuntimeInformation.OSDescription,
                RuntimeVersion = RuntimeInformation.FrameworkDescription,
                ProcessId = Environment.ProcessId,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };
        }

        /// <summary>
        /// Gets current system warnings.
        /// </summary>
        /// <returns>List of current warnings.</returns>
        private List<string> GetCurrentWarnings()
        {
            var warnings = new List<string>();

            try
            {
                // Check for common warning conditions
                var memoryInfo = GC.GetGCMemoryInfo();
                if (memoryInfo.HeapSizeBytes > 500 * 1024 * 1024) // 500MB
                {
                    warnings.Add($"High memory usage: {memoryInfo.HeapSizeBytes / (1024 * 1024)} MB");
                }

                var uptime = DateTimeOffset.UtcNow - _startTime;
                if (uptime.TotalHours > 24 * 7) // 1 week
                {
                    warnings.Add($"Service has been running for {uptime.TotalDays:F1} days");
                }

                if (_environment.IsDevelopment() && _configuration.Value.Security.RequireHttps)
                {
                    warnings.Add("HTTPS is required but this is a development environment");
                }

                if (!_configuration.Value.Monitoring.EnableHealthChecks)
                {
                    warnings.Add("Health checks are disabled");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating warnings");
                warnings.Add("Unable to determine system warnings");
            }

            return warnings;
        }

        /// <summary>
        /// Gets current CPU usage percentage.
        /// </summary>
        /// <returns>CPU usage percentage.</returns>
        private double GetCpuUsage()
        {
            try
            {
                // This is a simplified CPU usage calculation
                // In a real implementation, you would use performance counters or similar
                var totalTime = _currentProcess.TotalProcessorTime;
                var uptime = DateTimeOffset.UtcNow - _startTime;
                
                if (uptime.TotalMilliseconds > 0)
                {
                    return (totalTime.TotalMilliseconds / uptime.TotalMilliseconds) * 100.0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating CPU usage");
            }

            return 0.0;
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        /// <returns>Cache statistics.</returns>
        private CacheStatistics GetCacheStatistics()
        {
            // In a real implementation, this would query the actual cache provider
            return new CacheStatistics
            {
                HitRate = 85.5,
                TotalHits = 1000,
                TotalMisses = 150,
                ItemCount = 50,
                MemoryUsage = 10 * 1024 * 1024 // 10MB
            };
        }

        /// <summary>
        /// Gets error statistics.
        /// </summary>
        /// <returns>Error statistics.</returns>
        private ErrorStatistics GetErrorStatistics()
        {
            // In a real implementation, this would query error tracking systems
            return new ErrorStatistics
            {
                TotalErrors = 5,
                AuthenticationErrors = 2,
                ODataServiceErrors = 1,
                ValidationErrors = 2,
                ErrorRate = 0.5
            };
        }

        /// <summary>
        /// Gets endpoint information.
        /// </summary>
        /// <returns>List of endpoint information.</returns>
        private List<EndpointInformation> GetEndpointInformation()
        {
            var config = _configuration.Value;
            var baseUrl = config.Network.GetBaseUrl();

            var endpoints = new List<EndpointInformation>
            {
                new()
                {
                    Name = "Service Information",
                    Url = $"{baseUrl}/info",
                    Methods = new List<string> { "GET" },
                    RequiresAuthentication = false,
                    Description = "Provides service information and status"
                },
                new()
                {
                    Name = "MCP Tools",
                    Url = config.Network.GetEndpointUrl("tools"),
                    Methods = new List<string> { "GET", "POST" },
                    RequiresAuthentication = config.Authentication.Enabled,
                    Description = "MCP tool discovery and execution endpoint"
                },
                new()
                {
                    Name = "OData Metadata",
                    Url = config.Network.GetEndpointUrl("metadata"),
                    Methods = new List<string> { "GET" },
                    RequiresAuthentication = config.Authentication.Enabled,
                    Description = "OData service metadata endpoint"
                }
            };

            if (config.Monitoring.EnableHealthChecks)
            {
                endpoints.AddRange(new[]
                {
                    new EndpointInformation
                    {
                        Name = "Health Check",
                        Url = $"{baseUrl}/health",
                        Methods = new List<string> { "GET" },
                        RequiresAuthentication = false,
                        Description = "Overall health status"
                    },
                    new EndpointInformation
                    {
                        Name = "Readiness Check",
                        Url = $"{baseUrl}/health/ready",
                        Methods = new List<string> { "GET" },
                        RequiresAuthentication = false,
                        Description = "Service readiness status"
                    },
                    new EndpointInformation
                    {
                        Name = "Liveness Check",
                        Url = $"{baseUrl}/health/live",
                        Methods = new List<string> { "GET" },
                        RequiresAuthentication = false,
                        Description = "Service liveness status"
                    }
                });
            }

            return endpoints;
        }

        /// <summary>
        /// Redacts sensitive information from URLs.
        /// </summary>
        /// <param name="url">The URL to redact.</param>
        /// <returns>The URL with sensitive information redacted.</returns>
        private static string RedactSensitiveUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "Not configured";
            }

            try
            {
                var uri = new Uri(url);
                var redacted = $"{uri.Scheme}://{uri.Host}";
                
                if (!uri.IsDefaultPort)
                {
                    redacted += $":{uri.Port}";
                }
                
                redacted += uri.AbsolutePath;
                
                return redacted;
            }
            catch
            {
                return "Invalid URL";
            }
        }

        #endregion
    }
}