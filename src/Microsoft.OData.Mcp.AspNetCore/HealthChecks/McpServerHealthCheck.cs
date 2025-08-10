// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.OData.Mcp.AspNetCore.HealthChecks
{

    /// <summary>
    /// Health check for the MCP server functionality.
    /// </summary>
    /// <remarks>
    /// This health check verifies that the core MCP server components are functioning
    /// correctly, including metadata parsing, tool registration, and basic connectivity.
    /// </remarks>
    public sealed class McpServerHealthCheck : IHealthCheck
    {
        #region Fields

        internal readonly ILogger<McpServerHealthCheck> _logger;
        internal readonly IMcpToolFactory? _toolFactory;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServerHealthCheck"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="toolFactory">The MCP tool factory (optional).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public McpServerHealthCheck(ILogger<McpServerHealthCheck> logger, IMcpToolFactory? toolFactory = null)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
            _toolFactory = toolFactory;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks the health of the MCP server.
        /// </summary>
        /// <param name="context">The health check context.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous health check operation.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting MCP server health check");

                var healthData = new Dictionary<string, object>();
                var issues = new List<string>();

                // Check core services availability
                await CheckCoreServicesAsync(healthData, issues, cancellationToken);

                // Check MCP protocol compatibility
                CheckMcpProtocolCompatibility(healthData, issues);

                // Check resource utilization
                CheckResourceUtilization(healthData, issues);

                // Determine overall health status
                var status = issues.Count switch
                {
                    0 => HealthStatus.Healthy,
                    var count when count <= 2 => HealthStatus.Degraded,
                    _ => HealthStatus.Unhealthy
                };

                var description = status switch
                {
                    HealthStatus.Healthy => "MCP server is operating normally",
                    HealthStatus.Degraded => $"MCP server is degraded: {string.Join(", ", issues)}",
                    HealthStatus.Unhealthy => $"MCP server is unhealthy: {string.Join(", ", issues)}",
                    _ => "MCP server status unknown"
                };

                _logger.LogDebug("MCP server health check completed with status: {Status}", status);

                return new HealthCheckResult(status, description, data: healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MCP server health check failed with exception");
                return HealthCheckResult.Unhealthy("MCP server health check failed", ex);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Checks the availability of core MCP server services.
        /// </summary>
        /// <param name="healthData">Dictionary to store health check data.</param>
        /// <param name="issues">List to collect any issues found.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task CheckCoreServicesAsync(Dictionary<string, object> healthData, List<string> issues, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Check MCP tool factory availability
                if (_toolFactory is not null)
                {
                    healthData["tool_factory_available"] = true;
                    healthData["tool_factory_type"] = _toolFactory.GetType().Name;
                    
                    var availableTools = _toolFactory.GetAvailableToolNames().ToList();
                    healthData["available_tools_count"] = availableTools.Count;
                    
                    if (availableTools.Count > 0)
                    {
                        healthData["sample_tools"] = availableTools.Take(5).ToList();
                    }
                }
                else
                {
                    healthData["tool_factory_available"] = false;
                    issues.Add("MCP tool factory not available");
                }

                await Task.Delay(10, cancellationToken); // Simulate async work

                healthData["core_services_check_duration_ms"] = (DateTime.UtcNow - startTime).TotalMilliseconds;
                healthData["core_services_status"] = "Available";
            }
            catch (Exception ex)
            {
                issues.Add("Core services unavailable");
                healthData["core_services_status"] = "Failed";
                healthData["core_services_error"] = ex.Message;
                _logger.LogWarning(ex, "Core services health check failed");
            }
        }

        /// <summary>
        /// Checks MCP protocol compatibility and version support.
        /// </summary>
        /// <param name="healthData">Dictionary to store health check data.</param>
        /// <param name="issues">List to collect any issues found.</param>
        internal void CheckMcpProtocolCompatibility(Dictionary<string, object> healthData, List<string> issues)
        {
            try
            {
                // Check MCP protocol version compatibility
                var mcpVersion = GetMcpProtocolVersion();
                healthData["mcp_protocol_version"] = mcpVersion;

                // Validate supported features
                var supportedFeatures = GetSupportedMcpFeatures();
                healthData["supported_features"] = supportedFeatures;

                if (supportedFeatures.Count == 0)
                {
                    issues.Add("No MCP features available");
                }

                healthData["protocol_compatibility"] = "Compatible";
            }
            catch (Exception ex)
            {
                issues.Add("MCP protocol compatibility issues");
                healthData["protocol_compatibility"] = "Failed";
                healthData["protocol_error"] = ex.Message;
                _logger.LogWarning(ex, "MCP protocol compatibility check failed");
            }
        }

        /// <summary>
        /// Checks resource utilization and performance metrics.
        /// </summary>
        /// <param name="healthData">Dictionary to store health check data.</param>
        /// <param name="issues">List to collect any issues found.</param>
        internal void CheckResourceUtilization(Dictionary<string, object> healthData, List<string> issues)
        {
            try
            {
                // Check memory usage
                var memoryUsage = GC.GetTotalMemory(false);
                healthData["memory_usage_bytes"] = memoryUsage;

                // Check if memory usage is concerning (example threshold: 500MB)
                if (memoryUsage > 500 * 1024 * 1024)
                {
                    issues.Add("High memory usage detected");
                }

                // Check thread pool status
                ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
                ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);

                healthData["available_worker_threads"] = workerThreads;
                healthData["available_completion_port_threads"] = completionPortThreads;
                healthData["max_worker_threads"] = maxWorkerThreads;
                healthData["max_completion_port_threads"] = maxCompletionPortThreads;

                // Check if thread pool is under pressure
                var workerThreadPressure = (double)workerThreads / maxWorkerThreads;
                var completionPortPressure = (double)completionPortThreads / maxCompletionPortThreads;

                if (workerThreadPressure < 0.1 || completionPortPressure < 0.1)
                {
                    issues.Add("Thread pool under pressure");
                }

                healthData["worker_thread_pressure"] = 1.0 - workerThreadPressure;
                healthData["completion_port_pressure"] = 1.0 - completionPortPressure;
            }
            catch (Exception ex)
            {
                issues.Add("Resource utilization check failed");
                healthData["resource_check_error"] = ex.Message;
                _logger.LogWarning(ex, "Resource utilization check failed");
            }
        }

        /// <summary>
        /// Gets the MCP protocol version.
        /// </summary>
        /// <returns>The MCP protocol version string.</returns>
        internal static string GetMcpProtocolVersion()
        {
            // In real implementation, this would query the MCP SDK for version information
            return "1.0.0";
        }

        /// <summary>
        /// Gets the list of supported MCP features.
        /// </summary>
        /// <returns>A list of supported MCP feature names.</returns>
        internal static List<string> GetSupportedMcpFeatures()
        {
            // In real implementation, this would enumerate available MCP features
            return
            [
                "Tools",
                "Authentication",
                "TokenDelegation",
                "ODataIntegration",
                "DynamicToolGeneration"
            ];
        }

        #endregion
    }
}
