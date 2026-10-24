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
        /// EdmLib serializes bound-action annotations as <c>Namespace.Action(BindingType, …)</c>.
        /// </summary>
        [TestMethod]
        public void ParseFromString_ParameterizedBoundTarget_SetsIsTask()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:Reference Uri="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml">
                    <edmx:Include Namespace="Microsoft.OData.Mcp.V1" Alias="Mcp" />
                  </edmx:Reference>
                  <edmx:DataServices>
                    <Schema Namespace="Host" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="Customer">
                        <Key>
                          <PropertyRef Name="Id" />
                        </Key>
                        <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                      </EntityType>
                      <Action Name="Share" IsBound="true">
                        <Parameter Name="binding" Type="Host.Customer" />
                        <Parameter Name="userName" Type="Edm.String" />
                      </Action>
                      <Annotations Target="Host.Share(Host.Customer, Edm.String)">
                        <Annotation Term="Mcp.Task" Bool="true" />
                      </Annotations>
                      <EntityContainer Name="Container">
                        <EntitySet Name="Customers" EntityType="Host.Customer" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var action = new CsdlParser().ParseFromString(xml).Actions.Single();

            action.IsTask.Should().BeTrue();
        }

        /// <summary>
        /// EdmLib serializes unbound-action annotations as <c>Namespace.Action()</c>.
        /// </summary>
        [TestMethod]
        public void ParseFromString_ParameterizedUnboundTarget_SetsIsTask()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="Host" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Authorize" />
                      <Annotations Target="Host.Authorize()">
                        <Annotation Term="Microsoft.OData.Mcp.V1.Task" Bool="true" />
                      </Annotations>
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var action = new CsdlParser().ParseFromString(xml).Actions.Single();

            action.IsTask.Should().BeTrue();
        }

        /// <summary>
        /// OASIS §14.2.2 allows out-of-line <c>Annotations</c> in a different schema from the action.
        /// </summary>
        [TestMethod]
        public void ParseFromString_CrossSchemaParameterizedTarget_SetsIsTask()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:Reference Uri="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml">
                    <edmx:Include Namespace="Microsoft.OData.Mcp.V1" Alias="Mcp" />
                  </edmx:Reference>
                  <edmx:DataServices>
                    <Schema Namespace="Host" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="Customer">
                        <Key>
                          <PropertyRef Name="Id" />
                        </Key>
                        <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                      </EntityType>
                      <Annotations Target="Default.Authorize(Host.Customer)">
                        <Annotation Term="Mcp.Task" Bool="true" />
                      </Annotations>
                    </Schema>
                    <Schema Namespace="Default" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Authorize" IsBound="true">
                        <Parameter Name="bindingParameter" Type="Host.Customer" />
                      </Action>
                      <Action Name="Reset" />
                      <EntityContainer Name="Container">
                        <EntitySet Name="Customers" EntityType="Host.Customer" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var model = new CsdlParser().ParseFromString(xml);

            model.Actions.Single(action => action.Name == "Authorize").IsTask.Should().BeTrue();
            model.Actions.Single(action => action.Name == "Reset").IsTask.Should().BeFalse();
        }

        /// <summary>
        /// TripPin layout: types and operations share one schema; Task is nested on the Action.
        /// Bound actions have no ActionImport.
        /// </summary>
        [TestMethod]
        public void ParseFromString_TripPinLayout_InlineTaskOnBoundAction()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:Reference Uri="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml">
                    <edmx:Include Namespace="Microsoft.OData.Mcp.V1" Alias="Mcp" />
                  </edmx:Reference>
                  <edmx:DataServices>
                    <Schema Namespace="Trippin" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="Person">
                        <Key>
                          <PropertyRef Name="UserName" />
                        </Key>
                        <Property Name="UserName" Type="Edm.String" Nullable="false" />
                      </EntityType>
                      <Action Name="ShareTrip" IsBound="true">
                        <Parameter Name="personInstance" Type="Trippin.Person" />
                        <Parameter Name="userName" Type="Edm.String" />
                        <Parameter Name="tripId" Type="Edm.Int32" />
                        <Annotation Term="Mcp.Task" Bool="true" />
                      </Action>
                      <Action Name="ResetDataSource" />
                      <EntityContainer Name="Container">
                        <EntitySet Name="People" EntityType="Trippin.Person" />
                        <ActionImport Name="ResetDataSource" Action="Trippin.ResetDataSource" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var model = new CsdlParser().ParseFromString(xml);
            var share = model.Actions.Single(action => action.Name == "ShareTrip");

            share.Namespace.Should().Be("Trippin");
            share.IsBound.Should().BeTrue();
            share.IsTask.Should().BeTrue();
            model.Actions.Single(action => action.Name == "ResetDataSource").IsTask.Should().BeFalse();
            model.EntityContainer!.ActionImports.Should().ContainSingle(import => import.Name == "ResetDataSource");
            model.EntityContainer.ActionImports.Should().NotContain(import => import.Name == "ShareTrip");
        }

        /// <summary>
        /// Restier layout: operations live with the entity types; Default holds only the container;
        /// a second schema holds the return complex type. Task is nested on the Core action.
        /// </summary>
        [TestMethod]
        public void ParseFromString_RestierLayout_InlineTaskUsesDeclaredOperationNamespace()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:Reference Uri="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml">
                    <edmx:Include Namespace="Microsoft.OData.Mcp.V1" Alias="Mcp" />
                  </edmx:Reference>
                  <edmx:DataServices>
                    <Schema Namespace="Host.Core" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="IntegrationConnection">
                        <Key>
                          <PropertyRef Name="Id" />
                        </Key>
                        <Property Name="Id" Type="Edm.Guid" Nullable="false" />
                      </EntityType>
                      <Action Name="Authorize" IsBound="true">
                        <Parameter Name="bindingParameter" Type="Host.Core.IntegrationConnection" />
                        <ReturnType Type="Host.Business.ConnectionAuthorizationTicket" />
                        <Annotation Term="Mcp.Task" Bool="true" />
                      </Action>
                      <Function Name="IsOnline">
                        <ReturnType Type="Edm.Boolean" Nullable="false" />
                      </Function>
                    </Schema>
                    <Schema Namespace="Host.Business" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <ComplexType Name="ConnectionAuthorizationTicket">
                        <Property Name="ConnectUri" Type="Edm.String" />
                      </ComplexType>
                    </Schema>
                    <Schema Namespace="Default" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityContainer Name="Container">
                        <EntitySet Name="IntegrationConnections" EntityType="Host.Core.IntegrationConnection" />
                        <FunctionImport Name="IsOnline" Function="Host.Core.IsOnline" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var model = new CsdlParser().ParseFromString(xml);
            var authorize = model.Actions.Single();

            authorize.Name.Should().Be("Authorize");
            authorize.Namespace.Should().Be("Host.Core");
            authorize.IsTask.Should().BeTrue();
            authorize.ReturnType.Should().Be("Host.Business.ConnectionAuthorizationTicket");
            model.EntityContainer!.Namespace.Should().Be("Default");
            model.EntityContainer.ActionImports.Should().BeEmpty();
            model.EntityContainer.FunctionImports.Should().ContainSingle(import => import.Function == "Host.Core.IsOnline");
        }

        /// <summary>
        /// OASIS Example 42: <c>Container/ActionImport</c> targets the unbound import, which
        /// resolves to the declared action.
        /// </summary>
        [TestMethod]
        public void ParseFromString_ActionImportTarget_SetsIsTask()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="Trippin" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="ResetDataSource">
                      </Action>
                      <Action Name="ShareTrip" IsBound="true">
                        <Parameter Name="personInstance" Type="Trippin.Person" />
                      </Action>
                      <EntityType Name="Person">
                        <Key>
                          <PropertyRef Name="UserName" />
                        </Key>
                        <Property Name="UserName" Type="Edm.String" Nullable="false" />
                      </EntityType>
                      <EntityContainer Name="Container">
                        <ActionImport Name="ResetDataSource" Action="Trippin.ResetDataSource" />
                      </EntityContainer>
                      <Annotations Target="Trippin.Container/ResetDataSource">
                        <Annotation Term="Microsoft.OData.Mcp.V1.Task" Bool="true" />
                      </Annotations>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var model = new CsdlParser().ParseFromString(xml);

            model.Actions.Single(action => action.Name == "ResetDataSource").IsTask.Should().BeTrue();
            model.Actions.Single(action => action.Name == "ShareTrip").IsTask.Should().BeFalse();
        }

        /// <summary>
        /// OASIS Example 42: a slash after an entity type is a property path, not an action.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EntityTypeSlashAction_DoesNotSetIsTask()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="Host.Core" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="IntegrationConnection">
                        <Key>
                          <PropertyRef Name="Id" />
                        </Key>
                        <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                      </EntityType>
                      <Action Name="Authorize" IsBound="true">
                        <Parameter Name="binding" Type="Host.Core.IntegrationConnection" />
                      </Action>
                      <Annotations Target="Host.Core.IntegrationConnection/Authorize">
                        <Annotation Term="Microsoft.OData.Mcp.V1.Task" Bool="true" />
                      </Annotations>
                      <EntityContainer Name="Container">
                        <EntitySet Name="IntegrationConnections" EntityType="Host.Core.IntegrationConnection" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var action = new CsdlParser().ParseFromString(xml).Actions.Single();

            action.IsTask.Should().BeFalse();
        }

        /// <summary>
        /// Collection-bound overload target uses <c>Collection(Type)</c> in the parentheses.
        /// </summary>
        [TestMethod]
        public void ParseFromString_CollectionBoundOverloadTarget_SetsIsTask()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="Host" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="Product">
                        <Key>
                          <PropertyRef Name="Id" />
                        </Key>
                        <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                      </EntityType>
                      <Action Name="Discount" IsBound="true">
                        <Parameter Name="bindingParameter" Type="Collection(Host.Product)" />
                        <Parameter Name="percent" Type="Edm.Decimal" />
                      </Action>
                      <Annotations Target="Host.Discount(Collection(Host.Product))">
                        <Annotation Term="Microsoft.OData.Mcp.V1.Task" Bool="true" />
                      </Annotations>
                      <EntityContainer Name="Container">
                        <EntitySet Name="Products" EntityType="Host.Product" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var action = new CsdlParser().ParseFromString(xml).Actions.Single();

            action.IsTask.Should().BeTrue();
            action.BindingParameterType.Should().Be("Collection(Host.Product)");
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
