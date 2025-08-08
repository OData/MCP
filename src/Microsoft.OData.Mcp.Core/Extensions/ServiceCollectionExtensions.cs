using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Server;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.OData.Mcp.Core.Tools.Generators;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring OData MCP Server services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds core OData MCP Server services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration root.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddODataMcpServerCore(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // Register configuration
            services.Configure<McpServerConfiguration>(configuration.GetSection("McpServer"));
            
            // Register core services
            services.TryAddSingleton<ICsdlMetadataParser, CsdlParser>();
            services.TryAddSingleton<IMcpToolFactory, McpToolFactory>();
            
            // Register tool generators
            services.TryAddSingleton<IQueryToolGenerator, QueryToolGenerator>();
            services.TryAddSingleton<ICrudToolGenerator, CrudToolGenerator>();
            services.TryAddSingleton<INavigationToolGenerator, NavigationToolGenerator>();
            
            // Register MCP tools using attribute-based approach
            services.AddSingleton<ODataMcpTools>();
            services.AddSingleton<DynamicODataMcpTools>();
            
            // Register HTTP client for OData service communication
            services.AddHttpClient("OData", (serviceProvider, client) =>
            {
                var config = serviceProvider.GetRequiredService<IOptions<McpServerConfiguration>>().Value;
                
                if (!string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
                {
                    client.BaseAddress = new Uri(config.ODataService.BaseUrl);
                }
                
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("OData-Version", "4.0");
                client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                
                // Set timeout
                client.Timeout = config.ODataService.RequestTimeout;
            });
            
            return services;
        }

        /// <summary>
        /// Adds OData MCP Server with custom configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Action to configure server options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddODataMcpServerCore(
            this IServiceCollection services,
            Action<McpServerConfiguration> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            // Configure options
            services.Configure(configureOptions);
            
            // Register core services
            services.TryAddSingleton<ICsdlMetadataParser, CsdlParser>();
            services.TryAddSingleton<IMcpToolFactory, McpToolFactory>();
            
            // Register tool generators
            services.TryAddSingleton<IQueryToolGenerator, QueryToolGenerator>();
            services.TryAddSingleton<ICrudToolGenerator, CrudToolGenerator>();
            services.TryAddSingleton<INavigationToolGenerator, NavigationToolGenerator>();
            
            // Register MCP tools using attribute-based approach
            services.AddSingleton<ODataMcpTools>();
            services.AddSingleton<DynamicODataMcpTools>();
            
            // Register HTTP client
            services.AddHttpClient("OData", (serviceProvider, client) =>
            {
                var config = serviceProvider.GetRequiredService<IOptions<McpServerConfiguration>>().Value;
                
                if (!string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
                {
                    client.BaseAddress = new Uri(config.ODataService.BaseUrl);
                }
                
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("OData-Version", "4.0");
                client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                
                client.Timeout = config.ODataService.RequestTimeout;
            });
            
            return services;
        }

        /// <summary>
        /// Configures the OData MCP Server to use the official MCP SDK.
        /// </summary>
        /// <param name="builder">The MCP server builder.</param>
        /// <returns>The MCP server builder for chaining.</returns>
        public static IMcpServerBuilder WithODataTools(this IMcpServerBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Register OData tools with the MCP server
            builder.WithToolsFromAssembly(typeof(ODataMcpTools).Assembly);
            
            return builder;
        }
    }
}
