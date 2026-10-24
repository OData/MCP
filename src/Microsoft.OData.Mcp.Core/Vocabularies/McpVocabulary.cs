// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.OData.Mcp.Core.Vocabularies
{

    /// <summary>
    /// Identifiers and CSDL for the <c>Microsoft.OData.Mcp.V1</c> vocabulary.
    /// </summary>
    /// <remarks>
    /// Core ships the vocabulary document so the CSDL parser and the Tools host can read
    /// <c>Task</c> / <c>ElicitUrlProperties</c> from remote <c>$metadata</c>. AspNetCore loads the
    /// same XML into EdmLib when a host stamps those terms onto an <c>IEdmModel</c>.
    /// </remarks>
    public static class McpVocabulary
    {

        #region Fields

        /// <summary>
        /// Preferred include alias for <see cref="Namespace"/>.
        /// </summary>
        public const string Alias = "Mcp";

        /// <summary>
        /// JSON property names elicited when <see cref="ElicitUrlPropertiesTerm"/> is omitted.
        /// </summary>
        public static readonly string[] DefaultElicitUrlProperties = ["connectUri", "url", "authorizationUrl", "verificationUri"];

        /// <summary>
        /// Qualified <c>ElicitUrlProperties</c> term.
        /// </summary>
        public const string ElicitUrlPropertiesTerm = Namespace + "." + ElicitUrlPropertiesTermName;

        /// <summary>
        /// Unqualified <c>ElicitUrlProperties</c> term name.
        /// </summary>
        public const string ElicitUrlPropertiesTermName = "ElicitUrlProperties";

        /// <summary>
        /// Vocabulary namespace.
        /// </summary>
        public const string Namespace = "Microsoft.OData.Mcp.V1";

        /// <summary>
        /// Manifest name of the embedded CSDL document.
        /// </summary>
        public const string ResourceName = "Microsoft.OData.Mcp.V1.xml";

        /// <summary>
        /// Qualified <c>Task</c> term.
        /// </summary>
        public const string TaskTerm = Namespace + "." + TaskTermName;

        /// <summary>
        /// Unqualified <c>Task</c> term name.
        /// </summary>
        public const string TaskTermName = "Task";

        /// <summary>
        /// Sentence appended to a named action tool when <see cref="TaskTerm"/> is true.
        /// </summary>
        public const string TaskToolDescription = "Runs as an MCP task when the client opts in; open any returned URL to finish.";

        static readonly Lazy<string> EmbeddedCsdl = new(LoadCsdlXml, true);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the vocabulary CSDL XML.
        /// </summary>
        /// <value>The embedded <c>Microsoft.OData.Mcp.V1.xml</c> document.</value>
        public static string CsdlXml => EmbeddedCsdl.Value;

        #endregion

        #region Internal Methods

        /// <summary>
        /// Reads the embedded vocabulary document.
        /// </summary>
        /// <returns>
        /// The CSDL XML.
        /// </returns>
        /// <exception cref="InvalidOperationException">The resource is missing from the assembly.</exception>
        internal static string LoadCsdlXml()
        {
            var assembly = typeof(McpVocabulary).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found in {assembly.GetName().Name}.");
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }

        #endregion

    }

}
