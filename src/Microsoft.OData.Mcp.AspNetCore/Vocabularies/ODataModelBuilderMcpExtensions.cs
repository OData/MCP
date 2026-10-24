// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.OData.Mcp.AspNetCore.Vocabularies;

namespace Microsoft.OData.ModelBuilder
{

    /// <summary>
    /// ModelBuilder extensions that stamp MCP Task annotations during <c>GetEdmModel</c>.
    /// </summary>
    public static class ODataModelBuilderMcpExtensions
    {

        #region Public Methods

        /// <summary>
        /// Marks this action as an MCP task. <c>GetEdmModel</c> attaches
        /// <c>Microsoft.OData.Mcp.V1</c> and stamps the Task term.
        /// </summary>
        /// <param name="action">The action to annotate.</param>
        /// <param name="elicitUrlProperties">Optional JSON property names for <c>ElicitUrlProperties</c>.</param>
        /// <returns>
        /// <paramref name="action"/> so further configuration can chain.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// builder.EntityType&lt;Order&gt;().Action("Ship").HasMcpTask().Returns&lt;bool&gt;();
        /// </code>
        /// </example>
        /// <remarks>
        /// The host does not wrap <c>GetEdmModel</c>. The vocabulary is referenced the first time this
        /// configuration is applied.
        /// </remarks>
        public static ActionConfiguration HasMcpTask(this ActionConfiguration action, params string[] elicitUrlProperties)
        {
            ArgumentNullException.ThrowIfNull(action);

            var configuration = new McpTaskTermConfiguration();
            if (elicitUrlProperties is { Length: > 0 })
            {
                configuration.ElicitUrlProperties = elicitUrlProperties;
            }

            action.VocabularyTermConfigurations[typeof(McpTaskTermConfiguration)] = configuration;
            return action;
        }

        #endregion

    }

}
