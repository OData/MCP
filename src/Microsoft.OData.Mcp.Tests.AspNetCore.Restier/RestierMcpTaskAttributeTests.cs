// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// <c>[McpTask]</c> on Restier API methods stamps the finished EDM at <c>UseODataMcp</c>.
    /// </summary>
    [TestClass]
    public class RestierMcpTaskAttributeTests : RestierToolTestBase
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierMcpTaskAttributeTests"/> class.
        /// </summary>
        public RestierMcpTaskAttributeTests()
            : base(nameof(RestierMcpTaskAttributeTests))
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// HTTP GET <c>odata/$metadata</c> includes the vocabulary reference and a parseable Task term on
        /// <c>Share</c>. Sibling <c>Reset</c> stays one-shot.
        /// </summary>
        [TestMethod]
        public async Task HttpMetadata_ExportsReferenceAndTaskTerm()
        {
            using var client = CreateClient();
            using var response = await client.GetAsync("odata/$metadata");
            var xml = await response.Content.ReadAsStringAsync();

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, xml);
            AssertExportsTaskVocabulary(xml, "Share", "Reset");
        }

        /// <summary>
        /// Session CSDL (the MCP <c>odata://odata/$metadata</c> resource) matches GET <c>$metadata</c>.
        /// </summary>
        [TestMethod]
        public void SessionMetadataXml_ExportsReferenceAndTaskTerm()
        {
            AssertExportsTaskVocabulary(Session().MetadataXml, "Share", "Reset");
        }

        /// <summary>
        /// <c>Share</c> is tasked; sibling <c>Reset</c> stays one-shot. No Restier package and no extra host line.
        /// </summary>
        [TestMethod]
        public void UseODataMcp_McpTaskOnShare_StampsTask_LeavesReset()
        {
            var share = Catalog().Tools.Single(tool => tool.Name == "action_mcpcustomer_share");
            var reset = Catalog().Tools.Single(tool => tool.Name == "action_reset");

            share.IsTask.Should().BeTrue();
            share.Description.Should().Contain(McpVocabulary.TaskToolDescription);
            reset.IsTask.Should().BeFalse();
            reset.Description.Should().NotContain(McpVocabulary.TaskToolDescription);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Asserts that <paramref name="xml"/> is CSDL an external client can parse: vocabulary
        /// <c>edmx:Reference</c>, alias <c>Mcp</c>, Task on <paramref name="taskedAction"/>, and
        /// no Task on <paramref name="oneShotAction"/>.
        /// </summary>
        /// <param name="xml">A service <c>$metadata</c> document.</param>
        /// <param name="taskedAction">The action that must have <c>Mcp.Task</c>.</param>
        /// <param name="oneShotAction">The sibling action that must stay unannotated.</param>
        internal static void AssertExportsTaskVocabulary(string? xml, string taskedAction, string oneShotAction)
        {
            xml.Should().NotBeNullOrWhiteSpace();
            xml.Should().Contain(McpVocabulary.ReferenceUri);
            xml.Should().Contain($"Namespace=\"{McpVocabulary.Namespace}\"");
            xml.Should().Contain($"Alias=\"{McpVocabulary.Alias}\"");
            xml.Should().Contain("Term=\"Mcp.Task\"");

            var parsed = new CsdlParser().ParseFromString(xml!);
            var tasked = parsed.Actions.Single(action => action.Name == taskedAction);
            tasked.IsTask.Should().BeTrue();
            tasked.Namespace.Should().Be(typeof(McpCustomer).Namespace);
            parsed.Actions.Single(action => action.Name == oneShotAction).IsTask.Should().BeFalse();

            var document = XDocument.Parse(xml!);
            XNamespace edm = "http://docs.oasis-open.org/odata/ns/edm";
            var action = document.Descendants(edm + "Action")
                .Single(element => element.Attribute("Name")?.Value == taskedAction);
            ((XElement)action.Parent!).Attribute("Namespace")!.Value.Should().Be(typeof(McpCustomer).Namespace);
            action.Elements(edm + "Annotation")
                .Select(element => element.Attribute("Term")?.Value)
                .Should().Contain("Mcp.Task");
            document.Descendants(edm + "Schema")
                .Where(schema => schema.Attribute("Namespace")?.Value == "Default")
                .SelectMany(schema => schema.Elements(edm + "Action"))
                .Should().BeEmpty("Default holds the container; operations live with the entity types");
        }

        #endregion

    }

}
