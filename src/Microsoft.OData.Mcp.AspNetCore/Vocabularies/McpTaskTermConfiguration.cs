// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.ModelBuilder;

namespace Microsoft.OData.Mcp.AspNetCore.Vocabularies
{

    /// <summary>
    /// ModelBuilder vocabulary configuration for <c>Microsoft.OData.Mcp.V1.Task</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="SetVocabularyAnnotations"/> attaches the vocabulary before the base apply so
    /// <c>FindTerm</c> succeeds during <c>GetEdmModel</c>.
    /// </remarks>
    internal sealed class McpTaskTermConfiguration : VocabularyTermConfiguration
    {

        #region Properties

        /// <summary>
        /// Gets or sets optional <c>Org.OData.Core.V1.Description</c> text.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets optional JSON property names for <c>ElicitUrlProperties</c>.
        /// </summary>
        public IReadOnlyList<string>? ElicitUrlProperties { get; set; }

        /// <inheritdoc />
        public override string TermName => McpVocabulary.TaskTerm;

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public override void SetVocabularyAnnotations(EdmModel model, IEdmVocabularyAnnotatable target)
        {
            McpTaskAnnotations.EnsureReferenced(model);
            base.SetVocabularyAnnotations(model, target);
            McpTaskAnnotations.WriteExistingInline(model, target, McpVocabulary.Namespace, McpVocabulary.TaskTermName);

            if (ElicitUrlProperties is { Count: > 0 })
            {
                McpTaskAnnotations.StampElicitUrlProperties(model, target, ElicitUrlProperties);
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                McpTaskAnnotations.StampDescription(model, target, Description);
            }
        }

        /// <inheritdoc />
        public override IEdmExpression ToEdmExpression()
        {
            return new EdmBooleanConstant(true);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets the Task configuration on <paramref name="action"/>, adding it when missing so
        /// <c>HasMcpTask</c> and description can share one instance.
        /// </summary>
        /// <param name="action">The action being configured.</param>
        /// <returns>
        /// The Task configuration.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is <see langword="null"/>.</exception>
        internal static McpTaskTermConfiguration GetOrAdd(ActionConfiguration action)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (action.VocabularyTermConfigurations.TryGetValue(typeof(McpTaskTermConfiguration), out var existing)
                && existing is McpTaskTermConfiguration found)
            {
                return found;
            }

            var configuration = new McpTaskTermConfiguration();
            action.VocabularyTermConfigurations[typeof(McpTaskTermConfiguration)] = configuration;
            return configuration;
        }

        #endregion

    }

}
