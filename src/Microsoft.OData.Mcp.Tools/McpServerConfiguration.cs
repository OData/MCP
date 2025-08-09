using System;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Server;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.OData.Mcp.Core.Tools.Generators;

namespace Microsoft.OData.Mcp.Tools
{

    /// <summary>
    /// Provides configuration methods for the MCP server that can be reused in tests.
    /// </summary>
    public static class McpServerConfiguration
    {

        /// <summary>
        /// Configures the core MCP server services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">The configuration to use.</param>
        /// <param name="odataServiceUrl">The OData service URL to connect to.</param>
        /// <param name="authToken">Optional authentication token for the OData service.</param>
        /// <remarks>
        /// RWM: Thus should move to a set of Extension methids for starting the MCP Server.
        /// </remarks>
        public static void ConfigureMcpServices(
            IServiceCollection services, 
            IConfiguration configuration,
            string odataServiceUrl,
            string? authToken = null)
        {
            // Add core MCP services
            services.AddODataMcpServerCore(configuration);
            
            // Add parser
            services.AddSingleton<ICsdlMetadataParser, CsdlParser>();
            
            // Add MCP tool services
            services.AddScoped<ODataMcpTools>();
            services.AddScoped<DynamicODataMcpTools>();
            
            // Add tool generation services
            services.AddScoped<McpToolFactory>();
            services.AddScoped<IMcpToolFactory, McpToolFactory>();
            services.AddScoped<IQueryToolGenerator, QueryToolGenerator>();
            services.AddScoped<ICrudToolGenerator, CrudToolGenerator>();
            services.AddScoped<INavigationToolGenerator, NavigationToolGenerator>();
            
            // Add HTTP client factory for OData calls
            services.AddHttpClient("OData", client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/xml");
                client.DefaultRequestHeaders.Add("Accept", "text/xml");
                
                if (!string.IsNullOrWhiteSpace(authToken))
                {
                    client.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", authToken);
                }
            });
            
            // Configure OData service URL if provided
            if (!string.IsNullOrWhiteSpace(odataServiceUrl))
            {
                services.Configure<Core.Configuration.McpServerConfiguration>(options =>
                {
                    options.ODataService.BaseUrl = odataServiceUrl;
                });
            }
            
            // Configure authentication if token provided
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                services.Configure<Core.Configuration.McpServerConfiguration>(options =>
                {
                    options.Authentication.Enabled = true;
                    // Additional auth configuration would go here
                });
            }
        }
        
        /// <summary>
        /// Configures logging for the MCP server.
        /// </summary>
        /// <param name="builder">The logging builder to configure.</param>
        /// <param name="verbose">Whether to enable verbose logging.</param>
        public static void ConfigureLogging(ILoggingBuilder builder, bool verbose = false)
        {
            builder.ClearProviders();
            builder.AddConsole();
            
            if (verbose)
            {
                builder.SetMinimumLevel(LogLevel.Debug);
            }
            else
            {
                builder.SetMinimumLevel(LogLevel.Information);
            }
        }

    }

}
