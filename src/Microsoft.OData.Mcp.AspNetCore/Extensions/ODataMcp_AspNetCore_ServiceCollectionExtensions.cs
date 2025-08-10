// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OData.Mcp.AspNetCore.Routing;
using Microsoft.OData.Mcp.Core;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Routing;
using Microsoft.OData.Mcp.Core.Services;

namespace Microsoft.Extensions.DependencyInjection
{

    /// <summary>
    /// Extension methods for registering OData MCP automatic routing services.
    /// </summary>
    public static class ODataMcp_AspNetCore_ServiceCollectionExtensions
    {

        /// <summary>
        /// Adds OData MCP automatic routing services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <example>
        /// <code>
        /// builder.Services.AddControllers()
        ///     .AddOData(options => options
        ///         .AddRouteComponents("api/v1", GetV1Model())
        ///         .AddRouteComponents("api/v2", GetV2Model()));
        /// 
        /// builder.Services.AddODataMcp(); // Automatically enables MCP for all routes
        /// </code>
        /// </example>
        public static IServiceCollection AddODataMcp(this IServiceCollection services)
        {
            return services.AddODataMcp(_ => { });
        }

        /// <summary>
        /// Adds OData MCP automatic routing services to the service collection with configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">The options configuration delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is null.
        /// </exception>
        /// <example>
        /// <code>
        /// builder.Services.AddODataMcp(options =>
        /// {
        ///     options.AutoRegisterRoutes = true;
        ///     options.ExcludeRoutes = new[] { "internal", "legacy" };
        ///     options.EnableDynamicModels = false;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddODataMcp(
            this IServiceCollection services,
            Action<ODataMcpOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            // CRITICAL: Register all core MCP services first
            // This includes IMcpToolFactory, ICsdlMetadataParser, tool generators, etc.
            services.AddODataMcpCore(config =>
            {
                // AspNetCore scenarios typically don't have a single OData service URL
                // Each route will have its own model, so we leave BaseUrl empty
                config.ODataService.BaseUrl = string.Empty;
            });

            // Configure options using the Options pattern
            services.Configure<ODataMcpOptions>(configureOptions);
            
            // Also register as singleton for backward compatibility and middleware injection
            services.AddSingleton<ODataMcpOptions>(sp =>
            {
                var options = new ODataMcpOptions();
                configureOptions(options);
                return options;
            });

            // Register AspNetCore-specific routing services
            services.TryAddSingleton<IMcpEndpointRegistry, McpEndpointRegistry>();
            services.TryAddSingleton<IMcpRouteConvention, ODataMcpRouteConvention>();
            
            // Conditionally register dynamic model refresh service
            var optionsInstance = new ODataMcpOptions();
            configureOptions(optionsInstance);
            if (optionsInstance.EnableDynamicModels)
            {
                services.AddHostedService<DynamicModelRefreshService>();
            }

            // Add memory cache if not already registered
            services.TryAddSingleton<IMemoryCache, MemoryCache>();

            // Mark that OData MCP automatic routing is enabled
            services.Configure<ODataMcpMarkerOptions>(o => o.IsEnabled = true);

            return services;
        }

    }

}
