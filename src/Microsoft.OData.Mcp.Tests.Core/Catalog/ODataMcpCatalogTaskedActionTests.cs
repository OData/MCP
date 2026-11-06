// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Catalog resolution of tasked actions for named tools and <c>odata_call</c>.
    /// </summary>
    [TestClass]
    public class ODataMcpCatalogTaskedActionTests
    {

        #region Public Methods

        /// <summary>
        /// Named action tools copy <c>IsTask</c> and elicit property names from the EDM action.
        /// </summary>
        [TestMethod]
        public void BuildTools_TaskedAction_CopiesIsTaskAndElicitUrlProperties()
        {
            var catalog = Catalog();
            var tool = catalog.Tools.Single(item => item.Name == "action_authorize");

            tool.IsTask.Should().BeTrue();
            tool.ElicitUrlProperties.Should().Equal("connectUri");
            catalog.Tools.Single(item => item.Name == "action_reset").IsTask.Should().BeFalse();
        }

        /// <summary>
        /// <c>odata_call</c> of a tasked action resolves the same EDM action as the named tool.
        /// </summary>
        [TestMethod]
        public void TryGetTaskedAction_ODataCall_MatchesNamedTool()
        {
            var catalog = Catalog();
            var arguments = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("Authorize")
            };

            catalog.TryGetTaskedAction("action_authorize", null, out var named).Should().BeTrue();
            catalog.TryGetTaskedAction("odata_call", arguments, out var call).Should().BeTrue();
            catalog.TryGetTaskedAction("action_reset", null, out _).Should().BeFalse();
            catalog.TryGetTaskedAction("odata_query", null, out _).Should().BeFalse();

            named!.Name.Should().Be("Authorize");
            call!.Name.Should().Be("Authorize");
            named.ElicitUrlProperties.Should().Equal("connectUri");
        }

        /// <summary>
        /// Named action tools copy <c>ElicitForm</c> from the EDM action.
        /// </summary>
        [TestMethod]
        public void BuildTools_ElicitFormAction_CopiesForm()
        {
            var catalog = FormCatalog();
            var tool = catalog.Tools.Single(item => item.Name == "action_ship");

            tool.ElicitForm.Should().NotBeNull();
            tool.ElicitForm!.Fields[0].Name.Should().Be("trackingNumber");
            catalog.Tools.Single(item => item.Name == "action_reset").ElicitForm.Should().BeNull();
        }

        /// <summary>
        /// <c>odata_call</c> of a form-elicited action resolves the same EDM action as the named tool.
        /// Task annotation is not required.
        /// </summary>
        [TestMethod]
        public void TryGetElicitFormAction_ODataCall_MatchesNamedTool()
        {
            var catalog = FormCatalog();
            var arguments = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("Ship")
            };

            catalog.TryGetElicitFormAction("action_ship", null, out var named).Should().BeTrue();
            catalog.TryGetElicitFormAction("odata_call", arguments, out var call).Should().BeTrue();
            catalog.TryGetElicitFormAction("action_reset", null, out _).Should().BeFalse();
            catalog.TryGetElicitFormAction("odata_query", null, out _).Should().BeFalse();
            catalog.TryGetElicitFormAction("odata_call", null, out _).Should().BeFalse();

            named!.Name.Should().Be("Ship");
            call!.Name.Should().Be("Ship");
            named.IsTask.Should().BeFalse();
            named.ElicitForm!.Message.Should().Be("Ship this order");
        }

        /// <summary>
        /// <c>odata_call</c> of an action without a form is not a form elicitation.
        /// </summary>
        [TestMethod]
        public void TryGetElicitFormAction_ODataCall_NonFormAction_IsFalse()
        {
            var catalog = FormCatalog();
            var arguments = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("Reset")
            };

            catalog.TryGetElicitFormAction("odata_call", arguments, out _).Should().BeFalse();
        }

        /// <summary>
        /// Bound and unbound form actions with the same EDM name are distinguished by binding type.
        /// </summary>
        [TestMethod]
        public void TryGetElicitFormAction_BoundAndUnbound_SameName_SelectsByBinding()
        {
            var catalog = BoundAndUnboundFormCatalog();
            catalog.TryGetElicitFormAction("action_ship", null, out var unbound).Should().BeTrue();
            catalog.TryGetElicitFormAction("action_customer_ship", null, out var bound).Should().BeTrue();

            unbound!.ElicitForm!.Message.Should().Be("unbound");
            bound!.ElicitForm!.Message.Should().Be("bound");
        }

        /// <summary>
        /// Bound form actions carry <c>elicit_form</c> on <c>odata_describe_type</c>.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_BoundFormAction_MarksElicitForm()
        {
            var runtime = new ODataToolRuntime(BoundAndUnboundFormCatalog(), new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("    Ship() // writes, elicit_form");
            result.Text.Should().NotContain("// elicit;");
            result.Text.Should().NotContain("// elicit,");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Catalog whose unbound Authorize is tasked and Reset is not.
        /// </summary>
        /// <returns>
        /// The catalog.
        /// </returns>
        internal static ODataMcpCatalog Catalog()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Authorize">
                        <Annotation Term="Microsoft.OData.Mcp.V1.Task" Bool="true" />
                        <Annotation Term="Microsoft.OData.Mcp.V1.ElicitUrlProperties">
                          <Collection>
                            <String>connectUri</String>
                          </Collection>
                        </Annotation>
                      </Action>
                      <Action Name="Reset" />
                      <EntityContainer Name="Container">
                        <ActionImport Name="Authorize" Action="S.Authorize" />
                        <ActionImport Name="Reset" Action="S.Reset" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;

            return new ODataMcpCatalog(new CsdlParser().ParseFromString(xml), new ODataMcpCatalogOptions());
        }

        /// <summary>
        /// Catalog whose unbound Ship elicits a form and Reset does not.
        /// </summary>
        /// <returns>The catalog.</returns>
        internal static ODataMcpCatalog FormCatalog()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Ship">
                        <Annotation Term="Microsoft.OData.Mcp.V1.ElicitForm">
                          <Form xmlns="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml"
                                Message="Ship this order">
                            <Field Name="trackingNumber" Type="string" Required="true"/>
                          </Form>
                        </Annotation>
                      </Action>
                      <Action Name="Reset" />
                      <EntityContainer Name="Container">
                        <ActionImport Name="Ship" Action="S.Ship" />
                        <ActionImport Name="Reset" Action="S.Reset" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;

            return new ODataMcpCatalog(new CsdlParser().ParseFromString(xml), new ODataMcpCatalogOptions());
        }

        /// <summary>
        /// Catalog with bound and unbound Ship actions, both form-elicited.
        /// </summary>
        /// <returns>The catalog.</returns>
        internal static ODataMcpCatalog BoundAndUnboundFormCatalog()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="Customer">
                        <Key><PropertyRef Name="Id"/></Key>
                        <Property Name="Id" Type="Edm.String" Nullable="false"/>
                      </EntityType>
                      <Action Name="Ship" IsBound="true">
                        <Parameter Name="binding" Type="S.Customer"/>
                        <Annotation Term="Microsoft.OData.Mcp.V1.ElicitForm">
                          <Form xmlns="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml"
                                Message="bound">
                            <Field Name="trackingNumber" Type="string"/>
                          </Form>
                        </Annotation>
                      </Action>
                      <Action Name="Ship">
                        <Annotation Term="Microsoft.OData.Mcp.V1.ElicitForm">
                          <Form xmlns="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml"
                                Message="unbound">
                            <Field Name="trackingNumber" Type="string"/>
                          </Form>
                        </Annotation>
                      </Action>
                      <EntityContainer Name="Container">
                        <EntitySet Name="Customers" EntityType="S.Customer"/>
                        <ActionImport Name="Ship" Action="S.Ship"/>
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;

            return new ODataMcpCatalog(new CsdlParser().ParseFromString(xml), new ODataMcpCatalogOptions());
        }

        #endregion

    }

}
