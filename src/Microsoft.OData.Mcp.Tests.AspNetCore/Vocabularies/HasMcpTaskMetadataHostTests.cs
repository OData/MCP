// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Vocabularies
{

    /// <summary>
    /// GET <c>$metadata</c> and the in-process session CSDL export the MCP vocabulary so
    /// Tools and other OData stacks can read <c>Mcp.Task</c>.
    /// </summary>
    [TestClass]
    public class HasMcpTaskMetadataHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a convention host whose bound <c>Authorize</c> action is tasked.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                var builder = new ODataConventionModelBuilder
                {
                    Namespace = typeof(Customer).Namespace!
                };
                builder.EntitySet<Customer>("Customers");
                builder.EntityType<Customer>().Action("Authorize").HasMcpTask().HasMcpElicitUrl("connectUri");
                builder.EntityType<Customer>().Action("Reset");

                services
                    .AddControllers()
                    .AddApplicationPart(typeof(MetadataController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", builder.GetEdmModel());
                    });
                services.AddODataMcp();
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                    app.UseODataMcp();
                });
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// HTTP GET <c>/odata/$metadata</c> includes the vocabulary reference and a parseable Task term on
        /// <c>Authorize</c>. Sibling <c>Reset</c> stays one-shot.
        /// </summary>
        [TestMethod]
        public async Task HttpMetadata_ExportsReferenceAndTaskTerm()
        {
            using var client = TestServer.CreateClient();
            using var response = await client.GetAsync("/odata/$metadata");
            var xml = await response.Content.ReadAsStringAsync();

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, xml);
            AssertExportsTaskVocabulary(xml, "Authorize", "Reset");
        }

        /// <summary>
        /// Session CSDL (the MCP <c>odata://odata/$metadata</c> resource) matches GET <c>$metadata</c>.
        /// </summary>
        [TestMethod]
        public void SessionMetadataXml_ExportsReferenceAndTaskTerm()
        {
            var xml = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].MetadataXml;

            AssertExportsTaskVocabulary(xml, "Authorize", "Reset");
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
            tasked.ElicitUrlProperties.Should().Equal("connectUri");
            tasked.Namespace.Should().Be(typeof(Customer).Namespace);
            parsed.Actions.Single(action => action.Name == oneShotAction).IsTask.Should().BeFalse();

            var document = XDocument.Parse(xml!);
            XNamespace edm = "http://docs.oasis-open.org/odata/ns/edm";
            var action = document.Descendants(edm + "Action")
                .Single(element => element.Attribute("Name")?.Value == taskedAction);
            ((XElement)action.Parent!).Attribute("Namespace")!.Value.Should().Be(typeof(Customer).Namespace);
            action.Elements(edm + "Annotation")
                .Select(element => element.Attribute("Term")?.Value)
                .Should().Contain("Mcp.Task")
                .And.Contain("Mcp.ElicitUrlProperties");
        }

        #endregion

    }

}
