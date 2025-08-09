using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Routing
{

    /// <summary>
    /// Represents an MCP route entry with its associated OData information.
    /// </summary>
    public sealed class McpRouteEntry
    {

        /// <summary>
        /// Gets the name of the OData route.
        /// </summary>
        public required string RouteName { get; init; }

        /// <summary>
        /// Gets the OData route prefix (e.g., "api/v1", "odata", or empty for root).
        /// </summary>
        public required string ODataRoutePrefix { get; init; }

        /// <summary>
        /// Gets the base path for MCP endpoints (e.g., "/api/v1/mcp").
        /// </summary>
        public required string McpBasePath { get; init; }

        /// <summary>
        /// Gets a value indicating whether this route was explicitly registered.
        /// </summary>
        public bool IsExplicit { get; init; }

        /// <summary>
        /// Gets the custom MCP path if one was specified.
        /// </summary>
        public string? CustomMcpPath { get; init; }

        /// <summary>
        /// Gets additional metadata about this route.
        /// </summary>
        public Dictionary<string, object> Metadata { get; init; } = [];

    }

}
