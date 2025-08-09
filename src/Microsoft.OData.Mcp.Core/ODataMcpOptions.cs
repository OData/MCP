using System;

namespace Microsoft.OData.Mcp.Core
{

    /// <summary>
    /// Configuration options for OData MCP integration.
    /// </summary>
    public class ODataMcpOptions
    {

        /// <summary>
        /// Gets or sets a value indicating whether to automatically register MCP endpoints 
        /// for all OData routes.
        /// </summary>
        /// <value>
        /// <c>true</c> to automatically register MCP endpoints; otherwise, <c>false</c>.
        /// Default is <c>true</c>.
        /// </value>
        public bool AutoRegisterRoutes { get; set; } = true;

        /// <summary>
        /// Gets or sets the routes to exclude from automatic MCP registration.
        /// </summary>
        /// <value>
        /// An array of route names to exclude. Default is an empty array.
        /// </value>
        public string[] ExcludeRoutes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets a value indicating whether to enable dynamic model updates.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable dynamic models; otherwise, <c>false</c>.
        /// Default is <c>false</c> for performance.
        /// </value>
        public bool EnableDynamicModels { get; set; } = false;

        /// <summary>
        /// Gets or sets the tool naming pattern.
        /// </summary>
        /// <value>
        /// The pattern for generating tool names. Default is "{route}.{entity}.{operation}".
        /// Available placeholders: {route}, {entity}, {operation}.
        /// </value>
        public string ToolNamingPattern { get; set; } = "{route}.{entity}.{operation}";

        /// <summary>
        /// Gets or sets the maximum number of tools to generate per entity.
        /// </summary>
        /// <value>
        /// The maximum number of tools per entity, or <c>null</c> for unlimited.
        /// Default is <c>null</c>.
        /// </value>
        public int? MaxToolsPerEntity { get; set; }

        /// <summary>
        /// Gets or sets the cache duration for dynamic content.
        /// </summary>
        /// <value>
        /// The duration to cache dynamic content. Default is 5 minutes.
        /// </value>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets a value indicating whether to use aggressive caching.
        /// </summary>
        /// <value>
        /// <c>true</c> to use aggressive caching with ETags and long expiration;
        /// otherwise, <c>false</c>. Default is <c>true</c>.
        /// </value>
        public bool UseAggressiveCaching { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable request logging.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable detailed request logging; otherwise, <c>false</c>.
        /// Default is <c>false</c>.
        /// </value>
        public bool EnableRequestLogging { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum request size for tool execution.
        /// </summary>
        /// <value>
        /// The maximum request size in bytes. Default is 1MB (1,048,576 bytes).
        /// </value>
        public long MaxRequestSize { get; set; } = 1048576; // 1MB

        /// <summary>
        /// Gets or sets a value indicating whether to include metadata in tool descriptions.
        /// </summary>
        /// <value>
        /// <c>true</c> to include detailed metadata; otherwise, <c>false</c>.
        /// Default is <c>true</c>.
        /// </value>
        public bool IncludeMetadata { get; set; } = true;

        /// <summary>
        /// Gets or sets the default page size for query operations.
        /// </summary>
        /// <value>
        /// The default page size. Default is 100.
        /// </value>
        public int DefaultPageSize { get; set; } = 100;

        /// <summary>
        /// Gets or sets the maximum page size for query operations.
        /// </summary>
        /// <value>
        /// The maximum page size. Default is 1000.
        /// </value>
        public int MaxPageSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to enable CORS for MCP endpoints.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable CORS; otherwise, <c>false</c>.
        /// Default is <c>true</c>.
        /// </value>
        public bool EnableCors { get; set; } = true;

        /// <summary>
        /// Gets or sets the allowed CORS origins.
        /// </summary>
        /// <value>
        /// An array of allowed origins. Default allows all origins ("*").
        /// </value>
        public string[] AllowedOrigins { get; set; } = new[] { "*" };

    }

}
