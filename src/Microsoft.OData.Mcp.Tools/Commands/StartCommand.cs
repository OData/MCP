using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Tools.Server;
// using ModelContextProtocol; // Will be added when SDK integration is complete

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// Command to start the OData MCP server.
    /// </summary>
    [Command(Name = "start", Description = "Start the OData MCP server")]
    public class StartCommand
    {

        /// <summary>
        /// Gets or sets the OData service URL.
        /// </summary>
        [Argument(0, "OData service URL")]
        [Required]
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the server port (0 for STDIO mode).
        /// </summary>
        [Option("-p|--port", Description = "Server port (0 for STDIO mode, default)")]
        public int Port { get; set; } = 0;

        /// <summary>
        /// Gets or sets the authentication token.
        /// </summary>
        [Option("-t|--auth-token", Description = "Authentication token for the OData service")]
        public string? AuthToken { get; set; }

        /// <summary>
        /// Gets or sets the configuration file path.
        /// </summary>
        [Option("-c|--config", Description = "Configuration file path")]
        public string? ConfigFile { get; set; }

        /// <summary>
        /// Gets or sets whether to enable verbose logging.
        /// </summary>
        [Option("-v|--verbose", Description = "Enable verbose logging")]
        public bool Verbose { get; set; }

        internal IHost? _host;

        /// <summary>
        /// Executes the start command.
        /// </summary>
        /// <returns>Exit code.</returns>
        public async Task<int> OnExecuteAsync()
        {
            try
            {
                // Validate URL
                if (string.IsNullOrWhiteSpace(Url))
                {
                    Console.Error.WriteLine("Error: OData service URL is required.");
                    return 1;
                }

                if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    Console.Error.WriteLine($"Error: Invalid URL: {Url}");
                    return 1;
                }

                // Build configuration
                var configBuilder = new ConfigurationBuilder();

                if (!string.IsNullOrWhiteSpace(ConfigFile))
                {
                    configBuilder.AddJsonFile(ConfigFile, optional: false, reloadOnChange: true);
                }

                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["McpServer:ODataService:BaseUrl"] = Url,
                    ["McpServer:ODataService:AuthToken"] = AuthToken,
                    ["McpServer:ServerPort"] = Port.ToString(),
                    ["Logging:LogLevel:Default"] = Verbose ? "Debug" : "Information"
                });

                var configuration = configBuilder.Build();

                // Run the appropriate server mode
                if (Port == 0)
                {
                    //RWM: This is the default mode.
                    await RunSdkStdioServerAsync(configuration);
                }
                else
                {
                    // HTTP mode - future implementation
                    Console.Error.WriteLine("HTTP mode not yet implemented. Use port 0 for STDIO mode.");
                    return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                if (Verbose)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
                return 1;
            }
        }

        /// <summary>
        /// Runs the server using the MCP SDK implementation.
        /// </summary>
        internal async Task RunSdkStdioServerAsync(IConfiguration configuration)
        {
            var builder = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    config.AddConfiguration(configuration);
                })
                .ConfigureServices((context, services) =>
                {
                    // Configure MCP server services
                    McpServerConfiguration.ConfigureMcpServices(services, context.Configuration, Url!, AuthToken);

                    // Add MCP SDK server with OData tools
                    services.AddODataMcpServer(Url!, AuthToken);

                    // Use STDIO transport
                    services.WithStdioTransport();
                });

            _host = builder.Build();

            var logger = _host.Services.GetRequiredService<ILogger<StartCommand>>();
            logger.LogInformation("Starting OData MCP Server with SDK implementation (STDIO mode)");

            await _host.RunAsync();
        }

    }

}
