using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Authentication.Models;
using Microsoft.OData.Mcp.Authentication.Services;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.OData.Mcp.Middleware.Configuration;
using Microsoft.OData.Mcp.Middleware.Services;

namespace Microsoft.OData.Mcp.Middleware.Extensions
{
    /// <summary>
    /// Extension methods for configuring MCP middleware services in the dependency injection container.
    /// </summary>
    /// <remarks>
    /// These extensions provide a fluent API for registering all services required for MCP middleware
    /// integration, including metadata discovery, tool generation, and authentication.
    /// </remarks>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds MCP middleware services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration to bind settings from.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
        public static IServiceCollection AddMcpMiddleware(this IServiceCollection services, IConfiguration configuration)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
#endif

            // Configure middleware options
            services.Configure<McpMiddlewareOptions>(configuration.GetSection("McpMiddleware"));

            // Add core services
            services.AddSingleton<CsdlParser>();
            services.AddScoped<IMcpToolFactory, McpToolFactory>();

            // Add authentication services if enabled
            var middlewareOptions = new McpMiddlewareOptions();
            configuration.GetSection("McpMiddleware").Bind(middlewareOptions);

            if (middlewareOptions.Authentication.Enabled)
            {
                services.Configure<McpAuthenticationOptions>(configuration.GetSection("McpMiddleware:Authentication"));
                services.AddScoped<ITokenValidationService, TokenValidationService>();
            }

            // Add metadata discovery service
            services.AddSingleton<IMetadataDiscoveryService, MetadataDiscoveryService>();
            services.AddHostedService(provider => (MetadataDiscoveryService)provider.GetRequiredService<IMetadataDiscoveryService>());

            // Add HTTP client for metadata discovery
            services.AddHttpClient("MetadataDiscovery", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/xml");
            });

            // Add HTTP client for OData service calls
            services.AddHttpClient("ODataService", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(2);
            });

            return services;
        }

        /// <summary>
        /// Adds MCP middleware services with custom configuration.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configureOptions">Action to configure the middleware options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is null.</exception>
        public static IServiceCollection AddMcpMiddleware(this IServiceCollection services, Action<McpMiddlewareOptions> configureOptions)
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

            // Configure middleware options
            services.Configure(configureOptions);

            // Validate options during configuration
            var options = new McpMiddlewareOptions();
            configureOptions(options);
            
            var validationErrors = options.Validate().ToList();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException($"Invalid MCP middleware configuration: {string.Join(", ", validationErrors)}");
            }

            // Add core services
            services.AddSingleton<CsdlParser>();
            services.AddScoped<IMcpToolFactory, McpToolFactory>();

            // Add authentication services if enabled
            if (options.Authentication.Enabled)
            {
                services.Configure<McpAuthenticationOptions>(authOptions =>
                {
                    authOptions.Enabled = options.Authentication.Enabled;
                    authOptions.Scheme = options.Authentication.Scheme;
                    authOptions.JwtBearer = options.Authentication.JwtBearer;
                    authOptions.TokenDelegation = options.Authentication.TokenDelegation;
                    authOptions.ScopeAuthorization = options.Authentication.ScopeAuthorization;
                    authOptions.RequireHttps = options.Authentication.RequireHttps;
                    authOptions.Timeout = options.Authentication.Timeout;
                });
                services.AddScoped<ITokenValidationService, TokenValidationService>();
            }

            // Add metadata discovery service
            services.AddSingleton<IMetadataDiscoveryService, MetadataDiscoveryService>();
            services.AddHostedService(provider => (MetadataDiscoveryService)provider.GetRequiredService<IMetadataDiscoveryService>());

            // Add HTTP clients
            services.AddHttpClient("MetadataDiscovery", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/xml");
            });

            services.AddHttpClient("ODataService", client =>
            {
                client.Timeout = options.RequestTimeout;
            });

            return services;
        }

        /// <summary>
        /// Adds MCP middleware services with the specified configuration options.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="options">The middleware configuration options.</param>        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="options"/> is null.</exception>
        public static IServiceCollection AddMcpMiddleware(this IServiceCollection services, McpMiddlewareOptions options)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
#endif

            return services.AddMcpMiddleware(opts =>
            {
                opts.Enabled = options.Enabled;
                opts.BasePath = options.BasePath;
                opts.MetadataPath = options.MetadataPath;
                opts.MetadataRefreshInterval = options.MetadataRefreshInterval;
                opts.EnableAuthentication = options.EnableAuthentication;
                opts.EnableToolFiltering = options.EnableToolFiltering;
                opts.EnableRateLimiting = options.EnableRateLimiting;
                opts.EnableLogging = options.EnableLogging;
                opts.IncludeHealthChecks = options.IncludeHealthChecks;
                opts.IncludeDetailedErrors = options.IncludeDetailedErrors;
                opts.RequestTimeout = options.RequestTimeout;
                opts.MaxRequestBodySize = options.MaxRequestBodySize;
                opts.IntegrationMode = options.IntegrationMode;
            });
        }

        /// <summary>
        /// Adds MCP middleware services with development-friendly defaults.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddMcpMiddlewareForDevelopment(this IServiceCollection services)
        {
            return services.AddMcpMiddleware(McpMiddlewareOptions.Development);
        }

        /// <summary>
        /// Adds MCP middleware services with production-optimized defaults.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddMcpMiddlewareForProduction(this IServiceCollection services)
        {
            return services.AddMcpMiddleware(McpMiddlewareOptions.Production);
        }

        /// <summary>
        /// Adds MCP middleware services with a specific metadata URL.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="metadataUrl">The URL where OData metadata can be retrieved.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="metadataUrl"/> is null or whitespace.</exception>
        public static IServiceCollection AddMcpMiddlewareWithMetadata(this IServiceCollection services, string metadataUrl, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(metadataUrl);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (string.IsNullOrWhiteSpace(metadataUrl))
            {
                throw new ArgumentException("Metadata URL cannot be null or whitespace.", nameof(metadataUrl));
            }
#endif

            return services.AddMcpMiddleware(options =>
            {
                // Parse metadata URL to extract base and path
                var uri = new Uri(metadataUrl);
                var baseUrl = $"{uri.Scheme}://{uri.Authority}";
                var metadataPath = uri.PathAndQuery;

                options.ServiceRootUrl = baseUrl;
                options.MetadataPath = metadataPath;
                options.AutoDiscoverMetadata = true;

                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Adds MCP middleware services with authentication disabled.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <remarks>
        /// This method is useful for development scenarios or internal services where authentication
        /// is handled by other layers (e.g., network security, VPN, etc.).
        /// </remarks>
        public static IServiceCollection AddMcpMiddlewareWithoutAuth(this IServiceCollection services, Action<McpMiddlewareOptions>? configureOptions = null)
        {
            return services.AddMcpMiddleware(options =>
            {
                options.Authentication.Enabled = false;
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Adds MCP middleware services with custom tool generation options.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configureToolGeneration">Action to configure tool generation options.</param>
        /// <param name="configureOptions">Optional action to configure additional middleware options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configureToolGeneration"/> is null.</exception>
        public static IServiceCollection AddMcpMiddlewareWithToolOptions(this IServiceCollection services, Action<McpToolGenerationOptions> configureToolGeneration, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureToolGeneration);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (configureToolGeneration is null)
            {
                throw new ArgumentNullException(nameof(configureToolGeneration));
            }
#endif

            return services.AddMcpMiddleware(options =>
            {
                configureToolGeneration(options.ToolGeneration);
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Adds MCP middleware services with read-only tool generation.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <remarks>
        /// This method configures tool generation to only include read operations (Get, Query, Navigate)
        /// and excludes write operations (Create, Update, Delete). This is useful for read-only scenarios
        /// or when write access should be restricted.
        /// </remarks>
        public static IServiceCollection AddMcpMiddlewareReadOnly(this IServiceCollection services, Action<McpMiddlewareOptions>? configureOptions = null)
        {
            return services.AddMcpMiddleware(options =>
            {
                options.ToolGeneration = McpToolGenerationOptions.ReadOnly();
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Adds MCP middleware services with performance-optimized settings.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <remarks>
        /// This method configures the middleware with settings optimized for performance,
        /// including simplified tool generation and aggressive caching.
        /// </remarks>
        public static IServiceCollection AddMcpMiddlewareForPerformance(this IServiceCollection services, Action<McpMiddlewareOptions>? configureOptions = null)
        {
            return services.AddMcpMiddleware(options =>
            {
                options.ToolGeneration = McpToolGenerationOptions.Performance();
                options.EnableCaching = true;
                options.CacheDuration = TimeSpan.FromHours(4);
                options.MetadataRefreshInterval = TimeSpan.FromHours(2);
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Adds MCP middleware services with entity-specific configuration.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="includeEntityTypes">Collection of entity type names to include in tool generation.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="includeEntityTypes"/> is null.</exception>
        /// <remarks>
        /// This method configures tool generation to only include specified entity types,
        /// which is useful for exposing only a subset of the OData model through MCP.
        /// </remarks>
        public static IServiceCollection AddMcpMiddlewareForEntities(this IServiceCollection services, IEnumerable<string> includeEntityTypes, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(includeEntityTypes);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (includeEntityTypes is null)
            {
                throw new ArgumentNullException(nameof(includeEntityTypes));
            }
#endif

            return services.AddMcpMiddleware(options =>
            {
                options.ToolGeneration.IncludeEntityTypes = new HashSet<string>(includeEntityTypes);
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Adds MCP middleware services with custom base path.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="basePath">The base path for MCP endpoints.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="basePath"/> is null or whitespace.</exception>
        public static IServiceCollection AddMcpMiddlewareAt(this IServiceCollection services, string basePath, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (string.IsNullOrWhiteSpace(basePath))
            {
                throw new ArgumentException("Base path cannot be null or whitespace.", nameof(basePath));
            }
#endif

            return services.AddMcpMiddleware(options =>
            {
                options.BasePath = basePath.StartsWith('/') ? basePath : $"/{basePath}";
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Conditionally adds MCP middleware services based on a condition.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="configureOptions">Optional action to configure middleware options when condition is true.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <remarks>
        /// This method allows conditional registration of MCP middleware services based on
        /// runtime conditions such as environment settings or feature flags.
        /// </remarks>
        public static IServiceCollection AddMcpMiddlewareWhen(this IServiceCollection services, bool condition, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
#endif

            if (condition)
            {
                return configureOptions is not null 
                    ? services.AddMcpMiddleware(configureOptions) 
                    : services.AddMcpMiddleware(McpMiddlewareOptions.Default);
            }

            return services;
        }

        /// <summary>
        /// Conditionally adds MCP middleware services based on a condition delegate.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="condition">The condition delegate to evaluate.</param>
        /// <param name="configureOptions">Optional action to configure middleware options when condition is true.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="condition"/> is null.</exception>
        /// <remarks>
        /// This method allows conditional registration of MCP middleware services based on a delegate
        /// that receives the service collection for evaluation.
        /// </remarks>
        public static IServiceCollection AddMcpMiddlewareWhen(this IServiceCollection services, Func<IServiceCollection, bool> condition, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(condition);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (condition is null)
            {
                throw new ArgumentNullException(nameof(condition));
            }
#endif

            if (condition(services))
            {
                return configureOptions is not null 
                    ? services.AddMcpMiddleware(configureOptions) 
                    : services.AddMcpMiddleware(McpMiddlewareOptions.Default);
            }

            return services;
        }
    }
}
