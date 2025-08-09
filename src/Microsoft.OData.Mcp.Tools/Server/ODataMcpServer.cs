using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Tools;
// using ModelContextProtocol; // Will be added when SDK integration is complete

namespace Microsoft.OData.Mcp.Tools.Server
{
    /// <summary>
    /// MCP server implementation using the official SDK.
    /// </summary>
    public class ODataMcpServer : IHostedService
    {
        internal readonly ILogger<ODataMcpServer> _logger;
        internal readonly IServiceProvider _serviceProvider;
        internal readonly string _odataUrl;
        internal readonly string? _authToken;
        internal readonly IMcpToolFactory _toolFactory;
        internal readonly ICsdlMetadataParser _metadataParser;
        internal EdmModel? _edmModel;
        internal IEnumerable<McpToolDefinition>? _tools;

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpServer"/> class.
        /// </summary>
        public ODataMcpServer(
            ILogger<ODataMcpServer> logger,
            IServiceProvider serviceProvider,
            IMcpToolFactory toolFactory,
            ICsdlMetadataParser metadataParser,
            string odataUrl,
            string? authToken = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
            _metadataParser = metadataParser ?? throw new ArgumentNullException(nameof(metadataParser));
            _odataUrl = odataUrl ?? throw new ArgumentNullException(nameof(odataUrl));
            _authToken = authToken;
        }

        /// <summary>
        /// Starts the MCP server.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting OData MCP Server");
            _logger.LogInformation("OData Service URL: {Url}", _odataUrl);

            try
            {
                // Fetch and parse OData metadata
                await InitializeODataModelAsync(cancellationToken);

                // Generate tools from the model
                await GenerateToolsAsync(cancellationToken);

                _logger.LogInformation("OData MCP Server started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start OData MCP Server");
                throw;
            }
        }

        /// <summary>
        /// Stops the MCP server.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping OData MCP Server");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Initializes the OData model by fetching metadata.
        /// </summary>
        internal async Task InitializeODataModelAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching OData metadata from {Url}", _odataUrl);

            using var httpClient = new System.Net.Http.HttpClient();
            
            if (!string.IsNullOrWhiteSpace(_authToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
            }

            var metadataUrl = $"{_odataUrl.TrimEnd('/')}/$metadata";
            var response = await httpClient.GetAsync(metadataUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var metadataXml = await response.Content.ReadAsStringAsync();
            _edmModel = _metadataParser.ParseFromString(metadataXml);

            _logger.LogInformation("Parsed OData model with {EntityCount} entity types", 
                _edmModel?.EntityTypes?.Count ?? 0);
        }

        /// <summary>
        /// Generates MCP tools from the OData model.
        /// </summary>
        internal async Task GenerateToolsAsync(CancellationToken cancellationToken)
        {
            if (_edmModel == null)
            {
                _logger.LogWarning("No EDM model available for tool generation");
                return;
            }

            var options = new McpToolGenerationOptions
            {
                MaxToolCount = 100,
                GenerateCrudTools = true,
                GenerateQueryTools = true,
                GenerateNavigationTools = true
            };

            _tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);
            _logger.LogInformation("Generated {ToolCount} MCP tools", _tools?.Count() ?? 0);
        }

        /// <summary>
        /// Gets the generated tools.
        /// </summary>
        public IEnumerable<McpToolDefinition> GetTools()
        {
            return _tools ?? Enumerable.Empty<McpToolDefinition>();
        }

        /// <summary>
        /// Executes a tool by name.
        /// </summary>
        public async Task<McpToolResult> ExecuteToolAsync(
            string toolName, 
            JsonDocument parameters,
            CancellationToken cancellationToken = default)
        {
            if (_tools == null)
            {
                return McpToolResult.Error("Tools not initialized", "TOOLS_NOT_INITIALIZED");
            }

            var tool = _tools.FirstOrDefault(t => 
                t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

            if (tool == null)
            {
                return McpToolResult.NotFound($"Tool '{toolName}' not found");
            }

            // Create execution context
            var context = new McpToolContext
            {
                Model = _edmModel ?? new EdmModel(),
                ServiceBaseUrl = _odataUrl,
                HttpClientFactory = _serviceProvider.GetRequiredService<System.Net.Http.IHttpClientFactory>(),
                CorrelationId = Guid.NewGuid().ToString(),
                CancellationToken = cancellationToken
            };

            // Execute the tool
            return await tool.Handler(context, parameters);
        }
    }
}