// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Server;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.OData.Mcp.Tools.Services
{

    /// <summary>
    /// Background service that generates dynamic MCP tools based on OData metadata.
    /// </summary>
    public class DynamicToolGeneratorService : IHostedService
    {

        #region Fields

        private readonly ICsdlMetadataParser _metadataParser;
        private readonly ILogger<DynamicToolGeneratorService> _logger;
        private readonly IMcpToolFactory _toolFactory;
        private readonly IOptions<McpServerConfiguration> _configuration;
        private readonly DynamicODataMcpTools _dynamicTools;
        private readonly IServiceProvider _serviceProvider;
        private readonly EdmModel? _preloadedModel;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicToolGeneratorService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="toolFactory">The tool factory for generating dynamic tools.</param>
        /// <param name="metadataParser">The CSDL metadata parser.</param>
        /// <param name="configuration">The server configuration.</param>
        /// <param name="dynamicTools">The dynamic OData tools instance.</param>
        /// <param name="serviceProvider">The service provider for DI.</param>
        public DynamicToolGeneratorService(
            ILogger<DynamicToolGeneratorService> logger,
            IMcpToolFactory toolFactory,
            ICsdlMetadataParser metadataParser,
            IOptions<McpServerConfiguration> configuration,
            DynamicODataMcpTools dynamicTools,
            IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
            _metadataParser = metadataParser ?? throw new ArgumentNullException(nameof(metadataParser));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _dynamicTools = dynamicTools ?? throw new ArgumentNullException(nameof(dynamicTools));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            
            // Try to get the pre-loaded model from DI
            _preloadedModel = serviceProvider.GetService<EdmModel>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the service and generates dynamic tools.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting dynamic tool logging for OData service");
                
                EdmModel model;
                
                // Check if we have a pre-loaded model from DI
                if (_preloadedModel != null)
                {
                    _logger.LogInformation("Using pre-loaded EDM model from dependency injection");
                    model = _preloadedModel;
                }
                else
                {
                    // Fetch metadata and parse it
                    var config = _configuration.Value;
                    var metadataUrl = $"{config.ODataService.BaseUrl?.TrimEnd('/') ?? throw new InvalidOperationException("OData service base URL is not configured")}{config.ODataService.MetadataPath}";
                    
                    _logger.LogInformation("Fetching metadata from: {MetadataUrl}", metadataUrl);
                    
                    using var httpClient = new System.Net.Http.HttpClient();
                    if (!string.IsNullOrWhiteSpace(config.ODataService.Authentication?.BearerToken))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ODataService.Authentication.BearerToken);
                    }
                    
                    var response = await httpClient.GetAsync(metadataUrl);
                    response.EnsureSuccessStatusCode();
                    
                    var metadataXml = await response.Content.ReadAsStringAsync();
                    model = _metadataParser.ParseFromString(metadataXml);
                    
                    _logger.LogInformation("Successfully fetched and parsed OData metadata");
                }
                
                _logger.LogInformation("Found {EntityTypeCount} entity types and {EntitySetCount} entity sets",
                    model.EntityTypes.Count,
                    model.EntityContainers.SelectMany(c => c.EntitySets).Count());

                // Generate dynamic tools based on the model
                var options = new McpToolGenerationOptions
                {
                    GenerateCrudTools = true,
                    GenerateQueryTools = true,
                    GenerateNavigationTools = true,
                    IncludeExamples = true,
                    MaxToolCount = 100, // Limit total tools to avoid overwhelming the system
                    ToolVersion = "1.0.0"
                };

                _logger.LogInformation("Generating dynamic MCP tools with options: {Options}", 
                    $"CRUD={options.GenerateCrudTools}, Query={options.GenerateQueryTools}, Navigation={options.GenerateNavigationTools}");

                var generatedTools = await _toolFactory.GenerateToolsAsync(model, options);
                var toolsList = generatedTools.ToList();

                _logger.LogInformation("Generated {ToolCount} dynamic tool definitions", toolsList.Count);
                
                // Note: The actual tools have already been registered in StartCommand
                // This service now primarily logs what tools are available
                
                // Log all generated tools
                _logger.LogInformation("=== REGISTERED MCP TOOLS ===");
                
                // Log static tools from ODataMcpTools
                _logger.LogInformation("Static OData Tools:");
                _logger.LogInformation("  - QueryEntitySet: Query any entity set with OData options");
                _logger.LogInformation("  - GetEntity: Get a single entity by key");
                _logger.LogInformation("  - CreateEntity: Create a new entity");
                _logger.LogInformation("  - UpdateEntity: Update an existing entity");
                _logger.LogInformation("  - DeleteEntity: Delete an entity");
                _logger.LogInformation("  - NavigateRelationship: Navigate to related entities");
                _logger.LogInformation("  - GetMetadata: Get OData service metadata");

                // Log dynamic discovery tools
                _logger.LogInformation("Dynamic Discovery Tools:");
                _logger.LogInformation("  - DiscoverEntitySets: List all available entity sets");
                _logger.LogInformation("  - DescribeEntityType: Get schema for an entity type");
                _logger.LogInformation("  - GenerateQueryExamples: Generate sample OData queries");
                _logger.LogInformation("  - ValidateQuery: Validate an OData query URL");

                // Log dynamically generated entity-specific tools
                if (toolsList.Any())
                {
                    _logger.LogInformation("Entity-Specific Tools Generated:");
                    
                    // Group by entity type for better organization
                    var toolsByEntity = toolsList.GroupBy(t => 
                    {
                        if (t.Metadata != null && t.Metadata.TryGetValue("EntityType", out var entityType))
                        {
                            return entityType?.ToString() ?? "Unknown";
                        }
                        return "Unknown";
                    });
                    
                    foreach (var entityGroup in toolsByEntity.OrderBy(g => g.Key))
                    {
                        _logger.LogInformation("  {EntityType}:", entityGroup.Key);
                        foreach (var tool in entityGroup.OrderBy(t => t.Name))
                        {
                            _logger.LogInformation("    - {ToolName}: {Description}", tool.Name, tool.Description);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No entity-specific tools were generated. This might indicate an issue with the metadata or tool generation.");
                }

                _logger.LogInformation("=== END OF REGISTERED TOOLS ===");
                _logger.LogInformation("Total tools available: {TotalCount}", 
                    11 + toolsList.Count); // 7 static + 4 dynamic discovery + generated tools

                if (_preloadedModel != null)
                {
                    _logger.LogInformation("Dynamic tools have been registered during server initialization");
                    _logger.LogInformation("All {Count} entity-specific tools are available as first-class MCP tools", toolsList.Count);
                }
                else
                {
                    _logger.LogInformation("Dynamic tools generated but not registered (metadata fetched after initialization)");
                    _logger.LogInformation("Use the generic OData tools for entity operations");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate dynamic tools");
                // Don't throw - allow the server to start even if dynamic tool generation fails
                // The static tools will still be available
            }
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping dynamic tool generator service");
            return Task.CompletedTask;
        }

        #endregion

        #region Private Methods


        #endregion

    }

}