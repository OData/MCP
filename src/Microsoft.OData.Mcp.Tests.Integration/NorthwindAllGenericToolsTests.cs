// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Integration
{

    /// <summary>
    /// Live generic tools other than query against Northwind.
    /// </summary>
    [TestClass]
    public class NorthwindAllGenericToolsTests
    {

        #region Public Methods

        /// <summary>
        /// Describe Products contains ProductID and Category.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Northwind_Products_ContainsProductIDAndCategoryNav()
        {
            var (runtime, _) = await CreateNorthwindAsync();
            var bySet = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Products"), CancellationToken.None);
            var byType = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Product"), CancellationToken.None);

            bySet.IsError.Should().BeFalse(bySet.Text);
            byType.IsError.Should().BeFalse(byType.Text);
            bySet.Text.Should().Contain("ProductID: int // key");
            bySet.Text.Should().Contain("Category? -> Category");
            byType.Text.Should().Be(bySet.Text);
        }

        /// <summary>
        /// Describe Order_Details has composite keys.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Northwind_OrderDetails_CompositeKeys()
        {
            var (runtime, _) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Order_Details"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().StartWith("Order_Detail  (set: Order_Details, key: OrderID, ProductID)");
        }

        /// <summary>
        /// List entity sets includes Products, Customers, and Order_Details keys.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_Northwind_ContainsProductsCustomersOrderDetails()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Products");
            result.StructuredContent.Should().Contain("Customers");
            result.StructuredContent.Should().Contain("Order_Details");
            result.StructuredContent.Should().Contain("OrderID");
            result.StructuredContent.Should().Contain("ProductID");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Twenty concurrent list-entity-set calls succeed.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ConcurrentTwentyCalls_AllSucceed()
        {
            var (runtime, _) = await CreateNorthwindAsync();
            var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None)));

            results.Should().HaveCount(20).And.OnlyContain(result => !result.IsError);
        }

        /// <summary>
        /// Northwind lists only declared operations.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_Northwind_EmptyOrDeclaredOnly()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().MatchRegex(@"Declared operations: \d+\.");
            result.StructuredContent.Should().NotContain("GetNearestAirport");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Unknown operations are not declared.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_Northwind_AnyNameNotInModel_NotDeclared()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Ghost"), CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("Operation 'Ghost' is not declared in the model.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Creating Products on read-only Northwind fails.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_Northwind_Products_IsError4xxOr405()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Products", "body", """{"ProductName":"Nope","Discontinued":false}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("OData request failed with status");
            capture.Last!.Method.Should().Be(HttpMethod.Post);
        }

        /// <summary>
        /// Deleting Products on read-only Northwind fails.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_Northwind_405Or4xx()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("OData request failed with status");
            capture.Last!.Method.Should().Be(HttpMethod.Delete);
        }

        /// <summary>
        /// Get ALFKI quotes the string key.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Northwind_CustomerALFKI_StringKeyQuoted()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "ALFKI"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')");
            result.StructuredContent.Should().Contain("ALFKI");
        }

        /// <summary>
        /// Already-quoted ALFKI is not double-quoted.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Northwind_CustomerAlreadyQuoted_NotDoubleQuoted()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "'ALFKI'"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')");
            capture.Last.RelativePath.Should().NotBe("Customers('''ALFKI''')");
        }

        /// <summary>
        /// Composite Order_Details keys must be unquoted on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Northwind_OrderDetails_CompositeKey()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var preview = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Order_Details", "filter", "OrderID eq 10248 and ProductID eq 11", "top", 1),
                CancellationToken.None);
            preview.IsError.Should().BeFalse(preview.Text);
            var result = await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "Order_Details", "key", "OrderID=10248,ProductID=11"),
                CancellationToken.None);

            capture.Last!.RelativePath.Should().Be("Order_Details(OrderID=10248,ProductID=11)");
            capture.Last.RelativePath.Should().NotBe("Order_Details('OrderID=10248,ProductID=11')");
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Get Products(1) matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Northwind_Product1_MatchHttp()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None);
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var odata = await http.GetStringAsync($"{LiveOData.Northwind}/Products(1)");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Products(1)");
            odata.Should().Contain("Product");
        }

        /// <summary>
        /// Navigate Customer ALFKI Orders.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_Northwind_CustomerALFKIOrders_ToMany_MatchHttp()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Customers", "key", "ALFKI", "navigation", "Orders", "top", 1),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')/Orders");
        }

        /// <summary>
        /// Navigate Product 1 Category.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_Northwind_Product1Category_ToOne_MatchHttp()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Products", "key", "1", "navigation", "Category"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Products(1)/Category");
            result.StructuredContent.Should().Contain("Category");
        }

        /// <summary>
        /// Navigate Product 1 Order_Details.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_Northwind_Product1OrderDetails_ToMany_Page()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Products", "key", "1", "navigation", "Order_Details", "top", 2),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Products(1)/Order_Details");
        }

        /// <summary>
        /// $filter on query is ignored.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DollarFilter_IsIgnoredSoUnfilteredResult()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "$filter", "CustomerID eq 'NoSuch'", "top", 5),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("filter");
            result.StructuredContent.Should().Contain("ALFKI");
        }

        /// <summary>
        /// Omit top does not add top on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_OmitTop_DoesNotAddTopOnWire()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Categories"), CancellationToken.None);

            capture.Last!.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// Skip+top matches the HTTP twin via <see cref="ODataFeedReader"/>.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_ProductsSkip1Top1OrderbyProductID_MatchHttp()
        {
            var (runtime, _) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "orderby", "ProductID", "skip", 1, "top", 1),
                CancellationToken.None);
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var odata = await http.GetStringAsync($"{LiveOData.Northwind}/Products?$orderby=ProductID&$skip=1&$top=1");

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadStrings(result.StructuredContent!, "ProductName", "productName")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(odata, "ProductName", "productName"));
        }

        /// <summary>
        /// Updating Products on read-only Northwind fails.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_Northwind_IsError4xxOr405()
        {
            var (runtime, capture) = await CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Products", "key", "1", "body", """{"ProductName":"Nope"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("OData request failed with status");
            capture.Last!.Method.Should().Be(HttpMethod.Patch);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates a capturing remote runtime bound to live Northwind.
        /// </summary>
        /// <param name="configure">Optional catalog configuration.</param>
        /// <returns>
        /// Runtime and capture.
        /// </returns>
        internal static async Task<(ODataToolRuntime Runtime, CapturingODataExecutor Capture)> CreateNorthwindAsync(Action<ODataMcpCatalogOptions>? configure = null)
        {
            using var http = new HttpClient();
            var xml = await http.GetStringAsync($"{LiveOData.Northwind.TrimEnd('/')}/$metadata");
            var options = new ODataMcpCatalogOptions { RouteName = "remote" };
            configure?.Invoke(options);
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(xml), options);
            var services = new ServiceCollection();
            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = new Uri(LiveOData.Northwind.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });
            var inner = new RemoteODataExecutor(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());
            var capture = new CapturingODataExecutor(inner);

            return (new ODataToolRuntime(catalog, capture), capture);
        }

        #endregion

    }

}
