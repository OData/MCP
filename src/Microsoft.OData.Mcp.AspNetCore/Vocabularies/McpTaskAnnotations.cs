// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Edm.Vocabularies.V1;
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
        /// <param name="description">Optional <c>Org.OData.Core.V1.Description</c> on the action.</param>
        /// <example>
        /// <code>
        /// McpTaskAnnotations.StampTask(edmModel, authorizeAction);
        /// </code>
        /// </example>
        /// <remarks>
        /// Used after OData routes are mapped, when the finished model is already in hand.
        /// The annotation is serialized inline on <paramref name="target"/> so it appears in
        /// the schema that already declares the action.
        /// </remarks>
        public static void StampTask(EdmModel model, IEdmVocabularyAnnotatable target, IReadOnlyList<string>? elicitUrlProperties = null, string? description = null)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(target);

            EnsureReferenced(model);

            var term = model.FindTerm(McpVocabulary.TaskTerm)
                ?? throw new InvalidOperationException($"Term '{McpVocabulary.TaskTerm}' was not found after referencing the MCP vocabulary.");
            SetInlineAnnotation(model, target, term, new EdmBooleanConstant(true));

            if (elicitUrlProperties is { Count: > 0 })
            {
                StampElicitUrlProperties(model, target, elicitUrlProperties);
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                StampDescription(model, target, description);
            }
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
        /// Writes <paramref name="value"/> as an inline child of <paramref name="target"/> so
        /// <c>CsdlWriter</c> emits it in the schema that already declares the operation.
        /// </summary>
        /// <param name="model">The service model.</param>
        /// <param name="target">The annotatable already on <paramref name="model"/> (the <c>IEdmAction</c>).</param>
        /// <param name="term">The vocabulary term.</param>
        /// <param name="value">The annotation value.</param>
        /// <remarks>
        /// OASIS CSDL XML 4.01 §14.2.2: the target MAY be specified by nesting the annotation
        /// in the model element. That uses the action's declared <see cref="IEdmSchemaElement.Namespace"/>;
        /// it does not construct a <c>Target</c> path.
        /// </remarks>
        internal static void SetInlineAnnotation(EdmModel model, IEdmVocabularyAnnotatable target, IEdmTerm term, IEdmExpression value)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(term);
            ArgumentNullException.ThrowIfNull(value);

            var annotation = new EdmVocabularyAnnotation(target, term, value);
            annotation.SetSerializationLocation(model, EdmVocabularyAnnotationSerializationLocation.Inline);
            model.SetVocabularyAnnotation(annotation);
        }

        /// <summary>
        /// Stamps <c>Org.OData.Core.V1.Description</c> on <paramref name="target"/>.
        /// </summary>
        /// <param name="model">The service model.</param>
        /// <param name="target">The action to annotate.</param>
        /// <param name="description">The description text.</param>
        internal static void StampDescription(EdmModel model, IEdmVocabularyAnnotatable target, string description)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);

            if (model.FindTerm(CoreVocabularyConstants.Description) is null)
            {
                model.AddReferencedModel(CoreVocabularyModel.Instance);
            }

            var term = model.FindTerm(CoreVocabularyConstants.Description)
                ?? throw new InvalidOperationException($"Term '{CoreVocabularyConstants.Description}' was not found after referencing the Core vocabulary.");
            SetInlineAnnotation(model, target, term, new EdmStringConstant(description.Trim()));
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
            SetInlineAnnotation(model, target, term, new EdmCollectionExpression(elements));
        }

        /// <summary>
        /// Forces an annotation already on <paramref name="target"/> to serialize inline.
        /// </summary>
        /// <param name="model">The service model.</param>
        /// <param name="target">The annotated element.</param>
        /// <param name="termNamespace">The term namespace.</param>
        /// <param name="termName">The unqualified term name.</param>
        /// <remarks>
        /// ModelBuilder's <c>VocabularyTermConfiguration</c> stamps first; this then pins
        /// serialization so EdmLib does not emit a cross-schema <c>Annotations Target</c>.
        /// </remarks>
        internal static void WriteExistingInline(EdmModel model, IEdmVocabularyAnnotatable target, string termNamespace, string termName)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(termNamespace);
            ArgumentException.ThrowIfNullOrWhiteSpace(termName);

            foreach (var annotation in model.FindDeclaredVocabularyAnnotations(target))
            {
                if (string.Equals(annotation.Term.Namespace, termNamespace, StringComparison.Ordinal)
                    && string.Equals(annotation.Term.Name, termName, StringComparison.Ordinal))
                {
                    annotation.SetSerializationLocation(model, EdmVocabularyAnnotationSerializationLocation.Inline);
                }
            }
        }

        #endregion

    }

}
