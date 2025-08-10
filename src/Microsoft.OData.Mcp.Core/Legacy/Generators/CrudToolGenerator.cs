using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Legacy;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Legacy.Generators
{

    /// <summary>
    /// Generates CRUD (Create, Read, Update, Delete) MCP tools from OData entity types.
    /// </summary>
    /// <remarks>
    /// This generator creates MCP tools that allow AI models to perform basic data operations
    /// on OData entities. It generates separate tools for each CRUD operation, with proper
    /// validation, documentation, and parameter handling.
    /// </remarks>
    public sealed class CrudToolGenerator
    {

        #region Fields

        internal readonly ILogger<CrudToolGenerator> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CrudToolGenerator"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public CrudToolGenerator(ILogger<CrudToolGenerator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Generates all CRUD tools for the specified entity set.
        /// </summary>
        /// <param name="entitySet">The entity set to generate tools for.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A collection of generated MCP tools for CRUD operations.</returns>
        public async Task<IEnumerable<McpTool>> GenerateAllCrudToolsAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
ArgumentNullException.ThrowIfNull(entitySet);
            ArgumentNullException.ThrowIfNull(entityType);
            ArgumentNullException.ThrowIfNull(options);

            _logger.LogDebug("Generating CRUD tools for entity set {EntitySet} with type {EntityType}",
                entitySet.Name, entityType.Name);

            var tools = new List<McpTool>();

            try
            {
                // Generate each type of CRUD tool based on options
                if (options.GenerateCreateTools)
                {
                    var createTool = await GenerateCreateToolAsync(entitySet, entityType, options, cancellationToken);
                    tools.Add(createTool);
                }

                if (options.GenerateReadTools)
                {
                    var readTool = await GenerateReadToolAsync(entitySet, entityType, options, cancellationToken);
                    tools.Add(readTool);
                }

                if (options.GenerateUpdateTools)
                {
                    var updateTool = await GenerateUpdateToolAsync(entitySet, entityType, options, cancellationToken);
                    tools.Add(updateTool);
                }

                if (options.GenerateDeleteTools)
                {
                    var deleteTool = await GenerateDeleteToolAsync(entitySet, entityType, options, cancellationToken);
                    tools.Add(deleteTool);
                }

                _logger.LogInformation("Generated {ToolCount} CRUD tools for entity set {EntitySet}",
                    tools.Count, entitySet.Name);

                return tools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating CRUD tools for entity set {EntitySet}", entitySet.Name);
                throw;
            }
        }

        /// <summary>
        /// Generates a CREATE tool for the specified entity type.
        /// </summary>
        /// <param name="entitySet">The entity set to create entities in.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A CREATE MCP tool for the entity type.</returns>
        public Task<McpTool> GenerateCreateToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var toolName = FormatToolName("Create", entityType.Name, options.NamingConvention);
            var description = GenerateCreateDescription(entitySet, entityType, options);
            var inputSchema = GenerateCreateInputSchema(entityType, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated CREATE tool {ToolName} for entity type {EntityType}",
                toolName, entityType.Name);

            return Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a READ tool for the specified entity type.
        /// </summary>
        /// <param name="entitySet">The entity set to read entities from.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A READ MCP tool for the entity type.</returns>
        public Task<McpTool> GenerateReadToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var toolName = FormatToolName("Get", entityType.Name, options.NamingConvention);
            var description = GenerateReadDescription(entitySet, entityType, options);
            var inputSchema = GenerateReadInputSchema(entityType, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated READ tool {ToolName} for entity type {EntityType}",
                toolName, entityType.Name);

            return Task.FromResult(tool);
        }

        /// <summary>
        /// Generates an UPDATE tool for the specified entity type.
        /// </summary>
        /// <param name="entitySet">The entity set containing entities to update.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An UPDATE MCP tool for the entity type.</returns>
        public Task<McpTool> GenerateUpdateToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var toolName = FormatToolName("Update", entityType.Name, options.NamingConvention);
            var description = GenerateUpdateDescription(entitySet, entityType, options);
            var inputSchema = GenerateUpdateInputSchema(entityType, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated UPDATE tool {ToolName} for entity type {EntityType}",
                toolName, entityType.Name);

            return Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a DELETE tool for the specified entity type.
        /// </summary>
        /// <param name="entitySet">The entity set containing entities to delete.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A DELETE MCP tool for the entity type.</returns>
        public Task<McpTool> GenerateDeleteToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var toolName = FormatToolName("Delete", entityType.Name, options.NamingConvention);
            var description = GenerateDeleteDescription(entitySet, entityType, options);
            var inputSchema = GenerateDeleteInputSchema(entityType, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated DELETE tool {ToolName} for entity type {EntityType}",
                toolName, entityType.Name);

            return Task.FromResult(tool);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Formats a tool name according to the specified naming convention.
        /// </summary>
        /// <param name="operation">The CRUD operation (Create, Read, Update, Delete).</param>
        /// <param name="entityName">The entity type name.</param>
        /// <param name="convention">The naming convention to apply.</param>
        /// <returns>The formatted tool name.</returns>
        internal static string FormatToolName(string operation, string entityName, ToolNamingConvention convention)
        {
            var baseName = $"{operation}{entityName}";

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
        internal static string ConvertToSnakeCase(string input)
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
        internal static string ConvertToKebabCase(string input)
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
        /// Generates a description for a CREATE tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the CREATE tool.</returns>
        internal static string GenerateCreateDescription(EdmEntitySet entitySet, EdmEntityType entityType, CrudToolGenerationOptions options)
        {
            var description = new StringBuilder();
            description.Append($"Creates a new {entityType.Name} entity in the {entitySet.Name} collection.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"This tool allows you to create a new {entityType.Name} record by providing the required and optional properties. ");
                
                if (options.IncludeValidation)
                {
                    description.Append("All provided values will be validated according to the entity schema before creation. ");
                }

                var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
                if (keyProperties.Count > 0)
                {
                    description.Append($"The system will automatically generate values for key properties: {string.Join(", ", keyProperties.Select(p => p.Name))}. ");
                }
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append("Example usage: Provide the entity properties as parameters to create a new record.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates a description for a READ tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the READ tool.</returns>
        internal static string GenerateReadDescription(EdmEntitySet entitySet, EdmEntityType entityType, CrudToolGenerationOptions options)
        {
            var description = new StringBuilder();
            description.Append($"Retrieves a specific {entityType.Name} entity from the {entitySet.Name} collection by its key.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"This tool allows you to fetch a single {entityType.Name} record by providing its unique identifier(s). ");

                var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
                if (keyProperties.Count > 0)
                {
                    description.Append($"You must provide values for the key properties: {string.Join(", ", keyProperties.Select(p => p.Name))}. ");
                }

                description.Append("The tool returns the complete entity with all its properties and values.");
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append("Example usage: Provide the key values to retrieve the specific entity.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates a description for an UPDATE tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the UPDATE tool.</returns>
        internal static string GenerateUpdateDescription(EdmEntitySet entitySet, EdmEntityType entityType, CrudToolGenerationOptions options)
        {
            var description = new StringBuilder();
            description.Append($"Updates an existing {entityType.Name} entity in the {entitySet.Name} collection.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"This tool allows you to modify the properties of an existing {entityType.Name} record. ");

                var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
                if (keyProperties.Count > 0)
                {
                    description.Append($"You must provide the key values ({string.Join(", ", keyProperties.Select(p => p.Name))}) to identify which entity to update. ");
                }

                description.Append("Only provide the properties you want to change - other properties will remain unchanged. ");

                if (options.IncludeValidation)
                {
                    description.Append("All provided values will be validated according to the entity schema before the update is applied.");
                }
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append("Example usage: Provide the key values and the properties to update.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates a description for a DELETE tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the DELETE tool.</returns>
        internal static string GenerateDeleteDescription(EdmEntitySet entitySet, EdmEntityType entityType, CrudToolGenerationOptions options)
        {
            var description = new StringBuilder();
            description.Append($"Deletes a specific {entityType.Name} entity from the {entitySet.Name} collection.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"This tool permanently removes a {entityType.Name} record from the data store. ");

                var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
                if (keyProperties.Count > 0)
                {
                    description.Append($"You must provide the key values ({string.Join(", ", keyProperties.Select(p => p.Name))}) to identify which entity to delete. ");
                }

                description.Append("This operation cannot be undone, so use it carefully. ");
                description.Append("The tool will return confirmation once the entity has been successfully deleted.");
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append("Example usage: Provide the key values to delete the specific entity.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates the input schema for a CREATE tool.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the CREATE tool input.</returns>
        internal static object GenerateCreateInputSchema(EdmEntityType entityType, CrudToolGenerationOptions options)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            var eligibleProperties = GetEligibleProperties(entityType, options)
                .Where(p => !p.IsKey) // Exclude key properties from CREATE - they're usually auto-generated
                .ToList();

            foreach (var property in eligibleProperties)
            {
                var propertySchema = GeneratePropertySchema(property, options);
                properties[property.Name] = propertySchema;

                if (!property.IsNullable && !property.HasDefaultValue)
                {
                    required.Add(property.Name);
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
        /// Generates the input schema for a READ tool.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the READ tool input.</returns>
        internal static object GenerateReadInputSchema(EdmEntityType entityType, CrudToolGenerationOptions options)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            // Only include key properties for READ operations
            var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();

            foreach (var property in keyProperties)
            {
                var propertySchema = GeneratePropertySchema(property, options);
                properties[property.Name] = propertySchema;
                required.Add(property.Name);
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
        /// Generates the input schema for an UPDATE tool.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the UPDATE tool input.</returns>
        internal static object GenerateUpdateInputSchema(EdmEntityType entityType, CrudToolGenerationOptions options)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            // Include key properties (required for identification)
            var keyProperties = entityType.Properties.Where(p => p.IsKey).ToList();
            foreach (var property in keyProperties)
            {
                var propertySchema = GeneratePropertySchema(property, options);
                properties[property.Name] = propertySchema;
                required.Add(property.Name);
            }

            // Include non-key properties (optional for partial updates)
            var nonKeyProperties = GetEligibleProperties(entityType, options)
                .Where(p => !p.IsKey)
                .ToList();

            foreach (var property in nonKeyProperties)
            {
                var propertySchema = GeneratePropertySchema(property, options);
                properties[property.Name] = propertySchema;
                // Non-key properties are optional for updates
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
        /// Generates the input schema for a DELETE tool.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the DELETE tool input.</returns>
        internal static object GenerateDeleteInputSchema(EdmEntityType entityType, CrudToolGenerationOptions options)
        {
            // Same as READ - only key properties needed for identification
            return GenerateReadInputSchema(entityType, options);
        }

        /// <summary>
        /// Gets the properties eligible for inclusion in tools based on the options.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A collection of eligible properties.</returns>
        internal static IEnumerable<EdmProperty> GetEligibleProperties(EdmEntityType entityType, CrudToolGenerationOptions options)
        {
            var properties = entityType.Properties.AsEnumerable();

            // Filter out excluded properties
            if (options.ExcludedProperties.TryGetValue(entityType.Name, out var excludedProps))
            {
                properties = properties.Where(p => !excludedProps.Contains(p.Name));
            }

            // Apply property count limit
            if (options.MaxPropertiesPerTool.HasValue)
            {
                properties = properties.Take(options.MaxPropertiesPerTool.Value);
            }

            return properties;
        }

        /// <summary>
        /// Generates a JSON schema for a single property.
        /// </summary>
        /// <param name="property">The property to generate schema for.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the property.</returns>
        internal static object GeneratePropertySchema(EdmProperty property, CrudToolGenerationOptions options)
        {
            var schema = new Dictionary<string, object>();

            // Map EDM types to JSON schema types
            var jsonType = MapEdmTypeToJsonType(property.TypeName);
            schema["type"] = jsonType;

            // Add description from schema if available and enabled
            if (options.UseSchemaDescriptions && !string.IsNullOrWhiteSpace(property.Description))
            {
                schema["description"] = property.Description;
            }
            else
            {
                schema["description"] = $"The {property.Name} property of the entity";
            }

            // Add constraints based on the property type
            AddPropertyConstraints(schema, property, jsonType);

            return schema;
        }

        /// <summary>
        /// Maps EDM type names to JSON Schema type names.
        /// </summary>
        /// <param name="edmType">The EDM type name.</param>
        /// <returns>The corresponding JSON Schema type.</returns>
        internal static string MapEdmTypeToJsonType(string edmType)
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

        /// <summary>
        /// Adds property-specific constraints to the JSON schema.
        /// </summary>
        /// <param name="schema">The schema dictionary to add constraints to.</param>
        /// <param name="property">The property to add constraints for.</param>
        /// <param name="jsonType">The JSON type of the property.</param>
        internal static void AddPropertyConstraints(Dictionary<string, object> schema, EdmProperty property, string jsonType)
        {
            // Add string constraints
            if (jsonType == "string")
            {
                if (property.MaxLength.HasValue && property.MaxLength.Value > 0)
                {
                    schema["maxLength"] = property.MaxLength.Value;
                }

                // Add format hints for known types
                if (property.TypeName?.ToLowerInvariant().Contains("datetime") == true)
                {
                    schema["format"] = "date-time";
                }
                else if (property.TypeName?.ToLowerInvariant().Contains("guid") == true)
                {
                    schema["format"] = "uuid";
                }
            }

            // Add numeric constraints
            if (jsonType == "integer" || jsonType == "number")
            {
                if (property.Scale.HasValue && property.Scale.Value > 0)
                {
                    schema["multipleOf"] = Math.Pow(10, -property.Scale.Value);
                }
            }
        }

        #endregion

    }

}
