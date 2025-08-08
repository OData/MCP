using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Mcp.Authentication.Models;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Unified configuration for MCP servers supporting both sidecar and middleware deployment modes.
    /// </summary>
    /// <remarks>
    /// This configuration provides a single, unified interface for configuring MCP servers
    /// regardless of deployment mode. It supports both standalone (sidecar) and embedded
    /// (middleware) deployment patterns with appropriate defaults for each scenario.
    /// </remarks>
    public sealed class McpServerConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets or sets the deployment mode for the MCP server.
        /// </summary>
        /// <value>The deployment mode determining how the server operates.</value>
        /// <remarks>
        /// The deployment mode affects how the server discovers metadata, handles authentication,
        /// and exposes endpoints. Each mode has different configuration requirements and behaviors.
        /// </remarks>
        public McpDeploymentMode DeploymentMode { get; set; } = McpDeploymentMode.Sidecar;

        /// <summary>
        /// Gets or sets the server information.
        /// </summary>
        /// <value>Basic information about the MCP server instance.</value>
        /// <remarks>
        /// This information is exposed through the server info endpoint and helps
        /// clients understand the capabilities and version of the server.
        /// </remarks>
        public McpServerInfo ServerInfo { get; set; } = new();

        /// <summary>
        /// Gets or sets the OData service configuration.
        /// </summary>
        /// <value>Configuration for connecting to and interacting with OData services.</value>
        /// <remarks>
        /// This configuration specifies how the MCP server discovers and communicates
        /// with the underlying OData service, including metadata endpoints and authentication.
        /// </remarks>
        public ODataServiceConfiguration ODataService { get; set; } = new();

        /// <summary>
        /// Gets or sets the authentication configuration.
        /// </summary>
        /// <value>Configuration for user authentication and token validation.</value>
        /// <remarks>
        /// Authentication configuration controls how users are authenticated and
        /// how tokens are validated and forwarded to the underlying OData service.
        /// </remarks>
        public McpAuthenticationOptions Authentication { get; set; } = new();

        /// <summary>
        /// Gets or sets the tool generation configuration.
        /// </summary>
        /// <value>Options controlling how MCP tools are generated from OData metadata.</value>
        /// <remarks>
        /// Tool generation options determine which operations are exposed as MCP tools,
        /// how they are named, and what authorization requirements they have.
        /// </remarks>
        public McpToolGenerationOptions ToolGeneration { get; set; } = new();

        /// <summary>
        /// Gets or sets the network and transport configuration.
        /// </summary>
        /// <value>Configuration for network endpoints, ports, and transport protocols.</value>
        /// <remarks>
        /// Network configuration specifies how the MCP server exposes its endpoints
        /// and communicates with clients and the underlying OData service.
        /// </remarks>
        public NetworkConfiguration Network { get; set; } = new();

        /// <summary>
        /// Gets or sets the caching configuration.
        /// </summary>
        /// <value>Configuration for metadata and tool caching behavior.</value>
        /// <remarks>
        /// Caching configuration controls how long metadata and generated tools are cached
        /// to improve performance and reduce load on the underlying OData service.
        /// </remarks>
        public CachingConfiguration Caching { get; set; } = new();

        /// <summary>
        /// Gets or sets the logging and monitoring configuration.
        /// </summary>
        /// <value>Configuration for logging, metrics, and health monitoring.</value>
        /// <remarks>
        /// Monitoring configuration controls what information is logged, how metrics
        /// are collected, and what health checks are performed.
        /// </remarks>
        public MonitoringConfiguration Monitoring { get; set; } = new();

        /// <summary>
        /// Gets or sets the security configuration.
        /// </summary>
        /// <value>Configuration for security policies and restrictions.</value>
        /// <remarks>
        /// Security configuration includes CORS policies, rate limiting, request size limits,
        /// and other security-related settings.
        /// </remarks>
        public SecurityConfiguration Security { get; set; } = new();

        /// <summary>
        /// Gets or sets the feature flags configuration.
        /// </summary>
        /// <value>Configuration for enabling/disabling specific features.</value>
        /// <remarks>
        /// Feature flags allow selective enabling of functionality for gradual rollouts,
        /// A/B testing, or environment-specific configurations.
        /// </remarks>
        public FeatureFlagsConfiguration FeatureFlags { get; set; } = new();

        /// <summary>
        /// Gets or sets custom configuration properties.
        /// </summary>
        /// <value>A dictionary of custom configuration values.</value>
        /// <remarks>
        /// Custom properties allow extending the configuration with application-specific
        /// settings that don't fit into the standard configuration categories.
        /// </remarks>
        public Dictionary<string, object> CustomProperties { get; set; } = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServerConfiguration"/> class.
        /// </summary>
        public McpServerConfiguration()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a configuration optimized for sidecar deployment.
        /// </summary>
        /// <param name="odataServiceUrl">The URL of the OData service to integrate with.</param>
        /// <returns>A configuration instance optimized for sidecar deployment.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="odataServiceUrl"/> is null or whitespace.</exception>
        public static McpServerConfiguration ForSidecar(string odataServiceUrl)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(odataServiceUrl);
#else
            if (string.IsNullOrWhiteSpace(odataServiceUrl))
            {
                throw new ArgumentException("OData service URL cannot be null or whitespace.", nameof(odataServiceUrl));
            }
#endif

            return new McpServerConfiguration
            {
                DeploymentMode = McpDeploymentMode.Sidecar,
                ServerInfo = new McpServerInfo
                {
                    Name = "OData MCP Sidecar",
                    Description = "Sidecar MCP server for OData service",
                    Version = "1.0.0"
                },
                ODataService = new ODataServiceConfiguration
                {
                    BaseUrl = odataServiceUrl,
                    MetadataPath = "/$metadata",
                    AutoDiscoverMetadata = true,
                    RefreshInterval = TimeSpan.FromMinutes(30)
                },
                Network = new NetworkConfiguration
                {
                    Port = 8080,
                    Host = "localhost",
                    BasePath = "/mcp",
                    EnableHttps = false
                },
                Authentication = new McpAuthenticationOptions
                {
                    Enabled = false // Authentication disabled by default for simplicity
                },
                ToolGeneration = McpToolGenerationOptions.Default(),
                Caching = new CachingConfiguration
                {
                    Enabled = true,
                    MetadataTtl = TimeSpan.FromHours(1),
                    ToolsTtl = TimeSpan.FromHours(2)
                }
            };
        }

        /// <summary>
        /// Creates a configuration optimized for middleware deployment.
        /// </summary>
        /// <param name="basePath">The base path for MCP endpoints within the host application.</param>
        /// <returns>A configuration instance optimized for middleware deployment.</returns>
        public static McpServerConfiguration ForMiddleware(string basePath = "/mcp")
        {
            return new McpServerConfiguration
            {
                DeploymentMode = McpDeploymentMode.Middleware,
                ServerInfo = new McpServerInfo
                {
                    Name = "OData MCP Middleware",
                    Description = "Embedded MCP server middleware",
                    Version = "1.0.0"
                },
                ODataService = new ODataServiceConfiguration
                {
                    MetadataPath = "/$metadata",
                    AutoDiscoverMetadata = true,
                    RefreshInterval = TimeSpan.FromMinutes(15),
                    UseHostContext = true
                },
                Network = new NetworkConfiguration
                {
                    BasePath = basePath,
                    UseHostConfiguration = true
                },
                Authentication = new McpAuthenticationOptions
                {
                    Enabled = false // Authentication disabled by default for simplicity
                },
                ToolGeneration = McpToolGenerationOptions.Default(),
                Caching = new CachingConfiguration
                {
                    Enabled = true,
                    MetadataTtl = TimeSpan.FromMinutes(30),
                    ToolsTtl = TimeSpan.FromHours(1)
                }
            };
        }

        /// <summary>
        /// Creates a configuration optimized for development environments.
        /// </summary>
        /// <param name="deploymentMode">The deployment mode for development.</param>
        /// <returns>A configuration instance with development-friendly settings.</returns>
        public static McpServerConfiguration ForDevelopment(McpDeploymentMode deploymentMode = McpDeploymentMode.Middleware)
        {
            var config = deploymentMode == McpDeploymentMode.Sidecar
                ? ForSidecar("http://localhost:5000")
                : ForMiddleware();

            config.Authentication.Enabled = false;
            config.Security.RequireHttps = false;
            config.Security.EnableDetailedErrors = true;
            config.Monitoring.LogLevel = "Debug";
            config.Caching.MetadataTtl = TimeSpan.FromMinutes(5);
            config.ODataService.RefreshInterval = TimeSpan.FromMinutes(2);
            config.FeatureFlags.EnableDevelopmentEndpoints = true;

            return config;
        }

        /// <summary>
        /// Creates a configuration optimized for production environments.
        /// </summary>
        /// <param name="deploymentMode">The deployment mode for production.</param>
        /// <returns>A configuration instance with production-optimized settings.</returns>
        public static McpServerConfiguration ForProduction(McpDeploymentMode deploymentMode = McpDeploymentMode.Sidecar)
        {
            var config = deploymentMode == McpDeploymentMode.Sidecar
                ? ForSidecar("https://api.example.com")
                : ForMiddleware();

            // Keep authentication disabled by default for simplicity (Easy As Fuck™ philosophy)
            // Users can enable it explicitly if needed
            config.Authentication.Enabled = false;
            config.Security.RequireHttps = true;
            config.Security.EnableDetailedErrors = false;
            config.Security.EnableRateLimiting = true;
            config.Monitoring.LogLevel = "Warning";
            config.Monitoring.EnableMetrics = true;
            config.Monitoring.EnableHealthChecks = true;
            config.Caching.MetadataTtl = TimeSpan.FromHours(4);
            config.Caching.ToolsTtl = TimeSpan.FromHours(8);
            config.ODataService.RefreshInterval = TimeSpan.FromHours(2);
            config.ToolGeneration = McpToolGenerationOptions.Performance();

            return config;
        }

        /// <summary>
        /// Validates the configuration for completeness and consistency.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            // Validate server info
            var serverInfoErrors = ServerInfo.Validate();
            errors.AddRange(serverInfoErrors.Select(e => $"ServerInfo: {e}"));

            // Validate OData service configuration
            var odataErrors = ODataService.Validate(DeploymentMode);
            errors.AddRange(odataErrors.Select(e => $"ODataService: {e}"));

            // Validate authentication configuration
            var authErrors = Authentication.Validate();
            errors.AddRange(authErrors.Select(e => $"Authentication: {e}"));

            // Validate tool generation configuration
            var toolErrors = ToolGeneration.Validate();
            errors.AddRange(toolErrors.Select(e => $"ToolGeneration: {e}"));

            // Validate network configuration
            var networkErrors = Network.Validate(DeploymentMode);
            errors.AddRange(networkErrors.Select(e => $"Network: {e}"));

            // Validate caching configuration
            var cachingErrors = Caching.Validate();
            errors.AddRange(cachingErrors.Select(e => $"Caching: {e}"));

            // Validate security configuration
            var securityErrors = Security.Validate();
            errors.AddRange(securityErrors.Select(e => $"Security: {e}"));

            // Cross-configuration validation
            if (DeploymentMode == McpDeploymentMode.Sidecar)
            {
                if (string.IsNullOrWhiteSpace(ODataService.BaseUrl))
                {
                    errors.Add("BaseUrl is required for sidecar deployment mode");
                }

                if (Network.UseHostConfiguration)
                {
                    errors.Add("UseHostConfiguration should be false for sidecar deployment mode");
                }
            }
            else if (DeploymentMode == McpDeploymentMode.Middleware)
            {
                if (!Network.UseHostConfiguration && Network.Port.HasValue)
                {
                    errors.Add("Port should not be specified for middleware deployment mode when UseHostConfiguration is false");
                }
            }

            return errors;
        }

        /// <summary>
        /// Applies environment-specific overrides to the configuration.
        /// </summary>
        /// <param name="environment">The environment name (e.g., "Development", "Production").</param>
        public void ApplyEnvironmentOverrides(string environment)
        {
            if (string.IsNullOrWhiteSpace(environment))
            {
                return;
            }

            switch (environment.ToLowerInvariant())
            {
                case "development":
                case "dev":
                    ApplyDevelopmentOverrides();
                    break;

                case "staging":
                case "stage":
                    ApplyStagingOverrides();
                    break;

                case "production":
                case "prod":
                    ApplyProductionOverrides();
                    break;

                case "testing":
                case "test":
                    ApplyTestingOverrides();
                    break;
            }
        }

        /// <summary>
        /// Gets configuration statistics for monitoring and diagnostics.
        /// </summary>
        /// <returns>A dictionary containing configuration statistics.</returns>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["DeploymentMode"] = DeploymentMode.ToString(),
                ["ServerName"] = ServerInfo.Name,
                ["ServerVersion"] = ServerInfo.Version,
                ["AuthenticationEnabled"] = Authentication.Enabled,
                ["CachingEnabled"] = Caching.Enabled,
                ["MetadataAutoDiscovery"] = ODataService.AutoDiscoverMetadata,
                ["ToolGenerationMode"] = ToolGeneration.OptimizeForPerformance ? "Performance" : "Standard",
                ["NetworkPort"] = Network.Port ?? 0,
                ["NetworkBasePath"] = Network.BasePath ?? string.Empty,
                ["SecurityHttpsRequired"] = Security.RequireHttps,
                ["MonitoringEnabled"] = Monitoring.EnableMetrics,
                ["CustomPropertiesCount"] = CustomProperties.Count
            };
        }

        /// <summary>
        /// Creates a deep copy of this configuration.
        /// </summary>
        /// <returns>A new configuration instance with the same settings.</returns>
        public McpServerConfiguration Clone()
        {
            return new McpServerConfiguration
            {
                DeploymentMode = DeploymentMode,
                ServerInfo = ServerInfo.Clone(),
                ODataService = ODataService.Clone(),
                Authentication = Authentication, // Note: This creates a reference, not a deep copy
                ToolGeneration = ToolGeneration.Clone(),
                Network = Network.Clone(),
                Caching = Caching.Clone(),
                Monitoring = Monitoring.Clone(),
                Security = Security.Clone(),
                FeatureFlags = FeatureFlags.Clone(),
                CustomProperties = new Dictionary<string, object>(CustomProperties)
            };
        }

        /// <summary>
        /// Merges another configuration into this one, with the other configuration taking precedence.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        public void MergeWith(McpServerConfiguration other)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(other);
#else
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }
#endif

            // Merge all sections
            ServerInfo.MergeWith(other.ServerInfo);
            ODataService.MergeWith(other.ODataService);
            ToolGeneration = other.ToolGeneration.Clone(); // Replace completely
            Network.MergeWith(other.Network);
            Caching.MergeWith(other.Caching);
            Monitoring.MergeWith(other.Monitoring);
            Security.MergeWith(other.Security);
            FeatureFlags.MergeWith(other.FeatureFlags);

            // Merge custom properties
            foreach (var kvp in other.CustomProperties)
            {
                CustomProperties[kvp.Key] = kvp.Value;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Applies development environment overrides.
        /// </summary>
        private void ApplyDevelopmentOverrides()
        {
            Authentication.Enabled = false;
            Security.RequireHttps = false;
            Security.EnableDetailedErrors = true;
            Monitoring.LogLevel = "Debug";
            Caching.MetadataTtl = TimeSpan.FromMinutes(5);
            FeatureFlags.EnableDevelopmentEndpoints = true;
        }

        /// <summary>
        /// Applies staging environment overrides.
        /// </summary>
        private void ApplyStagingOverrides()
        {
            // Keep authentication disabled by default for simplicity
            Authentication.Enabled = false;
            Security.RequireHttps = true;
            Security.EnableDetailedErrors = true;
            Monitoring.LogLevel = "Information";
            Monitoring.EnableMetrics = true;
            Caching.MetadataTtl = TimeSpan.FromMinutes(30);
        }

        /// <summary>
        /// Applies production environment overrides.
        /// </summary>
        private void ApplyProductionOverrides()
        {
            // Keep authentication disabled by default for simplicity (Easy As Fuck™ philosophy)
            // Users can enable it explicitly if needed
            Authentication.Enabled = false;
            Security.RequireHttps = true;
            Security.EnableDetailedErrors = false;
            Security.EnableRateLimiting = true;
            Monitoring.LogLevel = "Warning";
            Monitoring.EnableMetrics = true;
            Monitoring.EnableHealthChecks = true;
            Caching.MetadataTtl = TimeSpan.FromHours(4);
            ToolGeneration = McpToolGenerationOptions.Performance();
        }

        /// <summary>
        /// Applies testing environment overrides.
        /// </summary>
        private void ApplyTestingOverrides()
        {
            Authentication.Enabled = false;
            Security.RequireHttps = false;
            Security.EnableDetailedErrors = true;
            Monitoring.LogLevel = "Information";
            Caching.Enabled = false;
            ODataService.AutoDiscoverMetadata = false;
        }

        #endregion
    }

    /// <summary>
    /// Defines the deployment modes for MCP servers.
    /// </summary>
    public enum McpDeploymentMode
    {
        /// <summary>
        /// Sidecar deployment runs as a separate service alongside the OData service.
        /// </summary>
        Sidecar,

        /// <summary>
        /// Middleware deployment integrates directly into the host ASP.NET Core application.
        /// </summary>
        Middleware,

        /// <summary>
        /// Hybrid deployment combines aspects of both sidecar and middleware modes.
        /// </summary>
        Hybrid
    }
}
