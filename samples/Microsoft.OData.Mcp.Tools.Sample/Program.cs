using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Parsing;

namespace Microsoft.OData.Mcp.Tools.Sample
{
    /// <summary>
    /// Sample application demonstrating the standalone OData MCP Server connecting to external OData APIs.
    /// </summary>
    /// <remarks>
    /// This sample shows how to use the console-based MCP server to connect to any OData API
    /// and expose it through the Model Context Protocol for AI assistants like Claude.
    /// 
    /// Usage:
    /// - Quick start: dotnet run https://services.odata.org/V4/Northwind/Northwind.svc/$metadata
    /// - Configured mode: dotnet run (uses appsettings.json)
    /// - Command line: dotnet odata-mcp start "https://your-api.com/odata/$metadata"
    /// </remarks>
    public class Program
    {
        /// <summary>
        /// Main entry point for the console application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            // This sample demonstrates different ways to use the standalone MCP server:
            // 1. Run as a background service connecting to a configured OData API
            // 2. Start interactively with a specific OData API URL
            // 3. Use the command-line interface to explore OData APIs

            if (args.Length > 0 && args[0].StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // Quick start mode: dotnet run https://services.odata.org/V4/Northwind/Northwind.svc/$metadata
                return await RunInteractiveMode(args[0]);
            }
            else
            {
                // Host mode: Run as a configured service
                return await RunHostMode(args);
            }
        }

        private static async Task<int> RunInteractiveMode(string metadataUrl)
        {
            System.Console.WriteLine($"Starting OData MCP Server for: {metadataUrl}");
            System.Console.WriteLine();

            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Add core MCP services
                    // services.AddODataMcpConsole(); // TODO: Implement this extension method
                    
                    // Configure the metadata URL
                    services.Configure<Microsoft.OData.Mcp.Tools.Configuration.McpMiddlewareOptions>(options =>
                    {
                        options.ServiceRootUrl = metadataUrl.Replace("/$metadata", "");
                        options.MetadataPath = "/$metadata";
                        options.AutoDiscoverMetadata = true;
                        options.EnableCaching = true;
                        options.CacheDuration = TimeSpan.FromHours(1);
                    });

                    // Add the hosted service
                    // services.AddHostedService<McpServerHostedService>(); // TODO: Implement when service is ready
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                });

            try
            {
                var host = hostBuilder.Build();
                
                // Display connection information
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("OData MCP Server is starting...");
                logger.LogInformation("Metadata URL: {MetadataUrl}", metadataUrl);
                logger.LogInformation("Press Ctrl+C to stop the server");
                
                await host.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static async Task<int> RunHostMode(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                          .AddEnvironmentVariables()
                          .AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    // Add core MCP services
                    // services.AddODataMcpConsole(); // TODO: Implement this extension method
                    
                    // Configure from appsettings.json
                    services.Configure<Microsoft.OData.Mcp.Tools.Configuration.McpMiddlewareOptions>(
                        context.Configuration.GetSection("McpServer"));

                    // Add the hosted service
                    // services.AddHostedService<McpServerHostedService>(); // TODO: Implement when service is ready
                    
                    // Optional: Add health checks
                    services.AddHealthChecks();
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    
                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        logging.AddDebug();
                    }
                });

            try
            {
                var host = hostBuilder.Build();
                
                // Display startup information
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                
                System.Console.WriteLine();
                System.Console.WriteLine("=== OData MCP Server ===");
                System.Console.WriteLine($"Configuration loaded from: appsettings.json");
                System.Console.WriteLine();
                System.Console.WriteLine("Press Ctrl+C to stop the server");
                System.Console.WriteLine();
                
                await host.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Fatal error: {ex.Message}");
                if (args.Length > 0 && args[0] == "--debug")
                {
                    System.Console.Error.WriteLine(ex.ToString());
                }
                return 1;
            }
        }
    }

    /// <summary>
    /// Background service that runs the MCP server.
    /// </summary>
    public class McpServerHostedService : BackgroundService
    {
        private readonly ILogger<McpServerHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public McpServerHostedService(
            ILogger<McpServerHostedService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MCP Server hosted service is starting");

            // The actual MCP server runs in the middleware pipeline
            // This hosted service just keeps the application running
            
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }

            _logger.LogInformation("MCP Server hosted service is stopping");
        }
    }
}