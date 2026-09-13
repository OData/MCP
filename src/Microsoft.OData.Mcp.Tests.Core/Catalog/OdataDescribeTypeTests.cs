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
    /// <c>odata_describe_type</c> renders the compact declaration grammar (text) or compact JSON, never the old nullable blobs.
    /// </summary>
    [TestClass]
    public class OdataDescribeTypeTests
    {

        #region Public Methods

        /// <summary>
        /// The documented fixture renders header, docs, key marker, long-description line, and navigation in the declaration grammar.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Documented_TextMatchesGrammar()
        {
            var runtime = Runtime(CsdlParserDocumentationTests.DocumentedCsdl);
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().BeNull("text is one representation; JSON is not dual-emitted");
            result.Text.Should().Be(Lf("""
                Person  (set: People, key: UserName)
                  // A person who travels.
                  // Person records include friends and trips.
                  // People who travel.
                  // The People entity set in TripPin.
                  UserName: string // key; Unique person name.
                    // Used as the entity key.
                  FirstName: string // Given name.
                  Friends -> Person[] // Other people this person knows.
                """));
        }

        /// <summary>
        /// <c>format=json</c> renders the compact JSON shape with props, docs, and navs and omits empty sections.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Documented_JsonMatchesCompactShape()
        {
            var runtime = Runtime(CsdlParserDocumentationTests.DocumentedCsdl);
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People", "format", "json"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            using var document = JsonDocument.Parse(result.StructuredContent!);
            var person = document.RootElement.GetProperty("Person");

            document.RootElement.EnumerateObject().Should().ContainSingle();
            person.GetProperty("set").GetString().Should().Be("People");
            person.GetProperty("key").EnumerateArray().Select(item => item.GetString()).Should().Equal("UserName");
            person.GetProperty("description").GetString().Should().Be("A person who travels.");
            person.GetProperty("longDescription").GetString().Should().Be("Person records include friends and trips.");
            person.GetProperty("setDescription").GetString().Should().Be("People who travel.");
            person.GetProperty("props").GetProperty("UserName").GetString().Should().Be("string!");
            person.GetProperty("props").GetProperty("FirstName").GetString().Should().Be("string!");
            person.GetProperty("docs").GetProperty("UserName").GetString().Should().Be("Unique person name. Used as the entity key.");
            person.GetProperty("docs").GetProperty("Friends").GetString().Should().Be("Other people this person knows.");
            person.GetProperty("navs").GetProperty("Friends").GetString().Should().Be("Person[]");
            person.TryGetProperty("ops", out _).Should().BeFalse("no bound operations");
            result.StructuredContent.Should().NotContain("nullable");
            result.StructuredContent.Should().NotContain("entityTypeDescription");
            result.StructuredContent.Should().NotContain(":null");
        }

        /// <summary>
        /// Enum properties render members inline, a literal hint once, flags marker, store-generated keys, and short strings.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Enum_RendersMembersLiteralFlagsAndComputed()
        {
            var runtime = Runtime(CsdlParserMoreTests.EnumCsdl);
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Widgets"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Be(Lf("""
                Widget  (set: Widgets, key: Id)
                  // filter enums as NS.Color'{value}', NS.Permissions'{value}'
                  Id?: int // key, store-generated
                  Sequence: int
                  Name: string
                  Color: enum(Red|Green)
                  CreatedOn?: datetime
                  Version?: int64
                  Notes?: string
                  Access?: enum(Read|Write) // flags, comma-separated
                  Code?: string(8)
                """));
        }

        /// <summary>
        /// Bound operations appear on the type with argument types and markers; unbound operations do not.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Operations_ListsBoundOnlyWithMarkers()
        {
            var runtime = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var person = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People"), CancellationToken.None);
            var employee = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Staff", "format", "json"), CancellationToken.None);

            person.IsError.Should().BeFalse(person.Text);
            person.Text.Should().Be(Lf("""
                Person  (set: People, key: UserName)
                  UserName: string // key
                  FirstName: string
                  Age?: int64
                  operations
                    GetFavoriteAirline() -> string
                    Top(count: int) -> Person[] // collection
                    UpdateLastName(lastName: string) -> bool // writes
                """));
            person.Text.Should().NotContain("GetNearestAirport");
            person.Text.Should().NotContain("ResetDataSource");
            person.Text.Should().NotContain("isBound");

            employee.IsError.Should().BeFalse(employee.Text);
            using var document = JsonDocument.Parse(employee.StructuredContent!);
            var ops = document.RootElement.GetProperty("Employee").GetProperty("ops");
            ops.GetProperty("Promote").GetString().Should().Be("() // writes");
            ops.GetProperty("Top").GetString().Should().Be("(count: int) -> Person[] // collection");
            ops.GetProperty("UpdateLastName").GetString().Should().Be("(lastName: string) -> bool // writes");
        }

        /// <summary>
        /// A type with no entity set renders without the set part.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_TypeWithoutSet_OmitsSetFromHeader()
        {
            var runtime = Runtime(CsdlParserMoreTests.RichCsdl);
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Flight"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().StartWith("Flight  (key: Id)");
            result.Text.Should().NotContain("set:");
        }

        /// <summary>
        /// An unknown <c>format</c> is a tool error naming the accepted values.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_UnknownFormat_IsError()
        {
            var runtime = Runtime(CsdlParserDocumentationTests.DocumentedCsdl);
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People", "format", "garbage"), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("format").And.Contain("garbage").And.Contain("text").And.Contain("json");
        }

        /// <summary>
        /// Format is case-insensitive and a JSON null format falls back to text.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_FormatCaseInsensitive_NullIsText()
        {
            var runtime = Runtime(CsdlParserDocumentationTests.DocumentedCsdl);
            var upper = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People", "format", "JSON"), CancellationToken.None);
            var nullFormat = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People", "format", null), CancellationToken.None);

            upper.IsError.Should().BeFalse(upper.Text);
            upper.StructuredContent.Should().NotBeNullOrWhiteSpace();
            nullFormat.IsError.Should().BeFalse(nullFormat.Text);
            nullFormat.StructuredContent.Should().BeNull();
        }

        /// <summary>
        /// Live Northwind Customer renders the grammar with no null docs and no old keys.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Northwind_Customer_TextGrammar()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().StartWith("Customer  (set: Customers, key: CustomerID)");
            result.Text.Should().Contain("\n  CustomerID: string // key\n");
            result.Text.Should().Contain("\n  CompanyName?: string\n", "live Northwind V4 declares CompanyName nullable");
            result.Text.Should().Contain("\n  Orders -> Order[]\n");
            result.Text.Should().Contain("\n  CustomerDemographics -> CustomerDemographic[]");
            result.Text.Should().NotContain("NorthwindModel.", "one type namespace means no namespace on the header");
            result.Text.Should().NotContain("description");
            result.Text.Should().NotContain("nullable");
            result.Text.Should().NotContain("operations");
        }

        /// <summary>
        /// Live TripPin Person lists enums with members and bound operations with markers; unbound operations are absent.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_TripPin_Person_ListsEnumsAndBoundOperations()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadTripPinModelAsync(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().StartWith("Person  (set: People, key: UserName)");
            result.Text.Should().Contain("\n  // filter enums as Trippin.PersonGender'{value}', Trippin.Feature'{value}'\n", "one pattern per distinct enum the type uses, never a concrete member");
            result.Text.Should().Contain("\n  Gender: enum(Male|Female|Unknown)\n");
            result.Text.Should().Contain("\n  Features: enum(Feature1|Feature2|Feature3|Feature4)[]\n");
            result.Text.Should().Contain("\n  LastName?: string\n", "MaxLength 26 is above the inline threshold");
            result.Text.Should().Contain("\n  Friends -> Person[]\n");
            result.Text.Should().Contain("\n  BestFriend? -> Person\n");
            result.Text.Should().Contain("\n  operations\n");
            result.Text.Should().Contain("\n    GetFavoriteAirline() -> Airline\n");
            result.Text.Should().Contain("\n    GetFriendsTrips(userName: string) -> Trip[]\n");
            result.Text.Should().Contain("\n    UpdateLastName(lastName: string) -> bool // writes\n");
            result.Text.Should().Contain("\n    ShareTrip(userName: string, tripId: int) // writes");
            result.Text.Should().NotContain("GetNearestAirport");
            result.Text.Should().NotContain("ResetDataSource");
            result.Text.Should().NotContain("isBound");
            result.Text.Should().NotContain("personInstance", "the binding parameter is omitted");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Normalizes a raw literal to LF line endings so comparisons do not depend on the source file's newlines.
        /// </summary>
        /// <param name="text">The expected text.</param>
        /// <returns>
        /// The text with LF endings.
        /// </returns>
        internal static string Lf(string text)
        {
            return text.Replace("\r\n", "\n");
        }

        /// <summary>
        /// Creates a runtime over a CSDL fixture with an executor that must not be called.
        /// </summary>
        /// <param name="csdl">The CSDL fixture.</param>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal static ODataToolRuntime Runtime(string csdl)
        {
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(csdl), new ODataMcpCatalogOptions());

            return new ODataToolRuntime(catalog, new UnusedODataExecutor());
        }

        #endregion

    }

}
