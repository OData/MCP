using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Tools.Generators
{
    /// <summary>
    /// Interface for generating navigation MCP tools from OData entity relationships.
    /// </summary>
    /// <remarks>
    /// Navigation tool generators create MCP tools that allow AI models to traverse
    /// entity relationships and work with related entities, including getting related
    /// entities, adding relationships, and removing relationships.
    /// </remarks>
    public interface INavigationToolGenerator
    {
        /// <summary>
        /// Generates all navigation tools for the specified entity set and its relationships.
        /// </summary>
        /// <param name="entitySet">The entity set to generate tools for.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A collection of generated MCP tools for navigation operations.</returns>
        Task<IEnumerable<McpTool>> GenerateAllNavigationToolsAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            NavigationToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a tool for getting related entities via navigation properties.
        /// </summary>
        /// <param name="entitySet">The source entity set.</param>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navigationProperty">The navigation property to traverse.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A navigation MCP tool for getting related entities.</returns>
        Task<McpTool> GenerateGetRelatedToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            EdmNavigationProperty navigationProperty,
            NavigationToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a tool for adding relationships between entities.
        /// </summary>
        /// <param name="entitySet">The source entity set.</param>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navigationProperty">The navigation property for the relationship.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A navigation MCP tool for adding relationships.</returns>
        Task<McpTool> GenerateAddRelationshipToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            EdmNavigationProperty navigationProperty,
            NavigationToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a tool for removing relationships between entities.
        /// </summary>
        /// <param name="entitySet">The source entity set.</param>
        /// <param name="entityType">The source entity type.</param>
        /// <param name="navigationProperty">The navigation property for the relationship.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A navigation MCP tool for removing relationships.</returns>
        Task<McpTool> GenerateRemoveRelationshipToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            EdmNavigationProperty navigationProperty,
            NavigationToolGenerationOptions options,
            CancellationToken cancellationToken = default);
    }

}
