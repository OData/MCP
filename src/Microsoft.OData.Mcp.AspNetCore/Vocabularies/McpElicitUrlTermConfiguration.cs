// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.ModelBuilder;

namespace Microsoft.OData.Mcp.AspNetCore.Vocabularies
{

    /// <summary>
    /// ModelBuilder vocabulary configuration for <c>Microsoft.OData.Mcp.V1.ElicitUrlProperties</c>.
    /// </summary>
    internal sealed class McpElicitUrlTermConfiguration : VocabularyTermConfiguration
    {

        #region Properties

        /// <summary>
        /// Gets or sets JSON property names for <c>ElicitUrlProperties</c>.
        /// </summary>
        public IReadOnlyList<string> PropertyNames { get; set; } = [];

        /// <inheritdoc />
        public override string TermName => McpVocabulary.ElicitUrlPropertiesTerm;

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public override void SetVocabularyAnnotations(EdmModel model, IEdmVocabularyAnnotatable target)
        {
            McpTaskAnnotations.EnsureReferenced(model);
            base.SetVocabularyAnnotations(model, target);
            McpTaskAnnotations.WriteExistingInline(model, target, McpVocabulary.Namespace, McpVocabulary.ElicitUrlPropertiesTermName);
        }

        /// <inheritdoc />
        public override IEdmExpression ToEdmExpression()
        {
            IEdmExpression[] elements = [.. PropertyNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => (IEdmExpression)new EdmStringConstant(name))];

            return new EdmCollectionExpression(elements);
        }

        #endregion

    }

}
