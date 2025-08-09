using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Tools;
// using ModelContextProtocol; // Will be added when SDK integration is complete

namespace Microsoft.OData.Mcp.Tools.Server
{
    /// <summary>
    /// Integration layer between OData MCP tools and the ModelContextProtocol SDK.
    /// </summary>
    public static class McpSdkIntegration
    {
        /// <summary>
        /// Configures MCP server with OData tools.
        /// </summary>
        public static IServiceCollection AddODataMcpServer(
            this IServiceCollection services,
            string odataUrl,
            string? authToken = null)
        {
            // Add the MCP server using the SDK
            // Note: AddMcpServer extension would be provided by the SDK
            // For now, we'll register the server manually

            // Add our OData MCP server as a hosted service
            services.AddSingleton(provider => new ODataMcpServer(
                provider.GetRequiredService<ILogger<ODataMcpServer>>(),
                provider,
                provider.GetRequiredService<IMcpToolFactory>(),
                provider.GetRequiredService<Core.Parsing.ICsdlMetadataParser>(),
                odataUrl,
                authToken
            ));

            services.AddHostedService(provider => 
                provider.GetRequiredService<ODataMcpServer>());

            return services;
        }

        /// <summary>
        /// Creates MCP SDK compatible tool definitions from our tool definitions.
        /// </summary>
        public static IEnumerable<dynamic> ConvertToSdkTools(
            IEnumerable<McpToolDefinition> tools,
            ODataMcpServer server)
        {
            var sdkTools = new List<dynamic>();

            foreach (var tool in tools)
            {
                // Create a dynamic wrapper that the SDK can use
                var sdkTool = new DynamicMcpTool(tool, server);
                sdkTools.Add(sdkTool);
            }

            return sdkTools;
        }
    }

    /// <summary>
    /// Dynamic wrapper for MCP tools that integrates with the SDK.
    /// </summary>
    internal class DynamicMcpTool
    {
        internal readonly McpToolDefinition _tool;
        internal readonly ODataMcpServer _server;

        public DynamicMcpTool(McpToolDefinition tool, ODataMcpServer server)
        {
            _tool = tool;
            _server = server;
        }

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        [Description("Tool name")]
        public string Name => _tool.Name;

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        [Description("Tool description")]
        public string Description => _tool.Description;

        /// <summary>
        /// Gets the input schema.
        /// </summary>
        public JsonDocument? InputSchema => _tool.InputSchema;

        /// <summary>
        /// Executes the tool.
        /// </summary>
        public async Task<string> ExecuteAsync(string parametersJson)
        {
            JsonDocument? parameters = null;
            try
            {
                parameters = string.IsNullOrWhiteSpace(parametersJson) 
                    ? JsonDocument.Parse("{}") 
                    : JsonDocument.Parse(parametersJson);

                var result = await _server.ExecuteToolAsync(_tool.Name, parameters);

                if (result.IsSuccess && result.Data != null)
                {
                    return result.Data.RootElement.GetRawText();
                }
                else
                {
                    var errorObj = new
                    {
                        error = result.ErrorMessage ?? "Unknown error",
                        errorCode = result.ErrorCode ?? "UNKNOWN_ERROR"
                    };
                    return JsonSerializer.Serialize(errorObj);
                }
            }
            finally
            {
                parameters?.Dispose();
            }
        }
    }

    /// <summary>
    /// Extension methods for configuring the MCP SDK with OData.
    /// </summary>
    public static class McpServerBuilderExtensions
    {
        /// <summary>
        /// Uses STDIO transport for the MCP server.
        /// </summary>
        public static IServiceCollection WithStdioTransport(this IServiceCollection services)
        {
            // The SDK should handle this through its own configuration
            // For now, this is a placeholder for when the SDK transport is available
            return services;
        }

        /// <summary>
        /// Registers OData tools with the MCP server.
        /// </summary>
        public static IServiceCollection WithODataTools(
            this IServiceCollection services,
            string odataUrl,
            string? authToken = null)
        {
            // This will be called to register our dynamic tools
            services.AddSingleton(provider =>
            {
                var server = provider.GetRequiredService<ODataMcpServer>();
                var tools = server.GetTools();
                return McpSdkIntegration.ConvertToSdkTools(tools, server);
            });

            return services;
        }
    }
}
