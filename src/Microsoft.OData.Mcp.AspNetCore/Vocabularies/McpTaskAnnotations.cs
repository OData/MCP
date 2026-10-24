// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Mcp.Core.Vocabularies;

namespace Microsoft.OData.Mcp.AspNetCore.Vocabularies
{

    /// <summary>
    /// Attaches <c>Microsoft.OData.Mcp.V1</c> to an <see cref="EdmModel"/> and stamps Task annotations.
    /// </summary>
    public static class McpTaskAnnotations
    {

        #region Public Methods

        /// <summary>
        /// Adds the MCP vocabulary as a referenced model and an <c>edmx:Reference</c> Include
        /// when the service model does not already have it.
        /// </summary>
        /// <param name="model">The service model being built.</param>
        /// <example>
        /// <code>
        /// McpTaskAnnotations.EnsureReferenced(edmModel);
        /// </code>
        /// </example>
        /// <remarks>
        /// <c>FindTerm</c> only sees terms on referenced models.
        /// <c>SetEdmReferences</c> is merged so ModelBuilder's Core/Capabilities references stay on the model.
        /// </remarks>
        public static void EnsureReferenced(EdmModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (!IsVocabularyReferenced(model))
            {
                model.AddReferencedModel(McpVocabularyModel.Instance);
            }

            var references = model.GetEdmReferences()?.ToList() ?? [];
            if (references.Any(HasMcpInclude))
            {
                return;
            }

            var reference = new EdmReference(new Uri(McpVocabulary.ReferenceUri, UriKind.Absolute));
            reference.AddInclude(new EdmInclude(McpVocabulary.Alias, McpVocabulary.Namespace));
            references.Add(reference);
            model.SetEdmReferences(references);
        }

        /// <summary>
        /// Stamps <c>Microsoft.OData.Mcp.V1.Task</c> on an action, attaching the vocabulary if needed.
        /// </summary>
        /// <param name="model">The service model.</param>
        /// <param name="target">The action to annotate.</param>
        /// <param name="elicitUrlProperties">Optional JSON property names for <c>ElicitUrlProperties</c>.</param>
        /// <example>
        /// <code>
        /// McpTaskAnnotations.StampTask(edmModel, authorizeAction);
        /// </code>
        /// </example>
        /// <remarks>
        /// Used by Restier <c>McpTaskModelBuilder</c> after the inner builder produces operations.
        /// </remarks>
        public static void StampTask(EdmModel model, IEdmVocabularyAnnotatable target, IReadOnlyList<string>? elicitUrlProperties = null)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(target);

            EnsureReferenced(model);

            var term = model.FindTerm(McpVocabulary.TaskTerm)
                ?? throw new InvalidOperationException($"Term '{McpVocabulary.TaskTerm}' was not found after referencing the MCP vocabulary.");
            model.SetVocabularyAnnotation(new EdmVocabularyAnnotation(target, term, new EdmBooleanConstant(true)));

            if (elicitUrlProperties is null || elicitUrlProperties.Count == 0)
            {
                return;
            }

            StampElicitUrlProperties(model, target, elicitUrlProperties);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Returns whether <paramref name="reference"/> includes the MCP vocabulary namespace.
        /// </summary>
        /// <param name="reference">An <c>edmx:Reference</c>.</param>
        /// <returns>
        /// <c>true</c> when an Include targets <see cref="McpVocabulary.Namespace"/>.
        /// </returns>
        internal static bool HasMcpInclude(IEdmReference reference)
        {
            ArgumentNullException.ThrowIfNull(reference);

            return reference.Includes.Any(include =>
                string.Equals(include.Namespace, McpVocabulary.Namespace, StringComparison.Ordinal));
        }

        /// <summary>
        /// Returns whether the MCP vocabulary model is already a referenced model.
        /// </summary>
        /// <param name="model">The service model.</param>
        /// <returns>
        /// <c>true</c> when <see cref="McpVocabulary.TaskTerm"/> is already findable.
        /// </returns>
        internal static bool IsVocabularyReferenced(IEdmModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return model.FindTerm(McpVocabulary.TaskTerm) is not null;
        }

        /// <summary>
        /// Stamps <c>ElicitUrlProperties</c> on <paramref name="target"/>.
        /// </summary>
        /// <param name="model">The service model with the vocabulary referenced.</param>
        /// <param name="target">The action to annotate.</param>
        /// <param name="elicitUrlProperties">JSON property names that are URLs to elicit.</param>
        internal static void StampElicitUrlProperties(EdmModel model, IEdmVocabularyAnnotatable target, IReadOnlyList<string> elicitUrlProperties)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(elicitUrlProperties);

            var term = model.FindTerm(McpVocabulary.ElicitUrlPropertiesTerm)
                ?? throw new InvalidOperationException($"Term '{McpVocabulary.ElicitUrlPropertiesTerm}' was not found after referencing the MCP vocabulary.");
            IEdmExpression[] elements = [.. elicitUrlProperties
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => (IEdmExpression)new EdmStringConstant(name))];
            model.SetVocabularyAnnotation(new EdmVocabularyAnnotation(target, term, new EdmCollectionExpression(elements)));
        }

        #endregion

    }

}
