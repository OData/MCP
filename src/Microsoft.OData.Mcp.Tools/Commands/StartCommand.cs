using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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
        /// Gets or sets the authentication token.
        /// </summary>
        [Option("-t|--auth-token", Description = "Authentication token for the OData service")]
        public string? AuthToken { get; set; }

        /// <summary>
        /// Gets or sets whether to enable verbose logging.
        /// </summary>
        [Option("-v|--verbose", Description = "Enable verbose logging")]
        public bool Verbose { get; set; }

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
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["McpServer:ODataService:BaseUrl"] = Url,
                        ["McpServer:ODataService:Authentication:BearerToken"] = AuthToken,
                        ["Logging:LogLevel:Default"] = Verbose ? "Debug" : "Information"
                    })
                    .Build();

                // Create and run host
                var host = Host.CreateDefaultBuilder()
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        // MCP protocol requires all logs go to stderr
                        logging.AddConsole(options =>
                        {
                            options.LogToStandardErrorThreshold = LogLevel.Trace;
                        });
                        if (Verbose)
                        {
                            logging.SetMinimumLevel(LogLevel.Debug);
                        }
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // Register OData services
                        services.AddODataMcpCore(configuration);
                        
                        // Configure MCP server with STDIO transport
                        services
                            .AddMcpServer()
                            .WithODataTools()  // Uses Core's extension method
                            .WithStdioServerTransport();
                    })
                    .Build();

                await host.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (Verbose)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
                return 1;
            }
        }

    }

}