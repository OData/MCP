using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Middleware;
using Microsoft.OData.Mcp.AspNetCore.Routing;
using Microsoft.OData.Mcp.Core;
using Microsoft.OData.Mcp.Core.Routing;
using Microsoft.OData.Mcp.Core.Services;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering OData MCP automatic routing services.
    /// </summary>
    public static class ODataMcpServiceCollectionExtensions
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
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (configureOptions is null)
            {
                throw new ArgumentNullException(nameof(configureOptions));
            }
#endif

            // Configure options using the Options pattern
            services.Configure<ODataMcpOptions>(configureOptions);
            
            // Also register as singleton for backward compatibility
            services.AddSingleton<ODataMcpOptions>(sp =>
            {
                var options = new ODataMcpOptions();
                configureOptions(options);
                return options;
            });

            // Register core routing services
            services.TryAddSingleton<IMcpEndpointRegistry, McpEndpointRegistry>();
            services.TryAddSingleton<IMcpRouteConvention, ODataMcpRouteConvention>();
            
            // Note: IODataOptionsProvider should be implemented by the host application
            // if they want dollar prefix handling to work correctly
            
            // Conditionally register dynamic model refresh service
            var optionsInstance = new ODataMcpOptions();
            configureOptions(optionsInstance);
            if (optionsInstance.EnableDynamicModels)
            {
                services.AddHostedService<DynamicModelRefreshService>();
            }

            // Add memory cache if not already registered
            services.TryAddSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache, Microsoft.Extensions.Caching.Memory.MemoryCache>();

            // Mark that OData MCP automatic routing is enabled
            services.Configure<ODataMcpMarkerOptions>(o => o.IsEnabled = true);

            return services;
        }
    }

    /// <summary>
    /// Marker options to indicate OData MCP is enabled.
    /// </summary>
    internal class ODataMcpMarkerOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether OData MCP is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }
    }
}