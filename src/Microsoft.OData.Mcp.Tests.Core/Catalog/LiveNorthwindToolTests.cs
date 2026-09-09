// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    /// Live Northwind MCP tool tests against the public read-only OData V4 service.
    /// </summary>
    [TestClass]
    public class LiveNorthwindToolTests
    {

        #region Fields

        internal const string CompositeOrderDetailKey = "OrderID=10248,ProductID=11";
        internal const string CompositeQuotedTrap = "Order_Details('OrderID=10248,ProductID=11')";
        internal const string CompositeWirePath = "Order_Details(OrderID=10248,ProductID=11)";

        #endregion

        #region Public Methods

        /// <summary>
        /// Named create with empty args posts <c>{}</c> and Northwind rejects it.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_EmptyArgs_PostsEmptyObject_400()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync("create_customer", ToolArguments.Of(), CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.JsonBody.Should().Be("{}");
        }

        /// <summary>
        /// Named create cannot retarget Products.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UsingGenericEntitySetArg_Overwritten()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "create_customer",
                ToolArguments.Of("entitySet", "Products", "CompanyName", "Nope"),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.RelativePath.Should().Be("Customers");
        }

        /// <summary>
        /// Named create strips query option names from the synthesized body.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UsingListArgs_FilterTop_ExcludedFromBody()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "create_customer",
                ToolArguments.Of("filter", "x", "top", 1, "CompanyName", "Nope"),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.JsonBody.Should().Contain("CompanyName");
            capture.Last.JsonBody.Should().NotContain("filter");
        }

        /// <summary>
        /// Named create on read-only Northwind fails.
        /// </summary>
        [TestMethod]
        public async Task CreateProduct_Northwind_IsError()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "create_product",
                ToolArguments.Of("ProductName", "Nope", "Discontinued", false),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.Method.Should().Be(HttpMethod.Post);
        }

        /// <summary>
        /// Named delete ignores a leftover body.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_BodyIgnored()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "delete_customer",
                ToolArguments.Of("key", "ALFKI", "body", "{}"),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.Method.Should().Be(HttpMethod.Delete);
            capture.Last.JsonBody.Should().BeNull();
        }

        /// <summary>
        /// Named delete still binds Customers when entitySet is Products.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_EntitySetOverwrittenCannotDeleteProducts()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "delete_customer",
                ToolArguments.Of("entitySet", "Products", "key", "ALFKI"),
                CancellationToken.None);
            var products = await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "Products", "key", "1"),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Requests.Should().Contain(request => request.RelativePath == "Customers('ALFKI')" && request.Method == HttpMethod.Delete);
            products.IsError.Should().BeFalse(products.Text);
        }

        /// <summary>
        /// Named delete requires key.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_MissingKey_IsError()
        {
            var (runtime, _) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync("delete_customer", ToolArguments.Of(), CancellationToken.None);

            AssertMissing(result, "key");
        }

        /// <summary>
        /// Named product delete on read-only Northwind fails.
        /// </summary>
        [TestMethod]
        public async Task DeleteProduct_Northwind_IsError()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync("delete_product", ToolArguments.Of("key", "1"), CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.Method.Should().Be(HttpMethod.Delete);
            capture.Last.RelativePath.Should().Be("Products(1)");
        }

        /// <summary>
        /// Concurrent describe of every Northwind set succeeds or reports not-declared.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_ConcurrentDescribeAllNorthwindSets_AllSucceedOrNotDeclared()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var listed = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            listed.IsError.Should().BeFalse(listed.Text);
            var names = ReadEntitySetNames(listed.StructuredContent!);
            var results = await Task.WhenAll(names.Select(name => runtime.InvokeAsync(
                "odata_describe_type",
                ToolArguments.Of("name", name),
                CancellationToken.None)));

            results.Should().OnlyContain(result => !result.IsError || result.Text.Contains("is not declared in the model.", StringComparison.Ordinal));
        }

        /// <summary>
        /// Extra query args are ignored on describe.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_ExtraFilterTop_IgnoredAndStillDescribes()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_describe_type",
                ToolArguments.Of("name", "Customers", "filter", "x", "top", 1),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("CustomerID");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Describe requires name.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_MissingName_IsErrorMissingRequiredArgument()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of(), CancellationToken.None);

            AssertMissing(result, "name");
        }

        /// <summary>
        /// Order_Details exposes both composite keys.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Northwind_OrderDetails_CompositeKeys()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Order_Details"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().StartWith("Order_Detail  (set: Order_Details, key: OrderID, ProductID)");
        }

        /// <summary>
        /// Products describe includes ProductID and Category navigation.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Northwind_Products_ContainsProductIDAndCategoryNav()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var bySet = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Products"), CancellationToken.None);
            var byType = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Product"), CancellationToken.None);

            bySet.IsError.Should().BeFalse(bySet.Text);
            byType.IsError.Should().BeFalse(byType.Text);
            bySet.Text.Should().Contain("ProductID: int // key");
            bySet.Text.Should().Contain("Category? -> Category");
            byType.Text.Should().Be(bySet.Text);
        }

        /// <summary>
        /// A described property can be used in select.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_ThenQueryThatSet_UsesAPropertyFromTheCardInSelect()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var described = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Products"), CancellationToken.None);
            described.IsError.Should().BeFalse(described.Text);
            var queried = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "select", "ProductName", "top", 1),
                CancellationToken.None);

            queried.IsError.Should().BeFalse(queried.Text);
            capture.Last!.QueryOptions["select"].Should().Be("ProductName");
        }

        /// <summary>
        /// Unknown types do not produce HTTP 404.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_UnknownTypeGhost_IsErrorNotDeclared_NoHttp404()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Ghost"), CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("is not declared in the model.");
            result.Text.Should().NotContain("status 404");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Type matching is case-insensitive.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_WrongCase_PeopleVsPeople()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "customers"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("CustomerID: string // key");
        }

        /// <summary>
        /// Passing entitySet to named get cannot retarget Products.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_EntitySetPassed_OverwrittenToBoundSet()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "get_customer",
                ToolArguments.Of("entitySet", "Products", "key", "ALFKI"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')");
        }

        /// <summary>
        /// Named customer family is complete when advertised.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_FamilyNeverPartial()
        {
            var (runtime, _) = await NamedNorthwindAsync();
            var names = runtime._catalog.Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("list_customers");
            names.Should().Contain("get_customer");
            names.Should().Contain("create_customer");
            names.Should().Contain("update_customer");
            names.Should().Contain("delete_customer");
        }

        /// <summary>
        /// Named get requires key.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_MissingKey_IsError()
        {
            var (runtime, _) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync("get_customer", ToolArguments.Of(), CancellationToken.None);

            AssertMissing(result, "key");
        }

        /// <summary>
        /// Named get of Products(99999) is 404.
        /// </summary>
        [TestMethod]
        public async Task GetProduct_Northwind_99999_404()
        {
            var (runtime, _) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync("get_product", ToolArguments.Of("key", "99999"), CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// Named get product 1 matches generic get.
        /// </summary>
        [TestMethod]
        public async Task GetProduct_Northwind_Key1_Matches()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var named = await runtime.InvokeAsync("get_product", ToolArguments.Of("key", "1"), CancellationToken.None);
            var generic = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None);

            named.IsError.Should().BeFalse(named.Text);
            generic.IsError.Should().BeFalse(generic.Text);
            capture.Last!.RelativePath.Should().Be("Products(1)");
            named.StructuredContent.Should().Be(generic.StructuredContent);
        }

        /// <summary>
        /// Named list does not inject default top.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_DoesNotAddDefaultTop()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "list_customers",
                ToolArguments.Of("filter", "CustomerID eq 'ALFKI'"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// Named list ignores $filter.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_DollarFilter_IgnoredUnfiltered()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "list_customers",
                ToolArguments.Of("$filter", "CustomerID eq 'NoSuch'", "top", 5),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("filter");
            result.StructuredContent.Should().Contain("ALFKI");
        }

        /// <summary>
        /// Passing entitySet to named list cannot retarget Products.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_PassingEntitySet_OverriddenByCatalogBinding()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "list_customers",
                ToolArguments.Of("entitySet", "Products", "top", 1),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers");
        }

        /// <summary>
        /// Twenty concurrent list-entity-set calls succeed.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ConcurrentTwentyCalls_AllSucceed()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None)));

            results.Should().HaveCount(20).And.OnlyContain(result => !result.IsError);
        }

        /// <summary>
        /// Extra $filter on list entity sets is ignored.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_DollarFilterArgument_IsIgnoredAndStillSucceeds()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_list_entity_sets",
                ToolArguments.Of("$filter", "x", "entitySet", "Customers"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Products");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Excluding People omits that set from the list and named tools.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ExcludeEntitySets_OmitsPeopleFromList()
        {
            var catalog = new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl),
                new ODataMcpCatalogOptions { ExcludeEntitySets = ["People"] });
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().NotContain("People");
            catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_list_entity_sets").And.NotContain("list_people");
        }

        /// <summary>
        /// Tiny max response bytes fail list entity sets.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_MaxResponseBytesTiny_IsErrorSuggestingSelectTop()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync(options => options.MaxResponseBytes = 8);
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("select").And.Contain("top");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Northwind list includes Products, Customers, and composite Order_Details keys.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_Northwind_ContainsProductsCustomersOrderDetails()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            var names = ReadEntitySetNames(result.StructuredContent!);
            names.Should().Contain(["Products", "Customers", "Order_Details", "Orders", "Categories", "Employees"]);
            ReadEntitySet(result.StructuredContent!, "Order_Details").Should().Contain("OrderID").And.Contain("ProductID");
            result.Text.Should().Be($"Declared entity sets: {names.Count}.");
        }

        /// <summary>
        /// Null arguments list entity sets.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_NullArguments_Succeeds()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Products");
        }

        /// <summary>
        /// Every listed set can be described.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ThenDescribeType_EachName_SucceedsOrIsExcluded()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var listed = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            listed.IsError.Should().BeFalse(listed.Text);
            foreach (var name in ReadEntitySetNames(listed.StructuredContent!))
            {
                var described = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", name), CancellationToken.None);
                described.IsError.Should().BeFalse(described.Text);
            }
        }

        /// <summary>
        /// Querying the first listed set succeeds.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ThenQueryFirstSet_SucceedsOnReadSurfaces()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var listed = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            listed.IsError.Should().BeFalse(listed.Text);
            var queried = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", ReadEntitySetNames(listed.StructuredContent!).First(), "top", 1),
                CancellationToken.None);

            queried.IsError.Should().BeFalse(queried.Text);
        }

        /// <summary>
        /// Query args do not cause list entity sets to call OData.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_UsingQueryArgs_FilterTop_DoesNotQueryOData()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_list_entity_sets", ToolArguments.Of("filter", "true", "top", 1), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Listing operations does not call OData.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_DoesNotHitOData_SoNo404()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().NotContain("404");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Northwind lists only operations actually declared in metadata.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_Northwind_EmptyOrDeclaredOnly()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Be("Declared operations: 0.");
            result.StructuredContent.Should().Be("{}", "Northwind declares no unbound operations and nothing is invented");
        }

        /// <summary>
        /// The singular tool name is unknown.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_UnknownToolIfMisspelled_odata_list_operation()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_list_operation", null, CancellationToken.None);

            AssertUnknown(result, "odata_list_operation");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// list_order_details is emitted when the family fits the cap.
        /// </summary>
        [TestMethod]
        public async Task ListOrderDetails_Northwind_IfFamilyFitsCap()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync(options => options.IncludeEntitySets = ["Order_Details", "Products", "Customers"]);
            runtime._catalog.Tools.Select(tool => tool.Name).Should().Contain("list_order_details");
            var result = await runtime.InvokeAsync("list_order_details", ToolArguments.Of("top", 1), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Order_Details");
        }

        /// <summary>
        /// Named list products top 1 matches generic query and HTTP.
        /// </summary>
        [TestMethod]
        public async Task ListProducts_Northwind_Top1_MatchHttpAndGeneric()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var named = await runtime.InvokeAsync("list_products", ToolArguments.Of("orderby", "ProductID", "top", 1), CancellationToken.None);
            var generic = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "orderby", "ProductID", "top", 1),
                CancellationToken.None);
            var odata = await TwinGetAsync("Products?$orderby=ProductID&$top=1");

            named.IsError.Should().BeFalse(named.Text);
            generic.IsError.Should().BeFalse(generic.Text);
            capture.Last!.RelativePath.Should().Be("Products");
            ODataFeedReader.ReadStrings(named.StructuredContent!, "ProductName", "productName")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(generic.StructuredContent!, "ProductName", "productName"))
                .And
                .Equal(ODataFeedReader.ReadStrings(odata, "ProductName", "productName"));
        }

        /// <summary>
        /// Named list products uses GET only.
        /// </summary>
        [TestMethod]
        public async Task ListProducts_Northwind_WriteNotInvolved()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync("list_products", ToolArguments.Of("top", 1), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Get);
        }

        /// <summary>
        /// Northwind has no GetNearestAirport operation.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_Northwind_AnyNameNotInModel_NotDeclared()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "GetNearestAirport"), CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("Operation 'GetNearestAirport' is not declared in the model.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Unknown operations error without HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_UnknownOperationGhost_IsErrorNotDeclared_NoHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Ghost"), CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("Operation 'Ghost' is not declared in the model.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Generic create leftovers include query option names.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_UsingQueryArgs_FilterTop_AsOnlyExtras_PostsEmptyOrFilterAsProperty()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Products", "filter", "x", "top", 1),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.JsonBody.Should().Contain("filter");
            capture.Last.Method.Should().Be(HttpMethod.Post);
        }

        /// <summary>
        /// Creating Products on read-only Northwind is a 4xx/405 error.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_Northwind_Products_IsError4xxOr405()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Products", "body", """{"ProductName":"Nope","Discontinued":false}"""),
                CancellationToken.None);

            AssertStatusError(result);
            result.Text.Should().MatchRegex(@"status (4\d\d|5\d\d)");
            capture.Last!.Method.Should().Be(HttpMethod.Post);
        }

        /// <summary>
        /// Oversized create bodies are rejected without HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_MaxRequestBodyBytesExceeded_IsErrorNoHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync(options => options.MaxRequestBodyBytes = 64);
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Products", "body", new string('x', 80)),
                CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("request body exceeds the maximum size");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Deleting Northwind Products is a 4xx/405 error.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_Northwind_405Or4xx()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None);

            AssertStatusError(result);
            result.Text.Should().MatchRegex(@"status (4\d\d|5\d\d)");
            capture.Last!.Method.Should().Be(HttpMethod.Delete);
        }

        /// <summary>
        /// Apostrophes in delete keys are doubled.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_ApostropheKey_FormatKeyDoubled()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "Customers", "key", "O'Brien"),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.RelativePath.Should().Be("Customers('O''Brien')");
        }

        /// <summary>
        /// Filter on delete is not forwarded.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_UsingQueryArgs_FilterIgnoredNotAQuery()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "Products", "key", "1", "filter", "true"),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.Method.Should().Be(HttpMethod.Delete);
            capture.Last.QueryOptions.Should().BeEmpty();
        }

        /// <summary>
        /// Concurrent gets of Products(1) through Products(10) succeed.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_ConcurrentGets_NorthwindProducts1Through10()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var results = await Task.WhenAll(Enumerable.Range(1, 10).Select(id => runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "Products", "key", id.ToString()),
                CancellationToken.None)));

            results.Should().HaveCount(10).And.OnlyContain(result => !result.IsError);
        }

        /// <summary>
        /// Generic get equals named get_product for key 1.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_EqualsGetCustomerNamedTool()
        {
            var (runtime, _) = await NamedNorthwindAsync();
            var generic = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None);
            var named = await runtime.InvokeAsync("get_product", ToolArguments.Of("key", "1"), CancellationToken.None);

            generic.IsError.Should().BeFalse(generic.Text);
            named.StructuredContent.Should().Be(generic.StructuredContent);
        }

        /// <summary>
        /// Filter on get is forwarded as a query option.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_FilterOnGet_IsForwardedAsQueryOption()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "Products", "key", "1", "filter", "ProductID eq 1"),
                CancellationToken.None);

            capture.Last!.QueryOptions["filter"].Should().Be("ProductID eq 1");
        }

        /// <summary>
        /// Numeric JSON keys are unquoted.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyAsNumber_1_SucceedsNumeric()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", 1), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Products(1)");
        }

        /// <summary>
        /// Object keys are quoted as a whole.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyAsObject_CompositeAttempt()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "Order_Details", "key", new { OrderID = 10248, ProductID = 11 }),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.RelativePath.Should().StartWith("Order_Details('");
        }

        /// <summary>
        /// String key ALFKI is quoted once.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Northwind_CustomerALFKI_StringKeyQuoted()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "ALFKI"), CancellationToken.None);
            var odata = await TwinGetAsync("Customers('ALFKI')");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')");
            result.StructuredContent.Should().Contain("ALFKI");
            odata.Should().Contain("ALFKI");
        }

        /// <summary>
        /// Already-quoted ALFKI is not double-quoted.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Northwind_CustomerAlreadyQuoted_NotDoubleQuoted()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "'ALFKI'"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')");
            capture.Last.RelativePath.Should().NotBe("Customers('''ALFKI''')");
        }

        /// <summary>
        /// Get does not create entities.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Northwind_DoesNotCreate()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Get);
            capture.Last.JsonBody.Should().BeNull();
        }

        /// <summary>
        /// Composite Order_Details keys must be unquoted on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Northwind_OrderDetails_CompositeKey()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var preview = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Order_Details", "filter", "OrderID eq 10248 and ProductID eq 11", "top", 1),
                CancellationToken.None);
            preview.IsError.Should().BeFalse(preview.Text);
            preview.StructuredContent.Should().Contain("10248");
            var result = await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "Order_Details", "key", CompositeOrderDetailKey),
                CancellationToken.None);
            var odata = await TwinGetAsync(CompositeWirePath);

            capture.Last!.RelativePath.Should().Be(CompositeWirePath);
            capture.Last.RelativePath.Should().NotBe(CompositeQuotedTrap);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("10248");
            odata.Should().Contain("10248");
        }

        /// <summary>
        /// Product 1 matches the HTTP twin.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Northwind_Product1_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None);
            var odata = await TwinGetAsync("Products(1)");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Products(1)");
            result.StructuredContent.Should().Contain("Product");
            odata.Should().Contain("Product");
        }

        /// <summary>
        /// Unknown product keys 404.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_UnknownKey999_IsError404()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "99999"), CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// To-many navigate does not inject default top.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_DoesNotAddDefaultTopOnToMany()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Customers", "key", "ALFKI", "navigation", "Orders"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// Customer ALFKI Orders matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_Northwind_CustomerALFKIOrders_ToMany_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Customers", "key", "ALFKI", "navigation", "Orders", "orderby", "OrderID", "top", 2),
                CancellationToken.None);
            var odata = await TwinGetAsync("Customers('ALFKI')/Orders?$orderby=OrderID&$top=2");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')/Orders");
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(ODataFeedReader.ReadValueCount(odata));
        }

        /// <summary>
        /// Product 1 Category matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_Northwind_Product1Category_ToOne_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Products", "key", "1", "navigation", "Category"),
                CancellationToken.None);
            var odata = await TwinGetAsync("Products(1)/Category");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Products(1)/Category");
            result.StructuredContent.Should().Contain("Category");
            odata.Should().Contain("Category");
        }

        /// <summary>
        /// Product 1 Order_Details is a to-many page.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_Northwind_Product1OrderDetails_ToMany_Page()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Products", "key", "1", "navigation", "Order_Details", "top", 2),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Products(1)/Order_Details");
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().BeGreaterThan(0);
        }

        /// <summary>
        /// Skip/top on a navigation matches the HTTP twin.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_PageNavSet_SkipTopEqualsTwin()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Customers", "key", "ALFKI", "navigation", "Orders", "orderby", "OrderID", "skip", 1, "top", 1),
                CancellationToken.None);
            var odata = await TwinGetAsync("Customers('ALFKI')/Orders?$orderby=OrderID&$skip=1&$top=1");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["skip"].Should().Be("1");
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(ODataFeedReader.ReadValueCount(odata));
        }

        /// <summary>
        /// A non-navigation name 404s or 400s.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_WrongNameNotANav_IsError404or400()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Products", "key", "1", "navigation", "Nope"),
                CancellationToken.None);

            AssertStatusError(result);
            result.Text.Should().MatchRegex(@"status (400|404)");
            capture.Last!.RelativePath.Should().Be("Products(1)/Nope");
        }

        /// <summary>
        /// Ten concurrent Products top-1 queries succeed.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_ConcurrentTenQueries_NorthwindProductsTop1()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "top", 1),
                CancellationToken.None)));

            results.Should().HaveCount(10).And.OnlyContain(result => !result.IsError);
        }

        /// <summary>
        /// MCP does not clamp top to a hidden max.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DoesNotClampTopToMaxTop()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products", "top", 1000), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["top"].Should().Be("1000");
        }

        /// <summary>
        /// $filter is ignored so the result is unfiltered.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DollarFilter_IsIgnoredSoUnfilteredResult()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "$filter", "CustomerID eq 'NoSuch'", "top", 5),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("filter");
            result.StructuredContent.Should().Contain("ALFKI");
        }

        /// <summary>
        /// $top is ignored and not injected as top.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DollarTop_IsIgnoredNotInjectedAsTop()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Categories", "$top", 1), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("top");
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().BeGreaterThan(1);
        }

        /// <summary>
        /// Generic query equals named list for the same skip/top.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_EqualsListCustomers_SameSkipTop()
        {
            var (runtime, _) = await NamedNorthwindAsync();
            var generic = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "orderby", "CustomerID", "skip", 1, "top", 2),
                CancellationToken.None);
            var named = await runtime.InvokeAsync(
                "list_customers",
                ToolArguments.Of("orderby", "CustomerID", "skip", 1, "top", 2),
                CancellationToken.None);

            generic.IsError.Should().BeFalse(generic.Text);
            ODataFeedReader.ReadStrings(generic.StructuredContent!, "CustomerID", "customerID")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(named.StructuredContent!, "CustomerID", "customerID"));
        }

        /// <summary>
        /// Invalid filter syntax is OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_InvalidFilterSyntax_OData400()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "filter", "this is not filter"),
                CancellationToken.None);

            AssertStatusError(result);
            result.Text.Should().Contain("400");
        }

        /// <summary>
        /// A 2049-character filter is rejected without HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MaxFilterLengthExceeded_IsErrorNoHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "filter", new string('x', 2049)),
                CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("filter exceeds the maximum length");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// A 2048-character filter is allowed and sent.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MaxFilterLengthExact2048_IsAllowedAndSent()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "filter", new string('x', 2048), "top", 1),
                CancellationToken.None);

            capture.Requests.Should().NotBeEmpty();
            capture.Last!.QueryOptions["filter"].Should().HaveLength(2048);
        }

        /// <summary>
        /// Tiny max response bytes fail a query.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MaxResponseBytesExceeded_IsErrorSuggestSelectTop()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync(options => options.MaxResponseBytes = 64);
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products", "top", 1), CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("select").And.Contain("top");
        }

        /// <summary>
        /// Query requires entitySet.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MissingEntitySet_IsErrorContainsEntitySet()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("top", 1), CancellationToken.None);

            AssertMissing(result, "entitySet");
        }

        /// <summary>
        /// Count with top 0 returns a count when the service allows it.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_CountTrueTop0_ReturnsCount()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "top", 0, "count", true),
                CancellationToken.None);
            if (result.IsError)
            {
                result = await runtime.InvokeAsync(
                    "odata_query",
                    ToolArguments.Of("entitySet", "Products", "top", 1, "count", true),
                    CancellationToken.None);
            }

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCount(result.StructuredContent!).Should().NotBeNull();
        }

        /// <summary>
        /// String filter ALFKI matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_CustomersStringFilter_ALFKI()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "filter", "CustomerID eq 'ALFKI'"),
                CancellationToken.None);
            var odata = await TwinGetAsync("Customers?$filter=CustomerID eq 'ALFKI'");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["filter"].Should().Be("CustomerID eq 'ALFKI'");
            ODataFeedReader.ReadStrings(result.StructuredContent!, "CustomerID", "customerID")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(odata, "CustomerID", "customerID"))
                .And
                .Equal(["ALFKI"]);
        }

        /// <summary>
        /// Expand Category matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_ExpandCategory_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "expand", "Category", "top", 1, "orderby", "ProductID"),
                CancellationToken.None);
            var odata = await TwinGetAsync("Products?$expand=Category&$top=1&$orderby=ProductID");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["expand"].Should().Be("Category");
            result.StructuredContent.Should().Contain("Category");
            odata.Should().Contain("Category");
        }

        /// <summary>
        /// Filter ProductID eq 1 matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_FilterProductIDEq1_MatchHttp()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "filter", "ProductID eq 1"),
                CancellationToken.None);
            var odata = await TwinGetAsync("Products?$filter=ProductID eq 1");

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadStrings(result.StructuredContent!, "ProductName", "productName")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(odata, "ProductName", "productName"));
        }

        /// <summary>
        /// Omitting top does not add top on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_OmitTop_DoesNotAddTopOnWire()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Categories"), CancellationToken.None);

            capture.Last.Should().NotBeNull();
            capture.Last!.QueryOptions.Should().NotContainKey("top");
            capture.Last.RelativePath.Should().Be("Categories");
        }

        /// <summary>
        /// Skip 1 top 1 orderby ProductID matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_ProductsSkip1Top1OrderbyProductID_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "orderby", "ProductID", "skip", 1, "top", 1),
                CancellationToken.None);
            var odata = await TwinGetAsync("Products?$orderby=ProductID&$skip=1&$top=1");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["skip"].Should().Be("1");
            ODataFeedReader.ReadStrings(result.StructuredContent!, "ProductName", "productName")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(odata, "ProductName", "productName"))
                .And
                .HaveCount(1);
        }

        /// <summary>
        /// Products top 1 returns a product.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_ProductsTop1_ReturnsProduct()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products", "top", 1), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Product");
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(1);
        }

        /// <summary>
        /// Select ProductName matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_SelectProductName_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "select", "ProductName", "orderby", "ProductID", "top", 1),
                CancellationToken.None);
            var odata = await TwinGetAsync("Products?$select=ProductName&$orderby=ProductID&$top=1");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["select"].Should().Be("ProductName");
            ODataFeedReader.ReadStrings(result.StructuredContent!, "ProductName", "productName")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(odata, "ProductName", "productName"));
        }

        /// <summary>
        /// Skip 0 and skip 1 pages differ.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_Skip0VsSkip1_PagesDiffer()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var first = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "orderby", "ProductID", "skip", 0, "top", 1),
                CancellationToken.None);
            var second = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "orderby", "ProductID", "skip", 1, "top", 1),
                CancellationToken.None);

            first.IsError.Should().BeFalse(first.Text);
            second.IsError.Should().BeFalse(second.Text);
            ODataFeedReader.ReadStrings(first.StructuredContent!, "ProductName", "productName").Single()
                .Should()
                .NotBe(ODataFeedReader.ReadStrings(second.StructuredContent!, "ProductName", "productName").Single());
        }

        /// <summary>
        /// Query uses GET only.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Northwind_WriteVerbNotUsed()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products", "top", 1), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Get);
        }

        /// <summary>
        /// Top as string 1 succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TopAsString_1_Succeeds()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products", "top", "1"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["top"].Should().Be("1");
        }

        /// <summary>
        /// Boolean top is forwarded and OData 400s.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TopAsBoolTrue_OData400_IsErrorStatus400()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products", "top", true), CancellationToken.None);

            capture.Last!.QueryOptions["top"].Should().Be("true");
            AssertStatusError(result);
            result.Text.Should().Contain("400");
        }

        /// <summary>
        /// Unknown entity sets 404.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_UnknownEntitySet_IsErrorStatus404OrNotSuccess()
        {
            var (runtime, _) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "DoesNotExist"), CancellationToken.None);

            AssertStatusError(result);
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// A key on query is ignored so the result is a list.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_UsingGetArgs_KeyWithoutBeingGet_KeyIgnored()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "key", "1", "top", 2),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Products");
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(2);
        }

        /// <summary>
        /// Wide expand/select within guards succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_WideExpandSelectOnNorthwind_WithinGuards()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Products", "select", "ProductID,ProductName,CategoryID", "expand", "Category,Supplier", "top", 1),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["expand"].Should().Contain("Category");
        }

        /// <summary>
        /// Updating Northwind Products is a 4xx/405 error.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_Northwind_IsError4xxOr405()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Products", "key", "1", "body", """{"ProductName":"Nope"}"""),
                CancellationToken.None);

            AssertStatusError(result);
            result.Text.Should().MatchRegex(@"status (4\d\d|5\d\d)");
            capture.Last!.Method.Should().Be(HttpMethod.Patch);
        }

        /// <summary>
        /// The tool always PATCHes, never PUTs.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_PutVsPatch_ToolAlwaysPatch()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Products", "key", "1", "body", "{}"),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.Method.Should().Be(HttpMethod.Patch);
        }

        /// <summary>
        /// Query option leftovers land in the update body when body is omitted.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_UsingQueryArgs_TopOnUpdate_GoesIntoBodyIfNoBody()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Products", "key", "1", "top", 1),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.QueryOptions.Should().BeEmpty();
            capture.Last.JsonBody.Should().Contain("top");
        }

        /// <summary>
        /// Named update cannot retarget Products.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_EntitySetOverwritten()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "update_customer",
                ToolArguments.Of("entitySet", "Products", "key", "ALFKI", "body", """{"CompanyName":"Nope"}"""),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')");
        }

        /// <summary>
        /// Named product update on read-only Northwind fails.
        /// </summary>
        [TestMethod]
        public async Task UpdateProduct_Northwind_IsError()
        {
            var (runtime, capture) = await NamedNorthwindAsync();
            var result = await runtime.InvokeAsync(
                "update_product",
                ToolArguments.Of("key", "1", "body", """{"ProductName":"Nope"}"""),
                CancellationToken.None);

            AssertStatusError(result);
            capture.Last!.Method.Should().Be(HttpMethod.Patch);
            capture.Last.RelativePath.Should().Be("Products(1)");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Asserts a missing required argument error.
        /// </summary>
        /// <param name="result">The tool result.</param>
        /// <param name="name">The argument name.</param>
        internal static void AssertMissing(ODataToolInvocationResult result, string name)
        {
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain(name);
        }

        /// <summary>
        /// Asserts an OData HTTP failure with a status in the text.
        /// </summary>
        /// <param name="result">The tool result.</param>
        internal static void AssertStatusError(ODataToolInvocationResult result)
        {
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("OData request failed with status");
        }

        /// <summary>
        /// Asserts an unknown-tool error.
        /// </summary>
        /// <param name="result">The tool result.</param>
        /// <param name="name">The tool name.</param>
        internal static void AssertUnknown(ODataToolInvocationResult result, string name)
        {
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain($"Unknown tool '{name}'.");
        }

        /// <summary>
        /// Creates a Northwind runtime that pins named tools for Products and Customers.
        /// </summary>
        /// <returns>
        /// Runtime and capture.
        /// </returns>
        internal static Task<(ODataToolRuntime Runtime, CapturingODataExecutor Capture)> NamedNorthwindAsync()
        {
            return LiveToolRuntime.CreateNorthwindAsync(options => options.IncludeEntitySets = ["Products", "Customers"]);
        }

        /// <summary>
        /// Reads the JSON object for one listed entity set.
        /// </summary>
        /// <param name="json">List payload.</param>
        /// <param name="name">Set name.</param>
        /// <returns>
        /// Raw JSON for the set.
        /// </returns>
        internal static string ReadEntitySet(string json, string name)
        {
            using var document = JsonDocument.Parse(json);
            foreach (var item in document.RootElement.GetProperty("entitySets").EnumerateArray())
            {
                if (item.GetProperty("name").GetString() == name)
                {
                    return item.GetRawText();
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Reads listed entity-set names.
        /// </summary>
        /// <param name="json">List payload.</param>
        /// <returns>
        /// Names in payload order.
        /// </returns>
        internal static IReadOnlyList<string> ReadEntitySetNames(string json)
        {
            using var document = JsonDocument.Parse(json);
            return [.. document.RootElement.GetProperty("entitySets").EnumerateArray().Select(item => item.GetProperty("name").GetString() ?? string.Empty)];
        }

        /// <summary>
        /// GETs a Northwind relative URL as the HTTP twin.
        /// </summary>
        /// <param name="relative">Path and query.</param>
        /// <returns>
        /// Response body.
        /// </returns>
        internal static async Task<string> TwinGetAsync(string relative)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            return await http.GetStringAsync($"{LiveOData.Northwind.TrimEnd('/')}/{relative.TrimStart('/')}");
        }

        #endregion

    }

}
