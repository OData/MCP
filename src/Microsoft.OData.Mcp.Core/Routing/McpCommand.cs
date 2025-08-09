namespace Microsoft.OData.Mcp.Core.Routing
{
    /// <summary>
    /// Represents the type of MCP command.
    /// </summary>
    public enum McpCommand
    {
        /// <summary>
        /// Unknown or invalid command.
        /// </summary>
        Unknown,

        /// <summary>
        /// Server information request (/mcp/info or /mcp).
        /// </summary>
        Info,

        /// <summary>
        /// List all tools (/mcp/tools).
        /// </summary>
        Tools,

        /// <summary>
        /// Execute a tool (/mcp/tools/execute).
        /// </summary>
        ToolsExecute,

        /// <summary>
        /// Get information about a specific tool (/mcp/tools/{toolName}).
        /// </summary>
        ToolInfo
    }
}
