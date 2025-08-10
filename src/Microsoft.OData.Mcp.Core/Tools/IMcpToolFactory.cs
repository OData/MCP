// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    public interface IMcpToolFactory
    {

        /// <summary>
        /// Generates all MCP tools for the specified OData model.
        /// </summary>
        /// <param name="model">The OData model to generate tools for.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of generated MCP tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        Task<IEnumerable<McpToolDefinition>> GenerateToolsAsync(EdmModel model, McpToolGenerationOptions? options = null);

        /// <summary>
        /// Generates MCP tools for a specific entity type.
        /// </summary>
        /// <param name="entityType">The entity type to generate tools for.</param>
        /// <param name="model">The complete OData model for context.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of generated MCP tool definitions for the entity type.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityType"/> or <paramref name="model"/> is null.</exception>
        Task<IEnumerable<McpToolDefinition>> GenerateEntityToolsAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions? options = null);

        /// <summary>
        /// Generates CRUD operation tools for a specific entity type.
        /// </summary>
        /// <param name="entityType">The entity type to generate CRUD tools for.</param>
        /// <param name="model">The complete OData model for context.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of CRUD tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityType"/> or <paramref name="model"/> is null.</exception>
        Task<IEnumerable<McpToolDefinition>> GenerateCrudToolsAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions? options = null);

        /// <summary>
        /// Generates query tools for the OData model.
        /// </summary>
        /// <param name="model">The OData model to generate query tools for.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of query tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        Task<IEnumerable<McpToolDefinition>> GenerateQueryToolsAsync(EdmModel model, McpToolGenerationOptions? options = null);

        /// <summary>
        /// Generates navigation tools for entity relationships.
        /// </summary>
        /// <param name="entityType">The entity type to generate navigation tools for.</param>
        /// <param name="model">The complete OData model for context.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of navigation tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityType"/> or <paramref name="model"/> is null.</exception>
        Task<IEnumerable<McpToolDefinition>> GenerateNavigationToolsAsync(EdmEntityType entityType, EdmModel model, McpToolGenerationOptions? options = null);

        /// <summary>
        /// Generates tools for entity set operations (collection-level operations).
        /// </summary>
        /// <param name="entitySet">The entity set to generate tools for.</param>
        /// <param name="model">The complete OData model for context.</param>
        /// <param name="options">Options for tool generation.</param>
        /// <returns>A collection of entity set tool definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entitySet"/> or <paramref name="model"/> is null.</exception>
        Task<IEnumerable<McpToolDefinition>> GenerateEntitySetToolsAsync(EdmEntitySet entitySet, EdmModel model, McpToolGenerationOptions? options = null);

        /// <summary>
        /// Validates that the generated tools are compatible with the MCP specification.
        /// </summary>
        /// <param name="tools">The tools to validate.</param>
        /// <returns>A collection of validation errors, or empty if all tools are valid.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is null.</exception>
        IEnumerable<string> ValidateTools(IEnumerable<McpToolDefinition> tools);

        /// <summary>
        /// Gets the tool definition by name.
        /// </summary>
        /// <param name="toolName">The name of the tool to retrieve.</param>
        /// <returns>The tool definition if found; otherwise, null.</returns>
        McpToolDefinition? GetTool(string toolName);

        /// <summary>
        /// Gets all available tool names.
        /// </summary>
        /// <returns>A collection of all tool names that have been generated.</returns>
        IEnumerable<string> GetAvailableToolNames();

        /// <summary>
        /// Filters tools based on user authorization context.
        /// </summary>
        /// <param name="tools">The tools to filter.</param>
        /// <param name="userScopes">The user's OAuth2 scopes.</param>
        /// <param name="userRoles">The user's roles.</param>
        /// <param name="options">Options for authorization filtering.</param>
        /// <returns>A collection of tools the user is authorized to access.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is null.</exception>
        IEnumerable<McpToolDefinition> FilterToolsForUser(IEnumerable<McpToolDefinition> tools, IEnumerable<string> userScopes, IEnumerable<string> userRoles, McpToolGenerationOptions? options = null);

    }

}
