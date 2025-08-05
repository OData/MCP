using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Mcp.Authentication.Models;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.OData.Mcp.Middleware.Configuration
{
    /// <summary>
    /// Configuration options for the MCP middleware integration.
    /// </summary>
    /// <remarks>
    /// These options control how the MCP server is integrated into an existing
    /// ASP.NET Core application, including routing, metadata discovery, and tool generation.
    /// </remarks>
    public sealed class McpMiddlewareOptions
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the MCP middleware is enabled.
        /// </summary>
        /// <value><c>true</c> to enable MCP middleware; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When disabled, the middleware will pass through requests without processing them.
        /// This allows for easy enable/disable without removing middleware registration.
        /// </remarks>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the base path for MCP endpoints.
        /// </summary>
        /// <value>The base path for all MCP-related endpoints.</value>
        /// <remarks>
        /// All MCP endpoints will be prefixed with this path. For example, if set to "/mcp",
        /// the tools endpoint will be available at "/mcp/tools".
        /// </remarks>
        public string BasePath { get; set; } = "/mcp";

        /// <summary>
        /// Gets or sets the OData metadata endpoint path.
        /// </summary>
        /// <value>The path where OData metadata can be retrieved.</value>
        /// <remarks>
        /// This is used to automatically discover the OData model structure.
        /// Typically set to "/$metadata" for standard OData services.
        /// </remarks>
        public string MetadataPath { get; set; } = "/$metadata";

        /// <summary>
        /// Gets or sets the OData service root URL.
        /// </summary>
        /// <value>The root URL of the OData service, or null to auto-detect.</value>
        /// <remarks>
        /// When null, the middleware will attempt to detect the service root from
        /// the current request. Set this explicitly if the service root differs
        /// from the middleware host.
        /// </remarks>
        public string? ServiceRootUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to auto-discover OData metadata.
        /// </summary>
        /// <value><c>true</c> to auto-discover metadata; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, the middleware will automatically fetch and parse OData
        /// metadata from the configured endpoint. When disabled, metadata must be
        /// provided manually through configuration.
        /// </remarks>
        public bool AutoDiscoverMetadata { get; set; } = true;

        /// <summary>
        /// Gets or sets the interval for refreshing OData metadata.
        /// </summary>
        /// <value>The interval between metadata refresh attempts.</value>
        /// <remarks>
        /// The middleware will periodically refresh the OData metadata to pick up
        /// changes in the service structure. Set to TimeSpan.Zero to disable automatic refresh.
        /// </remarks>
        public TimeSpan MetadataRefreshInterval { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets a value indicating whether authentication is enabled.
        /// </summary>
        /// <value><c>true</c> to enable authentication; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, all MCP requests will require valid authentication tokens.
        /// </remarks>
        public bool EnableAuthentication { get; set; } = false;

        /// <summary>
        /// Gets or sets the authentication options for the MCP server.
        /// </summary>
        /// <value>The authentication configuration.</value>
        /// <remarks>
        /// These options control how the MCP server authenticates users and
        /// forwards authentication to the underlying OData service.
        /// </remarks>
        public McpAuthenticationOptions Authentication { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether tool filtering is enabled.
        /// </summary>
        /// <value><c>true</c> to enable tool filtering; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, tools will be filtered based on user permissions and scopes.
        /// </remarks>
        public bool EnableToolFiltering { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether rate limiting is enabled.
        /// </summary>
        /// <value><c>true</c> to enable rate limiting; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, requests will be rate limited to prevent abuse.
        /// </remarks>
        public bool EnableRateLimiting { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether logging is enabled.
        /// </summary>
        /// <value><c>true</c> to enable logging; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, MCP operations will be logged for monitoring and debugging.
        /// </remarks>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the tool generation options.
        /// </summary>
        /// <value>The options for generating MCP tools from OData metadata.</value>
        /// <remarks>
        /// These options control which tools are generated, how they are named,
        /// and what authorization requirements they have.
        /// </remarks>
        public McpToolGenerationOptions ToolGeneration { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether to enable CORS for MCP endpoints.
        /// </summary>
        /// <value><c>true</c> to enable CORS; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, appropriate CORS headers will be added to MCP responses
        /// to allow cross-origin requests from web applications.
        /// </remarks>
        public bool EnableCors { get; set; } = true;

        /// <summary>
        /// Gets or sets the CORS policy name to use for MCP endpoints.
        /// </summary>
        /// <value>The name of the CORS policy, or null to use default settings.</value>
        /// <remarks>
        /// If specified, this must match a CORS policy configured in the application.
        /// If null, a permissive default policy will be used.
        /// </remarks>
        public string? CorsPolicyName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable caching for metadata and tools.
        /// </summary>
        /// <value><c>true</c> to enable caching; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Caching improves performance by avoiding repeated metadata parsing and
        /// tool generation, but may delay updates when the OData service changes.
        /// </remarks>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache duration for generated tools.
        /// </summary>
        /// <value>The duration to cache generated tools.</value>
        /// <remarks>
        /// Cached tools will be reused for this duration before being regenerated
        /// from the latest metadata.
        /// </remarks>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets a value indicating whether to include health check endpoints.
        /// </summary>
        /// <value><c>true</c> to include health check endpoints; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, health check endpoints will be exposed under the MCP base path
        /// to monitor the health of the MCP integration.
        /// </remarks>
        public bool IncludeHealthChecks { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include detailed error information in responses.
        /// </summary>
        /// <value><c>true</c> to include detailed errors; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// In development environments, detailed error information can be helpful for debugging.
        /// In production, this should typically be disabled to avoid information disclosure.
        /// </remarks>
        public bool IncludeDetailedErrors { get; set; } = false;

        /// <summary>
        /// Gets or sets the request timeout for tool execution.
        /// </summary>
        /// <value>The maximum time allowed for tool execution.</value>
        /// <remarks>
        /// Tools that exceed this timeout will be cancelled and return an error.
        /// This prevents long-running operations from blocking the middleware.
        /// </remarks>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the maximum request body size for tool parameters.
        /// </summary>
        /// <value>The maximum size in bytes for tool parameter JSON.</value>
        /// <remarks>
        /// This prevents excessively large requests from consuming too much memory
        /// or processing time.
        /// </remarks>
        public long MaxRequestBodySize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Gets or sets custom headers to include in all MCP responses.
        /// </summary>
        /// <value>A dictionary of custom header name-value pairs.</value>
        /// <remarks>
        /// These headers will be added to all MCP responses and can be used for
        /// custom branding, security headers, or operational metadata.
        /// </remarks>
        public Dictionary<string, string> CustomHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets the middleware integration mode.
        /// </summary>
        /// <value>The mode determining how the middleware integrates with the application.</value>
        /// <remarks>
        /// Different modes provide different levels of integration with the host application,
        /// from simple passthrough to full takeover of specific routes.
        /// </remarks>
        public McpIntegrationMode IntegrationMode { get; set; } = McpIntegrationMode.Embedded;

        /// <summary>
        /// Gets or sets paths to exclude from MCP processing.
        /// </summary>
        /// <value>A collection of path patterns to exclude from MCP middleware processing.</value>
        /// <remarks>
        /// Requests matching these patterns will be passed through without MCP processing.
        /// Supports simple wildcard patterns using * and ?.
        /// </remarks>
        public List<string> ExcludePaths { get; set; } = new();

        /// <summary>
        /// Gets or sets paths to include for MCP processing.
        /// </summary>
        /// <value>A collection of path patterns to process with MCP middleware.</value>
        /// <remarks>
        /// When specified, only requests matching these patterns will be processed by MCP.
        /// If empty, all requests under the base path will be processed.
        /// </remarks>
        public List<string> IncludePaths { get; set; } = new();

        #endregion

        #region Static Properties

        /// <summary>
        /// Gets the default configuration options for MCP middleware.
        /// </summary>
        /// <value>A preconfigured instance with sensible defaults for most scenarios.</value>
        /// <remarks>
        /// The default configuration provides a baseline setup suitable for most applications.
        /// You can modify the returned instance or use it as a starting point for custom configurations.
        /// </remarks>
        public static McpMiddlewareOptions Default => new()
        {
            Enabled = true,
            BasePath = "/mcp",
            MetadataPath = "/$metadata",
            EnableAuthentication = false,
            EnableToolFiltering = true,
            EnableCaching = true,
            EnableRateLimiting = false,
            EnableLogging = true,
            IncludeHealthChecks = true,
            IncludeDetailedErrors = false,
            RequestTimeout = TimeSpan.FromMinutes(2),
            MaxRequestBodySize = 1024 * 1024, // 1MB
            IntegrationMode = McpIntegrationMode.Embedded,
            ExcludePaths = new List<string>(),
            IncludePaths = new List<string>(),
            CustomHeaders = new Dictionary<string, string>()
        };

        /// <summary>
        /// Gets configuration options optimized for development environments.
        /// </summary>
        /// <value>A preconfigured instance suitable for development use.</value>
        /// <remarks>
        /// Development configuration enables detailed errors, disables caching and rate limiting
        /// for easier debugging, and allows more permissive settings.
        /// </remarks>
        public static McpMiddlewareOptions Development => new()
        {
            Enabled = true,
            BasePath = "/mcp",
            MetadataPath = "/$metadata",
            EnableAuthentication = false,
            EnableToolFiltering = false,
            EnableCaching = false,
            EnableRateLimiting = false,
            EnableLogging = true,
            IncludeHealthChecks = true,
            IncludeDetailedErrors = true,
            RequestTimeout = TimeSpan.FromMinutes(10),
            MaxRequestBodySize = 10 * 1024 * 1024, // 10MB
            IntegrationMode = McpIntegrationMode.Embedded,
            ExcludePaths = new List<string>(),
            IncludePaths = new List<string>(),
            CustomHeaders = new Dictionary<string, string>()
        };

        /// <summary>
        /// Gets configuration options optimized for production environments.
        /// </summary>
        /// <value>A preconfigured instance suitable for production use.</value>
        /// <remarks>
        /// Production configuration enables security features, caching, rate limiting,
        /// and other settings appropriate for high-availability production systems.
        /// </remarks>
        public static McpMiddlewareOptions Production => new()
        {
            Enabled = true,
            BasePath = "/mcp",
            MetadataPath = "/$metadata",
            EnableAuthentication = true,
            EnableToolFiltering = true,
            EnableCaching = true,
            EnableRateLimiting = true,
            EnableLogging = true,
            IncludeHealthChecks = true,
            IncludeDetailedErrors = false,
            RequestTimeout = TimeSpan.FromMinutes(1),
            MaxRequestBodySize = 512 * 1024, // 512KB
            IntegrationMode = McpIntegrationMode.Embedded,
            ExcludePaths = new List<string>(),
            IncludePaths = new List<string>(),
            CustomHeaders = new Dictionary<string, string>()
        };

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpMiddlewareOptions"/> class.
        /// </summary>
        public McpMiddlewareOptions()
        {
        }

        #endregion

        #region Public Methods


        /// <summary>
        /// Validates the configuration options.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if configuration is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(BasePath))
            {
                errors.Add("BasePath cannot be null or whitespace.");
            }
            else if (!BasePath.StartsWith('/'))
            {
                errors.Add("BasePath must start with a forward slash.");
            }

            if (string.IsNullOrWhiteSpace(MetadataPath))
            {
                errors.Add("MetadataPath cannot be null or whitespace.");
            }

            if (AutoDiscoverMetadata && string.IsNullOrWhiteSpace(MetadataPath))
            {
                errors.Add("MetadataPath is required when AutoDiscoverMetadata is enabled.");
            }

            if (MetadataRefreshInterval < TimeSpan.Zero)
            {
                errors.Add("MetadataRefreshInterval cannot be negative.");
            }

            if (CacheDuration < TimeSpan.Zero)
            {
                errors.Add("CacheDuration cannot be negative.");
            }

            if (RequestTimeout <= TimeSpan.Zero)
            {
                errors.Add("RequestTimeout must be greater than zero.");
            }

            if (MaxRequestBodySize <= 0)
            {
                errors.Add("MaxRequestBodySize must be greater than zero.");
            }

            // Validate authentication options
            var authErrors = Authentication.Validate();
            errors.AddRange(authErrors.Select(e => $"Authentication: {e}"));

            // Validate tool generation options
            var toolErrors = ToolGeneration.Validate();
            errors.AddRange(toolErrors.Select(e => $"ToolGeneration: {e}"));

            return errors;
        }

        /// <summary>
        /// Determines whether the specified path should be processed by MCP middleware.
        /// </summary>
        /// <param name="path">The request path to check.</param>
        /// <returns><c>true</c> if the path should be processed; otherwise, <c>false</c>.</returns>
        public bool ShouldProcessPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // Check if path is under the base path
            if (!path.StartsWith(BasePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check exclusions first
            if (ExcludePaths.Any(pattern => MatchesPattern(path, pattern)))
            {
                return false;
            }

            // If inclusions are specified, path must match one of them
            if (IncludePaths.Count > 0)
            {
                return IncludePaths.Any(pattern => MatchesPattern(path, pattern));
            }

            return true;
        }

        /// <summary>
        /// Gets the full metadata URL based on the configuration.
        /// </summary>
        /// <param name="baseUrl">The base URL of the service.</param>
        /// <returns>The complete metadata URL.</returns>
        public string GetMetadataUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL cannot be null or whitespace.", nameof(baseUrl));
            }

            var serviceRoot = ServiceRootUrl ?? baseUrl.TrimEnd('/');
            return $"{serviceRoot}{MetadataPath}";
        }

        /// <summary>
        /// Creates a copy of these options.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public McpMiddlewareOptions Clone()
        {
            return new McpMiddlewareOptions
            {
                Enabled = Enabled,
                BasePath = BasePath,
                MetadataPath = MetadataPath,
                ServiceRootUrl = ServiceRootUrl,
                AutoDiscoverMetadata = AutoDiscoverMetadata,
                MetadataRefreshInterval = MetadataRefreshInterval,
                Authentication = Authentication, // Note: This creates a reference, not a deep copy
                ToolGeneration = ToolGeneration.Clone(),
                EnableCors = EnableCors,
                CorsPolicyName = CorsPolicyName,
                EnableCaching = EnableCaching,
                CacheDuration = CacheDuration,
                IncludeHealthChecks = IncludeHealthChecks,
                IncludeDetailedErrors = IncludeDetailedErrors,
                RequestTimeout = RequestTimeout,
                MaxRequestBodySize = MaxRequestBodySize,
                CustomHeaders = new Dictionary<string, string>(CustomHeaders),
                IntegrationMode = IntegrationMode,
                ExcludePaths = new List<string>(ExcludePaths),
                IncludePaths = new List<string>(IncludePaths)
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Checks if a path matches a pattern with simple wildcards.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <param name="pattern">The pattern to match against.</param>
        /// <returns><c>true</c> if the path matches the pattern; otherwise, <c>false</c>.</returns>
        private static bool MatchesPattern(string path, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            // Simple wildcard matching - convert to regex
            var regexPattern = pattern
                .Replace("*", ".*")
                .Replace("?", ".");

            return System.Text.RegularExpressions.Regex.IsMatch(
                path, 
                $"^{regexPattern}$", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        #endregion
    }

    /// <summary>
    /// Defines the integration modes for MCP middleware.
    /// </summary>
    public enum McpIntegrationMode
    {
        /// <summary>
        /// Embedded mode integrates MCP endpoints alongside existing application routes.
        /// </summary>
        Embedded,

        /// <summary>
        /// Proxy mode forwards MCP requests to an external MCP server.
        /// </summary>
        Proxy,

        /// <summary>
        /// Hybrid mode combines embedded and proxy functionality.
        /// </summary>
        Hybrid
    }
}