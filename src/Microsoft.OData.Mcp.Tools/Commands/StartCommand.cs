using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.OData.Mcp.Tools.Services;
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

                // Fetch and parse metadata before creating the host
                EdmModel? edmModel = null;
                List<McpServerTool>? dynamicTools = null;
                
                try
                {
                    if (Verbose)
                    {
                        Console.Error.WriteLine("Fetching OData metadata...");
                    }
                    
                    using var httpClient = new HttpClient();
                    if (!string.IsNullOrWhiteSpace(AuthToken))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
                    }
                    
                    var metadataUrl = $"{Url.TrimEnd('/')}/$metadata";
                    var response = await httpClient.GetAsync(metadataUrl);
                    response.EnsureSuccessStatusCode();
                    
                    var metadataXml = await response.Content.ReadAsStringAsync();
                    
                    // Parse the metadata
                    var parser = new CsdlParser(new NullLogger<CsdlParser>());
                    edmModel = parser.ParseFromString(metadataXml);
                    
                    if (Verbose)
                    {
                        Console.Error.WriteLine($"Successfully parsed metadata with {edmModel.EntityTypes.Count} entity types");
                    }
                    
                    // Generate tool definitions - create a temporary HttpClientFactory
                    var tempServices = new ServiceCollection();
                    tempServices.AddHttpClient();
                    var tempProvider = tempServices.BuildServiceProvider();
                    var httpClientFactory = tempProvider.GetRequiredService<IHttpClientFactory>();
                    
                    var toolFactory = new McpToolFactory(
                        logger: new NullLogger<McpToolFactory>(),
                        httpClientFactory: httpClientFactory
                    );
                    
                    var options = new McpToolGenerationOptions
                    {
                        GenerateCrudTools = true,
                        GenerateQueryTools = true,
                        GenerateNavigationTools = true,
                        IncludeExamples = true,
                        MaxToolCount = 200,
                        ToolVersion = "1.0.0"
                    };
                    
                    var toolDefinitions = await toolFactory.GenerateToolsAsync(edmModel, options);
                    
                    // Convert to McpServerTools
                    dynamicTools = new List<McpServerTool>();
                    foreach (var toolDef in toolDefinitions)
                    {
                        // Create a delegate for this specific tool
                        Func<Dictionary<string, object?>, CancellationToken, Task<string>> toolDelegate = 
                            async (parameters, ct) => 
                            {
                                // Create context for tool execution
                                var context = new McpToolContext()
                                {
                                    Model = edmModel,
                                    ServiceBaseUrl = Url,
                                    CancellationToken = ct
                                };
                                
                                // Add important properties from tool definition to context
                                if (!string.IsNullOrWhiteSpace(toolDef.TargetEntitySet))
                                {
                                    context.SetProperty("TargetEntitySet", toolDef.TargetEntitySet);
                                }
                                
                                if (!string.IsNullOrWhiteSpace(toolDef.TargetEntityType))
                                {
                                    context.SetProperty("TargetEntityType", toolDef.TargetEntityType);
                                }
                                
                                // Add metadata from tool definition
                                if (toolDef.Metadata != null)
                                {
                                    foreach (var kvp in toolDef.Metadata)
                                    {
                                        context.SetProperty(kvp.Key, kvp.Value);
                                    }
                                }
                                
                                // Execute the tool handler
                                var jsonParams = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
                                var result = await toolDef.Handler(context, jsonParams);
                                
                                // Return as JSON string
                                if (result.IsSuccess && result.Data != null)
                                {
                                    if (result.Data is JsonDocument jsonDoc)
                                    {
                                        return jsonDoc.RootElement.GetRawText();
                                    }
                                    return JsonSerializer.Serialize(result.Data);
                                }
                                else
                                {
                                    var errorResponse = new { error = result.ErrorMessage ?? "Operation failed", errorCode = result.ErrorCode };
                                    return JsonSerializer.Serialize(errorResponse);
                                }
                            };
                        
                        // Create McpServerTool from the delegate
                        var mcpTool = McpServerTool.Create(
                            toolDelegate,
                            new McpServerToolCreateOptions
                            {
                                Name = toolDef.Name,
                                Description = toolDef.Description,
                                ReadOnly = toolDef.OperationType == McpToolOperationType.Read,
                                Idempotent = toolDef.OperationType == McpToolOperationType.Read || 
                                            toolDef.OperationType == McpToolOperationType.Delete
                            }
                        );
                        
                        dynamicTools.Add(mcpTool);
                    }
                    
                    if (Verbose)
                    {
                        Console.Error.WriteLine($"Generated {dynamicTools.Count} dynamic MCP tools");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: Could not generate dynamic tools: {ex.Message}");
                    if (Verbose)
                    {
                        Console.Error.WriteLine(ex.ToString());
                    }
                    Console.Error.WriteLine("Continuing with static tools only...");
                }

                // Create cancellation token source for graceful shutdown
                var cts = new CancellationTokenSource();
                
                // Add shutdown tool to dynamic tools if we have a list
                if (dynamicTools == null)
                {
                    dynamicTools = new List<McpServerTool>();
                }
                
                // Create shutdown tool
                Func<Dictionary<string, object?>, CancellationToken, Task<string>> shutdownDelegate = 
                    (parameters, ct) => 
                    {
                        var reason = parameters.GetValueOrDefault("reason")?.ToString() ?? "User requested shutdown";
                        var delaySecondsObj = parameters.GetValueOrDefault("delay_seconds");
                        var delaySeconds = delaySecondsObj != null ? Convert.ToInt32(delaySecondsObj) : 2;
                        
                        // Validate delay
                        if (delaySeconds < 0 || delaySeconds > 10)
                        {
                            return Task.FromResult(JsonSerializer.Serialize(new { error = "Delay must be between 0 and 10 seconds" }));
                        }
                        
                        if (Verbose)
                        {
                            Console.Error.WriteLine($"Shutdown requested: {reason}");
                        }
                        
                        // Schedule cancellation after delay to allow response to be sent
                        _ = Task.Run(async () => 
                        {
                            if (delaySeconds > 0)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                            }
                            if (Verbose)
                            {
                                Console.Error.WriteLine("Initiating server shutdown...");
                            }
                            cts.Cancel();
                        });
                        
                        return Task.FromResult(JsonSerializer.Serialize(new 
                        { 
                            message = $"Server shutdown initiated. Reason: {reason}. Shutting down in {delaySeconds} second(s)." 
                        }));
                    };
                
                var shutdownTool = McpServerTool.Create(
                    shutdownDelegate,
                    new McpServerToolCreateOptions
                    {
                        Name = "shutdown_server",
                        Description = "Gracefully shuts down the OData MCP server",
                        ReadOnly = false,
                        Idempotent = false
                    }
                );
                
                dynamicTools.Add(shutdownTool);
                
                if (Verbose)
                {
                    Console.Error.WriteLine("Added shutdown_server tool to available tools");
                }

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
                        
                        // Set log levels based on verbosity
                        if (Verbose)
                        {
                            logging.SetMinimumLevel(LogLevel.Debug);
                        }
                        else
                        {
                            logging.SetMinimumLevel(LogLevel.Information);
                            // Suppress noisy MCP SDK logs unless verbose
                            logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
                        }
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // Register OData services
                        services.AddODataMcpCore(configuration);
                        
                        // Register the parsed model as singleton to avoid re-fetching
                        if (edmModel != null)
                        {
                            services.AddSingleton(edmModel);
                        }
                        
                        // Configure MCP server with dynamic or static tools
                        var builder = services.AddMcpServer();
                        
                        if (dynamicTools != null && dynamicTools.Count > 0)
                        {
                            // Register dynamic tools
                            builder.WithTools(dynamicTools);
                            Console.Error.WriteLine($"Registered {dynamicTools.Count} dynamic tools with MCP server");
                        }
                        
                        // Also register static tools
                        builder.WithODataTools()
                               .WithStdioServerTransport();
                        
                        // Add dynamic tool generation service for logging
                        // It will use the injected EdmModel if available
                        services.AddHostedService<DynamicToolGeneratorService>();
                    })
                    .Build();

                // Handle Ctrl+C gracefully
                Console.CancelKeyPress += (sender, e) =>
                {
                    if (Verbose)
                    {
                        Console.Error.WriteLine("Received interrupt signal, shutting down gracefully...");
                    }
                    cts.Cancel();
                    e.Cancel = true; // Prevent immediate termination
                };
                
                await host.RunAsync(cts.Token);
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