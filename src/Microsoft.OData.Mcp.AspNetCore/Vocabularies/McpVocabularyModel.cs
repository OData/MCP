// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Edm.Vocabularies.V1;
using Microsoft.OData.Mcp.Core.Vocabularies;

namespace Microsoft.OData.Mcp.AspNetCore.Vocabularies
{

    /// <summary>
    /// EdmLib facade for the <c>Microsoft.OData.Mcp.V1</c> vocabulary shipped in Core.
    /// </summary>
    /// <remarks>
    /// Parse the Core CSDL once and expose the <see cref="IEdmTerm"/> values hosts stamp onto a service model.
    /// </remarks>
    public static class McpVocabularyModel
    {

        #region Fields

        static readonly Lazy<IEdmModel> Parsed = new(Parse, true);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the <c>ElicitUrlProperties</c> term.
        /// </summary>
        public static IEdmTerm ElicitUrlPropertiesTerm =>
            Instance.FindDeclaredTerm(McpVocabulary.ElicitUrlPropertiesTerm)
            ?? throw new InvalidOperationException($"Term '{McpVocabulary.ElicitUrlPropertiesTerm}' was not found in the MCP vocabulary model.");

        /// <summary>
        /// Gets the parsed vocabulary model.
        /// </summary>
        public static IEdmModel Instance => Parsed.Value;

        /// <summary>
        /// Gets the <c>Task</c> term.
        /// </summary>
        public static IEdmTerm TaskTerm =>
            Instance.FindDeclaredTerm(McpVocabulary.TaskTerm)
            ?? throw new InvalidOperationException($"Term '{McpVocabulary.TaskTerm}' was not found in the MCP vocabulary model.");

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses the Core CSDL with the Core vocabulary referenced.
        /// </summary>
        /// <returns>
        /// The vocabulary <see cref="IEdmModel"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">The embedded CSDL does not parse.</exception>
        internal static IEdmModel Parse()
        {
            using var reader = XmlReader.Create(new StringReader(McpVocabulary.CsdlXml));
            if (!CsdlReader.TryParse(reader, [CoreVocabularyModel.Instance], false, out var model, out var errors) || model is null)
            {
                var detail = errors is null ? "unknown parse failure" : string.Join("; ", errors.Select(error => error.ErrorMessage));
                throw new InvalidOperationException($"Failed to parse MCP vocabulary CSDL. {detail}");
            }

            return model;
        }

        #endregion

    }

}
