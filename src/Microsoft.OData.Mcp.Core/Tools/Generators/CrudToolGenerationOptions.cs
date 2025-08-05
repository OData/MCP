using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Tools.Generators
{
    /// <summary>
    /// Options for controlling CRUD tool generation behavior.
    /// </summary>
    /// <remarks>
    /// These options allow fine-grained control over which tools are generated
    /// and how they behave, including validation, error handling, and feature enablement.
    /// CRUD tool generation can be customized to match specific API patterns,
    /// security requirements, and performance considerations.
    /// </remarks>
    public sealed class CrudToolGenerationOptions
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether to generate CREATE tools.
        /// </summary>
        /// <value><c>true</c> to generate CREATE tools; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// CREATE tools allow AI models to insert new entities into the OData service.
        /// Disable this if the service is read-only or create operations should not
        /// be exposed through the MCP interface.
        /// </remarks>
        public bool GenerateCreateTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate READ tools.
        /// </summary>
        /// <value><c>true</c> to generate READ tools; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// READ tools allow AI models to retrieve entities from the OData service.
        /// This is typically enabled unless the service contains sensitive data
        /// that should not be accessible through MCP.
        /// </remarks>
        public bool GenerateReadTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate UPDATE tools.
        /// </summary>
        /// <value><c>true</c> to generate UPDATE tools; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// UPDATE tools allow AI models to modify existing entities in the OData service.
        /// Disable this for services where data modification should be restricted
        /// or controlled through other mechanisms.
        /// </remarks>
        public bool GenerateUpdateTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate DELETE tools.
        /// </summary>
        /// <value><c>true</c> to generate DELETE tools; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// DELETE tools allow AI models to remove entities from the OData service.
        /// This is often disabled in production scenarios due to the irreversible
        /// nature of delete operations.
        /// </remarks>
        public bool GenerateDeleteTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include complex type properties in tools.
        /// </summary>
        /// <value><c>true</c> to include complex types; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Complex types represent structured data within entities. Including them
        /// in tools allows for complete entity manipulation but may increase tool
        /// complexity. Consider the trade-off between functionality and usability.
        /// </remarks>
        public bool IncludeComplexTypes { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include navigation properties in tools.
        /// </summary>
        /// <value><c>true</c> to include navigation properties; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Navigation properties represent relationships between entities. Including them
        /// can create very complex tools. It's often better to handle relationships
        /// through separate navigation tools rather than CRUD tools.
        /// </remarks>
        public bool IncludeNavigationProperties { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to generate detailed descriptions for tools.
        /// </summary>
        /// <value><c>true</c> to generate detailed descriptions; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Detailed descriptions help AI models understand what each tool does and
        /// how to use it effectively. This improves the quality of AI interactions
        /// but increases the size of tool definitions.
        /// </remarks>
        public bool GenerateDetailedDescriptions { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include validation in generated tools.
        /// </summary>
        /// <value><c>true</c> to include validation; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Validation helps ensure that AI models provide valid input parameters
        /// and can provide helpful error messages when validation fails. This
        /// improves reliability but may impact performance.
        /// </remarks>
        public bool IncludeValidation { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include examples in tool descriptions.
        /// </summary>
        /// <value><c>true</c> to include examples; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Examples demonstrate how to use tools effectively and can significantly
        /// improve AI model performance. However, they increase tool definition size
        /// and may expose sensitive data patterns.
        /// </remarks>
        public bool IncludeExamples { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of properties to include in a single tool.
        /// </summary>
        /// <value>The maximum property count, or null for no limit.</value>
        /// <remarks>
        /// Large entity types can result in tools with too many parameters, making them
        /// difficult for AI models to use effectively. This setting helps limit complexity
        /// by splitting large entities into multiple tools or omitting less important properties.
        /// </remarks>
        public int? MaxPropertiesPerTool { get; set; } = 20;

        /// <summary>
        /// Gets or sets the naming convention to use for generated tool names.
        /// </summary>
        /// <value>The naming convention for tool names.</value>
        /// <remarks>
        /// Consistent naming conventions help AI models understand and predict
        /// tool names. Choose a convention that matches your organization's
        /// standards and is familiar to the AI models you're working with.
        /// </remarks>
        public ToolNamingConvention NamingConvention { get; set; } = ToolNamingConvention.PascalCase;

        /// <summary>
        /// Gets or sets a value indicating whether to use schema descriptions for tool documentation.
        /// </summary>
        /// <value><c>true</c> to use schema descriptions; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// OData schemas may include descriptions and annotations that can be used
        /// to generate better tool documentation. Enable this to leverage existing
        /// schema documentation in your tool descriptions.
        /// </remarks>
        public bool UseSchemaDescriptions { get; set; } = true;

        /// <summary>
        /// Gets or sets custom properties that can be used by specific generators.
        /// </summary>
        /// <value>A dictionary of custom properties for generator-specific configuration.</value>
        /// <remarks>
        /// Custom properties allow extending the configuration with generator-specific
        /// settings that don't fit into the standard options. Different generators
        /// may use these properties for specialized behavior.
        /// </remarks>
        public Dictionary<string, object> CustomProperties { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of entity types to exclude from tool generation.
        /// </summary>
        /// <value>A collection of entity type names to exclude.</value>
        /// <remarks>
        /// Use this to exclude specific entity types from CRUD tool generation,
        /// such as system entities, audit tables, or entities that should only
        /// be accessed through specialized tools.
        /// </remarks>
        public HashSet<string> ExcludedEntityTypes { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of properties to exclude from tool generation.
        /// </summary>
        /// <value>A dictionary mapping entity type names to lists of excluded properties.</value>
        /// <remarks>
        /// Use this to exclude specific properties from tool generation, such as
        /// system fields, computed properties, or sensitive information that
        /// should not be exposed through MCP tools.
        /// </remarks>
        public Dictionary<string, HashSet<string>> ExcludedProperties { get; set; } = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CrudToolGenerationOptions"/> class.
        /// </summary>
        public CrudToolGenerationOptions()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates options optimized for read-only scenarios.
        /// </summary>
        /// <returns>CRUD tool generation options with only read operations enabled.</returns>
        public static CrudToolGenerationOptions ReadOnly()
        {
            return new CrudToolGenerationOptions
            {
                GenerateCreateTools = false,
                GenerateReadTools = true,
                GenerateUpdateTools = false,
                GenerateDeleteTools = false,
                IncludeComplexTypes = true,
                IncludeNavigationProperties = false,
                GenerateDetailedDescriptions = true,
                IncludeValidation = true,
                IncludeExamples = true
            };
        }

        /// <summary>
        /// Creates options optimized for high-security scenarios.
        /// </summary>
        /// <returns>CRUD tool generation options with restricted access and enhanced validation.</returns>
        public static CrudToolGenerationOptions HighSecurity()
        {
            return new CrudToolGenerationOptions
            {
                GenerateCreateTools = false,
                GenerateReadTools = true,
                GenerateUpdateTools = false,
                GenerateDeleteTools = false,
                IncludeComplexTypes = false,
                IncludeNavigationProperties = false,
                GenerateDetailedDescriptions = false,
                IncludeValidation = true,
                IncludeExamples = false,
                MaxPropertiesPerTool = 10
            };
        }

        /// <summary>
        /// Creates options optimized for development and testing scenarios.
        /// </summary>
        /// <returns>CRUD tool generation options with all features enabled for development.</returns>
        public static CrudToolGenerationOptions Development()
        {
            return new CrudToolGenerationOptions
            {
                GenerateCreateTools = true,
                GenerateReadTools = true,
                GenerateUpdateTools = true,
                GenerateDeleteTools = true,
                IncludeComplexTypes = true,
                IncludeNavigationProperties = true,
                GenerateDetailedDescriptions = true,
                IncludeValidation = true,
                IncludeExamples = true,
                MaxPropertiesPerTool = null // No limit
            };
        }

        /// <summary>
        /// Determines whether the specified entity type should be included in tool generation.
        /// </summary>
        /// <param name="entityTypeName">The name of the entity type to check.</param>
        /// <returns><c>true</c> if the entity type should be included; otherwise, <c>false</c>.</returns>
        public bool ShouldIncludeEntityType(string entityTypeName)
        {
            return !ExcludedEntityTypes.Contains(entityTypeName);
        }

        /// <summary>
        /// Determines whether the specified property should be included in tool generation.
        /// </summary>
        /// <param name="entityTypeName">The name of the entity type containing the property.</param>
        /// <param name="propertyName">The name of the property to check.</param>
        /// <returns><c>true</c> if the property should be included; otherwise, <c>false</c>.</returns>
        public bool ShouldIncludeProperty(string entityTypeName, string propertyName)
        {
            return !ExcludedProperties.TryGetValue(entityTypeName, out var excludedProps) ||
                   !excludedProps.Contains(propertyName);
        }

        /// <summary>
        /// Adds an entity type to the exclusion list.
        /// </summary>
        /// <param name="entityTypeName">The name of the entity type to exclude.</param>
        public void ExcludeEntityType(string entityTypeName)
        {
            if (!string.IsNullOrWhiteSpace(entityTypeName))
            {
                ExcludedEntityTypes.Add(entityTypeName);
            }
        }

        /// <summary>
        /// Adds a property to the exclusion list for a specific entity type.
        /// </summary>
        /// <param name="entityTypeName">The name of the entity type.</param>
        /// <param name="propertyName">The name of the property to exclude.</param>
        public void ExcludeProperty(string entityTypeName, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(entityTypeName) || string.IsNullOrWhiteSpace(propertyName))
                return;

            if (!ExcludedProperties.ContainsKey(entityTypeName))
            {
                ExcludedProperties[entityTypeName] = new HashSet<string>();
            }

            ExcludedProperties[entityTypeName].Add(propertyName);
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public CrudToolGenerationOptions Clone()
        {
            var clone = new CrudToolGenerationOptions
            {
                GenerateCreateTools = GenerateCreateTools,
                GenerateReadTools = GenerateReadTools,
                GenerateUpdateTools = GenerateUpdateTools,
                GenerateDeleteTools = GenerateDeleteTools,
                IncludeComplexTypes = IncludeComplexTypes,
                IncludeNavigationProperties = IncludeNavigationProperties,
                GenerateDetailedDescriptions = GenerateDetailedDescriptions,
                IncludeValidation = IncludeValidation,
                IncludeExamples = IncludeExamples,
                MaxPropertiesPerTool = MaxPropertiesPerTool,
                NamingConvention = NamingConvention,
                UseSchemaDescriptions = UseSchemaDescriptions,
                CustomProperties = new Dictionary<string, object>(CustomProperties),
                ExcludedEntityTypes = new HashSet<string>(ExcludedEntityTypes)
            };

            // Deep copy excluded properties
            foreach (var kvp in ExcludedProperties)
            {
                clone.ExcludedProperties[kvp.Key] = new HashSet<string>(kvp.Value);
            }

            return clone;
        }

        #endregion
    }
}