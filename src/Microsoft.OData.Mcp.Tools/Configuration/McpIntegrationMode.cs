namespace Microsoft.OData.Mcp.Tools.Configuration
{
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
