using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Tools.Generators
{
    /// <summary>
    /// Generates navigation MCP tools from OData entity relationships.
    /// </summary>
    /// <remarks>
    /// This generator creates MCP tools that allow AI models to traverse entity relationships
    /// and work with related entities. It supports getting related entities, adding relationships,
    /// and removing relationships for both collection and single navigation properties.
    /// </remarks>
    public sealed class NavigationToolGenerator : INavigationToolGenerator
    {
        #region Fields

        private readonly ILogger<NavigationToolGenerator> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationToolGenerator"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public NavigationToolGenerator(ILogger<NavigationToolGenerator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region INavigationToolGenerator Implementation

        /// <summary>
        /// Generates all navigation tools for the specified entity set and its relationships.
        /// </summary>
        /// <param name="entitySet">The entity set to generate tools for.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A collection of generated MCP tools for navigation operations.</returns>
        public async Task<IEnumerable<McpTool>> GenerateAllNavigationToolsAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            NavigationToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(entitySet);
            ArgumentNullException.ThrowIfNull(entityType);
            ArgumentNullException.ThrowIfNull(options);
#else
            if (entitySet is null) throw new ArgumentNullException(nameof(entitySet));
            if (entityType is null) throw new ArgumentNullException(nameof(entityType));
            if (options is null) throw new ArgumentNullException(nameof(options));
#endif

            _logger.LogDebug("Generating navigation tools for entity set {EntitySet} with type {EntityType}",
                entitySet.Name, entityType.Name);

            var tools = new List<McpTool>();

            try
            {
                var eligibleNavigationProperties = GetEligibleNavigationProperties(entityType, options);

                foreach (var navigationProperty in eligibleNavigationProperties)
                {
                    // Skip based on multiplicity settings
                    if (navigationProperty.IsCollection && !options.IncludeCollectionNavigations)
                        continue;
                    if (!navigationProperty.IsCollection && !options.IncludeSingleNavigations)
                        continue;

                    // Generate get related tools
                    if (options.GenerateGetRelatedTools)
                    {
                        var getRelatedTool = await GenerateGetRelatedToolAsync(
                            entitySet, entityType, navigationProperty, options, cancellationToken);
                        tools.Add(getRelatedTool);
                    }

                    // Generate add relationship tools (for collection navigations or editable single navigations)
                    if (options.GenerateAddRelationshipTools && 
                        (navigationProperty.IsCollection || !navigationProperty.IsRequired))
                    {
                        var addRelationshipTool = await GenerateAddRelationshipToolAsync(
                            entitySet, entityType, navigationProperty, options, cancellationToken);
                        tools.Add(addRelationshipTool);
                    }

                    // Generate remove relationship tools (for collection navigations or nullable single navigations)
                    if (options.GenerateRemoveRelationshipTools && 
                        (navigationProperty.IsCollection || !navigationProperty.IsRequired))
                    {
                        var removeRelationshipTool = await GenerateRemoveRelationshipToolAsync(
                            entitySet, entityType, navigationProperty, options, cancellationToken);
                        tools.Add(removeRelationshipTool);
                    }
                }

                _logger.LogInformation("Generated {ToolCount} navigation tools for entity set {EntitySet}",
                    tools.Count, entitySet.Name);

                return tools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating navigation tools for entity set {EntitySet}", entitySet.Name);
                throw;
            }
        }

        /// <summary>
        /// Generates a tool for getting related entities via navigation properties.
        /// </summary>
        /// <param name="entitySet">The source entity set.</param>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navigationProperty">The navigation property to traverse.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A navigation MCP tool for getting related entities.</returns>
        public Task<McpTool> GenerateGetRelatedToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            EdmNavigationProperty navigationProperty,
            NavigationToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var toolName = FormatToolName("Get", entityType.Name, navigationProperty.Name, options.NamingConvention);
            var description = GenerateGetRelatedDescription(entitySet, entityType, navigationProperty, options);
            var inputSchema = GenerateGetRelatedInputSchema(entityType, navigationProperty, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated GET RELATED tool {ToolName} for navigation {NavigationProperty}",
                toolName, navigationProperty.Name);

            return Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a tool for adding relationships between entities.
        /// </summary>
        /// <param name="entitySet">The source entity set.</param>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navigationProperty">The navigation property for the relationship.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A navigation MCP tool for adding relationships.</returns>
        public Task<McpTool> GenerateAddRelationshipToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            EdmNavigationProperty navigationProperty,
            NavigationToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var operation = navigationProperty.IsCollection ? "AddTo" : "Set";
            var toolName = FormatToolName(operation, entityType.Name, navigationProperty.Name, options.NamingConvention);
            var description = GenerateAddRelationshipDescription(entitySet, entityType, navigationProperty, options);
            var inputSchema = GenerateAddRelationshipInputSchema(entityType, navigationProperty, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated ADD RELATIONSHIP tool {ToolName} for navigation {NavigationProperty}",
                toolName, navigationProperty.Name);

            return Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a tool for removing relationships between entities.
        /// </summary>
        /// <param name="entitySet">The source entity set.</param>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navigationProperty">The navigation property for the relationship.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A navigation MCP tool for removing relationships.</returns>
        public Task<McpTool> GenerateRemoveRelationshipToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            EdmNavigationProperty navigationProperty,
            NavigationToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var operation = navigationProperty.IsCollection ? "RemoveFrom" : "Unset";
            var toolName = FormatToolName(operation, entityType.Name, navigationProperty.Name, options.NamingConvention);
            var description = GenerateRemoveRelationshipDescription(entitySet, entityType, navigationProperty, options);
            var inputSchema = GenerateRemoveRelationshipInputSchema(entityType, navigationProperty, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated REMOVE RELATIONSHIP tool {ToolName} for navigation {NavigationProperty}",
                toolName, navigationProperty.Name);

            return Task.FromResult(tool);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Formats a tool name according to the specified naming convention.
        /// </summary>
        /// <param name="operation">The navigation operation (Get, AddTo, RemoveFrom, Set, Unset).</param>
        /// <param name="entityName">The source entity type name.</param>
        /// <param name="navigationName">The navigation property name.</param>
        /// <param name="convention">The naming convention to apply.</param>
        /// <returns>The formatted tool name.</returns>
        private static string FormatToolName(string operation, string entityName, string navigationName, ToolNamingConvention convention)
        {
            var baseName = $"{operation}{entityName}{navigationName}";

            return convention switch
            {
                ToolNamingConvention.PascalCase => baseName,
                ToolNamingConvention.CamelCase => char.ToLowerInvariant(baseName[0]) + baseName[1..],
                ToolNamingConvention.SnakeCase => ConvertToSnakeCase(baseName),
                ToolNamingConvention.KebabCase => ConvertToKebabCase(baseName),
                _ => baseName
            };
        }

        /// <summary>
        /// Converts a string to snake_case.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The snake_case version of the string.</returns>
        private static string ConvertToSnakeCase(string input)
        {
            var result = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (i > 0 && char.IsUpper(input[i]))
                {
                    result.Append('_');
                }
                result.Append(char.ToLowerInvariant(input[i]));
            }
            return result.ToString();
        }

        /// <summary>
        /// Converts a string to kebab-case.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The kebab-case version of the string.</returns>
        private static string ConvertToKebabCase(string input)
        {
            var result = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (i > 0 && char.IsUpper(input[i]))
                {
                    result.Append('-');
                }
                result.Append(char.ToLowerInvariant(input[i]));
            }
            return result.ToString();
        }

        /// <summary>
        /// Gets the navigation properties eligible for tool generation.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A collection of eligible navigation properties.</returns>
        private static IEnumerable<EdmNavigationProperty> GetEligibleNavigationProperties(EdmEntityType entityType, NavigationToolGenerationOptions options)
        {
            var navigationProperties = entityType.NavigationProperties.AsEnumerable();

            // Filter out excluded navigation properties
            if (options.ExcludedNavigationProperties.TryGetValue(entityType.Name, out var excludedNavProps))
            {
                navigationProperties = navigationProperties.Where(np => !excludedNavProps.Contains(np.Name));
            }

            return navigationProperties;
        }

        /// <summary>
        /// Generates a description for a GET RELATED tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="navigationProperty">The navigation property.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the GET RELATED tool.</returns>
        private static string GenerateGetRelatedDescription(EdmEntitySet entitySet, EdmEntityType entityType, EdmNavigationProperty navigationProperty, NavigationToolGenerationOptions options)
        {
            var description = new StringBuilder();
            var relatedType = navigationProperty.IsCollection ? $"{navigationProperty.TargetTypeName} entities" : $"{navigationProperty.TargetTypeName} entity";
            
            description.Append($"Gets {relatedType} related to a {entityType.Name} via the {navigationProperty.Name} navigation property.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"This tool allows you to retrieve {relatedType} that are associated with a specific {entityType.Name} entity. ");

                var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
                if (keyProperties.Count > 0)
                {
                    description.Append($"You must provide the key values ({string.Join(", ", keyProperties.Select(p => p.Name))}) to identify the source {entityType.Name} entity. ");
                }

                if (navigationProperty.IsCollection && options.SupportQueryOptions)
                {
                    var supportedOptions = new List<string>();
                    if (options.SupportFilter) supportedOptions.Add("filtering");
                    if (options.SupportOrderBy) supportedOptions.Add("sorting");
                    if (options.SupportTop) supportedOptions.Add("limiting results");

                    if (supportedOptions.Count > 0)
                    {
                        description.Append($"For collection navigations, you can apply {string.Join(", ", supportedOptions)}. ");
                    }
                }

                description.Append($"The tool returns the related {relatedType} with all their properties and values.");
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"Example usage: Get the {navigationProperty.Name} for a specific {entityType.Name} by providing its key values.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates a description for an ADD RELATIONSHIP tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="navigationProperty">The navigation property.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the ADD RELATIONSHIP tool.</returns>
        private static string GenerateAddRelationshipDescription(EdmEntitySet entitySet, EdmEntityType entityType, EdmNavigationProperty navigationProperty, NavigationToolGenerationOptions options)
        {
            var description = new StringBuilder();
            var operation = navigationProperty.IsCollection ? "Adds a relationship" : "Sets the relationship";
            var relatedType = navigationProperty.TargetTypeName;
            
            description.Append($"{operation} between a {entityType.Name} and a {relatedType} via the {navigationProperty.Name} navigation property.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                
                if (navigationProperty.IsCollection)
                {
                    description.Append($"This tool allows you to add a {relatedType} entity to the {navigationProperty.Name} collection of a {entityType.Name} entity. ");
                    description.Append("The relationship is created without affecting existing relationships in the collection. ");
                }
                else
                {
                    description.Append($"This tool allows you to set the {navigationProperty.Name} property of a {entityType.Name} entity to reference a specific {relatedType} entity. ");
                    if (navigationProperty.IsRequired)
                    {
                        description.Append("This will replace any existing relationship as this navigation property is required. ");
                    }
                    else
                    {
                        description.Append("This will replace any existing relationship if one exists. ");
                    }
                }

                var sourceKeyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
                if (sourceKeyProperties.Count > 0)
                {
                    description.Append($"You must provide the source entity key values ({string.Join(", ", sourceKeyProperties.Select(p => p.Name))}) ");
                    description.Append($"and the target entity key to establish the relationship.");
                }
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"Example usage: {operation.ToLowerInvariant()} by providing both entity keys.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates a description for a REMOVE RELATIONSHIP tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="navigationProperty">The navigation property.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the REMOVE RELATIONSHIP tool.</returns>
        private static string GenerateRemoveRelationshipDescription(EdmEntitySet entitySet, EdmEntityType entityType, EdmNavigationProperty navigationProperty, NavigationToolGenerationOptions options)
        {
            var description = new StringBuilder();
            var operation = navigationProperty.IsCollection ? "Removes a relationship" : "Unsets the relationship";
            var relatedType = navigationProperty.TargetTypeName;
            
            description.Append($"{operation} between a {entityType.Name} and a {relatedType} via the {navigationProperty.Name} navigation property.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                
                if (navigationProperty.IsCollection)
                {
                    description.Append($"This tool allows you to remove a specific {relatedType} entity from the {navigationProperty.Name} collection of a {entityType.Name} entity. ");
                    description.Append("Only the specified relationship is removed; other relationships in the collection remain unchanged. ");
                }
                else
                {
                    description.Append($"This tool allows you to clear the {navigationProperty.Name} property of a {entityType.Name} entity, removing the reference to the related {relatedType} entity. ");
                    if (navigationProperty.IsRequired)
                    {
                        description.Append("Note: This operation may fail if the navigation property is required and cannot be null. ");
                    }
                }

                var sourceKeyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
                if (sourceKeyProperties.Count > 0)
                {
                    description.Append($"You must provide the source entity key values ({string.Join(", ", sourceKeyProperties.Select(p => p.Name))})");
                    
                    if (navigationProperty.IsCollection)
                    {
                        description.Append(" and the target entity key to identify which relationship to remove.");
                    }
                    else
                    {
                        description.Append(" to identify which entity's navigation property to clear.");
                    }
                }
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"Example usage: {operation.ToLowerInvariant()} by providing the entity key values.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates the input schema for a GET RELATED tool.
        /// </summary>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navigationProperty">The navigation property.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the GET RELATED tool input.</returns>
        private static object GenerateGetRelatedInputSchema(EdmEntityType entityType, EdmNavigationProperty navigationProperty, NavigationToolGenerationOptions options)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            // Add key properties for source entity identification
            var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
            foreach (var property in keyProperties)
            {
                var propertySchema = GeneratePropertySchema(property);
                properties[property.Name] = propertySchema;
                required.Add(property.Name);
            }

            // Add query options for collection navigations
            if (navigationProperty.IsCollection && options.SupportQueryOptions)
            {
                if (options.SupportFilter)
                {
                    properties["$filter"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = $"OData filter expression to filter related {navigationProperty.TargetTypeName} entities."
                    };
                }

                if (options.SupportOrderBy)
                {
                    properties["$orderby"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = $"OData orderby expression to sort related {navigationProperty.TargetTypeName} entities."
                    };
                }

                if (options.SupportTop)
                {
                    properties["$top"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = $"Maximum number of related entities to return (1-{options.MaxPageSize}).",
                        ["minimum"] = 1,
                        ["maximum"] = options.MaxPageSize,
                        ["default"] = options.DefaultPageSize
                    };
                }
            }

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties
            };

            if (required.Count > 0)
            {
                schema["required"] = required;
            }

            return schema;
        }

        /// <summary>
        /// Generates the input schema for an ADD RELATIONSHIP tool.
        /// </summary>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navigationProperty">The navigation property.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the ADD RELATIONSHIP tool input.</returns>
        private static object GenerateAddRelationshipInputSchema(EdmEntityType entityType, EdmNavigationProperty navigationProperty, NavigationToolGenerationOptions options)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            // Add key properties for source entity identification
            var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
            foreach (var property in keyProperties)
            {
                var propertySchema = GeneratePropertySchema(property);
                properties[property.Name] = propertySchema;
                required.Add(property.Name);
            }

            // Add target entity key (simplified - in real implementation, we'd need the target entity type)
            properties["relatedEntityKey"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = $"The key value of the {navigationProperty.TargetTypeName} entity to add to the relationship."
            };
            required.Add("relatedEntityKey");

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required
            };

            return schema;
        }

        /// <summary>
        /// Generates the input schema for a REMOVE RELATIONSHIP tool.
        /// </summary>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navigationProperty">The navigation property.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the REMOVE RELATIONSHIP tool input.</returns>
        private static object GenerateRemoveRelationshipInputSchema(EdmEntityType entityType, EdmNavigationProperty navigationProperty, NavigationToolGenerationOptions options)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            // Add key properties for source entity identification
            var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
            foreach (var property in keyProperties)
            {
                var propertySchema = GeneratePropertySchema(property);
                properties[property.Name] = propertySchema;
                required.Add(property.Name);
            }

            // For collection navigations, add target entity key to identify which relationship to remove
            if (navigationProperty.IsCollection)
            {
                properties["relatedEntityKey"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = $"The key value of the {navigationProperty.TargetTypeName} entity to remove from the relationship."
                };
                required.Add("relatedEntityKey");
            }

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required
            };

            return schema;
        }

        /// <summary>
        /// Generates a JSON schema for a single property.
        /// </summary>
        /// <param name="property">The property to generate schema for.</param>
        /// <returns>A JSON schema for the property.</returns>
        private static object GeneratePropertySchema(EdmProperty property)
        {
            var schema = new Dictionary<string, object>();

            // Map EDM types to JSON schema types
            var jsonType = MapEdmTypeToJsonType(property.TypeName);
            schema["type"] = jsonType;
            schema["description"] = $"The {property.Name} property value";

            return schema;
        }

        /// <summary>
        /// Maps EDM type names to JSON Schema type names.
        /// </summary>
        /// <param name="edmType">The EDM type name.</param>
        /// <returns>The corresponding JSON Schema type.</returns>
        private static string MapEdmTypeToJsonType(string edmType)
        {
            return edmType?.ToLowerInvariant() switch
            {
                "string" or "edm.string" => "string",
                "int32" or "edm.int32" or "int16" or "edm.int16" or "int64" or "edm.int64" => "integer",
                "decimal" or "edm.decimal" or "double" or "edm.double" or "single" or "edm.single" => "number",
                "boolean" or "edm.boolean" => "boolean",
                "datetime" or "edm.datetime" or "datetimeoffset" or "edm.datetimeoffset" => "string",
                "guid" or "edm.guid" => "string",
                "binary" or "edm.binary" => "string",
                _ => "string" // Default to string for unknown types
            };
        }

        #endregion
    }
}