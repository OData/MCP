// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using CloudNimble.McpEssentials.Core.Tasks.Elicitations;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.ModelBuilder;

namespace Microsoft.OData.Mcp.AspNetCore.Vocabularies
{

    /// <summary>
    /// ModelBuilder vocabulary configuration for <c>Microsoft.OData.Mcp.V1.ElicitForm</c>.
    /// </summary>
    internal sealed class McpElicitFormTermConfiguration : VocabularyTermConfiguration
    {

        #region Properties

        /// <summary>
        /// Gets or sets the live form instance stamped at <c>GetEdmModel</c>.
        /// </summary>
        public IFormElicitationSource? Form { get; set; }

        /// <summary>
        /// Gets or sets the keyed-DI name resolved at Rebuild when <see cref="Form"/> is omitted.
        /// </summary>
        public string? Name { get; set; }

        /// <inheritdoc />
        public override string TermName => McpVocabulary.ElicitFormTerm;

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public override void SetVocabularyAnnotations(EdmModel model, IEdmVocabularyAnnotatable target)
        {
            McpTaskAnnotations.EnsureReferenced(model);
            if (Form is not null)
            {
                McpTaskAnnotations.StampElicitForm(model, target, Form);
                return;
            }

            if (!string.IsNullOrWhiteSpace(Name))
            {
                McpTaskAnnotations.StampElicitFormName(model, target, Name);
                return;
            }

            base.SetVocabularyAnnotations(model, target);
            McpTaskAnnotations.WriteExistingInline(model, target, McpVocabulary.Namespace, McpVocabulary.ElicitFormTermName);
        }

        /// <inheritdoc />
        public override IEdmExpression ToEdmExpression()
        {
            return new EdmBooleanConstant(true);
        }

        #endregion

    }

}
