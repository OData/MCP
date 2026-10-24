// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Parsing
{

    /// <summary>
    /// <c>Microsoft.OData.Mcp.V1.Task</c> and <c>ElicitUrlProperties</c> on actions.
    /// </summary>
    [TestClass]
    public class CsdlParserMcpVocabularyTests
    {

        #region Public Methods

        /// <summary>
        /// A fully qualified Task annotation with no Bool is true.
        /// </summary>
        [TestMethod]
        public void ParseFromString_TaskTermWithoutBool_SetsIsTask()
        {
            var action = ParseAction("""
                <Action Name="Authorize">
                  <Annotation Term="Microsoft.OData.Mcp.V1.Task" />
                </Action>
                """);

            action.IsTask.Should().BeTrue();
            action.ElicitUrlProperties.Should().BeNull();
        }

        /// <summary>
        /// An aliased <c>Mcp.Task</c> term is true when the include alias maps to the MCP namespace.
        /// </summary>
        [TestMethod]
        public void ParseFromString_AliasedTaskTerm_SetsIsTask()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:Reference Uri="https://example/Microsoft.OData.Mcp.V1.xml">
                    <edmx:Include Namespace="Microsoft.OData.Mcp.V1" Alias="Mcp" />
                  </edmx:Reference>
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Authorize">
                        <Annotation Term="Mcp.Task" Bool="true" />
                      </Action>
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var action = new CsdlParser().ParseFromString(xml).Actions.Single();

            action.IsTask.Should().BeTrue();
        }

        /// <summary>
        /// <c>Bool="false"</c> leaves <see cref="Microsoft.OData.Mcp.Core.Models.EdmAction.IsTask"/> false.
        /// </summary>
        [TestMethod]
        public void ParseFromString_TaskTermFalse_LeavesIsTaskFalse()
        {
            var action = ParseAction("""
                <Action Name="Authorize">
                  <Annotation Term="Microsoft.OData.Mcp.V1.Task" Bool="false" />
                </Action>
                """);

            action.IsTask.Should().BeFalse();
        }

        /// <summary>
        /// Include aliases from one parse do not leak into the next parse on the same parser.
        /// </summary>
        [TestMethod]
        public void ParseFromString_IncludeAlias_DoesNotLeakAcrossParses()
        {
            var parser = new CsdlParser();
            const string aliased = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:Reference Uri="https://example/Microsoft.OData.Mcp.V1.xml">
                    <edmx:Include Namespace="Microsoft.OData.Mcp.V1" Alias="Mcp" />
                  </edmx:Reference>
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Authorize">
                        <Annotation Term="Mcp.Task" Bool="true" />
                      </Action>
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            const string leftover = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Authorize">
                        <Annotation Term="Mcp.Task" Bool="true" />
                      </Action>
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;

            parser.ParseFromString(aliased).Actions.Single().IsTask.Should().BeTrue();
            parser.ParseFromString(leftover).Actions.Single().IsTask.Should().BeFalse();
        }

        /// <summary>
        /// An unqualified <c>Task</c> term is ignored.
        /// </summary>
        [TestMethod]
        public void ParseFromString_BareTaskTerm_DoesNotSetIsTask()
        {
            var action = ParseAction("""
                <Action Name="Authorize">
                  <Annotation Term="Task" Bool="true" />
                </Action>
                """);

            action.IsTask.Should().BeFalse();
        }

        /// <summary>
        /// A Task term in another namespace is ignored.
        /// </summary>
        [TestMethod]
        public void ParseFromString_OtherNamespaceTask_DoesNotSetIsTask()
        {
            var action = ParseAction("""
                <Action Name="Authorize">
                  <Annotation Term="Contoso.Other.Task" Bool="true" />
                </Action>
                """);

            action.IsTask.Should().BeFalse();
        }

        /// <summary>
        /// Schema-level Annotations targeting a bound action set IsTask.
        /// </summary>
        [TestMethod]
        public void ParseFromString_TargetedTask_SetsIsTaskOnBoundAction()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="PodOverlord" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="IntegrationConnection">
                        <Key>
                          <PropertyRef Name="Id" />
                        </Key>
                        <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                      </EntityType>
                      <Action Name="Authorize" IsBound="true">
                        <Parameter Name="binding" Type="PodOverlord.IntegrationConnection" />
                      </Action>
                      <Annotations Target="PodOverlord.IntegrationConnection/Authorize">
                        <Annotation Term="Microsoft.OData.Mcp.V1.Task" Bool="true" />
                      </Annotations>
                      <EntityContainer Name="Container">
                        <EntitySet Name="IntegrationConnections" EntityType="PodOverlord.IntegrationConnection" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var action = new CsdlParser().ParseFromString(xml).Actions.Single();

            action.IsTask.Should().BeTrue();
        }

        /// <summary>
        /// <c>ElicitUrlProperties</c> copies the declared string collection.
        /// </summary>
        [TestMethod]
        public void ParseFromString_ElicitUrlProperties_CopiesStrings()
        {
            var action = ParseAction("""
                <Action Name="Authorize">
                  <Annotation Term="Microsoft.OData.Mcp.V1.Task" Bool="true" />
                  <Annotation Term="Microsoft.OData.Mcp.V1.ElicitUrlProperties">
                    <Collection>
                      <String>connectUri</String>
                      <String>authorizationUrl</String>
                    </Collection>
                  </Annotation>
                </Action>
                """);

            action.IsTask.Should().BeTrue();
            action.ElicitUrlProperties.Should().Equal("connectUri", "authorizationUrl");
        }

        /// <summary>
        /// An unannotated action stays one-shot with no elicit property list.
        /// </summary>
        [TestMethod]
        public void ParseFromString_UnannotatedAction_IsNotATask()
        {
            var action = ParseAction("""
                <Action Name="Reset" />
                """);

            action.IsTask.Should().BeFalse();
            action.ElicitUrlProperties.Should().BeNull();
            action.Name.Should().Be("Reset");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses a single action from a minimal schema wrapper.
        /// </summary>
        /// <param name="actionXml">The action element XML.</param>
        /// <returns>
        /// The parsed action.
        /// </returns>
        internal static Microsoft.OData.Mcp.Core.Models.EdmAction ParseAction(string actionXml)
        {
            var xml = $"""
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      {actionXml}
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;

            return new CsdlParser().ParseFromString(xml).Actions.Single();
        }

        #endregion

    }

}
