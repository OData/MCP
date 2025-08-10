using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Routing;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.OData.Mcp.AspNetCore.Middleware
{
    /// <summary>
    /// Middleware that handles MCP requests for OData routes.
    /// </summary>
    public class ODataMcpMiddleware
    {
        #region Fields

        internal readonly RequestDelegate _next;
        internal readonly ILogger<ODataMcpMiddleware> _logger;
        internal readonly IMcpEndpointRegistry _endpointRegistry;
        internal readonly IMcpToolFactory _toolFactory;
        internal readonly ODataMcpOptions _options;
        internal readonly ConcurrentDictionary<string, IEnumerable<McpToolDefinition>> _toolsCache;

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
            ODataMcpOptions options)
        {
            ArgumentNullException.ThrowIfNull(next);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(endpointRegistry);
            ArgumentNullException.ThrowIfNull(toolFactory);
            ArgumentNullException.ThrowIfNull(options);

            _next = next;
            _logger = logger;
            _endpointRegistry = endpointRegistry;
            _toolFactory = toolFactory;
            _options = options;
            _toolsCache = new ConcurrentDictionary<string, IEnumerable<McpToolDefinition>>();
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

        #region Internal Methods

        /// <summary>
        /// Handles MCP info requests.
        /// </summary>
        internal async Task HandleInfoRequest(HttpContext context, McpRouteEntry route)
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
        internal async Task HandleToolsRequest(HttpContext context, McpRouteEntry route)
        {
            var tools = await GetOrGenerateToolsAsync(route);
            
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
        internal async Task HandleToolInfoRequest(HttpContext context, McpRouteEntry route)
        {
            var toolName = context.Request.RouteValues["toolName"]?.ToString();
            if (string.IsNullOrEmpty(toolName))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var tools = await GetOrGenerateToolsAsync(route);
            var tool = tools.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
            
            if (tool == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync($"Tool '{toolName}' not found");
                return;
            }

            var response = new
            {
                name = tool.Name,
                description = tool.Description,
                inputSchema = tool.InputSchema,
                examples = tool.Examples
            };
            
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
        }

        /// <summary>
        /// Handles MCP tool execution requests.
        /// </summary>
        internal async Task HandleToolsExecuteRequest(HttpContext context, McpRouteEntry route)
        {
            // Read the request body
            string requestBody;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Request body is required");
                return;
            }

            JsonDocument? requestJson = null;
            try
            {
                requestJson = JsonDocument.Parse(requestBody);
                
                // Extract tool name and parameters
                if (!requestJson.RootElement.TryGetProperty("tool", out var toolElement) ||
                    toolElement.ValueKind != JsonValueKind.String)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Tool name is required");
                    return;
                }

                var toolName = toolElement.GetString();
                if (string.IsNullOrWhiteSpace(toolName))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Tool name cannot be empty");
                    return;
                }

                // Get parameters (optional)
                JsonDocument? parameters = null;
                if (requestJson.RootElement.TryGetProperty("parameters", out var paramsElement))
                {
                    parameters = JsonDocument.Parse(paramsElement.GetRawText());
                }
                else
                {
                    // Create empty parameters document
                    parameters = JsonDocument.Parse("{}");
                }

                // Find and execute the tool
                var tools = await GetOrGenerateToolsAsync(route);
                var tool = tools.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                
                if (tool == null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsync($"Tool '{toolName}' not found");
                    return;
                }

                // Get the EDM model for this route (required for context)
                var edmModel = GetEdmModelForRoute(route) ?? new EdmModel();
                
                // Create execution context
                var toolContext = new McpToolContext
                {
                    Model = edmModel,
                    ServiceBaseUrl = GetServiceBaseUrl(context, route),
                    HttpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>(),
                    CorrelationId = Guid.NewGuid().ToString()
                };

                // Add route metadata to context
                toolContext.SetProperty("RouteName", route.RouteName);
                toolContext.SetProperty("ODataRoutePrefix", route.ODataRoutePrefix);
                toolContext.SetProperty("Logger", context.RequestServices.GetRequiredService<ILogger<ODataMcpMiddleware>>());
                
                // Execute the tool
                var result = await tool.Handler(toolContext, parameters);
                
                // Return the result
                context.Response.ContentType = "application/json";
                
                if (result.IsSuccess)
                {
                    var successResponse = new
                    {
                        success = true,
                        data = result.Data?.RootElement,
                        correlationId = result.CorrelationId
                    };
                    
                    await context.Response.WriteAsync(JsonSerializer.Serialize(successResponse, new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    }));
                }
                else
                {
                    context.Response.StatusCode = result.ErrorCode switch
                    {
                        "NOT_FOUND" => StatusCodes.Status404NotFound,
                        "VALIDATION_ERROR" => StatusCodes.Status400BadRequest,
                        "UNAUTHORIZED" => StatusCodes.Status401Unauthorized,
                        "FORBIDDEN" => StatusCodes.Status403Forbidden,
                        _ => StatusCodes.Status500InternalServerError
                    };
                    
                    var errorResponse = new
                    {
                        success = false,
                        error = result.ErrorMessage,
                        errorCode = result.ErrorCode,
                        correlationId = result.CorrelationId
                    };
                    
                    await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    }));
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON in request body");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Invalid JSON in request body");
            }
            finally
            {
                requestJson?.Dispose();
            }
        }

        /// <summary>
        /// Gets or generates tools for a route.
        /// </summary>
        internal async Task<IEnumerable<McpToolDefinition>> GetOrGenerateToolsAsync(McpRouteEntry route)
        {
            return await _toolsCache.GetOrAddAsync(route.RouteName, async (key) =>
            {
                // For AspNetCore, we need to get the EDM model from somewhere
                // This is a simplified version - in production, you'd get this from the OData configuration
                var edmModel = GetEdmModelForRoute(route);
                
                if (edmModel is null)
                {
                    _logger.LogWarning("No EDM model found for route {RouteName}", route.RouteName);
                    return Enumerable.Empty<McpToolDefinition>();
                }

                var generationOptions = new McpToolGenerationOptions
                {
                    GenerateCrudTools = true,
                    GenerateQueryTools = true,
                    GenerateNavigationTools = true,
                    IncludeExamples = _options.IncludeMetadata,
                    MaxToolCount = 100
                };
                
                return await _toolFactory.GenerateToolsAsync(edmModel, generationOptions);
            });
        }

        /// <summary>
        /// Gets the EDM model for a route.
        /// </summary>
        internal EdmModel? GetEdmModelForRoute(McpRouteEntry route)
        {
            // TODO: This should be properly integrated with OData configuration
            // For now, return a simple model or null
            // In production, this would get the actual EDM model from the OData route configuration
            
            // Check if we have model metadata in the route
            if (route.Metadata.TryGetValue("EdmModel", out var modelObj) && modelObj is EdmModel model)
            {
                return model;
            }

            _logger.LogWarning("EDM model not found in route metadata for {RouteName}. Tool generation will be limited.", route.RouteName);
            return null;
        }

        /// <summary>
        /// Gets the service base URL for a route.
        /// </summary>
        internal string GetServiceBaseUrl(HttpContext context, McpRouteEntry route)
        {
            var request = context.Request;
            var scheme = request.Scheme;
            var host = request.Host.Value;
            var pathBase = request.PathBase.Value ?? "";
            
            var baseUrl = $"{scheme}://{host}{pathBase}";
            
            if (!string.IsNullOrWhiteSpace(route.ODataRoutePrefix))
            {
                baseUrl = $"{baseUrl.TrimEnd('/')}/{route.ODataRoutePrefix.TrimStart('/')}";
            }
            
            return baseUrl;
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for ConcurrentDictionary to support async operations.
    /// </summary>
    internal static class ConcurrentDictionaryExtensions
    {
        public static async Task<TValue> GetOrAddAsync<TKey, TValue>(
            this ConcurrentDictionary<TKey, TValue> dictionary,
            TKey key,
            Func<TKey, Task<TValue>> valueFactory) where TKey : notnull
        {
            if (dictionary.TryGetValue(key, out var value))
            {
                return value;
            }

            value = await valueFactory(key);
            return dictionary.GetOrAdd(key, value);
        }
    }
}
