using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Service for providing runtime information about the MCP sidecar service.
    /// </summary>
    /// <remarks>
    /// This service collects and provides information about the running MCP server instance,
    /// including version, configuration, health status, and runtime metrics.
    /// </remarks>
    public interface IServiceInformationService
    {
        /// <summary>
        /// Gets comprehensive service information.
        /// </summary>
        /// <returns>Service information including version, status, and configuration details.</returns>
        ServiceInformation GetServiceInformation();

        /// <summary>
        /// Gets basic service status information.
        /// </summary>
        /// <returns>Basic service status and health information.</returns>
        ServiceStatus GetServiceStatus();

        /// <summary>
        /// Gets detailed runtime metrics.
        /// </summary>
        /// <returns>Runtime metrics including performance counters and resource usage.</returns>
        RuntimeMetrics GetRuntimeMetrics();

        /// <summary>
        /// Gets the current configuration summary.
        /// </summary>
        /// <returns>Configuration summary with sensitive information redacted.</returns>
        ConfigurationSummary GetConfigurationSummary();
    }

    /// <summary>
    /// Comprehensive service information.
    /// </summary>
    public sealed class ServiceInformation
    {
        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the service version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the deployment mode.
        /// </summary>
        public string DeploymentMode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the service start time.
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Gets or sets the service uptime.
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets or sets the environment information.
        /// </summary>
        public EnvironmentInformation Environment { get; set; } = new();

        /// <summary>
        /// Gets or sets the service status.
        /// </summary>
        public ServiceStatus Status { get; set; } = new();

        /// <summary>
        /// Gets or sets the runtime metrics.
        /// </summary>
        public RuntimeMetrics Metrics { get; set; } = new();

        /// <summary>
        /// Gets or sets the configuration summary.
        /// </summary>
        public ConfigurationSummary Configuration { get; set; } = new();

        /// <summary>
        /// Gets or sets the endpoint information.
        /// </summary>
        public List<EndpointInformation> Endpoints { get; set; } = new();
    }

    /// <summary>
    /// Service status information.
    /// </summary>
    public sealed class ServiceStatus
    {
        /// <summary>
        /// Gets or sets the overall service health status.
        /// </summary>
        public string Health { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the service is ready to accept requests.
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the service is alive.
        /// </summary>
        public bool IsAlive { get; set; }

        /// <summary>
        /// Gets or sets the OData service connectivity status.
        /// </summary>
        public string ODataServiceStatus { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the authentication service status.
        /// </summary>
        public string AuthenticationStatus { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cache service status.
        /// </summary>
        public string CacheStatus { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any current warnings or issues.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets the last health check time.
        /// </summary>
        public DateTimeOffset LastHealthCheck { get; set; }
    }

    /// <summary>
    /// Runtime metrics information.
    /// </summary>
    public sealed class RuntimeMetrics
    {
        /// <summary>
        /// Gets or sets the total number of requests processed.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of requests currently being processed.
        /// </summary>
        public int ActiveRequests { get; set; }

        /// <summary>
        /// Gets or sets the average request duration in milliseconds.
        /// </summary>
        public double AverageRequestDuration { get; set; }

        /// <summary>
        /// Gets or sets the current memory usage in bytes.
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the CPU usage percentage.
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// Gets or sets the number of active connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Gets or sets cache statistics.
        /// </summary>
        public CacheStatistics Cache { get; set; } = new();

        /// <summary>
        /// Gets or sets error statistics.
        /// </summary>
        public ErrorStatistics Errors { get; set; } = new();
    }

    /// <summary>
    /// Cache statistics.
    /// </summary>
    public sealed class CacheStatistics
    {
        /// <summary>
        /// Gets or sets the cache hit rate percentage.
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Gets or sets the total number of cache hits.
        /// </summary>
        public long TotalHits { get; set; }

        /// <summary>
        /// Gets or sets the total number of cache misses.
        /// </summary>
        public long TotalMisses { get; set; }

        /// <summary>
        /// Gets or sets the number of items currently in cache.
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        /// Gets or sets the total cache memory usage in bytes.
        /// </summary>
        public long MemoryUsage { get; set; }
    }

    /// <summary>
    /// Error statistics.
    /// </summary>
    public sealed class ErrorStatistics
    {
        /// <summary>
        /// Gets or sets the total number of errors.
        /// </summary>
        public long TotalErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of authentication errors.
        /// </summary>
        public long AuthenticationErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of OData service errors.
        /// </summary>
        public long ODataServiceErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of validation errors.
        /// </summary>
        public long ValidationErrors { get; set; }

        /// <summary>
        /// Gets or sets the error rate percentage.
        /// </summary>
        public double ErrorRate { get; set; }
    }

    /// <summary>
    /// Configuration summary with sensitive information redacted.
    /// </summary>
    public sealed class ConfigurationSummary
    {
        /// <summary>
        /// Gets or sets the deployment mode.
        /// </summary>
        public string DeploymentMode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OData service base URL.
        /// </summary>
        public string ODataServiceUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the authentication configuration type.
        /// </summary>
        public string AuthenticationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether caching is enabled.
        /// </summary>
        public bool CachingEnabled { get; set; }

        /// <summary>
        /// Gets or sets the cache provider type.
        /// </summary>
        public string CacheProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether monitoring is enabled.
        /// </summary>
        public bool MonitoringEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// </summary>
        public bool TracingEnabled { get; set; }

        /// <summary>
        /// Gets or sets the enabled feature flags.
        /// </summary>
        public List<string> EnabledFeatures { get; set; } = new();
    }

    /// <summary>
    /// Environment information.
    /// </summary>
    public sealed class EnvironmentInformation
    {
        /// <summary>
        /// Gets or sets the environment name (Development, Production, etc.).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the machine name.
        /// </summary>
        public string MachineName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operating system description.
        /// </summary>
        public string OperatingSystem { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the .NET runtime version.
        /// </summary>
        public string RuntimeVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process ID.
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the working directory.
        /// </summary>
        public string WorkingDirectory { get; set; } = string.Empty;
    }

    /// <summary>
    /// Endpoint information.
    /// </summary>
    public sealed class EndpointInformation
    {
        /// <summary>
        /// Gets or sets the endpoint name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the endpoint URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTTP methods supported by the endpoint.
        /// </summary>
        public List<string> Methods { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the endpoint requires authentication.
        /// </summary>
        public bool RequiresAuthentication { get; set; }

        /// <summary>
        /// Gets or sets the endpoint description.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}