// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Vocabularies
{

    /// <summary>
    /// Locks the MCP vocabulary CSDL document shipped in Core.
    /// </summary>
    [TestClass]
    public class McpVocabularyTests
    {

        #region Public Methods

        /// <summary>
        /// Term identifiers match ACTIONS.md <c>Microsoft.OData.Mcp.V1</c>.
        /// </summary>
        [TestMethod]
        public void Identifiers_MatchActionsContract()
        {
            McpVocabulary.Alias.Should().Be("Mcp");
            McpVocabulary.Namespace.Should().Be("Microsoft.OData.Mcp.V1");
            McpVocabulary.TaskTermName.Should().Be("Task");
            McpVocabulary.ElicitUrlPropertiesTermName.Should().Be("ElicitUrlProperties");
            McpVocabulary.TaskTerm.Should().Be("Microsoft.OData.Mcp.V1.Task");
            McpVocabulary.ElicitUrlPropertiesTerm.Should().Be("Microsoft.OData.Mcp.V1.ElicitUrlProperties");
            McpVocabulary.ReferenceUri.Should().Be("https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml");
            McpVocabulary.TaskToolDescription.Should().Be("May run as an MCP task when the client opts in.");
            McpVocabulary.DefaultElicitUrlProperties.Should().Equal("connectUri", "url", "authorizationUrl", "verificationUri");
        }

        /// <summary>
        /// The embedded CSDL declares the Task and ElicitUrlProperties terms.
        /// </summary>
        [TestMethod]
        public void CsdlXml_DeclaresTaskAndElicitUrlProperties()
        {
            var xml = McpVocabulary.CsdlXml;

            xml.Should().NotBeNullOrWhiteSpace();
            xml.Should().Contain("Namespace=\"Microsoft.OData.Mcp.V1\"");
            xml.Should().Contain("Alias=\"Mcp\"");
            xml.Should().Contain("Name=\"Task\"");
            xml.Should().Contain("AppliesTo=\"Action\"");
            xml.Should().Contain("Name=\"ElicitUrlProperties\"");
            xml.Should().Contain("Type=\"Core.Tag\"");
            xml.Should().Contain("Type=\"Collection(Edm.String)\"");
        }

        /// <summary>
        /// The vocabulary document is well-formed CSDL the Core parser accepts.
        /// </summary>
        [TestMethod]
        public void CsdlXml_ParsesWithCsdlParser()
        {
            var act = () => new CsdlParser().ParseFromString(McpVocabulary.CsdlXml);

            act.Should().NotThrow();
        }

        #endregion

    }

}
