// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// <c>odata_list_operations</c> lists unbound operations only, as compact signatures.
    /// </summary>
    [TestClass]
    public class OdataListOperationsTests
    {

        #region Public Methods

        /// <summary>
        /// Unbound functions and actions render as a name-to-signature map; bound operations are absent.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_UnboundOnly_CompactSignatures()
        {
            var runtime = OdataDescribeTypeTests.Runtime(OdataDescribeModelTests.ShopCsdl);
            var result = await runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Be("""{"operations":{"Ping":"() -> string","Reset":"(hard?: bool) // writes"}}""");
            result.Text.Should().Be("Declared operations: 2.");
            result.StructuredContent.Should().NotContain("Top", "Top is bound to Customer and belongs on odata_describe_type");
            result.StructuredContent.Should().NotContain("isBound");
            result.StructuredContent.Should().NotContain("kind");
        }

        /// <summary>
        /// A model with no unbound operations returns an empty object and a zero count.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_None_EmptyObject()
        {
            var runtime = OdataDescribeTypeTests.Runtime(EdmTypeShapeTests.OperationsCsdl);
            var result = await runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Be("""{"operations":{"GetNearestAirport":"(lat: number) -> string","ResetDataSource":"() // writes"}}""");

            var none = OdataDescribeTypeTests.Runtime(CsdlParserDocumentationTests.DocumentedCsdl);
            var empty = await none.InvokeAsync("odata_list_operations", null, CancellationToken.None);

            empty.IsError.Should().BeFalse(empty.Text);
            empty.StructuredContent.Should().Be("{}");
            empty.Text.Should().Be("Declared operations: 0.");
        }

        /// <summary>
        /// Live TripPin lists the three unbound operations with parameter types and excludes the bound ones.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_TripPin_UnboundWithTypes()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadTripPinModelAsync(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Be("""{"operations":{"GetPersonWithMostFriends":"() -> Person","GetNearestAirport":"(lat: number, lon: number) -> Airport","ResetDataSource":"() // writes"}}""");
            result.Text.Should().Be("Declared operations: 3.");
        }

        #endregion

    }

}
