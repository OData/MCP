// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Tools
{

    /// <summary>
    /// Configuration options for MCP tool generation.
    /// </summary>
    /// <remarks>
    /// These options control how tools are generated from OData metadata,
    /// including which operations to include, authorization requirements,
    /// and performance optimizations.
    /// </remarks>
    public sealed class McpToolGenerationOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether to generate CRUD tools for entity types.
        /// </summary>
        /// <value><c>true</c> to generate CRUD tools; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// CRUD tools provide basic Create, Read, Update, and Delete operations
        /// for each entity type in the OData model.
        /// </remarks>
        public bool GenerateCrudTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate query tools.
        /// </summary>
        /// <value><c>true</c> to generate query tools; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Query tools provide advanced search and filtering capabilities
        /// using OData query syntax like $filter, $orderby, $select, and $expand.
        /// </remarks>
        public bool GenerateQueryTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate navigation tools.
        /// </summary>
        /// <value><c>true</c> to generate navigation tools; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Navigation tools allow traversing relationships between entities,
        /// following navigation properties defined in the OData model.
        /// </remarks>
        public bool GenerateNavigationTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate batch operation tools.
        /// </summary>
        /// <value><c>true</c> to generate batch tools; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Batch tools allow processing multiple operations in a single request,
        /// improving performance for bulk operations.
        /// </remarks>
        public bool GenerateBatchTools { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to generate tools for entity sets.
        /// </summary>
        /// <value><c>true</c> to generate entity set tools; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Entity set tools operate on collections of entities and may have
        /// different permissions or behaviors than individual entity tools.
        /// </remarks>
        public bool GenerateEntitySetTools { get; set; } = true;

        /// <summary>
        /// Gets or sets the entity types to include in tool generation.
        /// </summary>
        /// <value>A collection of entity type names to include, or empty to include all.</value>
        /// <remarks>
        /// When specified, only tools for the listed entity types will be generated.
        /// If empty, tools will be generated for all entity types in the model.
        /// Entity type names should be fully qualified (e.g., "MyNamespace.Customer").
        /// </remarks>
        public HashSet<string> IncludeEntityTypes { get; set; } = [];

        /// <summary>
        /// Gets or sets the entity types to exclude from tool generation.
        /// </summary>
        /// <value>A collection of entity type names to exclude.</value>
        /// <remarks>
        /// Entity types listed here will be excluded from tool generation
        /// even if they would otherwise be included. This takes precedence
        /// over the IncludeEntityTypes setting.
        /// </remarks>
        public HashSet<string> ExcludeEntityTypes { get; set; } = [];

        /// <summary>
        /// Gets or sets the operations to include for each entity type.
        /// </summary>
        /// <value>A collection of operation types to include.</value>
        /// <remarks>
        /// This allows fine-grained control over which operations are available
        /// for each entity type. If empty, all supported operations will be included.
        /// </remarks>
        public HashSet<McpToolOperationType> IncludeOperations { get; set; } = [];

        /// <summary>
        /// Gets or sets the operations to exclude for each entity type.
        /// </summary>
        /// <value>A collection of operation types to exclude.</value>
        /// <remarks>
        /// Operations listed here will be excluded from tool generation.
        /// This takes precedence over the IncludeOperations setting.
        /// </remarks>
        public HashSet<McpToolOperationType> ExcludeOperations { get; set; } = [];

        /// <summary>
        /// Gets or sets the default required scopes for generated tools.
        /// </summary>
        /// <value>A collection of OAuth2 scopes required by default for all tools.</value>
        /// <remarks>
        /// These scopes will be added to all generated tools unless overridden
        /// by entity-specific or operation-specific scope configurations.
        /// </remarks>
        public List<string> DefaultRequiredScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the default required roles for generated tools.
        /// </summary>
        /// <value>A collection of roles required by default for all tools.</value>
        /// <remarks>
        /// These roles will be added to all generated tools unless overridden
        /// by entity-specific or operation-specific role configurations.
        /// </remarks>
        public List<string> DefaultRequiredRoles { get; set; } = [];

        /// <summary>
        /// Gets or sets entity-specific scope requirements.
        /// </summary>
        /// <value>A dictionary mapping entity type names to their required scopes.</value>
        /// <remarks>
        /// This allows configuring different authorization requirements for different
        /// entity types. Entity type names should be fully qualified.
        /// </remarks>
        public Dictionary<string, List<string>> EntityScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets operation-specific scope requirements.
        /// </summary>
        /// <value>A dictionary mapping operation types to their required scopes.</value>
        /// <remarks>
        /// This allows configuring different authorization requirements for different
        /// operation types (e.g., read vs. write operations).
        /// </remarks>
        public Dictionary<McpToolOperationType, List<string>> OperationScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the tool name prefix.
        /// </summary>
        /// <value>A prefix to add to all generated tool names.</value>
        /// <remarks>
        /// The prefix helps avoid naming conflicts when multiple OData services
        /// are exposed through the same MCP server.
        /// </remarks>
        public string ToolNamePrefix { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tool name suffix.
        /// </summary>
        /// <value>A suffix to add to all generated tool names.</value>
        /// <remarks>
        /// The suffix can be used for versioning or other organizational purposes.
        /// </remarks>
        public string ToolNameSuffix { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to include examples in generated tools.
        /// </summary>
        /// <value><c>true</c> to include examples; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Examples help AI models understand how to use the tools effectively
        /// but increase the size of the tool definitions.
        /// </remarks>
        public bool IncludeExamples { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include detailed property schemas.
        /// </summary>
        /// <value><c>true</c> to include detailed schemas; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Detailed schemas provide full type information and validation rules
        /// but result in larger tool definitions and longer generation times.
        /// </remarks>
        public bool IncludeDetailedSchemas { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to optimize for performance.
        /// </summary>
        /// <value><c>true</c> to optimize for performance; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Performance optimization may reduce the number of generated tools
        /// or simplify their schemas to improve runtime performance.
        /// </remarks>
        public bool OptimizeForPerformance { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of tools to generate.
        /// </summary>
        /// <value>The maximum number of tools to generate, or null for no limit.</value>
        /// <remarks>
        /// This setting can be used to prevent generation of too many tools
        /// from very large OData models, which could impact performance.
        /// </remarks>
        public int? MaxToolCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum depth for navigation property traversal.
        /// </summary>
        /// <value>The maximum depth for following navigation properties.</value>
        /// <remarks>
        /// This prevents infinite recursion when generating navigation tools
        /// for models with circular references.
        /// </remarks>
        public int MaxNavigationDepth { get; set; } = 3;

        /// <summary>
        /// Gets or sets custom metadata to include in all generated tools.
        /// </summary>
        /// <value>A dictionary of custom metadata key-value pairs.</value>
        /// <remarks>
        /// This metadata will be added to all generated tools and can be used
        /// for custom processing or filtering logic.
        /// </remarks>
        public Dictionary<string, object> CustomMetadata { get; set; } = [];

        /// <summary>
        /// Gets or sets the tool version for generated tools.
        /// </summary>
        /// <value>The version string to assign to generated tools.</value>
        /// <remarks>
        /// This version is used for tool identification and compatibility checking.
        /// It should follow semantic versioning principles.
        /// </remarks>
        public string ToolVersion { get; set; } = "1.0.0";

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolGenerationOptions"/> class.
        /// </summary>
        public McpToolGenerationOptions()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates default options for tool generation.
        /// </summary>
        /// <returns>A new instance with default settings.</returns>
        public static McpToolGenerationOptions Default()
        {
            return new McpToolGenerationOptions();
        }

        /// <summary>
        /// Creates options optimized for performance.
        /// </summary>
        /// <returns>A new instance with performance-optimized settings.</returns>
        public static McpToolGenerationOptions Performance()
        {
            return new McpToolGenerationOptions
            {
                OptimizeForPerformance = true,
                IncludeExamples = false,
                IncludeDetailedSchemas = false,
                GenerateBatchTools = false,
                MaxNavigationDepth = 1
            };
        }

        /// <summary>
        /// Creates options for read-only access.
        /// </summary>
        /// <returns>A new instance configured for read-only operations.</returns>
        public static McpToolGenerationOptions ReadOnly()
        {
            return new McpToolGenerationOptions
            {
                IncludeOperations =
                [
                    McpToolOperationType.Read, 
                    McpToolOperationType.Query, 
                    McpToolOperationType.Navigate 
                ],
                GenerateBatchTools = false
            };
        }

        /// <summary>
        /// Determines whether the specified entity type should be included.
        /// </summary>
        /// <param name="entityTypeName">The entity type name to check.</param>
        /// <returns><c>true</c> if the entity type should be included; otherwise, <c>false</c>.</returns>
        public bool ShouldIncludeEntityType(string entityTypeName)
        {
            if (string.IsNullOrWhiteSpace(entityTypeName))
            {
                return false;
            }

            // Check exclusions first
            if (ExcludeEntityTypes.Contains(entityTypeName))
            {
                return false;
            }

            // If no inclusions specified, include all (except excluded)
            if (IncludeEntityTypes.Count == 0)
            {
                return true;
            }

            // Check if explicitly included
            return IncludeEntityTypes.Contains(entityTypeName);
        }

        /// <summary>
        /// Determines whether the specified operation should be included.
        /// </summary>
        /// <param name="operationType">The operation type to check.</param>
        /// <returns><c>true</c> if the operation should be included; otherwise, <c>false</c>.</returns>
        public bool ShouldIncludeOperation(McpToolOperationType operationType)
        {
            // Check exclusions first
            if (ExcludeOperations.Contains(operationType))
            {
                return false;
            }

            // If no inclusions specified, include all (except excluded)
            if (IncludeOperations.Count == 0)
            {
                return true;
            }

            // Check if explicitly included
            return IncludeOperations.Contains(operationType);
        }

        /// <summary>
        /// Gets the required scopes for the specified entity type.
        /// </summary>
        /// <param name="entityTypeName">The entity type name.</param>
        /// <returns>The required scopes for the entity type.</returns>
        public IEnumerable<string> GetEntityScopes(string entityTypeName)
        {
            var scopes = new List<string>(DefaultRequiredScopes);

            if (EntityScopes.TryGetValue(entityTypeName, out var entitySpecificScopes))
            {
                scopes.AddRange(entitySpecificScopes);
            }

            return scopes.Distinct();
        }

        /// <summary>
        /// Gets the required scopes for the specified operation type.
        /// </summary>
        /// <param name="operationType">The operation type.</param>
        /// <returns>The required scopes for the operation type.</returns>
        public IEnumerable<string> GetOperationScopes(McpToolOperationType operationType)
        {
            var scopes = new List<string>(DefaultRequiredScopes);

            if (OperationScopes.TryGetValue(operationType, out var operationSpecificScopes))
            {
                scopes.AddRange(operationSpecificScopes);
            }

            return scopes.Distinct();
        }

        /// <summary>
        /// Gets the required scopes for a specific entity type and operation combination.
        /// </summary>
        /// <param name="entityTypeName">The entity type name.</param>
        /// <param name="operationType">The operation type.</param>
        /// <returns>The combined required scopes.</returns>
        public IEnumerable<string> GetCombinedScopes(string entityTypeName, McpToolOperationType operationType)
        {
            var scopes = new HashSet<string>(DefaultRequiredScopes);

            if (EntityScopes.TryGetValue(entityTypeName, out var entitySpecificScopes))
            {
                foreach (var scope in entitySpecificScopes)
                {
                    scopes.Add(scope);
                }
            }

            if (OperationScopes.TryGetValue(operationType, out var operationSpecificScopes))
            {
                foreach (var scope in operationSpecificScopes)
                {
                    scopes.Add(scope);
                }
            }

            return scopes;
        }

        /// <summary>
        /// Formats a tool name with the configured prefix and suffix.
        /// </summary>
        /// <param name="baseName">The base name of the tool.</param>
        /// <returns>The formatted tool name.</returns>
        public string FormatToolName(string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
            {
                return baseName;
            }

            var formatted = baseName;

            if (!string.IsNullOrWhiteSpace(ToolNamePrefix))
            {
                formatted = $"{ToolNamePrefix}{formatted}";
            }

            if (!string.IsNullOrWhiteSpace(ToolNameSuffix))
            {
                formatted = $"{formatted}{ToolNameSuffix}";
            }

            return formatted;
        }

        /// <summary>
        /// Validates the options for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the options are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (MaxToolCount.HasValue && MaxToolCount.Value <= 0)
            {
                errors.Add("MaxToolCount must be greater than zero when specified.");
            }

            if (MaxNavigationDepth < 0)
            {
                errors.Add("MaxNavigationDepth cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(ToolVersion))
            {
                errors.Add("ToolVersion cannot be null or whitespace.");
            }

            // Check for conflicting entity type inclusions/exclusions
            var conflictingEntityTypes = IncludeEntityTypes.Intersect(ExcludeEntityTypes).ToList();
            if (conflictingEntityTypes.Count > 0)
            {
                errors.Add($"Entity types cannot be both included and excluded: {string.Join(", ", conflictingEntityTypes)}");
            }

            // Check for conflicting operation inclusions/exclusions
            var conflictingOperations = IncludeOperations.Intersect(ExcludeOperations).ToList();
            if (conflictingOperations.Count > 0)
            {
                errors.Add($"Operations cannot be both included and excluded: {string.Join(", ", conflictingOperations)}");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of these options.
        /// </summary>
        /// <returns>A new instance with the same settings as this instance.</returns>
        public McpToolGenerationOptions Clone()
        {
            return new McpToolGenerationOptions
            {
                GenerateCrudTools = GenerateCrudTools,
                GenerateQueryTools = GenerateQueryTools,
                GenerateNavigationTools = GenerateNavigationTools,
                GenerateBatchTools = GenerateBatchTools,
                GenerateEntitySetTools = GenerateEntitySetTools,
                IncludeEntityTypes = new HashSet<string>(IncludeEntityTypes),
                ExcludeEntityTypes = new HashSet<string>(ExcludeEntityTypes),
                IncludeOperations = new HashSet<McpToolOperationType>(IncludeOperations),
                ExcludeOperations = new HashSet<McpToolOperationType>(ExcludeOperations),
                DefaultRequiredScopes = new List<string>(DefaultRequiredScopes),
                DefaultRequiredRoles = new List<string>(DefaultRequiredRoles),
                EntityScopes = new Dictionary<string, List<string>>(EntityScopes.ToDictionary(kvp => kvp.Key, kvp => new List<string>(kvp.Value))),
                OperationScopes = new Dictionary<McpToolOperationType, List<string>>(OperationScopes.ToDictionary(kvp => kvp.Key, kvp => new List<string>(kvp.Value))),
                ToolNamePrefix = ToolNamePrefix,
                ToolNameSuffix = ToolNameSuffix,
                IncludeExamples = IncludeExamples,
                IncludeDetailedSchemas = IncludeDetailedSchemas,
                OptimizeForPerformance = OptimizeForPerformance,
                MaxToolCount = MaxToolCount,
                MaxNavigationDepth = MaxNavigationDepth,
                CustomMetadata = new Dictionary<string, object>(CustomMetadata),
                ToolVersion = ToolVersion
            };
        }

        #endregion

    }

}
