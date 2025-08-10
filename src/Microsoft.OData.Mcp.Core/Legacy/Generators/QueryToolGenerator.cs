// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

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
    /// Generates query MCP tools from OData entity types.
    /// </summary>
    /// <remarks>
    /// This generator creates MCP tools that allow AI models to perform advanced querying
    /// operations on OData entities, including filtering, sorting, projection, and expansion.
    /// It supports all standard OData query options like $filter, $orderby, $select, $expand,
    /// $top, $skip, and $search.
    /// </remarks>
    public sealed class QueryToolGenerator
    {

        #region Fields

        internal readonly ILogger<QueryToolGenerator> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryToolGenerator"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public QueryToolGenerator(ILogger<QueryToolGenerator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region IQueryToolGenerator Implementation

        /// <summary>
        /// Generates all query tools for the specified entity set.
        /// </summary>
        /// <param name="entitySet">The entity set to generate tools for.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A collection of generated MCP tools for query operations.</returns>
        public async Task<IEnumerable<McpTool>> GenerateAllQueryToolsAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            QueryToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
ArgumentNullException.ThrowIfNull(entitySet);
            ArgumentNullException.ThrowIfNull(entityType);
            ArgumentNullException.ThrowIfNull(options);

            _logger.LogDebug("Generating query tools for entity set {EntitySet} with type {EntityType}",
                entitySet.Name, entityType.Name);

            var tools = new List<McpTool>();

            try
            {
                // Generate each type of query tool based on options
                if (options.GenerateListTools)
                {
                    var listTool = await GenerateListToolAsync(entitySet, entityType, options, cancellationToken);
                    tools.Add(listTool);
                }

                if (options.GenerateSearchTools && options.SupportSearch)
                {
                    var searchTool = await GenerateSearchToolAsync(entitySet, entityType, options, cancellationToken);
                    tools.Add(searchTool);
                }

                if (options.GenerateCountTools)
                {
                    var countTool = await GenerateCountToolAsync(entitySet, entityType, options, cancellationToken);
                    tools.Add(countTool);
                }

                _logger.LogInformation("Generated {ToolCount} query tools for entity set {EntitySet}",
                    tools.Count, entitySet.Name);

                return tools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating query tools for entity set {EntitySet}", entitySet.Name);
                throw;
            }
        }

        /// <summary>
        /// Generates a query tool for listing entities with filtering and sorting.
        /// </summary>
        /// <param name="entitySet">The entity set to query.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A query MCP tool for the entity type.</returns>
        public Task<McpTool> GenerateListToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            QueryToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var toolName = FormatToolName("List", entityType.Name, options.NamingConvention);
            var description = GenerateListDescription(entitySet, entityType, options);
            var inputSchema = GenerateListInputSchema(entityType, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated LIST tool {ToolName} for entity type {EntityType}",
                toolName, entityType.Name);

            return Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a search tool for full-text search across entity properties.
        /// </summary>
        /// <param name="entitySet">The entity set to search.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A search MCP tool for the entity type.</returns>
        public Task<McpTool> GenerateSearchToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            QueryToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var toolName = FormatToolName("Search", entityType.Name, options.NamingConvention);
            var description = GenerateSearchDescription(entitySet, entityType, options);
            var inputSchema = GenerateSearchInputSchema(entityType, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated SEARCH tool {ToolName} for entity type {EntityType}",
                toolName, entityType.Name);

            return Task.FromResult(tool);
        }

        /// <summary>
        /// Generates a count tool for getting entity counts with optional filtering.
        /// </summary>
        /// <param name="entitySet">The entity set to count.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A count MCP tool for the entity type.</returns>
        public Task<McpTool> GenerateCountToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            QueryToolGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            var toolName = FormatToolName("Count", entityType.Name, options.NamingConvention);
            var description = GenerateCountDescription(entitySet, entityType, options);
            var inputSchema = GenerateCountInputSchema(entityType, options);

            var tool = new McpTool
            {
                Name = toolName,
                Description = description,
                InputSchema = inputSchema
            };

            _logger.LogDebug("Generated COUNT tool {ToolName} for entity type {EntityType}",
                toolName, entityType.Name);

            return Task.FromResult(tool);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Formats a tool name according to the specified naming convention.
        /// </summary>
        /// <param name="operation">The query operation (List, Search, Count).</param>
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
        /// Generates a description for a LIST tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the LIST tool.</returns>
        internal static string GenerateListDescription(EdmEntitySet entitySet, EdmEntityType entityType, QueryToolGenerationOptions options)
        {
            var description = new StringBuilder();
            description.Append($"Lists {entityType.Name} entities from the {entitySet.Name} collection with optional filtering, sorting, and paging.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"This tool allows you to retrieve multiple {entityType.Name} records with advanced query capabilities. ");

                var supportedOptions = new List<string>();
                if (options.SupportFilter) supportedOptions.Add("filtering ($filter)");
                if (options.SupportOrderBy) supportedOptions.Add("sorting ($orderby)");
                if (options.SupportSelect) supportedOptions.Add("field selection ($select)");
                if (options.SupportExpand) supportedOptions.Add("related entity expansion ($expand)");
                if (options.SupportTop) supportedOptions.Add("result limiting ($top)");
                if (options.SupportSkip) supportedOptions.Add("result paging ($skip)");

                if (supportedOptions.Count > 0)
                {
                    description.Append($"Supported query options: {string.Join(", ", supportedOptions)}. ");
                }

                description.Append($"Results are paginated with a default page size of {options.DefaultPageSize} and maximum of {options.MaxPageSize} entities per request.");
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append("Example usage: List entities with optional filters, sorting, and field selection to get exactly the data you need.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates a description for a SEARCH tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the SEARCH tool.</returns>
        internal static string GenerateSearchDescription(EdmEntitySet entitySet, EdmEntityType entityType, QueryToolGenerationOptions options)
        {
            var description = new StringBuilder();
            description.Append($"Performs full-text search across {entityType.Name} entities in the {entitySet.Name} collection.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"This tool allows you to search for {entityType.Name} records using natural language queries. ");
                description.Append("The search is performed across all searchable text fields in the entity. ");

                if (options.SupportOrderBy) description.Append("Results can be sorted by relevance or specific fields. ");
                if (options.SupportTop) description.Append($"Results are limited to a maximum of {options.MaxPageSize} entities. ");

                description.Append("Use this when you need to find entities based on text content rather than exact field matches.");
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append("Example usage: Search for entities containing specific keywords or phrases across all text fields.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates a description for a COUNT tool.
        /// </summary>
        /// <param name="entitySet">The entity set.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A description for the COUNT tool.</returns>
        internal static string GenerateCountDescription(EdmEntitySet entitySet, EdmEntityType entityType, QueryToolGenerationOptions options)
        {
            var description = new StringBuilder();
            description.Append($"Gets the count of {entityType.Name} entities in the {entitySet.Name} collection with optional filtering.");

            if (options.GenerateDetailedDescriptions)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append($"This tool returns the total number of {entityType.Name} records that match the specified criteria. ");

                if (options.SupportFilter)
                {
                    description.Append("You can apply filters to count only entities that meet specific conditions. ");
                }

                description.Append("This is useful for understanding data volume, implementing pagination, or validating query results before fetching actual data.");
            }

            if (options.IncludeExamples)
            {
                description.AppendLine();
                description.AppendLine();
                description.Append("Example usage: Count all entities or count entities matching specific filter criteria.");
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates the input schema for a LIST tool.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the LIST tool input.</returns>
        internal static object GenerateListInputSchema(EdmEntityType entityType, QueryToolGenerationOptions options)
        {
            var properties = new Dictionary<string, object>();

            // Add OData query option parameters
            if (options.SupportFilter)
            {
                properties["$filter"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = $"OData filter expression to filter {entityType.Name} entities. Use standard OData filter syntax.",
                    ["examples"] = new[] { $"Name eq 'John'", "Age gt 25", "startswith(Name, 'A')" }
                };
            }

            if (options.SupportOrderBy)
            {
                var sortableProperties = GetSortableProperties(entityType, options);
                properties["$orderby"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = $"OData orderby expression to sort {entityType.Name} entities. Use 'asc' or 'desc' for direction.",
                    ["examples"] = sortableProperties.Take(3).Select(p => $"{p} asc").Concat(new[] { $"{sortableProperties.FirstOrDefault()} desc" }).ToArray()
                };
            }

            if (options.SupportSelect)
            {
                var selectableProperties = GetSelectableProperties(entityType, options);
                properties["$select"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = $"Comma-separated list of properties to include in the response.",
                    ["examples"] = new[] { string.Join(",", selectableProperties.Take(3)), selectableProperties.FirstOrDefault() }
                };
            }

            if (options.SupportExpand)
            {
                var navigationProperties = GetNavigationProperties(entityType, options);
                if (navigationProperties.Any())
                {
                    properties["$expand"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Comma-separated list of navigation properties to expand in the response.",
                        ["examples"] = navigationProperties.Take(2).ToArray()
                    };
                }
            }

            if (options.SupportTop)
            {
                properties["$top"] = new Dictionary<string, object>
                {
                    ["type"] = "integer",
                    ["description"] = $"Maximum number of entities to return (1-{options.MaxPageSize}).",
                    ["minimum"] = 1,
                    ["maximum"] = options.MaxPageSize,
                    ["default"] = options.DefaultPageSize
                };
            }

            if (options.SupportSkip)
            {
                properties["$skip"] = new Dictionary<string, object>
                {
                    ["type"] = "integer",
                    ["description"] = "Number of entities to skip for pagination.",
                    ["minimum"] = 0
                };
            }

            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false
            };
        }

        /// <summary>
        /// Generates the input schema for a SEARCH tool.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the SEARCH tool input.</returns>
        internal static object GenerateSearchInputSchema(EdmEntityType entityType, QueryToolGenerationOptions options)
        {
            var properties = new Dictionary<string, object>
            {
                ["$search"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = $"Search query to find {entityType.Name} entities. Use natural language or keywords.",
                    ["examples"] = new[] { "john smith", "active users", "recent orders" }
                }
            };

            if (options.SupportOrderBy)
            {
                var sortableProperties = GetSortableProperties(entityType, options);
                properties["$orderby"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Optional ordering of search results. Defaults to relevance.",
                    ["examples"] = sortableProperties.Take(2).Select(p => $"{p} desc").ToArray()
                };
            }

            if (options.SupportTop)
            {
                properties["$top"] = new Dictionary<string, object>
                {
                    ["type"] = "integer",
                    ["description"] = $"Maximum number of search results to return (1-{options.MaxPageSize}).",
                    ["minimum"] = 1,
                    ["maximum"] = options.MaxPageSize,
                    ["default"] = options.DefaultPageSize
                };
            }

            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = new[] { "$search" },
                ["additionalProperties"] = false
            };
        }

        /// <summary>
        /// Generates the input schema for a COUNT tool.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A JSON schema for the COUNT tool input.</returns>
        internal static object GenerateCountInputSchema(EdmEntityType entityType, QueryToolGenerationOptions options)
        {
            var properties = new Dictionary<string, object>();

            if (options.SupportFilter)
            {
                properties["$filter"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = $"Optional OData filter expression to count only {entityType.Name} entities matching specific criteria.",
                    ["examples"] = new[] { $"Name eq 'John'", "Age gt 25", "IsActive eq true" }
                };
            }

            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false
            };
        }

        /// <summary>
        /// Gets the properties that can be used for sorting.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A collection of sortable property names.</returns>
        internal static IEnumerable<string> GetSortableProperties(EdmEntityType entityType, QueryToolGenerationOptions options)
        {
            var properties = entityType.Properties.AsEnumerable();

            // Filter out excluded properties
            if (options.ExcludedProperties.TryGetValue(entityType.Name, out var excludedProps))
            {
                properties = properties.Where(p => !excludedProps.Contains(p.Name));
            }

            // Include only sortable types (exclude complex types and large text fields)
            return properties
                .Where(p => IsSortableProperty(p))
                .Select(p => p.Name);
        }

        /// <summary>
        /// Gets the properties that can be selected in projections.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A collection of selectable property names.</returns>
        internal static IEnumerable<string> GetSelectableProperties(EdmEntityType entityType, QueryToolGenerationOptions options)
        {
            var properties = entityType.Properties.AsEnumerable();

            // Filter out excluded properties
            if (options.ExcludedProperties.TryGetValue(entityType.Name, out var excludedProps))
            {
                properties = properties.Where(p => !excludedProps.Contains(p.Name));
            }

            return properties.Select(p => p.Name);
        }

        /// <summary>
        /// Gets the navigation properties that can be expanded.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="options">Generation options.</param>
        /// <returns>A collection of expandable navigation property names.</returns>
        internal static IEnumerable<string> GetNavigationProperties(EdmEntityType entityType, QueryToolGenerationOptions options)
        {
            return entityType.NavigationProperties.Select(np => np.Name);
        }

        /// <summary>
        /// Determines if a property can be used for sorting.
        /// </summary>
        /// <param name="property">The property to check.</param>
        /// <returns><c>true</c> if the property is sortable; otherwise, <c>false</c>.</returns>
        internal static bool IsSortableProperty(EdmProperty property)
        {
            var typeName = property.TypeName?.ToLowerInvariant();
            return typeName switch
            {
                "string" or "edm.string" => property.MaxLength is null or <= 255, // Exclude large text fields
                "int32" or "edm.int32" or "int16" or "edm.int16" or "int64" or "edm.int64" => true,
                "decimal" or "edm.decimal" or "double" or "edm.double" or "single" or "edm.single" => true,
                "boolean" or "edm.boolean" => true,
                "datetime" or "edm.datetime" or "datetimeoffset" or "edm.datetimeoffset" => true,
                "guid" or "edm.guid" => true,
                _ => false
            };
        }

        #endregion

    }

}
