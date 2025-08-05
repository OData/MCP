using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.AspNetCore.Extensions;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Sidecar.Extensions;
using Microsoft.OData.Mcp.Sidecar.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Sidecar
{
    /// <summary>
    /// Entry point for the OData MCP Server sidecar application.
    /// </summary>
    /// <remarks>
    /// The sidecar service provides a standalone ASP.NET Core application that can be deployed
    /// alongside an existing OData service to provide MCP (Model Context Protocol) capabilities.
    /// </remarks>
    public class Program
    {
        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Application exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            // Configure Serilog for early logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting OData MCP Server Sidecar");
                Log.Information("Version: {Version}", GetVersion());
                Log.Information("Framework: {Framework}", Environment.Version);
                Log.Information("Arguments: {Args}", string.Join(" ", args));

                var builder = CreateWebApplicationBuilder(args);
                var app = builder.Build();

                ConfigureApplication(app);

                await app.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                return 1;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        /// <summary>
        /// Creates and configures the web application builder.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Configured web application builder.</returns>
        private static WebApplicationBuilder CreateWebApplicationBuilder(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure configuration sources
            ConfigureConfiguration(builder, args);

            // Configure logging
            ConfigureLogging(builder);

            // Configure services
            ConfigureServices(builder);

            return builder;
        }

        /// <summary>
        /// Configures the configuration sources and options.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        /// <param name="args">Command line arguments.</param>
        private static void ConfigureConfiguration(WebApplicationBuilder builder, string[] args)
        {
            builder.Configuration
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("MCP_")
                .AddCommandLine(args);

            // Bind and validate configuration
            var mcpConfig = new McpServerConfiguration();
            builder.Configuration.GetSection("McpServer").Bind(mcpConfig);
            
            // Ensure sidecar deployment mode
            mcpConfig.DeploymentMode = McpDeploymentMode.Sidecar;
            
            builder.Services.Configure<McpServerConfiguration>(config =>
            {
                builder.Configuration.GetSection("McpServer").Bind(config);
                config.DeploymentMode = McpDeploymentMode.Sidecar;
            });

            Log.Information("Configuration loaded for {Environment} environment", builder.Environment.EnvironmentName);
            Log.Information("Deployment mode: {DeploymentMode}", mcpConfig.DeploymentMode);
            Log.Information("OData service URL: {ServiceUrl}", mcpConfig.ODataService.BaseUrl);
        }

        /// <summary>
        /// Configures logging for the application.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        private static void ConfigureLogging(WebApplicationBuilder builder)
        {
            // Clear default logging providers
            builder.Logging.ClearProviders();

            // Configure Serilog
            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "ODataMcpSidecar")
                    .Enrich.WithProperty("Version", GetVersion())
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                    .WriteTo.File("logs/sidecar-.log", 
                        rollingInterval: RollingInterval.Day,
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: 10 * 1024 * 1024); // 10MB
            });

            // Add Windows Event Log for Windows service deployment
            if (OperatingSystem.IsWindows())
            {
                builder.Logging.AddEventLog(settings =>
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        settings.SourceName = "OData MCP Server";
                        settings.LogName = "Application";
                    }
                });
            }
        }

        /// <summary>
        /// Configures services for dependency injection.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            var services = builder.Services;
            var configuration = builder.Configuration;

            // Add core MCP server services
            services.AddODataMcpServer(configuration);

            // Add sidecar-specific services
            services.AddSingleton<IServiceInformationService, ServiceInformationService>();
            services.AddSingleton<IConfigurationValidationService, ConfigurationValidationService>();
            services.AddHostedService<StartupValidationService>();
            services.AddHostedService<MetadataDiscoveryService>();

            // Add health checks
            services.AddHealthChecks()
                .AddCheck<ODataServiceHealthCheck>("odata-service")
                .AddCheck<McpServerHealthCheck>("mcp-server")
                .AddCheck<AuthenticationHealthCheck>("authentication");

            // Configure CORS
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    var mcpConfig = configuration.GetSection("McpServer").Get<McpServerConfiguration>() ?? new();
                    var corsConfig = mcpConfig.Network.Cors;

                    if (corsConfig.Enabled)
                    {
                        if (corsConfig.AllowedOrigins.Contains("*"))
                        {
                            policy.AllowAnyOrigin();
                        }
                        else
                        {
                            policy.WithOrigins(corsConfig.AllowedOrigins.ToArray());
                        }

                        policy.WithMethods(corsConfig.AllowedMethods.ToArray());

                        if (corsConfig.AllowedHeaders.Contains("*"))
                        {
                            policy.AllowAnyHeader();
                        }
                        else
                        {
                            policy.WithHeaders(corsConfig.AllowedHeaders.ToArray());
                        }

                        if (corsConfig.AllowCredentials)
                        {
                            policy.AllowCredentials();
                        }

                        policy.SetPreflightMaxAge(corsConfig.MaxAge);
                    }
                });
            });

            // Add Windows Service and systemd support
            if (OperatingSystem.IsWindows())
            {
                services.AddWindowsService(options =>
                {
                    options.ServiceName = "ODataMcpServer";
                });
            }

            if (OperatingSystem.IsLinux())
            {
                services.AddSystemd();
            }

            // Add OpenTelemetry if enabled
            var monitoringConfig = configuration.GetSection("McpServer:Monitoring").Get<MonitoringConfiguration>() ?? new();
            if (monitoringConfig.EnableTracing && monitoringConfig.OpenTelemetry.Enabled)
            {
                services.AddOpenTelemetry()
                    .WithTracing(tracing =>
                    {
                        tracing
                            .AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .SetSampler(new TraceIdRatioBasedSampler(monitoringConfig.TracingSamplingRate))
                            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                                .AddService(monitoringConfig.OpenTelemetry.ServiceName, monitoringConfig.OpenTelemetry.ServiceVersion));

                        if (!string.IsNullOrWhiteSpace(monitoringConfig.OpenTelemetry.OtlpEndpoint))
                        {
                            tracing.AddOtlpExporter(options =>
                            {
                                options.Endpoint = new Uri(monitoringConfig.OpenTelemetry.OtlpEndpoint);
                            });
                        }
                        else
                        {
                            tracing.AddConsoleExporter();
                        }
                    });
            }
        }

        /// <summary>
        /// Configures the application pipeline.
        /// </summary>
        /// <param name="app">The web application.</param>
        private static void ConfigureApplication(WebApplication app)
        {
            var configuration = app.Configuration;
            var mcpConfig = configuration.GetSection("McpServer").Get<McpServerConfiguration>() ?? new();

            // Configure exception handling
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                if (mcpConfig.Network.EnableHttps && mcpConfig.Security.RequireHttps)
                {
                    app.UseHsts();
                }
            }

            // Configure HTTPS redirection
            if (mcpConfig.Network.EnableHttps && mcpConfig.Security.RequireHttps)
            {
                app.UseHttpsRedirection();
            }

            // Add security headers
            app.UseSecurityHeaders(mcpConfig.Security.SecurityHeaders);

            // Add request logging
            if (mcpConfig.Monitoring.LogRequestResponse)
            {
                app.UseSerilogRequestLogging(options =>
                {
                    options.IncludeQueryInRequestPath = true;
                    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                    {
                        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
                        diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
                    };
                });
            }

            // Configure CORS
            if (mcpConfig.Network.Cors.Enabled)
            {
                app.UseCors();
            }

            // Add routing
            app.UseRouting();

            // Add authentication and authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // OData MCP server is configured through dependency injection

            // Add health checks
            if (mcpConfig.Monitoring.EnableHealthChecks)
            {
                app.MapHealthChecks("/health");
                app.MapHealthChecks("/health/ready", new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("ready")
                });
                app.MapHealthChecks("/health/live", new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("live")
                });
            }

            // Add service information endpoint
            app.MapGet("/info", (IServiceInformationService serviceInfo) => 
                Results.Ok(serviceInfo.GetServiceInformation()));

            Log.Information("Application pipeline configured successfully");
        }

        /// <summary>
        /// Gets the application version.
        /// </summary>
        /// <returns>The application version string.</returns>
        private static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                         ?? assembly.GetName().Version?.ToString()
                         ?? "1.0.0";
            return version;
        }
    }
}
