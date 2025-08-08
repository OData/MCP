using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core;
using Microsoft.OData.Mcp.Core.Routing;
using Microsoft.OData.Mcp.Core.Tools;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.AspNetCore.Middleware
{
    /// <summary>
    /// Middleware that handles MCP requests for OData routes.
    /// </summary>
    public class ODataMcpMiddleware
    {
        #region Fields

        private readonly RequestDelegate _next;
        private readonly ILogger<ODataMcpMiddleware> _logger;
        private readonly IMcpEndpointRegistry _endpointRegistry;
        private readonly IMcpToolFactory _toolFactory;
        private readonly AspNetCore.ODataMcpOptions _options;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="endpointRegistry">The MCP endpoint registry.</param>
        /// <param name="toolFactory">The tool factory.</param>
        /// <param name="options">The MCP options.</param>
        public ODataMcpMiddleware(
            RequestDelegate next,
            ILogger<ODataMcpMiddleware> logger,
            IMcpEndpointRegistry endpointRegistry,
            IMcpToolFactory toolFactory,
            AspNetCore.ODataMcpOptions options)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(next);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(endpointRegistry);
            ArgumentNullException.ThrowIfNull(toolFactory);
            ArgumentNullException.ThrowIfNull(options);
#else
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _endpointRegistry = endpointRegistry ?? throw new ArgumentNullException(nameof(endpointRegistry));
            _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            return;
#endif

            _next = next;
            _logger = logger;
            _endpointRegistry = endpointRegistry;
            _toolFactory = toolFactory;
            _options = options;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // Check if this is an MCP request
            if (!_endpointRegistry.TryGetEndpoint(context.Request.Path, out var route, out var command))
            {
                // Not an MCP request, pass through
                await _next(context);
                return;
            }

            _logger.LogDebug("Processing MCP request for route '{RouteName}' with command '{Command}'", 
                route.RouteName, command);

            try
            {
                // Handle based on command type
                switch (command)
                {
                    case McpCommand.Info:
                        await HandleInfoRequest(context, route);
                        break;
                    
                    case McpCommand.Tools:
                        await HandleToolsRequest(context, route);
                        break;
                    
                    case McpCommand.ToolInfo:
                        await HandleToolInfoRequest(context, route);
                        break;
                    
                    case McpCommand.ToolsExecute:
                        await HandleToolsExecuteRequest(context, route);
                        break;
                    
                    default:
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MCP request");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Internal server error");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles MCP info requests.
        /// </summary>
        private async Task HandleInfoRequest(HttpContext context, McpRouteEntry route)
        {
            var response = new
            {
                name = $"OData MCP Server - {route.RouteName}",
                version = "1.0.0",
                description = $"Model Context Protocol server for OData route '{route.ODataRoutePrefix}'",
                capabilities = new
                {
                    tools = true,
                    resources = false,
                    prompts = false
                }
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
        }

        /// <summary>
        /// Handles MCP tools list requests.
        /// </summary>
        private async Task HandleToolsRequest(HttpContext context, McpRouteEntry route)
        {
            // Generate tools dynamically using the factory
            // Note: In a real implementation, you might want to cache these
            var generationOptions = new McpToolGenerationOptions
            {
                GenerateCrudTools = true,
                GenerateQueryTools = true,
                GenerateNavigationTools = true,
                IncludeExamples = _options.IncludeMetadata
            };
            
            // For now, return empty tools list since we need the EDM model
            // TODO: Get the EDM model for this route and generate tools
            var tools = new List<McpToolDefinition>();
            
            var response = new
            {
                tools = tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    inputSchema = t.InputSchema
                })
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
        }

        /// <summary>
        /// Handles MCP tool info requests.
        /// </summary>
        private async Task HandleToolInfoRequest(HttpContext context, McpRouteEntry route)
        {
            var toolName = context.Request.RouteValues["toolName"]?.ToString();
            if (string.IsNullOrEmpty(toolName))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            // For now, return not found since we need to implement tool lookup
            // TODO: Implement tool lookup using the factory
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"Tool '{toolName}' not found");
            return;

            // Original code to be reimplemented:
            // var fullToolName = string.IsNullOrWhiteSpace(route.RouteName) || route.RouteName.Equals("default", StringComparison.OrdinalIgnoreCase)
            //     ? toolName
            //     : $"{route.RouteName}.{toolName}";
            // Get tool from factory or cache

            // This code is unreachable due to the return above
            // Keeping it commented for future implementation
            // var response = new
            // {
            //     name = tool.Name,
            //     description = tool.Description,
            //     inputSchema = tool.InputSchema,
            //     examples = tool.Examples
            // };
            // 
            // context.Response.ContentType = "application/json";
            // await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions 
            // { 
            //     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //     WriteIndented = true
            // }));
        }

        /// <summary>
        /// Handles MCP tool execution requests.
        /// </summary>
        private async Task HandleToolsExecuteRequest(HttpContext context, McpRouteEntry route)
        {
            // TODO: Implement tool execution
            // This would involve:
            // 1. Reading the request body to get tool name and parameters
            // 2. Looking up the tool from the cache
            // 3. Validating parameters against the tool's input schema
            // 4. Executing the tool handler
            // 5. Returning the result

            context.Response.StatusCode = StatusCodes.Status501NotImplemented;
            await context.Response.WriteAsync("Tool execution not yet implemented");
        }

        #endregion
    }
}