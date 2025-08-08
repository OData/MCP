using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core;
using Microsoft.OData.Mcp.Core.Routing;

namespace Microsoft.OData.Mcp.AspNetCore.Routing
{
    /// <summary>
    /// Automatically adds MCP endpoints to OData routes during registration.
    /// </summary>
    /// <remarks>
    /// This convention ensures that for each OData route registered, corresponding
    /// MCP endpoints are automatically created as siblings to the $metadata endpoint.
    /// </remarks>
    public class ODataMcpRouteConvention : IMcpRouteConvention
    {
        #region Fields

        private readonly ODataMcpOptions _options;
        private readonly IMcpEndpointRegistry _endpointRegistry;
        private readonly ODataRouteOptionsResolver _routeOptionsResolver;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpRouteConvention"/> class.
        /// </summary>
        /// <param name="options">The MCP options.</param>
        /// <param name="endpointRegistry">The MCP endpoint registry.</param>
        /// <param name="odataOptionsProvider">Optional provider for OData configuration options.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when required parameters are null.
        /// </exception>
        public ODataMcpRouteConvention(
            IOptions<ODataMcpOptions> options,
            IMcpEndpointRegistry endpointRegistry,
            IODataOptionsProvider? odataOptionsProvider = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(endpointRegistry);
#else
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (endpointRegistry is null)
            {
                throw new ArgumentNullException(nameof(endpointRegistry));
            }
#endif

            _options = options.Value;
            _endpointRegistry = endpointRegistry;
            _routeOptionsResolver = new ODataRouteOptionsResolver(odataOptionsProvider);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies MCP conventions to the endpoint route builder.
        /// </summary>
        /// <param name="endpointRouteBuilder">The endpoint route builder.</param>
        /// <param name="routePrefix">The OData route prefix.</param>
        /// <param name="routeName">The OData route name.</param>
        public void ApplyConvention(IEndpointRouteBuilder endpointRouteBuilder, string routePrefix, string routeName)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(endpointRouteBuilder);
            ArgumentException.ThrowIfNullOrWhiteSpace(routeName);
#else
            if (endpointRouteBuilder is null)
            {
                throw new ArgumentNullException(nameof(endpointRouteBuilder));
            }
            if (string.IsNullOrWhiteSpace(routeName))
            {
                throw new ArgumentException("Route name cannot be null or whitespace.", nameof(routeName));
            }
#endif

            // Check if this route should have MCP enabled
            if (!ShouldApplyMcp(routeName))
            {
                return;
            }

            // Normalize the route prefix
            var normalizedPrefix = NormalizeRoutePrefix(routePrefix);
            
            // Build the MCP base path
            var mcpBasePath = BuildMcpBasePath(normalizedPrefix);

            // Create the route entry
            var routeEntry = new McpRouteEntry
            {
                RouteName = routeName,
                ODataRoutePrefix = routePrefix ?? string.Empty,
                McpBasePath = mcpBasePath,
                IsExplicit = false
            };

            // Register in the endpoint registry
            _endpointRegistry.RegisterEndpoint(routeEntry);

            // Map the MCP endpoints
            MapMcpEndpoints(endpointRouteBuilder, mcpBasePath, routeName);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Determines whether MCP should be applied to this route.
        /// </summary>
        /// <param name="routeName">The route name.</param>
        /// <returns>True if MCP should be applied; otherwise, false.</returns>
        private bool ShouldApplyMcp(string routeName)
        {
            // Check if auto-registration is enabled
            if (!_options.AutoRegisterRoutes)
            {
                return false;
            }

            // Check if this route is excluded
            if (_options.ExcludeRoutes != null)
            {
                foreach (var excludedRoute in _options.ExcludeRoutes)
                {
                    if (string.Equals(excludedRoute, routeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Normalizes the route prefix for consistent handling.
        /// </summary>
        /// <param name="routePrefix">The route prefix to normalize.</param>
        /// <returns>The normalized route prefix.</returns>
        private static string NormalizeRoutePrefix(string? routePrefix)
        {
            if (string.IsNullOrWhiteSpace(routePrefix))
            {
                return string.Empty;
            }

            return routePrefix.Trim('/');
        }

        /// <summary>
        /// Builds the MCP base path from the route prefix.
        /// </summary>
        /// <param name="normalizedPrefix">The normalized route prefix.</param>
        /// <returns>The MCP base path.</returns>
        private static string BuildMcpBasePath(string normalizedPrefix)
        {
            if (string.IsNullOrEmpty(normalizedPrefix))
            {
                return "/mcp";
            }

            return $"/{normalizedPrefix}/mcp";
        }

        /// <summary>
        /// Maps the MCP endpoints for this route.
        /// </summary>
        /// <param name="endpointRouteBuilder">The endpoint route builder.</param>
        /// <param name="mcpBasePath">The MCP base path.</param>
        /// <param name="routeName">The route name.</param>
        private void MapMcpEndpoints(IEndpointRouteBuilder endpointRouteBuilder, string mcpBasePath, string routeName)
        {
            // For now, just register the routes with placeholder handlers
            // The actual implementation will be handled by the existing MCP infrastructure
            
            // Note: MCP endpoints don't use dollar prefixes regardless of OData settings
            // They are always /mcp, /mcp/tools, etc., not /$mcp
            
            // Map /mcp (info)
            endpointRouteBuilder.MapGet(mcpBasePath, async context =>
            {
                // TODO: Wire up to existing MCP info handler
                context.Response.StatusCode = StatusCodes.Status501NotImplemented;
                await context.Response.WriteAsync($"MCP info endpoint for route '{routeName}' not yet implemented");
            })
            .WithMetadata(new McpEndpointMetadata { Command = McpCommand.Info, RouteName = routeName });

            // Map /mcp/tools
            endpointRouteBuilder.MapGet($"{mcpBasePath}/tools", async context =>
            {
                // TODO: Wire up to existing MCP tools handler
                context.Response.StatusCode = StatusCodes.Status501NotImplemented;
                await context.Response.WriteAsync($"MCP tools endpoint for route '{routeName}' not yet implemented");
            })
            .WithMetadata(new McpEndpointMetadata { Command = McpCommand.Tools, RouteName = routeName });

            // Map /mcp/tools/{{toolName}}
            endpointRouteBuilder.MapGet($"{mcpBasePath}/tools/{{toolName}}", async context =>
            {
                var toolName = context.GetRouteValue("toolName")?.ToString();
                if (string.IsNullOrEmpty(toolName))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                // TODO: Wire up to existing MCP tool info handler
                context.Response.StatusCode = StatusCodes.Status501NotImplemented;
                await context.Response.WriteAsync($"MCP tool info for '{toolName}' on route '{routeName}' not yet implemented");
            })
            .WithMetadata(new McpEndpointMetadata { Command = McpCommand.ToolInfo, RouteName = routeName });

            // Map /mcp/tools/execute
            endpointRouteBuilder.MapPost($"{mcpBasePath}/tools/execute", async context =>
            {
                // TODO: Wire up to existing MCP execution handler
                context.Response.StatusCode = StatusCodes.Status501NotImplemented;
                await context.Response.WriteAsync($"MCP tools execute endpoint for route '{routeName}' not yet implemented");
            })
            .WithMetadata(new McpEndpointMetadata { Command = McpCommand.ToolsExecute, RouteName = routeName });
        }

        #endregion
    }

    /// <summary>
    /// Metadata for MCP endpoints.
    /// </summary>
    public class McpEndpointMetadata
    {
        /// <summary>
        /// Gets or sets the MCP command type.
        /// </summary>
        public McpCommand Command { get; set; }

        /// <summary>
        /// Gets or sets the OData route name.
        /// </summary>
        public string? RouteName { get; set; }
    }
}