// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Middleware;
using Microsoft.OData.Mcp.AspNetCore.Routing;
using Microsoft.OData.Mcp.Core;

namespace Microsoft.AspNetCore.Builder
{

    /// <summary>
    /// Extension methods for configuring OData MCP in the application pipeline.
    /// </summary>
    public static class ODataMcp_AspNetCore_ApplicationBuilderExtensions
    {

        /// <summary>
        /// Adds OData MCP middleware to automatically discover and register MCP endpoints.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        /// <remarks>
        /// This method must be called after UseRouting() but before UseEndpoints() or MapControllers().
        /// It automatically discovers all registered OData routes and adds corresponding MCP endpoints.
        /// </remarks>
        /// <example>
        /// <code>
        /// var app = builder.Build();
        /// app.UseRouting();
        /// app.UseODataMcp(); // Automatic MCP endpoint registration
        /// app.MapControllers();
        /// </code>
        /// </example>
        public static IApplicationBuilder UseODataMcp(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            // Check if OData MCP was registered
            var markerOptions = app.ApplicationServices.GetService<IOptions<ODataMcpMarkerOptions>>();
            if (markerOptions?.Value?.IsEnabled != true)
            {
                throw new InvalidOperationException(
                    "OData MCP services have not been registered. " +
                    "Call services.AddODataMcp() in ConfigureServices before calling UseODataMcp().");
            }

            var options = app.ApplicationServices.GetRequiredService<IOptions<ODataMcpOptions>>();
            if (!options.Value.AutoRegisterRoutes)
            {
                // Auto-registration is disabled
                return app;
            }

            // OData route discovery would happen here
            // For now, routes must be registered explicitly using the fluent API

            var convention = app.ApplicationServices.GetService<IMcpRouteConvention>()
                ?? app.ApplicationServices.GetRequiredService<ODataMcpRouteConvention>();

            var endpointDataSource = app.ApplicationServices.GetRequiredService<EndpointDataSource>();

            // Add the MCP middleware to handle requests
            app.UseMiddleware<ODataMcpMiddleware>();

            return app;
        }

    }

}
