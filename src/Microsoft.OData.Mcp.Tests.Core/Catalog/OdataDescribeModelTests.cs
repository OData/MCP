// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// <c>odata_describe_model</c> is the whole-service map: summary by default, complete on request, never EDMX.
    /// </summary>
    [TestClass]
    public class OdataDescribeModelTests
    {

        #region Fields

        /// <summary>
        /// CSDL with two related sets, a complex type, an enum, a bound operation, and unbound operations.
        /// </summary>
        internal const string ShopCsdl = """
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="Shop" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EnumType Name="Status">
                    <Member Name="Open" Value="0" />
                    <Member Name="Closed" Value="1" />
                  </EnumType>
                  <ComplexType Name="Address">
                    <Property Name="Street" Type="Edm.String" />
                    <Property Name="Geo" Type="Shop.Geo" />
                  </ComplexType>
                  <ComplexType Name="Geo">
                    <Property Name="Lat" Type="Edm.Double" Nullable="false" />
                  </ComplexType>
                  <EntityType Name="Customer">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Name" Type="Edm.String" Nullable="false" />
                    <Property Name="Home" Type="Shop.Address" />
                    <NavigationProperty Name="Orders" Type="Collection(Shop.Order)" />
                    <Annotation Term="Org.OData.Core.V1.Description" String="A buyer." />
                  </EntityType>
                  <EntityType Name="Order">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Status" Type="Shop.Status" Nullable="false" />
                    <NavigationProperty Name="Customer" Type="Shop.Customer" />
                  </EntityType>
                  <EntityType Name="Region">
                    <Key>
                      <PropertyRef Name="Code" />
                    </Key>
                    <Property Name="Code" Type="Edm.String" Nullable="false" MaxLength="3" />
                  </EntityType>
                  <Function Name="Top" IsBound="true">
                    <Parameter Name="customers" Type="Collection(Shop.Customer)" />
                    <Parameter Name="count" Type="Edm.Int32" Nullable="false" />
                    <ReturnType Type="Collection(Shop.Customer)" />
                  </Function>
                  <Function Name="Ping">
                    <ReturnType Type="Edm.String" />
                  </Function>
                  <Action Name="Reset">
                    <Parameter Name="hard" Type="Edm.Boolean" />
                  </Action>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Customers" EntityType="Shop.Customer" />
                    <EntitySet Name="Orders" EntityType="Shop.Order" />
                    <EntitySet Name="Regions" EntityType="Shop.Region" />
                    <FunctionImport Name="Ping" Function="Shop.Ping" />
                    <ActionImport Name="Reset" Action="Shop.Reset" />
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        #endregion

        #region Public Methods

        /// <summary>
        /// No arguments renders the summary: one header per set, navigations, no properties, unbound operations last.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_Default_SummaryText()
        {
            var runtime = OdataDescribeTypeTests.Runtime(ShopCsdl);
            var result = await runtime.InvokeAsync("odata_describe_model", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().BeNull();
            result.Text.Should().Be(OdataDescribeTypeTests.Lf("""
                Customer  (set: Customers, key: Id)
                  // A buyer.
                  Orders -> Order[]
                Order  (set: Orders, key: Id)
                  Customer? -> Customer
                Region  (set: Regions, key: Code)
                operations
                  Ping() -> string
                  Reset(hard?: bool) // writes
                """));
            result.Text.Should().NotContain("Name: string", "summary has no property lists");
            result.Text.Should().NotContain("Top(", "bound operations belong to the type, not the service");
        }

        /// <summary>
        /// <c>detail=complete</c> renders every in-scope type in the declaration grammar, then used complex types, then unbound operations.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_Complete_TextIncludesTypesComplexTypesAndOperations()
        {
            var runtime = OdataDescribeTypeTests.Runtime(ShopCsdl);
            var result = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("detail", "complete"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Be(OdataDescribeTypeTests.Lf("""
                Customer  (set: Customers, key: Id)
                  // A buyer.
                  Id: int // key
                  Name: string
                  Home?: Address
                  Orders -> Order[]
                  operations
                    Top(count: int) -> Customer[] // collection

                Order  (set: Orders, key: Id)
                  // filter enums as Shop.Status'{value}'
                  Id: int // key
                  Status: enum(Open|Closed)
                  Customer? -> Customer

                Region  (set: Regions, key: Code)
                  Code: string(3) // key

                Address  (complex)
                  Street?: string
                  Geo?: Geo

                Geo  (complex)
                  Lat: number

                operations
                  Ping() -> string
                  Reset(hard?: bool) // writes
                """));
        }

        /// <summary>
        /// <c>sets</c> scopes complete output to the named sets and the complex types they use.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_CompleteWithSets_ScopesTypesAndComplexTypes()
        {
            var runtime = OdataDescribeTypeTests.Runtime(ShopCsdl);
            var result = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("detail", "complete", "sets", new[] { "orders" }), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().StartWith("Order  (set: Orders, key: Id)");
            result.Text.Should().Contain("Status: enum(Open|Closed)");
            result.Text.Should().NotContain("Name: string", "Customer properties are out of scope");
            result.Text.Should().NotContain("(complex)", "Order uses no complex type");
            result.Text.Should().Contain("\noperations\n  Ping() -> string", "unbound operations are always listed");
        }

        /// <summary>
        /// <c>format=mermaid</c> renders a relationship-only erDiagram with cardinality from collection-ness.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_Mermaid_RelationshipsOnly()
        {
            var runtime = OdataDescribeTypeTests.Runtime(ShopCsdl);
            var result = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("format", "mermaid", "detail", "complete"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().BeNull();
            result.Text.Should().Be(OdataDescribeTypeTests.Lf("""
                erDiagram
                  Customer ||--o{ Order : Orders
                  Order ||--o| Customer : Customer
                  Region
                """));
            result.Text.Should().NotContain("{ string");
            result.Text.Should().NotContain("int");
        }

        /// <summary>
        /// <c>format=json</c> summary carries sets with type, key, navs and the unbound operation map.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_SummaryJson_SetsAndOperations()
        {
            var runtime = OdataDescribeTypeTests.Runtime(ShopCsdl);
            var result = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("format", "json"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            using var document = JsonDocument.Parse(result.StructuredContent!);
            var sets = document.RootElement.GetProperty("sets");
            var customers = sets.GetProperty("Customers");

            sets.EnumerateObject().Select(item => item.Name).Should().Equal("Customers", "Orders", "Regions");
            customers.GetProperty("type").GetString().Should().Be("Customer");
            customers.GetProperty("key").EnumerateArray().Select(item => item.GetString()).Should().Equal("Id");
            customers.GetProperty("description").GetString().Should().Be("A buyer.");
            customers.GetProperty("navs").GetProperty("Orders").GetString().Should().Be("Order[]");
            customers.TryGetProperty("props", out _).Should().BeFalse();
            sets.GetProperty("Regions").TryGetProperty("navs", out _).Should().BeFalse();
            document.RootElement.GetProperty("operations").GetProperty("Ping").GetString().Should().Be("() -> string");
            document.RootElement.GetProperty("operations").GetProperty("Reset").GetString().Should().Be("(hard?: bool) // writes");
            result.StructuredContent.Should().NotContain(":null");
        }

        /// <summary>
        /// <c>format=json</c> complete carries every type body, complex types, and operations.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_CompleteJson_TypesComplexTypesAndOperations()
        {
            var runtime = OdataDescribeTypeTests.Runtime(ShopCsdl);
            var result = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("format", "json", "detail", "complete"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            using var document = JsonDocument.Parse(result.StructuredContent!);
            var types = document.RootElement.GetProperty("types");

            types.GetProperty("Customer").GetProperty("props").GetProperty("Name").GetString().Should().Be("string!");
            types.GetProperty("Customer").GetProperty("ops").GetProperty("Top").GetString().Should().Be("(count: int) -> Customer[] // collection");
            types.GetProperty("Order").GetProperty("enumFilterLiterals").EnumerateArray().Select(item => item.GetString()).Should().Equal("Shop.Status'{value}'");
            document.RootElement.GetProperty("complexTypes").GetProperty("Address").GetProperty("props").GetProperty("Geo").GetString().Should().Be("Geo");
            document.RootElement.GetProperty("complexTypes").GetProperty("Geo").GetProperty("props").GetProperty("Lat").GetString().Should().Be("number!");
            document.RootElement.GetProperty("operations").GetProperty("Reset").GetString().Should().Be("(hard?: bool) // writes");
        }

        /// <summary>
        /// Unknown <c>detail</c>, <c>format</c>, or set names are tool errors; <c>sets</c> must be an array.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_BadArguments_AreErrors()
        {
            var runtime = OdataDescribeTypeTests.Runtime(ShopCsdl);
            var detail = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("detail", "everything"), CancellationToken.None);
            var format = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("format", "xml"), CancellationToken.None);
            var unknownSet = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("sets", new[] { "Ghosts" }), CancellationToken.None);
            var setsString = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("sets", "Customers"), CancellationToken.None);

            detail.IsError.Should().BeTrue();
            detail.Text.Should().Contain("detail").And.Contain("everything").And.Contain("summary").And.Contain("complete");
            format.IsError.Should().BeTrue();
            format.Text.Should().Contain("format").And.Contain("text").And.Contain("json").And.Contain("mermaid");
            unknownSet.IsError.Should().BeTrue();
            unknownSet.Text.Should().Contain("Ghosts").And.Contain("not declared");
            setsString.IsError.Should().BeTrue();
            setsString.Text.Should().Contain("sets").And.Contain("array");
        }

        /// <summary>
        /// Over <c>MaxResponseBytes</c> the tool errors with the size, the set count, and the way out; it never falls back to CSDL.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_Oversize_IsErrorMentioningSetsAndSummary()
        {
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(ShopCsdl), new ODataMcpCatalogOptions { MaxResponseBytes = 40 });
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("detail", "complete"), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("bytes").And.Contain("3 sets").And.Contain("sets").And.Contain("summary");
            result.Text.Should().NotContain("<edmx");
            result.Text.Should().NotContain("$metadata");
        }

        /// <summary>
        /// Live Northwind summary maps every set with its navigations and lists no properties.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_Northwind_SummaryHasSetsAndNavigations()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_describe_model", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("\nCustomer  (set: Customers, key: CustomerID)\n  Orders -> Order[]\n");
            result.Text.Should().Contain("\nOrder_Detail  (set: Order_Details, key: OrderID, ProductID)\n");
            result.Text.Should().NotContain("CompanyName?: string", "summary lists no properties (CompanyName still appears as a key of Customer_and_Suppliers_by_City)");
            result.Text.Should().NotContain("\noperations", "Northwind declares no unbound operations");
        }

        /// <summary>
        /// Live Northwind complete dumps every type with properties in one call.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_Northwind_CompleteHasProperties()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_describe_model", ToolArguments.Of("detail", "complete"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("\n  CompanyName?: string\n");
            result.Text.Should().Contain("\n  ProductID: int // key\n");
        }

        /// <summary>
        /// Live TripPin summary ends with the unbound operations and excludes bound ones.
        /// </summary>
        [TestMethod]
        public async Task DescribeModel_TripPin_SummaryEndsWithUnboundOperations()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadTripPinModelAsync(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_describe_model", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().StartWith("Person  (set: People, key: UserName)\n  Friends -> Person[]\n  BestFriend? -> Person\n  Trips -> Trip[]\n");
            result.Text.Should().EndWith(OdataDescribeTypeTests.Lf("""
                operations
                  GetPersonWithMostFriends() -> Person
                  GetNearestAirport(lat: number, lon: number) -> Airport
                  ResetDataSource() // writes
                """));
            result.Text.Should().NotContain("ShareTrip");
            result.Text.Should().NotContain("GetFavoriteAirline");
        }

        /// <summary>
        /// The eleventh generic sits between describe_type and query.
        /// </summary>
        [TestMethod]
        public async Task Catalog_GenericTools_IncludeDescribeModelAfterDescribeType()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var generic = catalog.Tools.Where(tool => tool.Name.StartsWith("odata_", System.StringComparison.Ordinal)).Select(tool => tool.Name).ToList();
            var describeModel = catalog.Tools.Single(tool => tool.Name == "odata_describe_model");

            generic.Should().HaveCount(11);
            generic.IndexOf("odata_describe_model").Should().Be(generic.IndexOf("odata_describe_type") + 1);
            generic.IndexOf("odata_query").Should().Be(generic.IndexOf("odata_describe_model") + 1);
            describeModel.ReadOnlyHint.Should().BeTrue();
            describeModel.IdempotentHint.Should().BeTrue();
            describeModel.InputSchema.Should().Contain("\"detail\"").And.Contain("\"summary\"").And.Contain("\"complete\"");
            describeModel.InputSchema.Should().Contain("\"format\"").And.Contain("\"mermaid\"");
            describeModel.InputSchema.Should().Contain("\"sets\"");
            describeModel.Description.Should().Contain("Do not read $metadata");
        }

        #endregion

    }

}
