// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
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

        #endregion

    }

}
