using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Microsoft.OData.Mcp.Core.Extensions;

namespace Microsoft.OData.Mcp.Console
{
    /// <summary>
    /// Console application demonstrating the OData MCP Server.
    /// </summary>
    /// <remarks>
    /// This application creates an MCP server that provides tools for interacting with OData services.
    /// It uses the official Model Context Protocol C# SDK for communication.
    /// </remarks>
    public class Program
    {
        /// <summary>
        /// Main entry point for the console application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Application exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                System.Console.WriteLine("Starting OData MCP Server Console Demo...");

                var builder = Host.CreateApplicationBuilder(args);

                // Configure services
                ConfigureServices(builder);

                // Build the host
                var host = builder.Build();

                // Display server information
                await DisplayServerInfoAsync(host);

                // Run the MCP server
                await host.RunAsync();

                return 0;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Application failed: {ex}");
                return 1;
            }
        }

        /// <summary>
        /// Configures services for the application.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        private static void ConfigureServices(HostApplicationBuilder builder)
        {
            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            // Add configuration sources (args are already configured by Host.CreateApplicationBuilder)
            builder.Configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("MCP_");

            // Add OData MCP Server core services
            builder.Services.AddODataMcpServerCore(builder.Configuration);

            // Add the official MCP server with OData tools
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithODataTools();
        }

        /// <summary>
        /// Displays server information before starting.
        /// </summary>
        /// <param name="host">The configured host.</param>
        private static async Task DisplayServerInfoAsync(IHost host)
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation("=== OData MCP Server Console Demo ===");
            logger.LogInformation("Server: OData MCP Server");
            logger.LogInformation("Version: 1.0.0");
            logger.LogInformation("Protocol: Model Context Protocol (MCP)");
            logger.LogInformation("Transport: STDIO");
            logger.LogInformation("=====================================");

            // Log available tools
            try
            {
                // Note: In a real scenario, tools would be discovered from the OData metadata
                logger.LogInformation("Available Tools:");
                logger.LogInformation("  - QueryEntitySet: Query OData entity sets with filtering and pagination");
                logger.LogInformation("  - GetEntity: Get a single entity by key");
                logger.LogInformation("  - CreateEntity: Create new entities");
                logger.LogInformation("  - GetMetadata: Get OData service metadata");
                logger.LogInformation("=====================================");

                await Task.Delay(100); // Small delay for clean output
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not enumerate available tools");
            }
        }
    }
}