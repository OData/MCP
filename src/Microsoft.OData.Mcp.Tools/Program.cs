using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tools.Server;

namespace Microsoft.OData.Mcp.Tools
{
    /// <summary>
    /// Entry point for the OData MCP Tools application.
    /// </summary>
    /// <remarks>
    /// This console application provides a standalone MCP server that can connect
    /// to external OData APIs and expose them through the Model Context Protocol.
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
            var rootCommand = new RootCommand("OData MCP Server - Connect to external OData APIs via Model Context Protocol");

            var startCommand = new Command("start", "Start an MCP server for an external OData API");
            
            var urlArgument = new Argument<string>(
                name: "url",
                description: "The OData metadata URL (e.g., https://api.example.com/odata/$metadata)");
            
            var portOption = new Option<int>(
                aliases: new[] { "--port", "-p" },
                description: "The port to run the MCP server on (0 or omit for STDIO mode)",
                getDefaultValue: () => 0);
            
            var authTokenOption = new Option<string?>(
                aliases: new[] { "--auth-token", "-t" },
                description: "Authentication token for the OData service");
            
            var configOption = new Option<string?>(
                aliases: new[] { "--config", "-c" },
                description: "Path to configuration file");
            
            var verboseOption = new Option<bool>(
                aliases: new[] { "--verbose", "-v" },
                description: "Enable verbose logging");

            startCommand.AddArgument(urlArgument);
            startCommand.AddOption(portOption);
            startCommand.AddOption(authTokenOption);
            startCommand.AddOption(configOption);
            startCommand.AddOption(verboseOption);

            startCommand.SetHandler(async (url, port, authToken, configFile, verbose) =>
            {
                try
                {
                    var builder = Host.CreateApplicationBuilder();
                    
                    // Configure logging (but suppress console output in STDIO mode)
                    if (port == 0)
                    {
                        // In STDIO mode, only log to debug/trace to avoid interfering with protocol
                        builder.Logging.ClearProviders();
                        if (verbose)
                        {
                            builder.Logging.AddDebug();
                        }
                    }
                    else
                    {
                        McpServerConfiguration.ConfigureLogging(builder.Logging, verbose);
                    }

                    // Load configuration
                    if (!string.IsNullOrWhiteSpace(configFile))
                    {
                        builder.Configuration.AddJsonFile(configFile, optional: false, reloadOnChange: true);
                    }

                    // Configure MCP services using the shared configuration
                    McpServerConfiguration.ConfigureMcpServices(
                        builder.Services,
                        builder.Configuration,
                        url,
                        authToken);

                    var host = builder.Build();
                    
                    if (port == 0)
                    {
                        // STDIO mode - default for MCP protocol
                        var stdioHost = new StdioMcpHost(host.Services, url, authToken);
                        
                        // Use cancellation token to handle Ctrl+C gracefully
                        using var cts = new CancellationTokenSource();
                        Console.CancelKeyPress += (sender, e) =>
                        {
                            e.Cancel = true;
                            cts.Cancel();
                        };
                        
                        await stdioHost.RunAsync(cts.Token);
                    }
                    else
                    {
                        // HTTP mode - optional for debugging
                        System.Console.WriteLine("Starting OData MCP Server");
                        System.Console.WriteLine($"OData Service URL: {url}");
                        System.Console.WriteLine($"MCP Server Port: {port}");
                        
                        var logger = host.Services.GetRequiredService<ILogger<Program>>();
                        
                        logger.LogInformation("MCP Server is starting...");
                        logger.LogInformation("OData Service: {Url}", url);
                        logger.LogInformation("Listening on port: {Port}", port);
                        
                        if (!string.IsNullOrWhiteSpace(authToken))
                        {
                            logger.LogInformation("Using authentication token");
                        }

                        System.Console.WriteLine();
                        System.Console.WriteLine($"MCP Server is running at http://localhost:{port}");
                        System.Console.WriteLine($"Connect your AI assistant to: http://localhost:{port}/mcp");
                        System.Console.WriteLine("Press Ctrl+C to stop the server");
                        System.Console.WriteLine();

                        await host.RunAsync();
                    }
                }
                catch (Exception ex)
                {
                    // In STDIO mode, errors should go to stderr
                    System.Console.Error.WriteLine($"Application terminated unexpectedly: {ex.Message}");
                    if (verbose)
                    {
                        System.Console.Error.WriteLine(ex.ToString());
                    }
                    Environment.Exit(1);
                }
            }, urlArgument, portOption, authTokenOption, configOption, verboseOption);

            // Add test command for development
            var testCommand = new Command("test", "Test OData metadata parsing");
            var testUrlArgument = new Argument<string>(
                name: "url",
                description: "The OData metadata URL to test");
            
            testCommand.AddArgument(testUrlArgument);
            
            testCommand.SetHandler(async (url) =>
            {
                try
                {
                    System.Console.WriteLine($"Testing OData metadata from: {url}");
                    System.Console.WriteLine();
                    
                    using var httpClient = new System.Net.Http.HttpClient();
                    var response = await httpClient.GetStringAsync(url);
                    
                    var parser = new CsdlParser();
                    var model = parser.ParseFromString(response);
                    
                    System.Console.WriteLine($"Successfully parsed metadata:");
                    System.Console.WriteLine($"  Entity Types: {model.EntityTypes.Count}");
                    System.Console.WriteLine($"  Complex Types: {model.ComplexTypes.Count}");
                    System.Console.WriteLine($"  Functions: {model.Functions.Count}");
                    System.Console.WriteLine($"  Actions: {model.Actions.Count}");
                    System.Console.WriteLine($"  Containers: {model.EntityContainers.Count}");
                    
                    if (model.EntityContainers.Count > 0 && model.EntityContainers[0] != null)
                    {
                        var container = model.EntityContainers[0];
                        System.Console.WriteLine($"  Entity Sets: {container.EntitySets.Count}");
                        System.Console.WriteLine($"  Singletons: {container.Singletons.Count}");
                    }
                    
                    System.Console.WriteLine();
                    System.Console.WriteLine("Entity Types:");
                    foreach (var entityType in model.EntityTypes)
                    {
                        System.Console.WriteLine($"  - {entityType.Name} ({entityType.Properties.Count} properties)");
                    }
                }
                catch (Exception ex)
                {
                    System.Console.Error.WriteLine($"Failed to parse metadata: {ex.Message}");
                    Environment.Exit(1);
                }
            }, testUrlArgument);

            rootCommand.AddCommand(startCommand);
            rootCommand.AddCommand(testCommand);

            // Add version command
            var versionCommand = new Command("version", "Show version information");
            versionCommand.SetHandler(() =>
            {
                var assembly = typeof(Program).Assembly;
                var version = assembly.GetName().Version ?? new Version(1, 0, 0);
                System.Console.WriteLine($"OData MCP Tools v{version}");
            });
            
            rootCommand.AddCommand(versionCommand);

            return await rootCommand.InvokeAsync(args);
        }
    }
}