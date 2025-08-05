using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.HealthChecks;
using Microsoft.OData.Mcp.Authentication.Models;
using Microsoft.OData.Mcp.Authentication.Services;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.OData.Mcp.AspNetCore.Extensions
{
    /// <summary>
    /// Extension methods for configuring OData MCP server services in the dependency injection container.
    /// </summary>
    /// <remarks>
    /// These extensions provide a fluent API for configuring all aspects of the OData MCP server,
    /// including authentication, metadata parsing, and MCP tool generation.
    /// </remarks>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the OData MCP server services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration to bind settings from.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
        public static IServiceCollection AddODataMcpServer(this IServiceCollection services, IConfiguration configuration)
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

            // Configure authentication options
            services.Configure<McpAuthenticationOptions>(configuration.GetSection("Authentication"));

            // Add core services
            services.AddSingleton<CsdlParser>();
            services.AddScoped<ITokenValidationService, TokenValidationService>();
            services.AddScoped<IMcpToolFactory, McpToolFactory>();

            // Add HTTP client for token delegation
            services.AddHttpClient("TokenDelegation", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            return services;
        }

        /// <summary>
        /// Adds the OData MCP server services with custom configuration.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configureOptions">Action to configure the authentication options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is null.</exception>
        public static IServiceCollection AddODataMcpServer(this IServiceCollection services, Action<McpAuthenticationOptions> configureOptions)
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

            // Configure authentication options
            services.Configure(configureOptions);

            // Add core services
            services.AddSingleton<CsdlParser>();
            services.AddScoped<ITokenValidationService, TokenValidationService>();
            services.AddScoped<IMcpToolFactory, McpToolFactory>();

            // Add HTTP client for token delegation
            services.AddHttpClient("TokenDelegation", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            return services;
        }

        /// <summary>
        /// Adds JWT bearer authentication for the OData MCP server.
        /// </summary>
        /// <param name="services">The service collection to add authentication to.</param>
        /// <param name="configuration">The configuration to bind JWT settings from.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
        public static IServiceCollection AddODataMcpAuthentication(this IServiceCollection services, IConfiguration configuration)
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

            var authOptions = new McpAuthenticationOptions();
            configuration.GetSection("Authentication").Bind(authOptions);

            return AddODataMcpAuthentication(services, authOptions);
        }

        /// <summary>
        /// Adds JWT bearer authentication for the OData MCP server with custom options.
        /// </summary>
        /// <param name="services">The service collection to add authentication to.</param>
        /// <param name="authOptions">The authentication options to use.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="authOptions"/> is null.</exception>
        public static IServiceCollection AddODataMcpAuthentication(this IServiceCollection services, McpAuthenticationOptions authOptions)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(authOptions);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (authOptions is null)
            {
                throw new ArgumentNullException(nameof(authOptions));
            }
#endif

            if (!authOptions.Enabled)
            {
                return services;
            }

            // Validate authentication options
            var validationErrors = authOptions.Validate();
            if (validationErrors.Any())
            {
                throw new InvalidOperationException($"Invalid authentication configuration: {string.Join(", ", validationErrors)}");
            }

            services.AddAuthentication(defaultScheme: authOptions.Scheme)
                .AddJwtBearer(authOptions.Scheme, options =>
                {
                    ConfigureJwtBearerOptions(options, authOptions.JwtBearer);
                });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = options.DefaultPolicy;
                
                // Add scope-based policies if configured
                if (authOptions.ScopeAuthorization.Enabled)
                {
                    ConfigureScopePolicies(options, authOptions.ScopeAuthorization);
                }
            });

            return services;
        }

        /// <summary>
        /// Adds CORS support for the OData MCP server.
        /// </summary>
        /// <param name="services">The service collection to add CORS to.</param>
        /// <param name="policyName">The name of the CORS policy.</param>
        /// <param name="configurePolicy">Action to configure the CORS policy.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="policyName"/> is null or whitespace.</exception>
        public static IServiceCollection AddODataMcpCors(this IServiceCollection services, string policyName, Action<Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder>? configurePolicy = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (string.IsNullOrWhiteSpace(policyName))
            {
                throw new ArgumentException("Policy name cannot be null or whitespace.", nameof(policyName));
            }
#endif

            services.AddCors(options =>
            {
                options.AddPolicy(policyName, builder =>
                {
                    if (configurePolicy is not null)
                    {
                        configurePolicy(builder);
                    }
                    else
                    {
                        // Default CORS policy for MCP servers
                        builder.AllowAnyOrigin()
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                    }
                });
            });

            return services;
        }

        /// <summary>
        /// Adds health checks for the OData MCP server.
        /// </summary>
        /// <param name="services">The service collection to add health checks to.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddODataMcpHealthChecks(this IServiceCollection services)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(services);
#else
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
#endif

            services.AddHealthChecks()
                .AddCheck<McpServerHealthCheck>("mcp_server")
                .AddCheck<AuthenticationHealthCheck>("authentication");

            return services;
        }

        #region Private Methods

        /// <summary>
        /// Configures JWT bearer options from MCP authentication settings.
        /// </summary>
        /// <param name="jwtOptions">The JWT bearer options to configure.</param>
        /// <param name="mcpJwtOptions">The MCP JWT options.</param>
        private static void ConfigureJwtBearerOptions(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions jwtOptions, Microsoft.OData.Mcp.Authentication.Models.JwtBearerOptions mcpJwtOptions)
        {
            if (!string.IsNullOrWhiteSpace(mcpJwtOptions.Authority))
                jwtOptions.Authority = mcpJwtOptions.Authority;
            if (!string.IsNullOrWhiteSpace(mcpJwtOptions.Audience))
                jwtOptions.Audience = mcpJwtOptions.Audience;
            if (!string.IsNullOrWhiteSpace(mcpJwtOptions.MetadataAddress))
                jwtOptions.MetadataAddress = mcpJwtOptions.MetadataAddress;
            jwtOptions.RequireHttpsMetadata = mcpJwtOptions.RequireHttpsMetadata;

            jwtOptions.TokenValidationParameters.ValidateIssuer = mcpJwtOptions.ValidateIssuer;
            jwtOptions.TokenValidationParameters.ValidateAudience = mcpJwtOptions.ValidateAudience;
            jwtOptions.TokenValidationParameters.ValidateLifetime = mcpJwtOptions.ValidateLifetime;
            jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey = mcpJwtOptions.ValidateIssuerSigningKey;
            jwtOptions.TokenValidationParameters.ClockSkew = mcpJwtOptions.ClockSkew;

            if (!string.IsNullOrWhiteSpace(mcpJwtOptions.Issuer))
            {
                jwtOptions.TokenValidationParameters.ValidIssuer = mcpJwtOptions.Issuer;
            }

            if (!string.IsNullOrWhiteSpace(mcpJwtOptions.Audience))
            {
                jwtOptions.TokenValidationParameters.ValidAudience = mcpJwtOptions.Audience;
            }

            // Configure events for logging and custom processing
            jwtOptions.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogWarning(context.Exception, "JWT authentication failed for request {RequestPath}", context.Request.Path);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    var subject = context.Principal?.Identity?.Name ?? "Unknown";
                    logger.LogDebug("JWT token validated successfully for user: {Subject}", subject);
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogDebug("JWT authentication challenge issued for request {RequestPath}", context.Request.Path);
                    return Task.CompletedTask;
                }
            };
        }

        /// <summary>
        /// Configures authorization policies based on scope requirements.
        /// </summary>
        /// <param name="options">The authorization options to configure.</param>
        /// <param name="scopeOptions">The scope authorization options.</param>
        private static void ConfigureScopePolicies(Microsoft.AspNetCore.Authorization.AuthorizationOptions options, ScopeAuthorizationOptions scopeOptions)
        {
            // Create policies for each operation type
            foreach (var kvp in scopeOptions.RequiredScopes)
            {
                var operationName = kvp.Key;
                var requiredScopes = kvp.Value;

                options.AddPolicy($"Scope_{operationName}", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(scopeOptions.ScopeClaimName, requiredScopes.ToArray());
                });
            }

            // Create policies for specific tools
            foreach (var kvp in scopeOptions.ToolScopes)
            {
                var toolName = kvp.Key;
                var requiredScopes = kvp.Value;

                options.AddPolicy($"Tool_{toolName}", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(scopeOptions.ScopeClaimName, requiredScopes.ToArray());
                });
            }

            // Create policies for entity operations
            foreach (var kvp in scopeOptions.EntityScopes)
            {
                var entityType = kvp.Key;
                var entityRequirements = kvp.Value;

                var operations = new[] { "read", "create", "update", "delete", "query", "navigate" };
                foreach (var operation in operations)
                {
                    var requiredScopes = entityRequirements.GetScopesForOperation(operation).ToList();
                    if (requiredScopes.Count > 0)
                    {
                        options.AddPolicy($"Entity_{entityType}_{operation}", policy =>
                        {
                            policy.RequireAuthenticatedUser();
                            policy.RequireClaim(scopeOptions.ScopeClaimName, requiredScopes.ToArray());
                        });
                    }
                }
            }
        }

        #endregion
    }
}