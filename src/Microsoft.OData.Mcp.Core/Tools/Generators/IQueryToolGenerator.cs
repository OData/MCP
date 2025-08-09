using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Tools.Generators
{

    /// <summary>
    /// Interface for generating query MCP tools from OData entity types.
    /// </summary>
    /// <remarks>
    /// Query tool generators create MCP tools that allow AI models to perform advanced
    /// querying operations on OData entities, including filtering, sorting, projection,
    /// and expansion of related entities.
    /// </remarks>
    public interface IQueryToolGenerator
    {

        /// <summary>
        /// Generates all query tools for the specified entity set.
        /// </summary>
        /// <param name="entitySet">The entity set to generate tools for.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A collection of generated MCP tools for query operations.</returns>
        Task<IEnumerable<McpTool>> GenerateAllQueryToolsAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            QueryToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a query tool for listing entities with filtering and sorting.
        /// </summary>
        /// <param name="entitySet">The entity set to query.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A query MCP tool for the entity type.</returns>
        Task<McpTool> GenerateListToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            QueryToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a search tool for full-text search across entity properties.
        /// </summary>
        /// <param name="entitySet">The entity set to search.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A search MCP tool for the entity type.</returns>
        Task<McpTool> GenerateSearchToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            QueryToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a count tool for getting entity counts with optional filtering.
        /// </summary>
        /// <param name="entitySet">The entity set to count.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A count MCP tool for the entity type.</returns>
        Task<McpTool> GenerateCountToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            QueryToolGenerationOptions options,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for controlling query tool generation behavior.
    /// </summary>
    /// <remarks>
    /// These options allow fine-grained control over which query features are enabled
    /// and how they behave, including OData query options and result formatting.
    /// </remarks>
    public sealed class QueryToolGenerationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to generate list tools.
        /// </summary>
        /// <value><c>true</c> to generate list tools; otherwise, <c>false</c>.</value>
        public bool GenerateListTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate search tools.
        /// </summary>
        /// <value><c>true</c> to generate search tools; otherwise, <c>false</c>.</value>
        public bool GenerateSearchTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate count tools.
        /// </summary>
        /// <value><c>true</c> to generate count tools; otherwise, <c>false</c>.</value>
        public bool GenerateCountTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support $filter query option.
        /// </summary>
        /// <value><c>true</c> to support filtering; otherwise, <c>false</c>.</value>
        public bool SupportFilter { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support $orderby query option.
        /// </summary>
        /// <value><c>true</c> to support ordering; otherwise, <c>false</c>.</value>
        public bool SupportOrderBy { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support $select query option.
        /// </summary>
        /// <value><c>true</c> to support projection; otherwise, <c>false</c>.</value>
        public bool SupportSelect { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support $expand query option.
        /// </summary>
        /// <value><c>true</c> to support expansion; otherwise, <c>false</c>.</value>
        public bool SupportExpand { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support $top query option.
        /// </summary>
        /// <value><c>true</c> to support top results; otherwise, <c>false</c>.</value>
        public bool SupportTop { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support $skip query option.
        /// </summary>
        /// <value><c>true</c> to support skipping results; otherwise, <c>false</c>.</value>
        public bool SupportSkip { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support $search query option.
        /// </summary>
        /// <value><c>true</c> to support full-text search; otherwise, <c>false</c>.</value>
        public bool SupportSearch { get; set; } = true;

        /// <summary>
        /// Gets or sets the default page size for query results.
        /// </summary>
        /// <value>The default number of entities to return in a single page.</value>
        public int DefaultPageSize { get; set; } = 50;

        /// <summary>
        /// Gets or sets the maximum page size allowed for queries.
        /// </summary>
        /// <value>The maximum number of entities that can be requested in a single page.</value>
        public int MaxPageSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to generate detailed descriptions for tools.
        /// </summary>
        /// <value><c>true</c> to generate detailed descriptions; otherwise, <c>false</c>.</value>
        public bool GenerateDetailedDescriptions { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include examples in tool descriptions.
        /// </summary>
        /// <value><c>true</c> to include examples; otherwise, <c>false</c>.</value>
        public bool IncludeExamples { get; set; } = true;

        /// <summary>
        /// Gets or sets the naming convention to use for generated tool names.
        /// </summary>
        /// <value>The naming convention for tool names.</value>
        public ToolNamingConvention NamingConvention { get; set; } = ToolNamingConvention.PascalCase;

        /// <summary>
        /// Gets or sets a value indicating whether to use schema descriptions for tool documentation.
        /// </summary>
        /// <value><c>true</c> to use schema descriptions; otherwise, <c>false</c>.</value>
        public bool UseSchemaDescriptions { get; set; } = true;

        /// <summary>
        /// Gets or sets the list of entity types to exclude from tool generation.
        /// </summary>
        /// <value>A collection of entity type names to exclude.</value>
        public HashSet<string> ExcludedEntityTypes { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of properties to exclude from filtering and sorting.
        /// </summary>
        /// <value>A dictionary mapping entity type names to lists of excluded properties.</value>
        public Dictionary<string, HashSet<string>> ExcludedProperties { get; set; } = [];

        /// <summary>
        /// Gets or sets custom properties that can be used by specific generators.
        /// </summary>
        /// <value>A dictionary of custom properties for generator-specific configuration.</value>
        public Dictionary<string, object> CustomProperties { get; set; } = [];

    }

}
