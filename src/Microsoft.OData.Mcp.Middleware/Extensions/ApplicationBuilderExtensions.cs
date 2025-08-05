using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Middleware.Configuration;
using Microsoft.OData.Mcp.Middleware.Middleware;

namespace Microsoft.OData.Mcp.Middleware.Extensions
{
    /// <summary>
    /// Extension methods for configuring MCP middleware in the application pipeline.
    /// </summary>
    /// <remarks>
    /// These extensions provide a fluent API for adding MCP server capabilities to
    /// existing ASP.NET Core applications through middleware integration.
    /// </remarks>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds MCP server middleware to the application pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        /// <remarks>
        /// This method adds the MCP server middleware using the configuration from dependency injection.
        /// The middleware must be configured through the <see cref="ServiceCollectionExtensions.AddMcpMiddleware(IServiceCollection, IConfiguration)"/> method.
        /// </remarks>
        public static IApplicationBuilder UseMcpServer(this IApplicationBuilder app)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(app);
#else
            if (app is null)
            {
                throw new ArgumentNullException(nameof(app));
            }
#endif

            return app.UseMiddleware<McpServerMiddleware>();
        }

        /// <summary>
        /// Adds MCP server middleware to the application pipeline with a configuration delegate.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="configureOptions">Action to configure the middleware options.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> or <paramref name="configureOptions"/> is null.</exception>
        /// <remarks>
        /// This method allows inline configuration of the middleware options without requiring
        /// separate service registration. It's useful for simple scenarios or testing.
        /// </remarks>
        public static IApplicationBuilder UseMcpServer(this IApplicationBuilder app, Action<McpMiddlewareOptions> configureOptions)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(configureOptions);
#else
            if (app is null)
            {
                throw new ArgumentNullException(nameof(app));
            }
            if (configureOptions is null)
            {
                throw new ArgumentNullException(nameof(configureOptions));
            }
#endif

            // Configure options inline
            var options = new McpMiddlewareOptions();
            configureOptions(options);

            // Validate options
            var validationErrors = options.Validate().ToList();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException($"Invalid MCP middleware configuration: {string.Join(", ", validationErrors)}");
            }

            // Configure services if not already configured
            var services = app.ApplicationServices.GetService<IServiceCollection>();
            if (services is not null)
            {
                services.Configure<McpMiddlewareOptions>(opts =>
                {
                    opts.Enabled = options.Enabled;
                    opts.BasePath = options.BasePath;
                    opts.MetadataPath = options.MetadataPath;
                    opts.ServiceRootUrl = options.ServiceRootUrl;
                    opts.AutoDiscoverMetadata = options.AutoDiscoverMetadata;
                    opts.MetadataRefreshInterval = options.MetadataRefreshInterval;
                    opts.Authentication = options.Authentication;
                    opts.ToolGeneration = options.ToolGeneration;
                    opts.EnableCors = options.EnableCors;
                    opts.CorsPolicyName = options.CorsPolicyName;
                    opts.EnableCaching = options.EnableCaching;
                    opts.CacheDuration = options.CacheDuration;
                    opts.IncludeHealthChecks = options.IncludeHealthChecks;
                    opts.IncludeDetailedErrors = options.IncludeDetailedErrors;
                    opts.RequestTimeout = options.RequestTimeout;
                    opts.MaxRequestBodySize = options.MaxRequestBodySize;
                    opts.CustomHeaders = options.CustomHeaders;
                    opts.IntegrationMode = options.IntegrationMode;
                    opts.ExcludePaths = options.ExcludePaths;
                    opts.IncludePaths = options.IncludePaths;
                });
            }

            return app.UseMiddleware<McpServerMiddleware>();
        }

        /// <summary>
        /// Adds MCP server middleware to the application pipeline with development-friendly defaults.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        /// <remarks>
        /// This method configures the middleware with settings optimized for development environments,
        /// including detailed error information and relaxed security settings.
        /// </remarks>
        public static IApplicationBuilder UseMcpServerForDevelopment(this IApplicationBuilder app)
        {
            return app.UseMcpServer(options =>
            {
                var devOptions = McpMiddlewareOptions.Development;
                
                options.Enabled = devOptions.Enabled;
                options.BasePath = devOptions.BasePath;
                options.MetadataPath = devOptions.MetadataPath;
                options.AutoDiscoverMetadata = devOptions.AutoDiscoverMetadata;
                options.MetadataRefreshInterval = devOptions.MetadataRefreshInterval;
                options.Authentication = devOptions.Authentication;
                options.ToolGeneration = devOptions.ToolGeneration;
                options.EnableCors = devOptions.EnableCors;
                options.EnableCaching = devOptions.EnableCaching;
                options.CacheDuration = devOptions.CacheDuration;
                options.IncludeHealthChecks = devOptions.IncludeHealthChecks;
                options.IncludeDetailedErrors = devOptions.IncludeDetailedErrors;
                options.RequestTimeout = devOptions.RequestTimeout;
                options.MaxRequestBodySize = devOptions.MaxRequestBodySize;
                options.IntegrationMode = devOptions.IntegrationMode;
            });
        }

        /// <summary>
        /// Adds MCP server middleware to the application pipeline with production-optimized defaults.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        /// <remarks>
        /// This method configures the middleware with settings optimized for production environments,
        /// including enhanced security and performance optimizations.
        /// </remarks>
        public static IApplicationBuilder UseMcpServerForProduction(this IApplicationBuilder app)
        {
            return app.UseMcpServer(options =>
            {
                var prodOptions = McpMiddlewareOptions.Production;
                
                options.Enabled = prodOptions.Enabled;
                options.BasePath = prodOptions.BasePath;
                options.MetadataPath = prodOptions.MetadataPath;
                options.AutoDiscoverMetadata = prodOptions.AutoDiscoverMetadata;
                options.MetadataRefreshInterval = prodOptions.MetadataRefreshInterval;
                options.Authentication = prodOptions.Authentication;
                options.ToolGeneration = prodOptions.ToolGeneration;
                options.EnableCors = prodOptions.EnableCors;
                options.EnableCaching = prodOptions.EnableCaching;
                options.CacheDuration = prodOptions.CacheDuration;
                options.IncludeHealthChecks = prodOptions.IncludeHealthChecks;
                options.IncludeDetailedErrors = prodOptions.IncludeDetailedErrors;
                options.RequestTimeout = prodOptions.RequestTimeout;
                options.MaxRequestBodySize = prodOptions.MaxRequestBodySize;
                options.IntegrationMode = prodOptions.IntegrationMode;
            });
        }

        /// <summary>
        /// Adds MCP server middleware to the application pipeline at a specific base path.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="basePath">The base path for MCP endpoints.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="basePath"/> is null or whitespace.</exception>
        /// <remarks>
        /// This method allows specifying a custom base path for MCP endpoints while optionally
        /// configuring additional middleware options.
        /// </remarks>
        public static IApplicationBuilder UseMcpServerAt(this IApplicationBuilder app, string basePath, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(app);
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
#else
            if (app is null)
            {
                throw new ArgumentNullException(nameof(app));
            }
            if (string.IsNullOrWhiteSpace(basePath))
            {
                throw new ArgumentException("Base path cannot be null or whitespace.", nameof(basePath));
            }
#endif

            return app.UseMcpServer(options =>
            {
                options.BasePath = basePath.StartsWith('/') ? basePath : $"/{basePath}";
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Adds MCP server middleware that only processes specific paths.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="includePaths">Collection of path patterns to include for MCP processing.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> or <paramref name="includePaths"/> is null.</exception>
        /// <remarks>
        /// This method configures the middleware to only process requests matching the specified path patterns.
        /// This is useful for selective MCP integration in applications with mixed routing requirements.
        /// </remarks>
        public static IApplicationBuilder UseMcpServerForPaths(this IApplicationBuilder app, IEnumerable<string> includePaths, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(includePaths);
#else
            if (app is null)
            {
                throw new ArgumentNullException(nameof(app));
            }
            if (includePaths is null)
            {
                throw new ArgumentNullException(nameof(includePaths));
            }
#endif

            return app.UseMcpServer(options =>
            {
                options.IncludePaths = includePaths.ToList();
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Adds MCP server middleware that excludes specific paths from processing.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="excludePaths">Collection of path patterns to exclude from MCP processing.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> or <paramref name="excludePaths"/> is null.</exception>
        /// <remarks>
        /// This method configures the middleware to skip processing for requests matching the specified path patterns.
        /// This is useful for preserving existing application routes while adding MCP capabilities.
        /// </remarks>
        public static IApplicationBuilder UseMcpServerExcludingPaths(this IApplicationBuilder app, IEnumerable<string> excludePaths, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(excludePaths);
#else
            if (app is null)
            {
                throw new ArgumentNullException(nameof(app));
            }
            if (excludePaths is null)
            {
                throw new ArgumentNullException(nameof(excludePaths));
            }
#endif

            return app.UseMcpServer(options =>
            {
                options.ExcludePaths = excludePaths.ToList();
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Adds MCP server middleware with custom headers.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="customHeaders">Dictionary of custom headers to include in MCP responses.</param>
        /// <param name="configureOptions">Optional action to configure additional options.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> or <paramref name="customHeaders"/> is null.</exception>
        /// <remarks>
        /// This method allows adding custom headers to all MCP responses, which can be useful for
        /// branding, security headers, or operational metadata.
        /// </remarks>
        public static IApplicationBuilder UseMcpServerWithHeaders(this IApplicationBuilder app, IDictionary<string, string> customHeaders, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(customHeaders);
#else
            if (app is null)
            {
                throw new ArgumentNullException(nameof(app));
            }
            if (customHeaders is null)
            {
                throw new ArgumentNullException(nameof(customHeaders));
            }
#endif

            return app.UseMcpServer(options =>
            {
                options.CustomHeaders = new Dictionary<string, string>(customHeaders);
                configureOptions?.Invoke(options);
            });
        }

        /// <summary>
        /// Conditionally adds MCP server middleware based on a condition.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="configureOptions">Optional action to configure middleware options when condition is true.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        /// <remarks>
        /// This method allows conditional registration of MCP middleware based on runtime conditions
        /// such as environment settings, feature flags, or configuration values.
        /// </remarks>
        public static IApplicationBuilder UseMcpServerWhen(this IApplicationBuilder app, bool condition, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(app);
#else
            if (app is null)
            {
                throw new ArgumentNullException(nameof(app));
            }
#endif

            if (condition)
            {
                return configureOptions is not null ? app.UseMcpServer(configureOptions) : app.UseMcpServer();
            }

            return app;
        }

        /// <summary>
        /// Conditionally adds MCP server middleware based on a condition delegate.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="condition">The condition delegate to evaluate.</param>
        /// <param name="configureOptions">Optional action to configure middleware options when condition is true.</param>
        /// <returns>The application builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> or <paramref name="condition"/> is null.</exception>
        /// <remarks>
        /// This method allows conditional registration of MCP middleware based on a delegate that
        /// receives the application services for evaluation.
        /// </remarks>
        public static IApplicationBuilder UseMcpServerWhen(this IApplicationBuilder app, Func<IServiceProvider, bool> condition, Action<McpMiddlewareOptions>? configureOptions = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(condition);
#else
            if (app is null)
            {
                throw new ArgumentNullException(nameof(app));
            }
            if (condition is null)
            {
                throw new ArgumentNullException(nameof(condition));
            }
#endif

            if (condition(app.ApplicationServices))
            {
                return configureOptions is not null ? app.UseMcpServer(configureOptions) : app.UseMcpServer();
            }

            return app;
        }
    }
}
