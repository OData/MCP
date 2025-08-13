// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Constants;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Tools
{

    /// <summary>
    /// Factory for creating MCP tools dynamically from OData metadata.
    /// </summary>
    /// <remarks>
    /// This factory generates MCP tools based on the parsed OData model, creating tools for
    /// CRUD operations, queries, and navigation between entities. The tools are generated
    /// dynamically to match the structure and capabilities of the OData service.
    /// </remarks>
    public sealed class McpToolFactory : IMcpToolFactory
    {

        #region Fields

        internal readonly ILogger<McpToolFactory> _logger;
        internal readonly IHttpClientFactory _httpClientFactory;
        internal readonly Dictionary<string, McpToolDefinition> _generatedTools = [];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolFactory"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="httpClientFactory">The HTTP client factory for OData requests.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> or <paramref name="httpClientFactory"/> is null.</exception>
        public McpToolFactory(ILogger<McpToolFactory> logger, IHttpClientFactory httpClientFactory)
        {
ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Generates all MCP tools for the specified OData model.
        /// </summary>
        /// <param name="model">The OData model to generate tools for.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of generated MCP tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public async Task<IEnumerable<McpToolDefinition>> GenerateToolsAsync(EdmModel model, McpToolGenerationOptions? options = null)
        {
ArgumentNullException.ThrowIfNull(model);

            options ??= McpToolGenerationOptions.Default();
            
            _logger.LogInformation("Starting tool generation for OData model with {EntityTypeCount} entity types", model.EntityTypes.Count);

            var allTools = new List<McpToolDefinition>();

            try
            {
                // Generate tools for each entity type
                foreach (var entityType in model.EntityTypes)
                {
                    if (!options.ShouldIncludeEntityType(entityType.FullName))
                    {
                        _logger.LogDebug("Skipping entity type {EntityType} based on generation options", entityType.FullName);
                        continue;
                    }

                    var entityTools = await GenerateEntityToolsAsync(entityType, model, options);
                    allTools.AddRange(entityTools);

                    // Check if we've hit the maximum tool count
                    if (options.MaxToolCount.HasValue && allTools.Count >= options.MaxToolCount.Value)
                    {
                        _logger.LogWarning("Reached maximum tool count limit of {MaxCount}, stopping generation", options.MaxToolCount.Value);
                        break;
                    }
                }

                // Generate general query tools if enabled
                if (options.GenerateQueryTools)
                {
                    var queryTools = await GenerateQueryToolsAsync(model, options);
                    allTools.AddRange(queryTools);
                }

                // Generate entity set tools if enabled
                if (options.GenerateEntitySetTools)
                {
                    foreach (var entitySet in model.EntityContainer?.EntitySets ?? Enumerable.Empty<EdmEntitySet>())
                    {
                        if (!options.ShouldIncludeEntityType(entitySet.EntityType))
                        {
                            continue;
                        }

                        var entitySetTools = await GenerateEntitySetToolsAsync(entitySet, model, options);
                        allTools.AddRange(entitySetTools);
                    }
                }

                // Store generated tools for retrieval
                _generatedTools.Clear();
                foreach (var tool in allTools)
                {
                    _generatedTools[tool.Name] = tool;
                }

                _logger.LogInformation("Successfully generated {ToolCount} MCP tools", allTools.Count);

                return allTools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate MCP tools from OData model");
                throw;
            }
        }

        /// <summary>
        /// Generates MCP tools for a specific entity type.
        /// </summary>
        /// <param name="entityType">The entity type to generate tools for.</param>
        /// <param name="model">The complete OData model for context.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of generated MCP tool definitions for the entity type.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityType"/> or <paramref name="model"/> is null.</exception>
        public async Task<IEnumerable<McpToolDefinition>> GenerateEntityToolsAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions? options = null)
        {
ArgumentNullException.ThrowIfNull(entityType);
            ArgumentNullException.ThrowIfNull(model);

            options ??= McpToolGenerationOptions.Default();
            
            _logger.LogDebug("Generating tools for entity type {EntityType}", entityType.FullName);

            var tools = new List<McpToolDefinition>();

            // Generate CRUD tools
            if (options.GenerateCrudTools)
            {
                var crudTools = await GenerateCrudToolsAsync(entityType, model, options);
                tools.AddRange(crudTools);
            }

            // Generate navigation tools
            if (options.GenerateNavigationTools)
            {
                var navigationTools = await GenerateNavigationToolsAsync(entityType, model, options);
                tools.AddRange(navigationTools);
            }

            return tools;
        }

        /// <summary>
        /// Generates CRUD operation tools for a specific entity type.
        /// </summary>
        /// <param name="entityType">The entity type to generate CRUD tools for.</param>
        /// <param name="model">The complete OData model for context.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of CRUD tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityType"/> or <paramref name="model"/> is null.</exception>
        public async Task<IEnumerable<McpToolDefinition>> GenerateCrudToolsAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions? options = null)
        {
ArgumentNullException.ThrowIfNull(entityType);
            ArgumentNullException.ThrowIfNull(model);

            options ??= McpToolGenerationOptions.Default();

            var tools = new List<McpToolDefinition>();

            // Generate Create tool
            if (options.ShouldIncludeOperation(McpToolOperationType.Create))
            {
                var createTool = await GenerateCreateToolAsync(entityType, model, options);
                tools.Add(createTool);
            }

            // Generate Read tool
            if (options.ShouldIncludeOperation(McpToolOperationType.Read))
            {
                var readTool = await GenerateReadToolAsync(entityType, model, options);
                tools.Add(readTool);
            }

            // Generate Update tool
            if (options.ShouldIncludeOperation(McpToolOperationType.Update))
            {
                var updateTool = await GenerateUpdateToolAsync(entityType, model, options);
                tools.Add(updateTool);
            }

            // Generate Delete tool
            if (options.ShouldIncludeOperation(McpToolOperationType.Delete))
            {
                var deleteTool = await GenerateDeleteToolAsync(entityType, model, options);
                tools.Add(deleteTool);
            }

            return tools;
        }

        /// <summary>
        /// Generates query tools for the OData model.
        /// </summary>
        /// <param name="model">The OData model to generate query tools for.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of query tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public async Task<IEnumerable<McpToolDefinition>> GenerateQueryToolsAsync(EdmModel model, McpToolGenerationOptions? options = null)
        {
ArgumentNullException.ThrowIfNull(model);

            options ??= McpToolGenerationOptions.Default();

            var tools = new List<McpToolDefinition>();

            if (options.ShouldIncludeOperation(McpToolOperationType.Query))
            {
                // Generate a general query tool for advanced scenarios
                var generalQueryTool = await GenerateGeneralQueryToolAsync(model, options);
                tools.Add(generalQueryTool);
            }

            return tools;
        }

        /// <summary>
        /// Generates navigation tools for entity relationships.
        /// </summary>
        /// <param name="entityType">The entity type to generate navigation tools for.</param>
        /// <param name="model">The complete OData model for context.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of navigation tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityType"/> or <paramref name="model"/> is null.</exception>
        public async Task<IEnumerable<McpToolDefinition>> GenerateNavigationToolsAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions? options = null)
        {
ArgumentNullException.ThrowIfNull(entityType);
            ArgumentNullException.ThrowIfNull(model);

            options ??= McpToolGenerationOptions.Default();

            var tools = new List<McpToolDefinition>();

            if (options.ShouldIncludeOperation(McpToolOperationType.Navigate))
            {
                foreach (var navProperty in entityType.NavigationProperties)
                {
                    var navTool = await GenerateNavigationToolAsync(entityType, navProperty, model, options);
                    tools.Add(navTool);
                }
            }

            return tools;
        }

        /// <summary>
        /// Generates tools for entity set operations (collection-level operations).
        /// </summary>
        /// <param name="entitySet">The entity set to generate tools for.</param>
        /// <param name="model">The complete OData model for context.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of entity set tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entitySet"/> or <paramref name="model"/> is null.</exception>
        public async Task<IEnumerable<McpToolDefinition>> GenerateEntitySetToolsAsync(EdmEntitySet entitySet, EdmModel model, McpToolGenerationOptions? options = null)
        {
ArgumentNullException.ThrowIfNull(entitySet);
            ArgumentNullException.ThrowIfNull(model);

            options ??= McpToolGenerationOptions.Default();

            var tools = new List<McpToolDefinition>();

            if (options.ShouldIncludeOperation(McpToolOperationType.Query))
            {
                var listTool = await GenerateEntitySetListToolAsync(entitySet, model, options);
                tools.Add(listTool);
            }

            return tools;
        }

        /// <summary>
        /// Validates that the generated tools are compatible with the MCP specification.
        /// </summary>
        /// <param name="tools">The tools to validate.</param>
        /// <returns>A collection of validation errors, or empty if all tools are valid.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is null.</exception>
        public IEnumerable<string> ValidateTools(IEnumerable<McpToolDefinition> tools)
        {
ArgumentNullException.ThrowIfNull(tools);

            var errors = new List<string>();
            var toolNames = new HashSet<string>();

            foreach (var tool in tools)
            {
                // Validate individual tool
                var toolErrors = tool.Validate();
                errors.AddRange(toolErrors.Select(e => $"Tool '{tool.Name}': {e}"));

                // Check for duplicate names
                if (!toolNames.Add(tool.Name))
                {
                    errors.Add($"Duplicate tool name: {tool.Name}");
                }
            }

            return errors;
        }

        /// <summary>
        /// Gets the tool definition by name.
        /// </summary>
        /// <param name="toolName">The name of the tool to retrieve.</param>
        /// <returns>The tool definition if found; otherwise, null.</returns>
        public McpToolDefinition? GetTool(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return null;
            }

            _generatedTools.TryGetValue(toolName, out var tool);
            return tool;
        }

        /// <summary>
        /// Gets all available tool names.
        /// </summary>
        /// <returns>A collection of all tool names that have been generated.</returns>
        public IEnumerable<string> GetAvailableToolNames()
        {
            return _generatedTools.Keys.ToList();
        }

        /// <summary>
        /// Filters tools based on user authorization context.
        /// </summary>
        /// <param name="tools">The tools to filter.</param>
        /// <param name="userScopes">The user's OAuth2 scopes.</param>
        /// <param name="userRoles">The user's roles.</param>
        /// <param name="options">Options for authorization filtering.</param>
        /// <returns>A collection of tools the user is authorized to access.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is null.</exception>
        public IEnumerable<McpToolDefinition> FilterToolsForUser(IEnumerable<McpToolDefinition> tools, IEnumerable<string> userScopes, IEnumerable<string> userRoles, McpToolGenerationOptions? options = null)
        {
ArgumentNullException.ThrowIfNull(tools);

            var scopes = userScopes?.ToList() ?? [];
            var roles = userRoles?.ToList() ?? [];

            return tools.Where(tool => tool.IsAuthorizedForUser(scopes, roles));
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Generates a Create tool for the specified entity type.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="model">The OData model.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A Create tool definition.</returns>
        internal async Task<McpToolDefinition> GenerateCreateToolAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions options)
        {
            var toolName = options.FormatToolName($"create_{entityType.Name.ToLowerInvariant()}");
            var description = $"Creates a new {entityType.Name} entity";

            var inputSchema = GenerateEntityInputSchema(entityType, required: true);
            var requiredScopes = options.GetCombinedScopes(entityType.FullName, McpToolOperationType.Create).ToList();
            
            // Find the entity set for this entity type
            var entitySet = model.EntityContainer?.EntitySets.FirstOrDefault(es => es.EntityType == entityType.FullName);

            var tool = McpToolDefinition.CreateCrudTool(
                toolName,
                description,
                McpToolOperationType.Create,
                entityType.FullName,
                inputSchema,
                CreateEntityHandler,
                entitySet?.Name);

            tool.RequiredScopes = requiredScopes;
            tool.RequiredRoles = new List<string>(options.DefaultRequiredRoles);
            tool.Version = options.ToolVersion;

            // Store entity metadata for use by handlers
            tool.Metadata["KeyProperties"] = entityType.Key;
            tool.Metadata["EntityType"] = entityType;  // Store the actual object for consistency
            tool.Metadata["EntityTypeName"] = entityType.FullName;
            tool.Metadata["AllProperties"] = entityType.Properties.Select(p => p.Name).ToList();
            tool.Metadata["GenerationOptions"] = options;

            if (options.IncludeExamples)
            {
                AddCreateExamples(tool, entityType);
            }

            return await Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a Read tool for the specified entity type.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="model">The OData model.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A Read tool definition.</returns>
        internal async Task<McpToolDefinition> GenerateReadToolAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions options)
        {
            var toolName = options.FormatToolName($"get_{entityType.Name.ToLowerInvariant()}");
            var description = $"Retrieves a {entityType.Name} entity by its key";

            var inputSchema = GenerateKeyInputSchema(entityType);
            var requiredScopes = options.GetCombinedScopes(entityType.FullName, McpToolOperationType.Read).ToList();
            
            // Find the entity set for this entity type
            var entitySet = model.EntityContainer?.EntitySets.FirstOrDefault(es => es.EntityType == entityType.FullName);

            var tool = McpToolDefinition.CreateCrudTool(
                toolName,
                description,
                McpToolOperationType.Read,
                entityType.FullName,
                inputSchema,
                ReadEntityHandler,
                entitySet?.Name);

            tool.RequiredScopes = requiredScopes;
            tool.RequiredRoles = new List<string>(options.DefaultRequiredRoles);
            tool.Version = options.ToolVersion;

            // Store entity metadata for use by handlers
            tool.Metadata["KeyProperties"] = entityType.Key;
            tool.Metadata["EntityType"] = entityType;  // Store the actual object for consistency
            tool.Metadata["EntityTypeName"] = entityType.FullName;
            tool.Metadata["AllProperties"] = entityType.Properties.Select(p => p.Name).ToList();
            tool.Metadata["GenerationOptions"] = options;

            if (options.IncludeExamples)
            {
                AddReadExamples(tool, entityType);
            }

            return await Task.FromResult(tool);
        }

        /// <summary>
        /// Generates an Update tool for the specified entity type.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="model">The OData model.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>An Update tool definition.</returns>
        internal async Task<McpToolDefinition> GenerateUpdateToolAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions options)
        {
            var toolName = options.FormatToolName($"update_{entityType.Name.ToLowerInvariant()}");
            var description = $"Updates an existing {entityType.Name} entity";

            var inputSchema = GenerateEntityUpdateSchema(entityType);
            var requiredScopes = options.GetCombinedScopes(entityType.FullName, McpToolOperationType.Update).ToList();
            
            // Find the entity set for this entity type
            var entitySet = model.EntityContainer?.EntitySets.FirstOrDefault(es => es.EntityType == entityType.FullName);

            var tool = McpToolDefinition.CreateCrudTool(
                toolName,
                description,
                McpToolOperationType.Update,
                entityType.FullName,
                inputSchema,
                UpdateEntityHandler,
                entitySet?.Name);

            tool.RequiredScopes = requiredScopes;
            tool.RequiredRoles = new List<string>(options.DefaultRequiredRoles);
            tool.Version = options.ToolVersion;

            // Store entity metadata for use by handlers
            tool.Metadata["KeyProperties"] = entityType.Key;
            tool.Metadata["EntityType"] = entityType;  // Store the actual object for consistency
            tool.Metadata["EntityTypeName"] = entityType.FullName;
            tool.Metadata["AllProperties"] = entityType.Properties.Select(p => p.Name).ToList();
            tool.Metadata["GenerationOptions"] = options;

            if (options.IncludeExamples)
            {
                AddUpdateExamples(tool, entityType);
            }

            return await Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a Delete tool for the specified entity type.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="model">The OData model.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A Delete tool definition.</returns>
        internal async Task<McpToolDefinition> GenerateDeleteToolAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions options)
        {
            var toolName = options.FormatToolName($"delete_{entityType.Name.ToLowerInvariant()}");
            var description = $"Deletes a {entityType.Name} entity";

            var inputSchema = GenerateKeyInputSchema(entityType);
            var requiredScopes = options.GetCombinedScopes(entityType.FullName, McpToolOperationType.Delete).ToList();
            
            // Find the entity set for this entity type
            var entitySet = model.EntityContainer?.EntitySets.FirstOrDefault(es => es.EntityType == entityType.FullName);

            var tool = McpToolDefinition.CreateCrudTool(
                toolName,
                description,
                McpToolOperationType.Delete,
                entityType.FullName,
                inputSchema,
                DeleteEntityHandler,
                entitySet?.Name);

            tool.RequiredScopes = requiredScopes;
            tool.RequiredRoles = new List<string>(options.DefaultRequiredRoles);
            tool.Version = options.ToolVersion;

            // Store entity metadata for use by handlers
            tool.Metadata["KeyProperties"] = entityType.Key;
            tool.Metadata["EntityType"] = entityType;  // Store the actual object for consistency
            tool.Metadata["EntityTypeName"] = entityType.FullName;
            tool.Metadata["AllProperties"] = entityType.Properties.Select(p => p.Name).ToList();
            tool.Metadata["GenerationOptions"] = options;

            if (options.IncludeExamples)
            {
                AddDeleteExamples(tool, entityType);
            }

            return await Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a general query tool for advanced scenarios.
        /// </summary>
        /// <param name="model">The OData model.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A general query tool definition.</returns>
        internal async Task<McpToolDefinition> GenerateGeneralQueryToolAsync(EdmModel model, McpToolGenerationOptions options)
        {
            var toolName = options.FormatToolName("odata_query");
            var description = "Executes advanced OData queries with full $filter, $orderby, $select, and $expand support";

            var inputSchema = GenerateQueryInputSchema();
            var requiredScopes = options.GetOperationScopes(McpToolOperationType.Query).ToList();

            var tool = McpToolDefinition.CreateQueryTool(
                toolName,
                description,
                inputSchema,
                QueryEntityHandler);

            tool.RequiredScopes = requiredScopes;
            tool.RequiredRoles = new List<string>(options.DefaultRequiredRoles);
            tool.Version = options.ToolVersion;

            if (options.IncludeExamples)
            {
                AddQueryExamples(tool, model);
            }

            return await Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a navigation tool for a specific navigation property.
        /// </summary>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navProperty">The navigation property.</param>
        /// <param name="model">The OData model.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A navigation tool definition.</returns>
        internal async Task<McpToolDefinition> GenerateNavigationToolAsync(EdmEntityType entityType, EdmNavigationProperty navProperty, EdmModel model, McpToolGenerationOptions options)
        {
            var toolName = options.FormatToolName($"navigate_{entityType.Name.ToLowerInvariant()}_{navProperty.Name.ToLowerInvariant()}");
            var description = $"Navigates from {entityType.Name} to {navProperty.Name}";

            var inputSchema = GenerateNavigationInputSchema(entityType, navProperty);
            var requiredScopes = options.GetCombinedScopes(entityType.FullName, McpToolOperationType.Navigate).ToList();

            var tool = new McpToolDefinition
            {
                Name = toolName,
                Description = description,
                Category = "Navigation",
                OperationType = McpToolOperationType.Navigate,
                TargetEntityType = entityType.FullName,
                InputSchema = inputSchema,
                Handler = NavigateEntityHandler,
                RequiredScopes = requiredScopes,
                RequiredRoles = new List<string>(options.DefaultRequiredRoles),
                Version = options.ToolVersion
            };

            if (options.IncludeExamples)
            {
                AddNavigationExamples(tool, entityType, navProperty);
            }

            return await Task.FromResult(tool);
        }

        /// <summary>
        /// Generates an entity set list tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="model">The OData model.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>An entity set list tool definition.</returns>
        internal async Task<McpToolDefinition> GenerateEntitySetListToolAsync(EdmEntitySet entitySet, EdmModel model, McpToolGenerationOptions options)
        {
            var toolName = options.FormatToolName($"list_{entitySet.Name.ToLowerInvariant()}");
            
            // Find the entity type for this entity set
            var entityType = model.EntityTypes.FirstOrDefault(et => 
                et.FullName == entitySet.EntityType || et.Name == entitySet.EntityType);
            
            // Build description that mentions binary field exclusion if applicable
            var description = $"Lists entities from the {entitySet.Name} collection with optional filtering and pagination";
            if (entityType != null && options.ExcludeBinaryFieldsByDefault)
            {
                var hasBinaryFields = entityType.Properties.Any(p => IsBinaryOrStreamField(p));
                if (hasBinaryFields)
                {
                    description += ". Binary/stream fields are excluded by default - use $select to explicitly include them if needed";
                }
            }

            var inputSchema = GenerateEntitySetQuerySchema();
            var requiredScopes = options.GetCombinedScopes(entitySet.EntityType, McpToolOperationType.Query).ToList();

            var tool = new McpToolDefinition
            {
                Name = toolName,
                Description = description,
                Category = "EntitySet",
                OperationType = McpToolOperationType.Query,
                TargetEntitySet = entitySet.Name,
                TargetEntityType = entitySet.EntityType,
                InputSchema = inputSchema,
                Handler = ListEntitiesHandler,
                RequiredScopes = requiredScopes,
                RequiredRoles = new List<string>(options.DefaultRequiredRoles),
                Version = options.ToolVersion,
                SupportsBatch = true
            };

            // Store entity type and options in metadata for use by the handler
            if (entityType != null)
            {
                tool.Metadata["EntityType"] = entityType;
                tool.Metadata["GenerationOptions"] = options;
            }

            if (options.IncludeExamples)
            {
                AddEntitySetExamples(tool, entitySet);
            }

            return await Task.FromResult(tool);
        }

        // Tool handler methods with REAL implementations
        internal async Task<McpToolResult> CreateEntityHandler(McpToolContext context, JsonDocument parameters)
        {
            try
            {
                // Extract entity type and set from context
                var entityTypeName = context.GetProperty<string>("TargetEntityType");
                var entitySetName = context.GetProperty<string>("TargetEntitySet");
                
                // If no entity set, try to derive it from entity type name
                if (string.IsNullOrWhiteSpace(entitySetName) && !string.IsNullOrWhiteSpace(entityTypeName))
                {
                    // Try plural form - this is a common convention
                    var typeName = entityTypeName.Split('.').Last();
                    entitySetName = typeName.EndsWith("y") ? typeName.Substring(0, typeName.Length - 1) + "ies" :
                                   typeName.EndsWith("s") ? typeName + "es" :
                                   typeName + "s";
                }
                
                if (string.IsNullOrWhiteSpace(entitySetName))
                {
                    return McpToolResult.ValidationError($"Entity set name not found in context", context.CorrelationId);
                }
                
                // Get the HTTP client
                var httpClient = _httpClientFactory.CreateClient("OData");
                
                // Build the URL
                var url = $"{context.ServiceBaseUrl?.TrimEnd('/')}/{entitySetName}";
                
                // Serialize the parameters as the entity data
                var jsonContent = parameters.RootElement.GetRawText();
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Make the POST request
                var response = await httpClient.PostAsync(url, content, context.CancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseDoc = JsonDocument.Parse(responseContent);
                    return McpToolResult.Success(responseDoc, context.CorrelationId);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create entity: {StatusCode} - {Error}", response.StatusCode, error);
                    return McpToolResult.Error($"Failed to create entity: {response.StatusCode}", response.StatusCode.ToString(), context.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating entity");
                return McpToolResult.Error(ex, context.CorrelationId);
            }
        }

        internal async Task<McpToolResult> ReadEntityHandler(McpToolContext context, JsonDocument parameters)
        {
            try
            {
                var entitySetName = context.GetProperty<string>("TargetEntitySet");
                
                if (string.IsNullOrWhiteSpace(entitySetName))
                {
                    return McpToolResult.ValidationError("Entity set name not found in context", context.CorrelationId);
                }
                
                // Get key properties from context metadata
                var keyProperties = context.GetProperty<List<string>>("KeyProperties") ?? new List<string>();
                if (keyProperties.Count == 0)
                {
                    return McpToolResult.ValidationError("No key properties found in entity metadata", context.CorrelationId);
                }
                
                // Check if parameters are wrapped in a "parameters" object
                var rootElement = parameters.RootElement;
                if (rootElement.TryGetProperty("parameters", out var paramsElement))
                {
                    rootElement = paramsElement;
                }
                
                // Extract key values from parameters
                var keyValues = new Dictionary<string, string>();
                foreach (var keyProp in keyProperties)
                {
                    if (rootElement.TryGetProperty(keyProp, out var keyElement))
                    {
                        var value = keyElement.ValueKind switch
                        {
                            JsonValueKind.String => keyElement.GetString(),
                            JsonValueKind.Number => keyElement.GetRawText(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            _ => keyElement.GetRawText()
                        };
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            keyValues[keyProp] = value;
                        }
                    }
                }
                
                // Validate we have all required keys
                if (keyValues.Count != keyProperties.Count)
                {
                    var missingKeys = keyProperties.Where(k => !keyValues.ContainsKey(k));
                    return McpToolResult.ValidationError($"Missing required key properties: {string.Join(", ", missingKeys)}", context.CorrelationId);
                }
                
                // Build the key string for OData URL
                string key;
                if (keyProperties.Count == 1)
                {
                    // Single key - just use the value
                    var keyValue = keyValues.Values.First();
                    // Quote string values
                    key = IsStringKey(keyValue) ? $"'{keyValue}'" : keyValue;
                }
                else
                {
                    // Composite key - format as Key1='value1',Key2='value2'
                    var keyParts = keyProperties.Select(kp =>
                    {
                        var value = keyValues[kp];
                        var quotedValue = IsStringKey(value) ? $"'{value}'" : value;
                        return $"{kp}={quotedValue}";
                    });
                    key = string.Join(",", keyParts);
                }
                
                if (string.IsNullOrWhiteSpace(key))
                {
                    return McpToolResult.ValidationError("Entity key is required", context.CorrelationId);
                }
                
                // Get the HTTP client
                var httpClient = _httpClientFactory.CreateClient("OData");
                
                // Build the URL
                var url = $"{context.ServiceBaseUrl?.TrimEnd('/')}/{entitySetName}({key})";
                
                // Add $select if specified (check both with and without $ prefix)
                if (rootElement.TryGetProperty("$select", out var selectElement) || 
                    rootElement.TryGetProperty("select", out selectElement))
                {
                    var select = selectElement.GetString();
                    if (!string.IsNullOrWhiteSpace(select))
                    {
                        url += $"?$select={Uri.EscapeDataString(select)}";
                    }
                }
                
                // Make the GET request
                var response = await httpClient.GetAsync(url, context.CancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseDoc = JsonDocument.Parse(responseContent);
                    return McpToolResult.Success(responseDoc, context.CorrelationId);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return McpToolResult.NotFound($"Entity with key '{key}' not found", context.CorrelationId);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to read entity: {StatusCode} - {Error}", response.StatusCode, error);
                    return McpToolResult.Error($"Failed to read entity: {response.StatusCode}", response.StatusCode.ToString(), context.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading entity");
                return McpToolResult.Error(ex, context.CorrelationId);
            }
        }

        internal async Task<McpToolResult> UpdateEntityHandler(McpToolContext context, JsonDocument parameters)
        {
            try
            {
                var entitySetName = context.GetProperty<string>("TargetEntitySet");
                
                if (string.IsNullOrWhiteSpace(entitySetName))
                {
                    return McpToolResult.ValidationError("Entity set name not found in context", context.CorrelationId);
                }
                
                // Get key properties from context metadata
                var keyProperties = context.GetProperty<List<string>>("KeyProperties") ?? new List<string>();
                if (keyProperties.Count == 0)
                {
                    return McpToolResult.ValidationError("No key properties found in entity metadata", context.CorrelationId);
                }
                
                // Check if parameters are wrapped in a "parameters" object
                var rootElement = parameters.RootElement;
                if (rootElement.TryGetProperty("parameters", out var paramsElement))
                {
                    rootElement = paramsElement;
                }
                
                // Extract key values from parameters
                var keyValues = new Dictionary<string, string>();
                foreach (var keyProp in keyProperties)
                {
                    if (rootElement.TryGetProperty(keyProp, out var keyElement))
                    {
                        var value = keyElement.ValueKind switch
                        {
                            JsonValueKind.String => keyElement.GetString(),
                            JsonValueKind.Number => keyElement.GetRawText(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            _ => keyElement.GetRawText()
                        };
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            keyValues[keyProp] = value;
                        }
                    }
                }
                
                // Validate we have all required keys
                if (keyValues.Count != keyProperties.Count)
                {
                    var missingKeys = keyProperties.Where(k => !keyValues.ContainsKey(k));
                    return McpToolResult.ValidationError($"Missing required key properties: {string.Join(", ", missingKeys)}", context.CorrelationId);
                }
                
                // Build the key string for OData URL
                string key;
                if (keyProperties.Count == 1)
                {
                    // Single key - just use the value
                    var keyValue = keyValues.Values.First();
                    // Quote string values
                    key = IsStringKey(keyValue) ? $"'{keyValue}'" : keyValue;
                }
                else
                {
                    // Composite key - format as Key1='value1',Key2='value2'
                    var keyParts = keyProperties.Select(kp =>
                    {
                        var value = keyValues[kp];
                        var quotedValue = IsStringKey(value) ? $"'{value}'" : value;
                        return $"{kp}={quotedValue}";
                    });
                    key = string.Join(",", keyParts);
                }
                
                if (string.IsNullOrWhiteSpace(key))
                {
                    return McpToolResult.ValidationError("Entity key is required for update", context.CorrelationId);
                }
                
                // Get the HTTP client
                var httpClient = _httpClientFactory.CreateClient("OData");
                
                // Build the URL
                var url = $"{context.ServiceBaseUrl?.TrimEnd('/')}/{entitySetName}({key})";
                
                // Extract ETag if provided
                string? etag = null;
                if (rootElement.TryGetProperty("@odata.etag", out var etagElement) ||
                    rootElement.TryGetProperty("etag", out etagElement) ||
                    rootElement.TryGetProperty("Etag", out etagElement))
                {
                    etag = etagElement.GetString();
                }
                
                // If no ETag provided, fetch the entity to get current ETag
                if (string.IsNullOrWhiteSpace(etag))
                {
                    try
                    {
                        var getResponse = await httpClient.GetAsync(url, context.CancellationToken);
                        if (getResponse.IsSuccessStatusCode)
                        {
                            var entityJson = await getResponse.Content.ReadAsStringAsync();
                            using var entityDoc = JsonDocument.Parse(entityJson);
                            
                            // Extract ETag from the fetched entity
                            if (entityDoc.RootElement.TryGetProperty("@odata.etag", out var fetchedEtagElement))
                            {
                                etag = fetchedEtagElement.GetString();
                                _logger.LogDebug("Auto-fetched ETag for update: {ETag}", etag);
                            }
                        }
                        // If fetch fails, continue without ETag (service may not require it)
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to auto-fetch ETag, continuing without it");
                    }
                }
                
                // Prepare the update data (exclude the key properties and metadata from the body)
                var updateData = new Dictionary<string, object>();
                foreach (var property in rootElement.EnumerateObject())
                {
                    // Exclude key properties, metadata properties, and ETag
                    if (!keyProperties.Contains(property.Name) && 
                        !property.Name.StartsWith("@") && 
                        !property.Name.StartsWith("$") && 
                        property.Name != "etag" &&
                        property.Name != "Etag" &&
                        property.Name != "parameters")
                    {
                        updateData[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString()!,
                            JsonValueKind.Number => property.Value.GetDecimal(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null!,
                            _ => JsonSerializer.Deserialize<object>(property.Value.GetRawText())!
                        };
                    }
                }
                
                var jsonContent = JsonSerializer.Serialize(updateData, JsonConstants.Default);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Use PATCH for partial updates
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = content
                };
                
                // Add If-Match header if we have an ETag
                if (!string.IsNullOrWhiteSpace(etag))
                {
                    request.Headers.Add("If-Match", etag);
                    _logger.LogDebug("Adding If-Match header with ETag: {ETag}", etag);
                }
                
                var response = await httpClient.SendAsync(request, context.CancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    // Some OData services return the updated entity, others return 204 No Content
                    if (response.Content.Headers.ContentLength > 0)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var responseDoc = JsonDocument.Parse(responseContent);
                        return McpToolResult.Success(responseDoc, context.CorrelationId);
                    }
                    else
                    {
                        return McpToolResult.Success(correlationId: context.CorrelationId);
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                {
                    return McpToolResult.Error(
                        "The entity has been modified by another user. Please retrieve the latest version and try again.",
                        "ETAG_MISMATCH",
                        context.CorrelationId);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.PreconditionRequired)
                {
                    return McpToolResult.Error(
                        "This service requires an ETag for updates. Please retrieve the entity first.",
                        "ETAG_REQUIRED",
                        context.CorrelationId);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to update entity: {StatusCode} - {Error}", response.StatusCode, error);
                    return McpToolResult.Error($"Failed to update entity: {response.StatusCode}", response.StatusCode.ToString(), context.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity");
                return McpToolResult.Error(ex, context.CorrelationId);
            }
        }

        internal async Task<McpToolResult> DeleteEntityHandler(McpToolContext context, JsonDocument parameters)
        {
            try
            {
                var entitySetName = context.GetProperty<string>("TargetEntitySet");
                
                if (string.IsNullOrWhiteSpace(entitySetName))
                {
                    return McpToolResult.ValidationError("Entity set name not found in context", context.CorrelationId);
                }
                
                // Get key properties from context metadata
                var keyProperties = context.GetProperty<List<string>>("KeyProperties") ?? new List<string>();
                if (keyProperties.Count == 0)
                {
                    return McpToolResult.ValidationError("No key properties found in entity metadata", context.CorrelationId);
                }
                
                // Check if parameters are wrapped in a "parameters" object
                var rootElement = parameters.RootElement;
                if (rootElement.TryGetProperty("parameters", out var paramsElement))
                {
                    rootElement = paramsElement;
                }
                
                // Extract key values from parameters
                var keyValues = new Dictionary<string, string>();
                foreach (var keyProp in keyProperties)
                {
                    if (rootElement.TryGetProperty(keyProp, out var keyElement))
                    {
                        var value = keyElement.ValueKind switch
                        {
                            JsonValueKind.String => keyElement.GetString(),
                            JsonValueKind.Number => keyElement.GetRawText(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            _ => keyElement.GetRawText()
                        };
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            keyValues[keyProp] = value;
                        }
                    }
                }
                
                // Validate we have all required keys
                if (keyValues.Count != keyProperties.Count)
                {
                    var missingKeys = keyProperties.Where(k => !keyValues.ContainsKey(k));
                    return McpToolResult.ValidationError($"Missing required key properties: {string.Join(", ", missingKeys)}", context.CorrelationId);
                }
                
                // Build the key string for OData URL
                string key;
                if (keyProperties.Count == 1)
                {
                    // Single key - just use the value
                    var keyValue = keyValues.Values.First();
                    // Quote string values
                    key = IsStringKey(keyValue) ? $"'{keyValue}'" : keyValue;
                }
                else
                {
                    // Composite key - format as Key1='value1',Key2='value2'
                    var keyParts = keyProperties.Select(kp =>
                    {
                        var value = keyValues[kp];
                        var quotedValue = IsStringKey(value) ? $"'{value}'" : value;
                        return $"{kp}={quotedValue}";
                    });
                    key = string.Join(",", keyParts);
                }
                
                if (string.IsNullOrWhiteSpace(key))
                {
                    return McpToolResult.ValidationError("Entity key is required for delete", context.CorrelationId);
                }
                
                // Get the HTTP client
                var httpClient = _httpClientFactory.CreateClient("OData");
                
                // Build the URL
                var url = $"{context.ServiceBaseUrl?.TrimEnd('/')}/{entitySetName}({key})";
                
                // Extract ETag if provided
                string? etag = null;
                if (rootElement.TryGetProperty("@odata.etag", out var etagElement) ||
                    rootElement.TryGetProperty("etag", out etagElement) ||
                    rootElement.TryGetProperty("Etag", out etagElement))
                {
                    etag = etagElement.GetString();
                }
                
                // If no ETag provided, fetch the entity to get current ETag
                if (string.IsNullOrWhiteSpace(etag))
                {
                    try
                    {
                        var getResponse = await httpClient.GetAsync(url, context.CancellationToken);
                        if (getResponse.IsSuccessStatusCode)
                        {
                            var entityJson = await getResponse.Content.ReadAsStringAsync();
                            using var entityDoc = JsonDocument.Parse(entityJson);
                            
                            // Extract ETag from the fetched entity
                            if (entityDoc.RootElement.TryGetProperty("@odata.etag", out var fetchedEtagElement))
                            {
                                etag = fetchedEtagElement.GetString();
                                _logger.LogDebug("Auto-fetched ETag for delete: {ETag}", etag);
                            }
                        }
                        // If fetch fails, continue without ETag (service may not require it)
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to auto-fetch ETag for delete, continuing without it");
                    }
                }
                
                // Create DELETE request
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                
                // Add If-Match header if we have an ETag
                if (!string.IsNullOrWhiteSpace(etag))
                {
                    request.Headers.Add("If-Match", etag);
                    _logger.LogDebug("Adding If-Match header for delete with ETag: {ETag}", etag);
                }
                
                // Make the DELETE request
                var response = await httpClient.SendAsync(request, context.CancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    return McpToolResult.Success(correlationId: context.CorrelationId);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return McpToolResult.NotFound($"Entity with key '{key}' not found", context.CorrelationId);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                {
                    return McpToolResult.Error(
                        "The entity has been modified by another user. Please retrieve the latest version and try again.",
                        "ETAG_MISMATCH",
                        context.CorrelationId);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.PreconditionRequired)
                {
                    return McpToolResult.Error(
                        "This service requires an ETag for deletion. Please retrieve the entity first.",
                        "ETAG_REQUIRED",
                        context.CorrelationId);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to delete entity: {StatusCode} - {Error}", response.StatusCode, error);
                    return McpToolResult.Error($"Failed to delete entity: {response.StatusCode}", response.StatusCode.ToString(), context.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity");
                return McpToolResult.Error(ex, context.CorrelationId);
            }
        }

        internal async Task<McpToolResult> QueryEntityHandler(McpToolContext context, JsonDocument parameters)
        {
            try
            {
                // This is a general query handler - can be used for any entity set
                var entitySet = parameters.RootElement.TryGetProperty("entitySet", out var entitySetElement) 
                    ? entitySetElement.GetString() 
                    : context.GetProperty<string>("TargetEntitySet");
                    
                if (string.IsNullOrWhiteSpace(entitySet))
                {
                    return McpToolResult.ValidationError("Entity set name is required", context.CorrelationId);
                }
                
                // Get the HTTP client
                var httpClient = _httpClientFactory.CreateClient("OData");
                
                // Build the URL with query parameters
                var queryBuilder = new UriBuilder($"{context.ServiceBaseUrl?.TrimEnd('/')}/{entitySet}");
                var queryParams = new List<string>();
                
                // Add OData query options
                if (parameters.RootElement.TryGetProperty("$filter", out var filterElement))
                {
                    var filter = filterElement.GetString();
                    if (!string.IsNullOrWhiteSpace(filter))
                        queryParams.Add($"$filter={Uri.EscapeDataString(filter)}");
                }
                
                if (parameters.RootElement.TryGetProperty("$orderby", out var orderbyElement))
                {
                    var orderby = orderbyElement.GetString();
                    if (!string.IsNullOrWhiteSpace(orderby))
                        queryParams.Add($"$orderby={Uri.EscapeDataString(orderby)}");
                }
                
                if (parameters.RootElement.TryGetProperty("$select", out var selectElement))
                {
                    var select = selectElement.GetString();
                    if (!string.IsNullOrWhiteSpace(select))
                        queryParams.Add($"$select={Uri.EscapeDataString(select)}");
                }
                
                if (parameters.RootElement.TryGetProperty("$expand", out var expandElement))
                {
                    var expand = expandElement.GetString();
                    if (!string.IsNullOrWhiteSpace(expand))
                        queryParams.Add($"$expand={Uri.EscapeDataString(expand)}");
                }
                
                if (parameters.RootElement.TryGetProperty("$top", out var topElement))
                {
                    queryParams.Add($"$top={topElement.GetInt32()}");
                }
                
                if (parameters.RootElement.TryGetProperty("$skip", out var skipElement))
                {
                    queryParams.Add($"$skip={skipElement.GetInt32()}");
                }
                
                if (parameters.RootElement.TryGetProperty("$count", out var countElement) && countElement.GetBoolean())
                {
                    queryParams.Add("$count=true");
                }
                
                if (queryParams.Count > 0)
                {
                    queryBuilder.Query = string.Join("&", queryParams);
                }
                
                // Make the GET request
                var response = await httpClient.GetAsync(queryBuilder.Uri, context.CancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseDoc = JsonDocument.Parse(responseContent);
                    return McpToolResult.Success(responseDoc, context.CorrelationId);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to query entities: {StatusCode} - {Error}", response.StatusCode, error);
                    return McpToolResult.Error($"Failed to query entities: {response.StatusCode}", response.StatusCode.ToString(), context.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying entities");
                return McpToolResult.Error(ex, context.CorrelationId);
            }
        }

        internal async Task<McpToolResult> NavigateEntityHandler(McpToolContext context, JsonDocument parameters)
        {
            try
            {
                var entitySetName = context.GetProperty<string>("TargetEntitySet");
                
                if (string.IsNullOrWhiteSpace(entitySetName))
                {
                    return McpToolResult.ValidationError("Entity set name not found in context", context.CorrelationId);
                }
                
                // Extract source entity key
                string? key = null;
                if (parameters.RootElement.TryGetProperty("id", out var idElement))
                {
                    key = idElement.GetString();
                }
                else if (parameters.RootElement.TryGetProperty("key", out var keyElement))
                {
                    key = keyElement.GetString();
                }
                
                if (string.IsNullOrWhiteSpace(key))
                {
                    return McpToolResult.ValidationError("Entity key is required for navigation", context.CorrelationId);
                }
                
                // Extract navigation property name
                string? navigationProperty = null;
                if (parameters.RootElement.TryGetProperty("navigationProperty", out var navPropElement))
                {
                    navigationProperty = navPropElement.GetString();
                }
                else if (parameters.RootElement.TryGetProperty("property", out var propElement))
                {
                    navigationProperty = propElement.GetString();
                }
                
                if (string.IsNullOrWhiteSpace(navigationProperty))
                {
                    return McpToolResult.ValidationError("Navigation property name is required", context.CorrelationId);
                }
                
                // Get the HTTP client
                var httpClient = _httpClientFactory.CreateClient("OData");
                
                // Build the URL for navigation
                var url = $"{context.ServiceBaseUrl?.TrimEnd('/')}/{entitySetName}({key})/{navigationProperty}";
                
                // Add query options if specified
                var queryParams = new List<string>();
                
                if (parameters.RootElement.TryGetProperty("$select", out var selectElement))
                {
                    var select = selectElement.GetString();
                    if (!string.IsNullOrWhiteSpace(select))
                        queryParams.Add($"$select={Uri.EscapeDataString(select)}");
                }
                
                if (parameters.RootElement.TryGetProperty("$expand", out var expandElement))
                {
                    var expand = expandElement.GetString();
                    if (!string.IsNullOrWhiteSpace(expand))
                        queryParams.Add($"$expand={Uri.EscapeDataString(expand)}");
                }
                
                if (parameters.RootElement.TryGetProperty("$filter", out var filterElement))
                {
                    var filter = filterElement.GetString();
                    if (!string.IsNullOrWhiteSpace(filter))
                        queryParams.Add($"$filter={Uri.EscapeDataString(filter)}");
                }
                
                if (parameters.RootElement.TryGetProperty("$top", out var topElement))
                {
                    queryParams.Add($"$top={topElement.GetInt32()}");
                }
                
                if (queryParams.Count > 0)
                {
                    url += "?" + string.Join("&", queryParams);
                }
                
                // Make the GET request
                var response = await httpClient.GetAsync(url, context.CancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseDoc = JsonDocument.Parse(responseContent);
                    return McpToolResult.Success(responseDoc, context.CorrelationId);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return McpToolResult.NotFound($"Entity with key '{key}' or navigation property '{navigationProperty}' not found", context.CorrelationId);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to navigate entity: {StatusCode} - {Error}", response.StatusCode, error);
                    return McpToolResult.Error($"Failed to navigate entity: {response.StatusCode}", response.StatusCode.ToString(), context.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating entity");
                return McpToolResult.Error(ex, context.CorrelationId);
            }
        }

        internal async Task<McpToolResult> ListEntitiesHandler(McpToolContext context, JsonDocument parameters)
        {
            try
            {
                // Get the entity set name from context or parameters
                var entitySetName = context.GetProperty<string>("TargetEntitySet");
                
                if (string.IsNullOrWhiteSpace(entitySetName) && parameters.RootElement.TryGetProperty("entitySet", out var entitySetElement))
                {
                    entitySetName = entitySetElement.GetString();
                }
                
                if (string.IsNullOrWhiteSpace(entitySetName))
                {
                    return McpToolResult.ValidationError("Entity set name is required", context.CorrelationId);
                }
                
                // Get the HTTP client
                var httpClient = _httpClientFactory.CreateClient("OData");
                
                // Build the URL with query parameters
                var queryBuilder = new UriBuilder($"{context.ServiceBaseUrl?.TrimEnd('/')}/{entitySetName}");
                var queryParams = new List<string>();
                
                // Add OData query options
                if (parameters.RootElement.TryGetProperty("$filter", out var filterElement))
                {
                    var filter = filterElement.GetString();
                    if (!string.IsNullOrWhiteSpace(filter))
                        queryParams.Add($"$filter={Uri.EscapeDataString(filter)}");
                }
                
                if (parameters.RootElement.TryGetProperty("$orderby", out var orderbyElement))
                {
                    var orderby = orderbyElement.GetString();
                    if (!string.IsNullOrWhiteSpace(orderby))
                        queryParams.Add($"$orderby={Uri.EscapeDataString(orderby)}");
                }
                
                // Handle $select with binary field exclusion
                bool selectSpecified = false;
                if (parameters.RootElement.TryGetProperty("$select", out var selectElement))
                {
                    var select = selectElement.GetString();
                    if (!string.IsNullOrWhiteSpace(select))
                    {
                        queryParams.Add($"$select={Uri.EscapeDataString(select)}");
                        selectSpecified = true;
                    }
                }
                
                // If no $select was specified, build a default one excluding binary fields
                if (!selectSpecified)
                {
                    // Try to get the entity type from context metadata
                    var entityType = context.GetProperty<EdmEntityType>("EntityType");
                    var options = context.GetProperty<McpToolGenerationOptions>("GenerationOptions");
                    
                    if (entityType != null)
                    {
                        // Use the provided options or default ones
                        var defaultSelect = BuildDefaultSelectForEntityType(entityType, options ?? McpToolGenerationOptions.Default());
                        if (!string.IsNullOrWhiteSpace(defaultSelect))
                        {
                            queryParams.Add($"$select={Uri.EscapeDataString(defaultSelect)}");
                            _logger.LogDebug("Applied default $select excluding binary fields: {Select}", defaultSelect);
                        }
                    }
                }
                
                if (parameters.RootElement.TryGetProperty("$expand", out var expandElement))
                {
                    var expand = expandElement.GetString();
                    if (!string.IsNullOrWhiteSpace(expand))
                        queryParams.Add($"$expand={Uri.EscapeDataString(expand)}");
                }
                
                if (parameters.RootElement.TryGetProperty("$top", out var topElement))
                {
                    queryParams.Add($"$top={topElement.GetInt32()}");
                }
                else
                {
                    // Default to top 20 if not specified to avoid huge responses
                    queryParams.Add("$top=20");
                }
                
                if (parameters.RootElement.TryGetProperty("$skip", out var skipElement))
                {
                    queryParams.Add($"$skip={skipElement.GetInt32()}");
                }
                
                if (parameters.RootElement.TryGetProperty("$count", out var countElement) && countElement.GetBoolean())
                {
                    queryParams.Add("$count=true");
                }
                
                // Add search if specified
                if (parameters.RootElement.TryGetProperty("$search", out var searchElement))
                {
                    var search = searchElement.GetString();
                    if (!string.IsNullOrWhiteSpace(search))
                        queryParams.Add($"$search={Uri.EscapeDataString(search)}");
                }
                
                if (queryParams.Count > 0)
                {
                    queryBuilder.Query = string.Join("&", queryParams);
                }
                
                _logger.LogDebug("Listing entities from {EntitySet} with URL: {Url}", entitySetName, queryBuilder.Uri);
                
                // Make the GET request
                var response = await httpClient.GetAsync(queryBuilder.Uri, context.CancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseDoc = JsonDocument.Parse(responseContent);
                    
                    // Log the count if available
                    if (responseDoc.RootElement.TryGetProperty("@odata.count", out var countProp))
                    {
                        _logger.LogDebug("Total count of entities: {Count}", countProp.GetInt32());
                    }
                    
                    return McpToolResult.Success(responseDoc, context.CorrelationId);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to list entities: {StatusCode} - {Error}", response.StatusCode, error);
                    return McpToolResult.Error($"Failed to list entities: {response.StatusCode}", response.StatusCode.ToString(), context.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing entities");
                return McpToolResult.Error(ex, context.CorrelationId);
            }
        }

        // Schema generation methods (these would generate appropriate JSON schemas)
        internal JsonDocument GenerateEntityInputSchema(EdmEntityType entityType, bool required = false)
        {
            var properties = new Dictionary<string, object>();
            var requiredProperties = new List<string>();

            // Add all properties from the entity type
            foreach (var property in entityType.Properties)
            {
                var propertySchema = new Dictionary<string, object>
                {
                    ["type"] = MapEdmTypeToJsonType(property.Type),
                    ["description"] = $"The {property.Name} property of type {property.Type}"
                };

                // Add nullable indicator if applicable
                if (property.Nullable && propertySchema["type"] is string type && type != "null")
                {
                    propertySchema["type"] = new[] { type, "null" };
                }

                properties[property.Name] = propertySchema;

                // For create operations, key properties and non-nullable properties are required
                if (required && (property.IsKey || !property.Nullable))
                {
                    requiredProperties.Add(property.Name);
                }
            }

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["description"] = $"Input schema for creating or updating {entityType.Name} entities",
                ["properties"] = properties
            };

            if (requiredProperties.Count > 0)
            {
                schema["required"] = requiredProperties;
            }

            var schemaJson = JsonSerializer.Serialize(schema, JsonConstants.Default);
            return JsonDocument.Parse(schemaJson);
        }

        internal JsonDocument GenerateKeyInputSchema(EdmEntityType entityType)
        {
            var properties = new Dictionary<string, object>();
            var requiredProperties = new List<string>();

            // Add only key properties from the entity type
            var keyProperties = entityType.Properties.Where(p => entityType.Key.Contains(p.Name)).ToList();
            
            foreach (var property in keyProperties)
            {
                var propertySchema = new Dictionary<string, object>
                {
                    ["type"] = MapEdmTypeToJsonType(property.Type),
                    ["description"] = $"The key property {property.Name} of type {property.Type}"
                };

                properties[property.Name] = propertySchema;
                requiredProperties.Add(property.Name); // All key properties are required
            }

            // Add optional $select parameter for read operations
            properties["$select"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Comma-separated list of properties to select (optional)"
            };

            // Add optional ETag parameter for delete operations with optimistic concurrency
            properties["@odata.etag"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "ETag for optimistic concurrency (optional). If not provided, current ETag will be fetched for delete operations."
            };

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["description"] = keyProperties.Count > 1 
                    ? $"Composite key schema for {entityType.Name} with keys: {string.Join(", ", requiredProperties)}"
                    : $"Key schema for {entityType.Name} with key: {requiredProperties.FirstOrDefault()}",
                ["properties"] = properties,
                ["required"] = requiredProperties
            };

            var schemaJson = JsonSerializer.Serialize(schema, JsonConstants.Default);
            return JsonDocument.Parse(schemaJson);
        }

        internal JsonDocument GenerateEntityUpdateSchema(EdmEntityType entityType)
        {
            var properties = new Dictionary<string, object>();
            var requiredProperties = new List<string>();

            // Add key properties as required (for identification)
            var keyProperties = entityType.Properties.Where(p => entityType.Key.Contains(p.Name)).ToList();
            foreach (var property in keyProperties)
            {
                var propertySchema = new Dictionary<string, object>
                {
                    ["type"] = MapEdmTypeToJsonType(property.Type),
                    ["description"] = $"The key property {property.Name} (required for identification)"
                };

                properties[property.Name] = propertySchema;
                requiredProperties.Add(property.Name);
            }

            // Add non-key properties as optional (for partial updates)
            var nonKeyProperties = entityType.Properties.Where(p => !entityType.Key.Contains(p.Name)).ToList();
            foreach (var property in nonKeyProperties)
            {
                var propertySchema = new Dictionary<string, object>
                {
                    ["type"] = MapEdmTypeToJsonType(property.Type),
                    ["description"] = $"The {property.Name} property (optional for update)"
                };

                // Add nullable indicator
                if (property.Nullable && propertySchema["type"] is string type && type != "null")
                {
                    propertySchema["type"] = new[] { type, "null" };
                }

                properties[property.Name] = propertySchema;
                // Non-key properties are optional for updates
            }

            // Add optional ETag parameter for optimistic concurrency
            properties["@odata.etag"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "ETag for optimistic concurrency. If not provided, current ETag will be fetched automatically."
            };

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["description"] = $"Update schema for {entityType.Name} entities (key properties required, others optional)",
                ["properties"] = properties,
                ["required"] = requiredProperties,
                ["additionalProperties"] = false
            };

            var schemaJson = JsonSerializer.Serialize(schema, JsonConstants.Default);
            return JsonDocument.Parse(schemaJson);
        }

        /// <summary>
        /// Determines if a key value should be quoted as a string in OData URLs.
        /// </summary>
        /// <param name="value">The key value to check.</param>
        /// <returns>True if the value should be quoted; otherwise, false.</returns>
        internal static bool IsStringKey(string value)
        {
            // If it starts with a quote, assume it's already quoted
            if (value.StartsWith("'") || value.StartsWith("\""))
            {
                return false;
            }
            
            // Try to parse as number
            if (int.TryParse(value, out _) || long.TryParse(value, out _) || 
                decimal.TryParse(value, out _) || double.TryParse(value, out _))
            {
                return false;
            }
            
            // Try to parse as boolean
            if (bool.TryParse(value, out _))
            {
                return false;
            }
            
            // Try to parse as GUID (GUIDs need quotes in OData)
            if (Guid.TryParse(value, out _))
            {
                return true;
            }
            
            // Everything else is treated as a string
            return true;
        }

        /// <summary>
        /// Determines if a property is a binary or stream field that should be excluded by default.
        /// </summary>
        /// <param name="property">The property to check.</param>
        /// <returns>True if the property is a binary or stream field; otherwise, false.</returns>
        internal static bool IsBinaryOrStreamField(EdmProperty property)
        {
            if (property == null || string.IsNullOrWhiteSpace(property.Type))
            {
                return false;
            }

            var type = property.Type.ToLowerInvariant();
            return type.Contains("edm.binary") || 
                   type.Contains("edm.stream") || 
                   type == "binary" || 
                   type == "stream";
        }

        /// <summary>
        /// Builds a default $select statement for an entity type, excluding binary and stream fields.
        /// </summary>
        /// <param name="entityType">The entity type to build the select for.</param>
        /// <param name="options">The generation options.</param>
        /// <returns>A comma-separated list of property names to select, or null if all properties should be included.</returns>
        internal static string? BuildDefaultSelectForEntityType(EdmEntityType entityType, McpToolGenerationOptions? options = null)
        {
            if (entityType == null || entityType.Properties == null || entityType.Properties.Count == 0)
            {
                return null;
            }

            // If binary exclusion is disabled, return null to select all properties
            if (options != null && !options.ExcludeBinaryFieldsByDefault)
            {
                return null;
            }

            var selectableProperties = entityType.Properties
                .Where(p => !IsBinaryOrStreamField(p))
                .Select(p => p.Name)
                .ToList();

            // If all properties are being selected anyway, return null
            if (selectableProperties.Count == entityType.Properties.Count)
            {
                return null;
            }

            // Return the filtered list
            return selectableProperties.Count > 0 ? string.Join(",", selectableProperties) : null;
        }

        /// <summary>
        /// Maps EDM type names to JSON Schema type names.
        /// </summary>
        /// <param name="edmType">The EDM type name.</param>
        /// <returns>The corresponding JSON Schema type.</returns>
        internal static string MapEdmTypeToJsonType(string? edmType)
        {
            if (string.IsNullOrWhiteSpace(edmType))
            {
                return "string";
            }

            // Remove Edm. prefix if present
            var typeName = edmType.StartsWith("Edm.") ? edmType.Substring(4) : edmType;

            return typeName.ToLowerInvariant() switch
            {
                "string" => "string",
                "int16" => "integer",
                "int32" => "integer",
                "int64" => "integer",
                "decimal" => "number",
                "double" => "number",
                "single" => "number",
                "boolean" => "boolean",
                "datetime" => "string",
                "datetimeoffset" => "string",
                "date" => "string",
                "timeofday" => "string",
                "guid" => "string",
                "binary" => "string",
                "byte" => "integer",
                "sbyte" => "integer",
                _ => "string" // Default to string for unknown types
            };
        }

        internal JsonDocument GenerateQueryInputSchema()
        {
            // Implementation would generate JSON schema for OData queries
            var schema = JsonSerializer.Serialize(new { type = "object", description = "OData query parameters" }, JsonConstants.Default);
            return JsonDocument.Parse(schema);
        }

        internal JsonDocument GenerateNavigationInputSchema(EdmEntityType entityType, EdmNavigationProperty navProperty)
        {
            // Implementation would generate JSON schema for navigation
            var schema = JsonSerializer.Serialize(new { type = "object", description = $"Navigation schema for {navProperty.Name}" }, JsonConstants.Default);
            return JsonDocument.Parse(schema);
        }

        internal JsonDocument GenerateEntitySetQuerySchema()
        {
            // Implementation would generate JSON schema for entity set queries
            var schema = JsonSerializer.Serialize(new { type = "object", description = "Entity set query parameters" }, JsonConstants.Default);
            return JsonDocument.Parse(schema);
        }

        // Example generation methods (these would add appropriate examples)
        internal void AddCreateExamples(McpToolDefinition tool, EdmEntityType entityType) { }
        internal void AddReadExamples(McpToolDefinition tool, EdmEntityType entityType) { }
        internal void AddUpdateExamples(McpToolDefinition tool, EdmEntityType entityType) { }
        internal void AddDeleteExamples(McpToolDefinition tool, EdmEntityType entityType) { }
        internal void AddQueryExamples(McpToolDefinition tool, EdmModel model) { }
        internal void AddNavigationExamples(McpToolDefinition tool, EdmEntityType entityType, EdmNavigationProperty navProperty) { }
        internal void AddEntitySetExamples(McpToolDefinition tool, EdmEntitySet entitySet) { }

        #endregion

    }

}
