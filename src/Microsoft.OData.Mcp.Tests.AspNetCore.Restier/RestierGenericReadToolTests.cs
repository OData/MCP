// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Restier API (no OData controller) tests for list, describe, query, get, and navigate.
    /// </summary>
    [TestClass]
    public class RestierGenericReadToolTests : RestierToolTestBase
    {

        #region Constructors

        /// <summary>
        /// Initializes Restier endpoint routing for <see cref="McpCustomerApi"/>.
        /// </summary>
        public RestierGenericReadToolTests()
            : base("RestierRead")
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Describe Customers contains Id and CompanyName and no Orders-less leak of Products.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Restier_Customers_ContainsIdAndCompanyName()
        {
            using var client = CreateClient();
            var metadata = await client.GetStringAsync("odata/$metadata");
            metadata.Should().Contain("McpCustomer");
            var described = await Runtime().InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"), CancellationToken.None);

            described.IsError.Should().BeFalse(described.Text);
            described.Text.Should().Contain("Id");
            described.Text.Should().Contain("CompanyName");
            described.Text.Should().Contain("Orders");
        }

        /// <summary>
        /// Get customer 1 is Contoso.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Restier_Customer1_Contoso()
        {
            using var client = CreateClient();
            var odata = await client.GetStringAsync("odata/Customers(1)");
            var result = await Runtime().InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            odata.Should().Contain("Contoso");
        }

        /// <summary>
        /// JSON-RPC list entity sets.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_JsonRpc_Restier()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_list_entity_sets", "{}");
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().Contain("Customers");
        }

        /// <summary>
        /// List entity sets matches Restier metadata.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_Restier_ReturnsCustomersMatchingMetadata()
        {
            using var client = CreateClient();
            var metadata = await client.GetStringAsync("odata/$metadata");
            metadata.Should().Contain("Customers");
            metadata.Should().Contain("McpCustomer");
            var listed = await Runtime().InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("Customers");
            listed.StructuredContent.Should().Contain("\"Id\"");
            listed.StructuredContent.Should().NotContain("Products");
        }

        /// <summary>
        /// Navigate Customers(1)/Orders matches Restier HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_Restier_Customer1Orders()
        {
            using var client = CreateClient();
            var odata = await client.GetAsync("odata/Customers(1)/Orders");
            var odataBody = await odata.Content.ReadAsStringAsync();
            var result = await Runtime().InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            if (odata.IsSuccessStatusCode)
            {
                result.StructuredContent.Should().Contain("100");
            }
        }

        /// <summary>
        /// Query Customers matches GET.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Restier_Customers_MatchesGet()
        {
            using var client = CreateClient();
            var odata = await client.GetStringAsync("odata/Customers");
            var result = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            result.StructuredContent.Should().Contain("Fabrikam");
            odata.Should().Contain("Contoso");
        }

        /// <summary>
        /// Filter Fabrikam matches.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Restier_FilterFabrikam_Matches()
        {
            using var client = CreateClient();
            var odata = await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'Fabrikam'");
            var result = await Runtime().InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "filter", "CompanyName eq 'Fabrikam'"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Fabrikam");
            result.StructuredContent.Should().NotContain("Contoso");
            odata.Should().Contain("Fabrikam");
        }

        /// <summary>
        /// Omit top does not inject a default top.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Restier_OmitTop_ReturnsAllSeededRowsNoInjectedTop()
        {
            var result = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            result.StructuredContent.Should().Contain("Fabrikam");
        }

        /// <summary>
        /// Top 1 orderby Id matches.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Restier_Top1OrderbyId_Matches()
        {
            using var client = CreateClient();
            var odata = await client.GetStringAsync("odata/Customers?$top=1&$orderby=Id");
            var result = await Runtime().InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "top", 1, "orderby", "Id"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
        }

        /// <summary>
        /// JSON-RPC query Customers.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_JsonRpcToolsCall_Restier_Customers()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_query", "{\"entitySet\":\"Customers\"}");
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// shutdown_server is absent.
        /// </summary>
        [TestMethod]
        public void ListEntitySets_AspNetCore_ToolsListContainsThisTool_OmitsShutdown()
        {
            var names = Session().Catalog.Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("odata_list_entity_sets");
            names.Should().NotContain("shutdown_server");
        }

        /// <summary>
        /// Extra <c>$filter</c> is ignored on list entity sets.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_DollarFilterArgument_IsIgnoredAndStillSucceeds()
        {
            var result = await Runtime().InvokeAsync("odata_list_entity_sets", ToolArguments.Of("$filter", "x", "entitySet", "Customers"), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Customers");
            result.StructuredContent.Should().Contain("Orders");
        }

        /// <summary>
        /// Null arguments succeed.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_NullArguments_Succeeds()
        {
            var result = await Runtime().InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().MatchRegex(@"Declared entity sets: \d+\.");
        }

        /// <summary>
        /// Every listed set can be described.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ThenDescribeType_EachName_SucceedsOrIsExcluded()
        {
            var listed = await Runtime().InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            using var document = JsonDocument.Parse(listed.StructuredContent!);
            foreach (var set in document.RootElement.GetProperty("entitySets").EnumerateArray())
            {
                var name = set.GetProperty("name").GetString();
                var described = await Runtime().InvokeAsync("odata_describe_type", ToolArguments.Of("name", name), CancellationToken.None);
                described.IsError.Should().BeFalse(described.Text);
            }
        }

        /// <summary>
        /// Missing describe name is an error.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_MissingName_IsErrorMissingRequiredArgument()
        {
            var result = await Runtime().InvokeAsync("odata_describe_type", ToolArguments.Of(), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// Case-insensitive Customers succeeds.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_WrongCase_CustomersSucceeds()
        {
            var result = await Runtime().InvokeAsync("odata_describe_type", ToolArguments.Of("name", "customers"), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("CompanyName");
        }

        /// <summary>
        /// Unknown type is not declared.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_UnknownTypeGhost_IsErrorNotDeclared_NoHttp404()
        {
            var result = await Runtime().InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Ghost"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("is not declared in the model");
            result.Text.Should().NotContain("status 404");
        }

        /// <summary>
        /// Skip and top match the HTTP twin.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Restier_SkipAndTop_MatchHttp()
        {
            using var client = CreateClient();
            var odata = await client.GetStringAsync("odata/Customers?$orderby=Id&$skip=1&$top=1");
            var result = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "orderby", "Id", "skip", 1, "top", 1), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odata)).And.Equal("Fabrikam");
        }

        /// <summary>
        /// Expand Orders matches GET $expand.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_Restier_ExpandOrders_Matches()
        {
            using var client = CreateClient();
            var odata = await client.GetStringAsync("odata/Customers?$expand=Orders");
            var result = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "expand", "Orders"), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Orders");
            odata.Should().Contain("Orders");
        }

        /// <summary>
        /// <c>$filter</c> is ignored so Contoso remains.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DollarFilter_IsIgnoredSoUnfilteredResult()
        {
            var result = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "$filter", "CompanyName eq 'NoSuch'"), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Missing entitySet is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MissingEntitySet_IsErrorContainsEntitySet()
        {
            var result = await Runtime().InvokeAsync("odata_query", ToolArguments.Of(), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Unknown set is not success.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_UnknownEntitySet_IsErrorStatus404OrNotSuccess()
        {
            using var client = CreateClient();
            var missing = await client.GetAsync("odata/DoesNotExist");
            missing.IsSuccessStatusCode.Should().BeFalse();
            var result = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "DoesNotExist"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("OData request failed with status");
        }

        /// <summary>
        /// Filter longer than 2048 is rejected without HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MaxFilterLengthExceeded_IsErrorNoHttp()
        {
            var result = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "filter", new string('A', 2049)), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("filter exceeds the maximum length");
        }

        /// <summary>
        /// Generic query skip/top equals named list_customers.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_NamedListCustomers_SamePageAsGeneric()
        {
            var generic = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "orderby", "Id", "skip", 1, "top", 2), CancellationToken.None);
            var named = await Runtime().InvokeAsync("list_customers", ToolArguments.Of("orderby", "Id", "skip", 1, "top", 2), CancellationToken.None);
            ODataFeedReader.ReadCompanyNames(generic.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(named.StructuredContent!));
        }

        /// <summary>
        /// Unknown key 999 is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_UnknownKey999_IsError404()
        {
            using var client = CreateClient();
            var missing = await client.GetAsync("odata/Customers(999)");
            ((int)missing.StatusCode).Should().Be(404);
            var result = await Runtime().InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "999"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_MissingKey_IsErrorContainsKey()
        {
            var result = await Runtime().InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Numeric key succeeds as Customers(1).
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyAsNumber_1_SucceedsNumeric()
        {
            var result = await Runtime().InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", 1), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// JSON-RPC get of customer 1.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_JsonRpcToolsCall_Restier_Customer1()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_get", """{"entitySet":"Customers","key":"1"}""");
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// Equals named get_mcp_customer.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_EqualsGetCustomerNamedTool()
        {
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("get_mcp_customer");
            Session().Catalog.Tools.Select(tool => tool.Name).Should().NotContain("get_customer");
            var generic = await Runtime().InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);
            var named = await Runtime().InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "1"), CancellationToken.None);
            generic.StructuredContent.Should().Contain("Contoso");
            named.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// To-one Orders(1)/Customer matches GET.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_OrdersToCustomer_ToOne()
        {
            using var client = CreateClient();
            var odata = await client.GetStringAsync("odata/Orders(1)/Customer");
            var result = await Runtime().InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Orders", "key", "1", "navigation", "Customer"), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            odata.Should().Contain("Contoso");
        }

        /// <summary>
        /// Wrong navigation name 404/400s.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_WrongNameNotANav_IsError404or400()
        {
            using var client = CreateClient();
            var twin = await client.GetAsync("odata/Customers(1)/Nope");
            twin.IsSuccessStatusCode.Should().BeFalse();
            var result = await Runtime().InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Nope"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().MatchRegex("status (400|404|501)");
        }

        /// <summary>
        /// Missing navigation is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_MissingNavigation_IsErrorContainsNavigation()
        {
            var result = await Runtime().InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("navigation");
        }

        /// <summary>
        /// JSON-RPC navigate Orders.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_JsonRpcToolsCall_RestierOrders()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_navigate", """{"entitySet":"Customers","key":"1","navigation":"Orders"}""");
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().Contain("100");
        }

        /// <summary>
        /// Create then query filter sees the new row.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_ThenCreateThenQueryFilter_SeesNewRow()
        {
            var created = await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":13,"CompanyName":"QueryAfterCreate"}"""), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            using var client = CreateClient();
            (await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'QueryAfterCreate'")).Should().Contain("QueryAfterCreate");
            var result = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "filter", "CompanyName eq 'QueryAfterCreate'"), CancellationToken.None);
            result.StructuredContent.Should().Contain("QueryAfterCreate");
        }

        #endregion

    }

}
