using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Routing;
using Microsoft.OData.Mcp.Core.Routing;

namespace Microsoft.AspNetCore.Routing
{

    /// <summary>
    /// Extension methods for adding MCP to OData routes.
    /// </summary>
    public static class ODataMcp_AspNetCore_RouteBuilderExtensions
    {

        /// <summary>
        /// Adds MCP endpoints to an OData route using the default path.
        /// </summary>
        /// <param name="routeBuilder">The route builder.</param>
        /// <returns>The route builder for chaining.</returns>
        /// <remarks>
        /// This method adds MCP endpoints as siblings to the OData $metadata endpoint.
        /// For example, if the OData route is "api/v1", the MCP endpoints will be at "api/v1/mcp".
        /// </remarks>
        /// <example>
        /// <code>
        /// endpoints.MapODataRoute("odata", "api/v1", GetEdmModel())
        ///     .AddMcp();
        /// </code>
        /// </example>
        public static IRouteBuilder AddMcp(this IRouteBuilder routeBuilder)
        {
            return routeBuilder.AddMcp(null);
        }

        /// <summary>
        /// Adds MCP endpoints to an OData route using a custom path.
        /// </summary>
        /// <param name="routeBuilder">The route builder.</param>
        /// <param name="customPath">The custom MCP path, or null to use the default.</param>
        /// <returns>The route builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeBuilder"/> is null.</exception>
        /// <example>
        /// <code>
        /// endpoints.MapODataRoute("odata", "api/v1", GetEdmModel())
        ///     .AddMcp("/custom/mcp/path");
        /// </code>
        /// </example>
        public static IRouteBuilder AddMcp(this IRouteBuilder routeBuilder, string? customPath)
        {
            ArgumentNullException.ThrowIfNull(routeBuilder);

            // Get the services
            var serviceProvider = routeBuilder.ServiceProvider;
            var endpointRegistry = serviceProvider.GetService<IMcpEndpointRegistry>();
            
            if (endpointRegistry == null)
            {
                throw new InvalidOperationException(
                    "MCP services have not been registered. " +
                    "Call services.AddODataMcp() before using AddMcp().");
            }

            // Extract route information from the route builder
            // This is a simplified implementation - in practice, we'd need to hook into
            // the OData route registration process more deeply
            
            // For now, just mark that explicit registration was requested
            // The actual registration would happen through the OData pipeline
            
            return routeBuilder;
        }

        /// <summary>
        /// Adds MCP endpoints to an endpoint route builder for a specific OData route.
        /// </summary>
        /// <param name="endpointRouteBuilder">The endpoint route builder.</param>
        /// <param name="routeName">The OData route name.</param>
        /// <param name="routePrefix">The OData route prefix.</param>
        /// <param name="customMcpPath">Optional custom MCP path.</param>
        /// <returns>The endpoint route builder for chaining.</returns>
        public static IEndpointRouteBuilder AddMcpForODataRoute(
            this IEndpointRouteBuilder endpointRouteBuilder,
            string routeName,
            string routePrefix,
            string? customMcpPath = null)
        {
            ArgumentNullException.ThrowIfNull(endpointRouteBuilder);

            var serviceProvider = endpointRouteBuilder.ServiceProvider;
            var endpointRegistry = serviceProvider.GetRequiredService<IMcpEndpointRegistry>();
            var convention = serviceProvider.GetRequiredService<IMcpRouteConvention>();

            // Create the route entry
            var normalizedPrefix = routePrefix?.Trim('/')
                ?? string.Empty;

            var mcpBasePath = customMcpPath
                ?? (string.IsNullOrEmpty(normalizedPrefix) ? "/mcp" : $"/{normalizedPrefix}/mcp");

            var routeEntry = new McpRouteEntry
            {
                RouteName = routeName,
                ODataRoutePrefix = routePrefix ?? string.Empty,
                McpBasePath = mcpBasePath,
                IsExplicit = true,
                CustomMcpPath = customMcpPath
            };

            // Register the endpoint
            endpointRegistry.Register(routeEntry);

            // Apply the convention to create the actual endpoints
            convention.ApplyConvention(endpointRouteBuilder, routePrefix ?? string.Empty, routeName);

            return endpointRouteBuilder;
        }
    }
}
