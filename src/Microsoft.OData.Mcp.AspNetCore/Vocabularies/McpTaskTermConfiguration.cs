// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

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

            if (ElicitUrlProperties is { Count: > 0 })
            {
                McpTaskAnnotations.StampElicitUrlProperties(model, target, ElicitUrlProperties);
            }
        }

        /// <inheritdoc />
        public override IEdmExpression ToEdmExpression()
        {
            return new EdmBooleanConstant(true);
        }

        #endregion

    }

}
