using System;

namespace Microsoft.OData.Mcp.AspNetCore
{
    /// <summary>
    /// Configuration options for OData MCP integration.
    /// </summary>
    public class ODataMcpOptions
    {
        /// <summary>
        /// Gets or sets whether to enable MCP endpoints.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to automatically register MCP routes.
        /// </summary>
        public bool AutoRegisterRoutes { get; set; } = true;

        /// <summary>
        /// Gets or sets the base path for MCP endpoints.
        /// </summary>
        public string BasePath { get; set; } = "/mcp";

        /// <summary>
        /// Gets or sets routes to exclude from MCP integration.
        /// </summary>
        public string[] ExcludeRoutes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets whether to enable dynamic model refresh.
        /// </summary>
        public bool EnableDynamicModels { get; set; } = false;

        /// <summary>
        /// Gets or sets the interval for refreshing dynamic models.
        /// </summary>
        public TimeSpan ModelRefreshInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets whether to enable detailed error messages.
        /// </summary>
        public bool EnableDetailedErrors { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable caching.
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache duration.
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets whether to auto-discover metadata.
        /// </summary>
        public bool AutoDiscoverMetadata { get; set; } = true;

        /// <summary>
        /// Gets or sets the request timeout.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets whether to include metadata in responses.
        /// </summary>
        public bool IncludeMetadata { get; set; } = true;
    }
}