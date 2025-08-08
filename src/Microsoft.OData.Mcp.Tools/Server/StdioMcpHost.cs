using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Server;

namespace Microsoft.OData.Mcp.Tools.Server
{
    /// <summary>
    /// MCP server host that communicates via STDIO (stdin/stdout).
    /// </summary>
    public class StdioMcpHost
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _odataUrl;
        private readonly string? _authToken;
        private readonly ILogger<StdioMcpHost> _logger;
        private readonly TextReader _input;
        private readonly TextWriter _output;
        private readonly ODataMcpTools _odataTools;
        private readonly DynamicODataMcpTools _dynamicTools;

        /// <summary>
        /// Initializes a new instance of the <see cref="StdioMcpHost"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider with configured services.</param>
        /// <param name="odataUrl">The OData service URL to connect to.</param>
        /// <param name="authToken">Optional authentication token.</param>
        public StdioMcpHost(IServiceProvider serviceProvider, string odataUrl, string? authToken = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _odataUrl = odataUrl ?? throw new ArgumentNullException(nameof(odataUrl));
            _authToken = authToken;
            _logger = serviceProvider.GetRequiredService<ILogger<StdioMcpHost>>();
            _input = Console.In;
            _output = Console.Out;
            
            // Get the tool instances
            _odataTools = serviceProvider.GetRequiredService<ODataMcpTools>();
            _dynamicTools = serviceProvider.GetRequiredService<DynamicODataMcpTools>();
        }

        /// <summary>
        /// Runs the MCP server using STDIO transport.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting MCP server in STDIO mode");
            _logger.LogInformation("OData Service: {Url}", _odataUrl);

            try
            {
                // Main message loop
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await _input.ReadLineAsync();
                    if (line == null)
                    {
                        // End of input stream
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        // Parse the JSON-RPC request
                        var request = JsonSerializer.Deserialize<JsonRpcRequest>(line);
                        if (request == null)
                        {
                            await SendErrorResponse(null, -32700, "Parse error");
                            continue;
                        }

                        // Process the request
                        await ProcessRequest(request);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to parse JSON-RPC request");
                        await SendErrorResponse(null, -32700, "Parse error");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing request");
                        await SendErrorResponse(null, -32603, "Internal error");
                    }
                }

                _logger.LogInformation("MCP server stopped");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("MCP server cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in MCP server");
                throw;
            }
        }

        private async Task ProcessRequest(JsonRpcRequest request)
        {
            _logger.LogDebug("Processing request: {Method}", request.Method);

            switch (request.Method)
            {
                case "initialize":
                    await HandleInitialize(request);
                    break;

                case "tools/list":
                    await HandleToolsList(request);
                    break;

                case "tools/call":
                    await HandleToolCall(request);
                    break;

                case "ping":
                    await HandlePing(request);
                    break;

                default:
                    await SendErrorResponse(request.Id, -32601, $"Method not found: {request.Method}");
                    break;
            }
        }

        private async Task HandleInitialize(JsonRpcRequest request)
        {
            var response = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = "OData MCP Server",
                    version = "1.0.0"
                }
            };

            await SendResponse(request.Id, response);
        }

        private async Task HandleToolsList(JsonRpcRequest request)
        {
            var tools = new List<object>
            {
                new
                {
                    name = "QueryEntitySet",
                    description = "Queries an OData entity set with optional filtering, sorting, and pagination",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            entitySet = new { type = "string", description = "The name of the entity set to query" },
                            filter = new { type = "string", description = "OData filter expression" },
                            orderby = new { type = "string", description = "OData orderby expression" },
                            select = new { type = "string", description = "Comma-separated list of properties to select" },
                            top = new { type = "integer", description = "Maximum number of results to return" },
                            skip = new { type = "integer", description = "Number of results to skip" },
                            count = new { type = "boolean", description = "Include the total count" }
                        },
                        required = new[] { "entitySet" }
                    }
                },
                new
                {
                    name = "GetEntity",
                    description = "Gets a single entity by its key from an OData service",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            entitySet = new { type = "string", description = "The name of the entity set" },
                            key = new { type = "string", description = "The entity key value" },
                            select = new { type = "string", description = "Comma-separated list of properties to select" }
                        },
                        required = new[] { "entitySet", "key" }
                    }
                },
                new
                {
                    name = "CreateEntity",
                    description = "Creates a new entity in the specified OData entity set",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            entitySet = new { type = "string", description = "The name of the entity set" },
                            entity = new { type = "string", description = "The entity data as JSON" }
                        },
                        required = new[] { "entitySet", "entity" }
                    }
                },
                new
                {
                    name = "GetMetadata",
                    description = "Gets the OData service metadata document",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { }
                    }
                },
                new
                {
                    name = "DiscoverEntitySets",
                    description = "Discovers and lists all available entity sets in the OData service",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { }
                    }
                },
                new
                {
                    name = "DescribeEntityType",
                    description = "Gets detailed schema information for a specific entity type",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            entityTypeName = new { type = "string", description = "The name of the entity type to describe" }
                        },
                        required = new[] { "entityTypeName" }
                    }
                }
            };

            var response = new { tools };
            await SendResponse(request.Id, response);
        }

        private async Task HandleToolCall(JsonRpcRequest request)
        {
            try
            {
                var toolCallParams = JsonSerializer.Deserialize<ToolCallParams>(request.Params?.ToString() ?? "{}");
                if (toolCallParams == null || string.IsNullOrEmpty(toolCallParams.Name))
                {
                    await SendErrorResponse(request.Id, -32602, "Invalid params");
                    return;
                }

                string result;
                switch (toolCallParams.Name)
                {
                    case "QueryEntitySet":
                        {
                            var args = JsonSerializer.Deserialize<QueryEntitySetArgs>(toolCallParams.Arguments?.ToString() ?? "{}");
                            result = await _odataTools.QueryEntitySet(
                                args?.EntitySet ?? "",
                                args?.Filter,
                                args?.OrderBy,
                                args?.Select,
                                args?.Top,
                                args?.Skip,
                                args?.Count ?? false
                            );
                            break;
                        }

                    case "GetEntity":
                        {
                            var args = JsonSerializer.Deserialize<GetEntityArgs>(toolCallParams.Arguments?.ToString() ?? "{}");
                            result = await _odataTools.GetEntity(
                                args?.EntitySet ?? "",
                                args?.Key ?? "",
                                args?.Select
                            );
                            break;
                        }

                    case "CreateEntity":
                        {
                            var args = JsonSerializer.Deserialize<CreateEntityArgs>(toolCallParams.Arguments?.ToString() ?? "{}");
                            result = await _odataTools.CreateEntity(
                                args?.EntitySet ?? "",
                                args?.Entity ?? "{}"
                            );
                            break;
                        }

                    case "GetMetadata":
                        result = await _odataTools.GetMetadata();
                        break;

                    case "DiscoverEntitySets":
                        result = await _dynamicTools.DiscoverEntitySets();
                        break;

                    case "DescribeEntityType":
                        {
                            var args = JsonSerializer.Deserialize<DescribeEntityTypeArgs>(toolCallParams.Arguments?.ToString() ?? "{}");
                            result = await _dynamicTools.DescribeEntityType(args?.EntityTypeName ?? "");
                            break;
                        }

                    default:
                        await SendErrorResponse(request.Id, -32602, $"Unknown tool: {toolCallParams.Name}");
                        return;
                }

                var response = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = result
                        }
                    }
                };

                await SendResponse(request.Id, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool");
                
                var errorResponse = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    },
                    isError = true
                };

                await SendResponse(request.Id, errorResponse);
            }
        }

        private async Task HandlePing(JsonRpcRequest request)
        {
            await SendResponse(request.Id, new { });
        }

        private async Task SendResponse(object? id, object result)
        {
            var response = new
            {
                jsonrpc = "2.0",
                id,
                result
            };

            var json = JsonSerializer.Serialize(response);
            await _output.WriteLineAsync(json);
            await _output.FlushAsync();
        }

        private async Task SendErrorResponse(object? id, int code, string message)
        {
            var response = new
            {
                jsonrpc = "2.0",
                id,
                error = new
                {
                    code,
                    message
                }
            };

            var json = JsonSerializer.Serialize(response);
            await _output.WriteLineAsync(json);
            await _output.FlushAsync();
        }

        #region Request/Response Types

        private class JsonRpcRequest
        {
            [JsonPropertyName("jsonrpc")]
            public string? JsonRpc { get; set; }

            [JsonPropertyName("id")]
            public object? Id { get; set; }

            [JsonPropertyName("method")]
            public string? Method { get; set; }

            [JsonPropertyName("params")]
            public object? Params { get; set; }
        }

        private class ToolCallParams
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("arguments")]
            public object? Arguments { get; set; }
        }

        private class QueryEntitySetArgs
        {
            [JsonPropertyName("entitySet")]
            public string? EntitySet { get; set; }

            [JsonPropertyName("filter")]
            public string? Filter { get; set; }

            [JsonPropertyName("orderby")]
            public string? OrderBy { get; set; }

            [JsonPropertyName("select")]
            public string? Select { get; set; }

            [JsonPropertyName("top")]
            public int? Top { get; set; }

            [JsonPropertyName("skip")]
            public int? Skip { get; set; }

            [JsonPropertyName("count")]
            public bool? Count { get; set; }
        }

        private class GetEntityArgs
        {
            [JsonPropertyName("entitySet")]
            public string? EntitySet { get; set; }

            [JsonPropertyName("key")]
            public string? Key { get; set; }

            [JsonPropertyName("select")]
            public string? Select { get; set; }
        }

        private class CreateEntityArgs
        {
            [JsonPropertyName("entitySet")]
            public string? EntitySet { get; set; }

            [JsonPropertyName("entity")]
            public string? Entity { get; set; }
        }

        private class DescribeEntityTypeArgs
        {
            [JsonPropertyName("entityTypeName")]
            public string? EntityTypeName { get; set; }
        }

        #endregion
    }
}