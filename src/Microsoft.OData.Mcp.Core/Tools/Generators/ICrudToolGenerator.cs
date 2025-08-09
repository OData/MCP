using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Tools.Generators
{

    /// <summary>
    /// Interface for generating CRUD (Create, Read, Update, Delete) MCP tools from OData entity types.
    /// </summary>
    /// <remarks>
    /// CRUD tool generators create MCP tools that allow AI models to perform basic data operations
    /// on OData entities. Each generator is responsible for creating tools for specific operations
    /// like creating new entities, reading existing ones, updating properties, and deleting entities.
    /// The generated tools include appropriate parameter validation, error handling, and documentation
    /// to ensure they can be used effectively by AI models.
    /// </remarks>
    public interface ICrudToolGenerator
    {

        /// <summary>
        /// Generates all CRUD tools for the specified entity set.
        /// </summary>
        /// <param name="entitySet">The entity set to generate tools for.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A collection of generated MCP tools for CRUD operations.</returns>
        /// <remarks>
        /// This method generates a complete set of CRUD tools based on the provided options.
        /// The number and type of tools generated depends on the options configuration,
        /// such as whether create, update, or delete operations are enabled.
        /// </remarks>
        /// <example>
        /// <code>
        /// var options = new CrudToolGenerationOptions
        /// {
        ///     GenerateCreateTools = true,
        ///     GenerateReadTools = true,
        ///     GenerateUpdateTools = false,
        ///     GenerateDeleteTools = false
        /// };
        /// 
        /// var tools = await generator.GenerateAllCrudToolsAsync(entitySet, entityType, options);
        /// foreach (var tool in tools)
        /// {
        ///     Console.WriteLine($"Generated tool: {tool.Name}");
        /// }
        /// </code>
        /// </example>
        Task<IEnumerable<McpTool>> GenerateAllCrudToolsAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a CREATE tool for the specified entity type.
        /// </summary>
        /// <param name="entitySet">The entity set to create entities in.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A CREATE MCP tool for the entity type.</returns>
        /// <remarks>
        /// The CREATE tool allows AI models to insert new entities into the specified entity set.
        /// The tool includes parameters for all non-computed properties and appropriate validation
        /// based on the entity type definition and generation options.
        /// </remarks>
        /// <example>
        /// <code>
        /// var createTool = await generator.GenerateCreateToolAsync(entitySet, entityType, options);
        /// // The tool can then be used to create new entities:
        /// // createCustomer(name: "John Doe", email: "john@example.com")
        /// </code>
        /// </example>
        Task<McpTool> GenerateCreateToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a READ tool for the specified entity type.
        /// </summary>
        /// <param name="entitySet">The entity set to read entities from.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A READ MCP tool for the entity type.</returns>
        /// <remarks>
        /// The READ tool allows AI models to retrieve entities from the specified entity set.
        /// The tool typically includes parameters for key values and optional filtering,
        /// and returns the requested entity data in a structured format.
        /// </remarks>
        /// <example>
        /// <code>
        /// var readTool = await generator.GenerateReadToolAsync(entitySet, entityType, options);
        /// // The tool can then be used to read entities:
        /// // readCustomer(customerId: 123)
        /// </code>
        /// </example>
        Task<McpTool> GenerateReadToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates an UPDATE tool for the specified entity type.
        /// </summary>
        /// <param name="entitySet">The entity set containing entities to update.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An UPDATE MCP tool for the entity type.</returns>
        /// <remarks>
        /// The UPDATE tool allows AI models to modify existing entities in the specified entity set.
        /// The tool includes parameters for key values to identify the entity and optional
        /// parameters for each modifiable property. Supports both full and partial updates.
        /// </remarks>
        /// <example>
        /// <code>
        /// var updateTool = await generator.GenerateUpdateToolAsync(entitySet, entityType, options);
        /// // The tool can then be used to update entities:
        /// // updateCustomer(customerId: 123, email: "newemail@example.com")
        /// </code>
        /// </example>
        Task<McpTool> GenerateUpdateToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a DELETE tool for the specified entity type.
        /// </summary>
        /// <param name="entitySet">The entity set containing entities to delete.</param>
        /// <param name="entityType">The entity type definition.</param>
        /// <param name="options">Options controlling tool generation behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A DELETE MCP tool for the entity type.</returns>
        /// <remarks>
        /// The DELETE tool allows AI models to remove entities from the specified entity set.
        /// The tool includes parameters for key values to identify the entity to delete
        /// and may include safety checks or confirmation prompts based on options.
        /// </remarks>
        /// <example>
        /// <code>
        /// var deleteTool = await generator.GenerateDeleteToolAsync(entitySet, entityType, options);
        /// // The tool can then be used to delete entities:
        /// // deleteCustomer(customerId: 123)
        /// </code>
        /// </example>
        Task<McpTool> GenerateDeleteToolAsync(
            EdmEntitySet entitySet,
            EdmEntityType entityType,
            CrudToolGenerationOptions options,
            CancellationToken cancellationToken = default);

    }

}
