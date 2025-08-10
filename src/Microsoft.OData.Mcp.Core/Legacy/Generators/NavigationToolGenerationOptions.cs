// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Legacy.Generators
{

    /// <summary>
    /// Options for controlling navigation tool generation behavior.
    /// </summary>
    /// <remarks>
    /// These options allow fine-grained control over which navigation tools are generated
    /// and how they behave, including relationship management and traversal capabilities.
    /// </remarks>
    public sealed class NavigationToolGenerationOptions
    {

        /// <summary>
        /// Gets or sets a value indicating whether to generate tools for getting related entities.
        /// </summary>
        /// <value><c>true</c> to generate get related tools; otherwise, <c>false</c>.</value>
        public bool GenerateGetRelatedTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate tools for adding relationships.
        /// </summary>
        /// <value><c>true</c> to generate add relationship tools; otherwise, <c>false</c>.</value>
        public bool GenerateAddRelationshipTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to generate tools for removing relationships.
        /// </summary>
        /// <value><c>true</c> to generate remove relationship tools; otherwise, <c>false</c>.</value>
        public bool GenerateRemoveRelationshipTools { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include collection navigation properties.
        /// </summary>
        /// <value><c>true</c> to include collection navigations; otherwise, <c>false</c>.</value>
        public bool IncludeCollectionNavigations { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include single navigation properties.
        /// </summary>
        /// <value><c>true</c> to include single navigations; otherwise, <c>false</c>.</value>
        public bool IncludeSingleNavigations { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support query options on related entities.
        /// </summary>
        /// <value><c>true</c> to support query options; otherwise, <c>false</c>.</value>
        public bool SupportQueryOptions { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support filtering on related entities.
        /// </summary>
        /// <value><c>true</c> to support filtering; otherwise, <c>false</c>.</value>
        public bool SupportFilter { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support ordering on related entities.
        /// </summary>
        /// <value><c>true</c> to support ordering; otherwise, <c>false</c>.</value>
        public bool SupportOrderBy { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to support limiting results on related entities.
        /// </summary>
        /// <value><c>true</c> to support top results; otherwise, <c>false</c>.</value>
        public bool SupportTop { get; set; } = true;

        /// <summary>
        /// Gets or sets the default page size for related entity results.
        /// </summary>
        /// <value>The default number of related entities to return.</value>
        public int DefaultPageSize { get; set; } = 25;

        /// <summary>
        /// Gets or sets the maximum page size for related entity results.
        /// </summary>
        /// <value>The maximum number of related entities to return.</value>
        public int MaxPageSize { get; set; } = 500;

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
        /// Gets or sets the list of entity types to exclude from navigation tool generation.
        /// </summary>
        /// <value>A collection of entity type names to exclude.</value>
        public HashSet<string> ExcludedEntityTypes { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of navigation properties to exclude from tool generation.
        /// </summary>
        /// <value>A dictionary mapping entity type names to lists of excluded navigation properties.</value>
        public Dictionary<string, HashSet<string>> ExcludedNavigationProperties { get; set; } = [];

        /// <summary>
        /// Gets or sets custom properties that can be used by specific generators.
        /// </summary>
        /// <value>A dictionary of custom properties for generator-specific configuration.</value>
        public Dictionary<string, object> CustomProperties { get; set; } = [];

    }

}
