using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Server;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.Extensions.DependencyInjection
{

    /// <summary>
    /// Extension methods for configuring OData MCP Server services.
    /// </summary>
    /// <remarks>
    /// Provides extension methods for registering core OData MCP services with the dependency injection container.
    /// These methods support both IConfiguration-based and Action-based configuration patterns.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Configure from appsettings.json
    /// services.AddODataMcpCore(configuration);
    /// 
    /// // Configure programmatically
    /// services.AddODataMcpCore(config =>
    /// {
    ///     config.ODataService.BaseUrl = "https://services.odata.org/V4/Northwind/Northwind.svc";
    ///     config.ODataService.Authentication.Type = ODataAuthenticationType.Bearer;
    ///     config.ODataService.Authentication.BearerToken = "token";
    /// });
    /// 
    /// // Add custom named HTTP client
    /// services.AddODataHttpClient("MyODataClient");
    /// </code>
    /// </example>
    public static class ODataMcp_Core_ServiceCollectionExtensions
    {

        #region Public Methods

        /// <summary>
        /// Adds core OData MCP Server services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration root.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
        /// <remarks>
        /// Registers all core services required for OData MCP functionality, including parsers, tool generators,
        /// and HTTP clients. Configuration is loaded from the "McpServer" section of the provided IConfiguration.
        /// </remarks>
        /// <example>
        /// <code>
        /// var builder = WebApplication.CreateBuilder(args);
        /// builder.Services.AddODataMcpCore(builder.Configuration);
        /// </code>
        /// </example>
        public static IServiceCollection AddODataMcpCore(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // Register configuration from IConfiguration
            services.Configure<McpServerConfiguration>(configuration.GetSection("McpServer"));
            
            // Register all core services
            return services.AddODataMcpCoreServices();
        }

        /// <summary>
        /// Adds OData MCP Server with custom configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Action to configure server options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is null.</exception>
        /// <remarks>
        /// Registers all core services required for OData MCP functionality with programmatic configuration.
        /// This overload is useful when configuration needs to be built dynamically or when not using IConfiguration.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddODataMcpCore(config =>
        /// {
        ///     config.ODataService.BaseUrl = Environment.GetEnvironmentVariable("ODATA_URL");
        ///     config.ODataService.RequestTimeout = TimeSpan.FromMinutes(5);
        ///     config.Caching.Enabled = true;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddODataMcpCore(
            this IServiceCollection services,
            Action<McpServerConfiguration> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            // Register configuration from Action
            services.Configure(configureOptions);
            
            // Register all core services
            return services.AddODataMcpCoreServices();
        }

        /// <summary>
        /// Adds a configured HTTP client for OData service communication.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// Registers a named HTTP client "OData" configured with standard OData headers and authentication.
        /// This method is called automatically by AddODataMcpCore but can be used independently if needed.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddODataHttpClient();
        /// </code>
        /// </example>
        public static IServiceCollection AddODataHttpClient(this IServiceCollection services)
        {
            return services.AddODataHttpClient("OData");
        }

        /// <summary>
        /// Adds a configured HTTP client for OData service communication with a custom name.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="clientName">The name for the HTTP client.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientName"/> is null or whitespace.</exception>
        /// <remarks>
        /// Registers a named HTTP client configured for OData communication. The client is configured with:
        /// - Standard OData headers (Accept, OData-Version, OData-MaxVersion)
        /// - Base URL from configuration
        /// - Authentication headers based on configuration (Bearer, API Key, or Basic)
        /// - Timeout settings from configuration
        /// </remarks>
        /// <example>
        /// <code>
        /// // Register multiple OData clients for different services
        /// services.AddODataHttpClient("NorthwindClient");
        /// services.AddODataHttpClient("AdventureWorksClient");
        /// 
        /// // Use the named client
        /// var client = httpClientFactory.CreateClient("NorthwindClient");
        /// </code>
        /// </example>
        public static IServiceCollection AddODataHttpClient(
            this IServiceCollection services, 
            string clientName)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

            services.AddHttpClient(clientName, (serviceProvider, client) =>
            {
                var config = serviceProvider.GetRequiredService<IOptions<McpServerConfiguration>>().Value;
                
                // Set base address if configured
                if (!string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
                {
                    client.BaseAddress = new Uri(config.ODataService.BaseUrl);
                }
                
                // Add standard OData headers
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("OData-Version", "4.0");
                client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                
                // Configure timeout
                client.Timeout = config.ODataService.RequestTimeout;
                
                // Add authentication headers if configured
                var auth = config.ODataService.Authentication;
                if (auth != null && auth.Type != ODataAuthenticationType.None)
                {
                    switch (auth.Type)
                    {
                        case ODataAuthenticationType.Bearer:
                            if (!string.IsNullOrWhiteSpace(auth.BearerToken))
                            {
                                client.DefaultRequestHeaders.Authorization = 
                                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.BearerToken);
                            }
                            break;
                            
                        case ODataAuthenticationType.ApiKey:
                            if (!string.IsNullOrWhiteSpace(auth.ApiKey))
                            {
                                var headerName = auth.ApiKeyHeader ?? "X-API-Key";
                                client.DefaultRequestHeaders.Add(headerName, auth.ApiKey);
                            }
                            break;
                            
                        case ODataAuthenticationType.Basic:
                            if (auth.BasicAuth != null)
                            {
                                var credentials = Convert.ToBase64String(
                                    System.Text.Encoding.UTF8.GetBytes(
                                        $"{auth.BasicAuth.Username}:{auth.BasicAuth.Password}"));
                                client.DefaultRequestHeaders.Authorization = 
                                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                            }
                            break;
                    }
                }
            });
            
            return services;
        }

        /// <summary>
        /// Configures the OData MCP Server to use the official MCP SDK.
        /// </summary>
        /// <param name="builder">The MCP server builder.</param>
        /// <returns>The MCP server builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
        /// <remarks>
        /// Registers all OData tools from the Core assembly with the MCP server builder.
        /// This method is used when integrating with the official ModelContextProtocol SDK.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMcpServer()
        ///     .WithODataTools()
        ///     .WithStdioTransport();
        /// </code>
        /// </example>
        public static IMcpServerBuilder WithODataTools(this IMcpServerBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Register OData tools with the MCP server
            builder.WithToolsFromAssembly(typeof(ODataMcpTools).Assembly);
            
            return builder;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Adds the core MCP services (internal helper to avoid duplication).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        internal static IServiceCollection AddODataMcpCoreServices(this IServiceCollection services)
        {
            // Register parsers
            services.TryAddSingleton<ICsdlMetadataParser, CsdlParser>();
            
            // Register tool factory
            services.TryAddSingleton<IMcpToolFactory, McpToolFactory>();
            
            // Register MCP tools using attribute-based approach
            services.AddSingleton<ODataMcpTools>();
            services.AddSingleton<DynamicODataMcpTools>();
            // SystemMcpTools registration removed temporarily - needs special handling for IHostApplicationLifetime
            
            // Register OData HTTP client
            services.AddODataHttpClient();
            
            return services;
        }

        #endregion

    }

}
