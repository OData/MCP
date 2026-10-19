// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.OData.Mcp.Tests.Core.Catalog.ODataDescribeTypeTests;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Complex and enumeration types, including those declared in a second schema namespace, are describable and
    /// appear in the complete model when only an operation references them.
    /// </summary>
    [TestClass]
    public class ODataDescribeTypeNamespaceTests
    {

        #region Fields

        /// <summary>
        /// Two schemas whose short type names collide: <c>Core.Address</c> is complex, <c>Business.Address</c> is an enumeration.
        /// </summary>
        internal const string CollidingCsdl = """
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="Core" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <ComplexType Name="Address">
                    <Property Name="Street" Type="Edm.String" />
                    <Property Name="Kind" Type="Business.Address" />
                  </ComplexType>
                  <EntityType Name="Site">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Location" Type="Core.Address" />
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Sites" EntityType="Core.Site" />
                  </EntityContainer>
                </Schema>
                <Schema Namespace="Business" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EnumType Name="Address">
                    <Member Name="Home" Value="0" />
                    <Member Name="Work" Value="1" />
                  </EnumType>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        /// <summary>
        /// Entities in <c>PodOverlord.Core</c>; a bound and an unbound action return a complex type declared in
        /// <c>PodOverlord.Business</c>, which uses an enumeration a collection-bound function also returns.
        /// </summary>
        internal const string TwoNamespaceCsdl = """
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="PodOverlord.Core" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="IntegrationConnection">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Guid" Nullable="false" />
                    <Property Name="DisplayName" Type="Edm.String" Nullable="false" />
                    <NavigationProperty Name="Peers" Type="Collection(PodOverlord.Core.IntegrationConnection)" />
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="IntegrationConnections" EntityType="PodOverlord.Core.IntegrationConnection" />
                  </EntityContainer>
                </Schema>
                <Schema Namespace="PodOverlord.Business" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <Action Name="Authorize" IsBound="true">
                    <Parameter Name="bindingParameter" Type="PodOverlord.Core.IntegrationConnection" />
                    <ReturnType Type="PodOverlord.Business.ConnectionAuthorizationTicket" />
                  </Action>
                  <Action Name="Reauthorize">
                    <ReturnType Type="PodOverlord.Business.ConnectionAuthorizationTicket" />
                  </Action>
                  <Function Name="Statuses" IsBound="true">
                    <Parameter Name="bindingParameter" Type="Collection(PodOverlord.Core.IntegrationConnection)" />
                    <ReturnType Type="Collection(PodOverlord.Business.ConnectionAuthorizationStatus)" />
                  </Function>
                  <ComplexType Name="ConnectionAuthorizationTicket">
                    <Property Name="ConnectUri" Type="Edm.String" />
                    <Property Name="ExpiresIn" Type="Edm.Int32" />
                    <Property Name="Message" Type="Edm.String" />
                    <Property Name="Status" Type="PodOverlord.Business.ConnectionAuthorizationStatus" Nullable="false" />
                  </ComplexType>
                  <EnumType Name="ConnectionAuthorizationStatus">
                    <Member Name="Started" Value="0" />
                    <Member Name="Succeeded" Value="1" />
                    <Member Name="Failed" Value="2" />
                  </EnumType>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        #endregion

        #region Public Methods

        /// <summary>
        /// A short name shared by types in two namespaces is a tool error naming both full names; each full name resolves,
        /// and a property reference to a colliding type keeps its namespace.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_CollidingShortName_IsAmbiguousAndReferencesAreQualified()
        {
            var runtime = Runtime(CollidingCsdl);
            var ambiguous = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Address"), CancellationToken.None);
            var complex = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Core.Address"), CancellationToken.None);
            var enumeration = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Business.Address"), CancellationToken.None);
            var site = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Sites"), CancellationToken.None);

            ambiguous.IsError.Should().BeTrue();
            ambiguous.Text.Should().Contain("'Address'").And.Contain("Core.Address").And.Contain("Business.Address");
            complex.IsError.Should().BeFalse(complex.Text);
            complex.Text.Should().StartWith("Core.Address  (complex)");
            enumeration.IsError.Should().BeFalse(enumeration.Text);
            enumeration.Text.Should().StartWith("Business.Address  (enum)");
            site.IsError.Should().BeFalse(site.Text);
            site.Text.Should().Contain("\n  Location?: Core.Address", "the short name Address would not say which of the two types this is");
        }

        /// <summary>
        /// A complex type an entity property holds is filterable, so it keeps the enum literal hint, and no operation returns it.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_ComplexTypeHeldByEntity_KeepsFilterHint()
        {
            var runtime = Runtime(CollidingCsdl);
            var text = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Core.Address"), CancellationToken.None);
            var json = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Core.Address", "format", "json"), CancellationToken.None);

            text.IsError.Should().BeFalse(text.Text);
            text.Text.Should().Be(Lf("""
                Core.Address  (complex)
                  // filter enums as Business.Address'{value}'
                  Street?: string
                  Kind?: enum(Home|Work)
                """));

            json.IsError.Should().BeFalse(json.Text);
            using var document = JsonDocument.Parse(json.StructuredContent!);
            var address = document.RootElement.GetProperty("Core.Address");
            address.GetProperty("enumFilterLiterals").EnumerateArray().Select(item => item.GetString()).Should().Equal("Business.Address'{value}'");
            address.TryGetProperty("returnedBy", out _).Should().BeFalse("no operation returns the type");
        }

        /// <summary>
        /// A complex type from the second namespace resolves by short name, full name, and any casing. Only operations
        /// return it, so it names them and carries no filter hint.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_SecondNamespaceComplexType_ResolvesByShortAndFullName()
        {
            var runtime = Runtime(TwoNamespaceCsdl);
            var expected = Lf("""
                PodOverlord.Business.ConnectionAuthorizationTicket  (complex)
                  // returned by IntegrationConnection.Authorize, Reauthorize
                  ConnectUri?: string
                  ExpiresIn?: int
                  Message?: string
                  Status: enum(Started|Succeeded|Failed)
                """);

            foreach (var name in new[] { "ConnectionAuthorizationTicket", "PodOverlord.Business.ConnectionAuthorizationTicket", "connectionauthorizationticket" })
            {
                var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", name), CancellationToken.None);

                result.IsError.Should().BeFalse(result.Text);
                result.Text.Should().Be(expected, name);
            }
        }

        /// <summary>
        /// <c>format=json</c> for a complex type is the same body the complete model embeds under <c>complexTypes</c>.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_SecondNamespaceComplexType_JsonMatchesCompleteModelBody()
        {
            var runtime = Runtime(TwoNamespaceCsdl);
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "ConnectionAuthorizationTicket", "format", "json"), CancellationToken.None);
            var model = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("detail", "complete", "format", "json"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            model.IsError.Should().BeFalse(model.Text);
            using var document = JsonDocument.Parse(result.StructuredContent!);
            using var modelDocument = JsonDocument.Parse(model.StructuredContent!);
            var ticket = document.RootElement.GetProperty("PodOverlord.Business.ConnectionAuthorizationTicket");

            document.RootElement.EnumerateObject().Should().ContainSingle();
            ticket.GetProperty("props").GetProperty("Status").GetString().Should().Be("enum(Started|Succeeded|Failed)!");
            ticket.GetProperty("props").GetProperty("ExpiresIn").GetString().Should().Be("int");
            ticket.GetProperty("returnedBy").EnumerateArray().Select(item => item.GetString()).Should().Equal("IntegrationConnection.Authorize", "Reauthorize");
            ticket.TryGetProperty("enumFilterLiterals", out _).Should().BeFalse("no entity holds the type, so it cannot be filtered");
            ticket.GetRawText().Should().Be(modelDocument.RootElement.GetProperty("complexTypes").GetProperty("PodOverlord.Business.ConnectionAuthorizationTicket").GetRawText());
        }

        /// <summary>
        /// An enumeration from the second namespace renders the operations that return it, its filter literal (an enum
        /// value is always filterable wherever a property holds it), and its members and values.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_SecondNamespaceEnumType_RendersMembers()
        {
            var runtime = Runtime(TwoNamespaceCsdl);
            var text = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "ConnectionAuthorizationStatus"), CancellationToken.None);
            var json = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "PodOverlord.Business.ConnectionAuthorizationStatus", "format", "json"), CancellationToken.None);

            text.IsError.Should().BeFalse(text.Text);
            text.Text.Should().Be(Lf("""
                PodOverlord.Business.ConnectionAuthorizationStatus  (enum)
                  // returned by IntegrationConnection[].Statuses
                  // filter enums as PodOverlord.Business.ConnectionAuthorizationStatus'{value}'
                  Started = 0
                  Succeeded = 1
                  Failed = 2
                """));

            json.IsError.Should().BeFalse(json.Text);
            using var document = JsonDocument.Parse(json.StructuredContent!);
            var status = document.RootElement.GetProperty("PodOverlord.Business.ConnectionAuthorizationStatus");
            status.GetProperty("members").GetProperty("Succeeded").GetInt64().Should().Be(1);
            status.GetProperty("returnedBy").EnumerateArray().Select(item => item.GetString()).Should().Equal("IntegrationConnection[].Statuses");
            status.GetProperty("enumFilterLiterals").EnumerateArray().Select(item => item.GetString()).Should().Equal("PodOverlord.Business.ConnectionAuthorizationStatus'{value}'");
            status.TryGetProperty("flags", out _).Should().BeFalse("the enumeration is not a flags type");
        }

        /// <summary>
        /// A name qualified with a namespace that does not declare the type is still an unknown type.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_WrongNamespace_IsError()
        {
            var runtime = Runtime(TwoNamespaceCsdl);
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "PodOverlord.Core.ConnectionAuthorizationTicket"), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Type or entity set 'PodOverlord.Core.ConnectionAuthorizationTicket' is not declared in the model.");
        }

        /// <summary>
        /// The complete model includes a complex type that only an operation return type references.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_Complete_IncludesComplexTypesReachedThroughOperations()
        {
            var runtime = Runtime(TwoNamespaceCsdl);
            var result = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("detail", "complete"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("\n    Authorize() -> ConnectionAuthorizationTicket // writes");
            result.Text.Should().Contain("\n\nPodOverlord.Business.ConnectionAuthorizationTicket  (complex)\n");
        }

        /// <summary>
        /// Mermaid navigation targets use the same header name as their source nodes, so one type is one node.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_Mermaid_TargetsMatchQualifiedNodeNames()
        {
            var runtime = Runtime(TwoNamespaceCsdl);
            var result = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("format", "mermaid"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Be(Lf("""
                erDiagram
                  "PodOverlord.Core.IntegrationConnection" ||--o{ "PodOverlord.Core.IntegrationConnection" : Peers
                """));
        }

        #endregion

    }

}
