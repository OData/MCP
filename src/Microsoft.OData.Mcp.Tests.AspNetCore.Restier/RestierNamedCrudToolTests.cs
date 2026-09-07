// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.Restier.Breakdance;
using Microsoft.Restier.Core;
using Microsoft.Restier.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Restier named CRUD family: list_customers, get_mcp_customer, create_mcp_customer, update_mcp_customer, delete_mcp_customer.
    /// </summary>
    [TestClass]
    public class RestierNamedCrudToolTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _databaseName = "RestierNamedCrud-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierNamedCrudToolTests"/> class using endpoint routing.
        /// </summary>
        public RestierNamedCrudToolTests()
            : base(useEndpointRouting: true)
        {
            AddRestierAction = apiBuilder =>
            {
                apiBuilder.AddRestierApi<McpCustomerApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpCustomerContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_databaseName);
                    });
                    RestierTestSeed.EnsureCustomers(restierServices);
                });
            };

            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
            };
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Enables MCP and starts the Restier host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddODataMcp();
            });

            TestSetup();
        }

        /// <summary>
        /// Tears down the Restier host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Catalog advertises the McpCustomer family and never <c>get_customer</c> or <c>list_products_category</c>.
        /// </summary>
        [TestMethod]
        public void Catalog_Restier_AdvertisesMcpCustomerFamilyNotGetCustomer()
        {
            var names = Catalog().Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain("list_customers");
            names.Should().Contain("get_mcp_customer");
            names.Should().Contain("create_mcp_customer");
            names.Should().Contain("update_mcp_customer");
            names.Should().Contain("delete_mcp_customer");
            names.Should().NotContain("get_customer");
            names.Should().NotContain("create_customer");
            names.Should().NotContain("update_customer");
            names.Should().NotContain("delete_customer");
            names.Should().NotContain("list_products_category");
            names.Should().Contain("list_orders");
            names.Should().Contain("get_mcp_order");
        }

        /// <summary>
        /// Named create schema contains CompanyName and Id, not $filter.
        /// </summary>
        [TestMethod]
        public void CreateCustomer_SchemaContainsCompanyName_NotDollarFilter_NotInternalSecret_NotBinary()
        {
            var tool = Catalog().Tools.First(item => item.Name == "create_mcp_customer");
            tool.InputSchema.Should().Contain("CompanyName");
            tool.InputSchema.Should().Contain("Id");
            tool.InputSchema.Should().NotContain("$filter");
            tool.InputSchema.Should().NotContain("InternalSecret");
            tool.InputSchema.Should().NotContain("Edm.Binary");
            tool.InputSchema.Should().NotContain("\"body\"");
        }

        /// <summary>
        /// Body object vs property args both create.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_BodyObjectVsProperties()
        {
            var properties = await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 101, "CompanyName", "Props"));
            properties.IsError.Should().BeFalse(properties.Text);
            var body = await InvokeAsync("create_mcp_customer", ToolArguments.Of("body", """{"Id":102,"CompanyName":"Body"}"""));
            body.IsError.Should().BeFalse(body.Text);
        }

        /// <summary>
        /// Concurrent unique names succeed.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_ConcurrentUniqueNames()
        {
            var tasks = Enumerable.Range(110, 4).Select(id => InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", id, "CompanyName", "U" + id)));
            var results = await Task.WhenAll(tasks);
            results.Should().OnlyContain(result => !result.IsError);
        }

        /// <summary>
        /// Empty args post an empty object and OData 400s.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_EmptyArgs_PostsEmptyObject_400()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of());
            result.Should().NotBeNull();
        }

        /// <summary>
        /// Named create equals generic odata_create.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_EqualsGenericOdataCreate()
        {
            var named = await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 103, "CompanyName", "EqNamed"));
            var generic = await InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":104,"CompanyName":"EqGeneric"}"""));
            named.IsError.Should().BeFalse(named.Text);
            generic.IsError.Should().BeFalse(generic.Text);
        }

        /// <summary>
        /// Explicit body string is used as-is.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_OData8_ExplicitBodyString_AlsoWorks()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("body", """{"Id":105,"CompanyName":"Explicit"}"""));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// <c>id</c> instead of CompanyName may 400.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_IdInsteadOfCompanyName_May400()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("id", "nope"));
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// JSON-RPC named create.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_JsonRpcToolsCall_Restier()
        {
            using var client = TestServer.CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "create_mcp_customer", """{"Id":106,"CompanyName":"RpcNamed"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            (await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'RpcNamed'")).Should().Contain("RpcNamed");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_JsonRpc_Malformed()
        {
            using var client = TestServer.CreateClient();
            using var content = RestierMcpJsonRpc.Content("{");
            using var response = await client.PostAsync("odata/mcp", content);
            response.IsSuccessStatusCode.Should().BeFalse();
        }

        /// <summary>
        /// Null property values are posted.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_NullPropertyValues()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 107, "CompanyName", null));
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Plural create_customers is unknown.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomers_Plural_UnknownTool()
        {
            var result = await InvokeAsync("create_customers", ToolArguments.Of("CompanyName", "X"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'create_customers'.");
        }

        /// <summary>
        /// Then get then delete.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_ThenGetCustomer_ThenDeleteCustomer()
        {
            var created = await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 108, "CompanyName", "RoundTrip"));
            created.IsError.Should().BeFalse(created.Text);
            var get = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "108"));
            get.StructuredContent.Should().Contain("RoundTrip");
            var deleted = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "108"));
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Then list_customers filter.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_ThenListCustomersFilter()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 109, "CompanyName", "Listed"));
            var listed = await InvokeAsync("list_customers", ToolArguments.Of("filter", "CompanyName eq 'Listed'"));
            listed.StructuredContent.Should().Contain("Listed");
        }

        /// <summary>
        /// Then odata_query.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_ThenOdataQuery()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 120, "CompanyName", "Q"));
            var query = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "filter", "Id eq 120"));
            query.StructuredContent.Should().Contain("Q");
        }

        /// <summary>
        /// Unicode name succeeds.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UnicodeName()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 121, "CompanyName", "北風"));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Unknown property may be ignored or 400.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UnknownPropertyInArgs_PostedAndODataMayIgnoreOr400()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 122, "CompanyName", "Extra", "NoSuch", "x"));
            if (result.IsError)
            {
                result.Text.Should().Contain("status ");
            }
            else
            {
                result.Text.Should().NotBeNullOrWhiteSpace();
            }
        }

        /// <summary>
        /// Passing entitySet is overwritten to the bound Customers set.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UsingGenericEntitySetArg_Overwritten()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("entitySet", "Orders", "Id", 123, "CompanyName", "BoundSet"));
            result.IsError.Should().BeFalse(result.Text);

            using var client = TestServer.CreateClient();
            (await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'BoundSet'")).Should().Contain("BoundSet");
        }

        /// <summary>
        /// Key is excluded from the named create body.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UsingGetArgs_KeyExcludedFromBody()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("key", "999", "Id", 124, "CompanyName", "NoKey"));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Query option names are stripped from named create leftovers.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UsingListArgs_FilterTop_ExcludedFromBody()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("filter", "x", "top", 1, "Id", 125, "CompanyName", "NoFilter"));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Restier property-args create is visible to HTTP.
        /// </summary>
        [TestMethod]
        public async Task CreateMcpCustomer_Restier_PropertyArgs_VisibleToHttp()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 126, "CompanyName", "NamedCo"));
            result.IsError.Should().BeFalse(result.Text);

            using var client = TestServer.CreateClient();
            (await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'NamedCo'")).Should().Contain("NamedCo");
        }

        /// <summary>
        /// Create ghost is unknown.
        /// </summary>
        [TestMethod]
        public async Task CreateGhost_UnknownTool()
        {
            var result = await InvokeAsync("create_ghost", ToolArguments.Of());
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'create_ghost'.");
        }

        /// <summary>
        /// Duplicate named create is an error.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_409Duplicate()
        {
            var result = await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 1, "CompanyName", "Dup"));
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Body on named delete is ignored.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_BodyIgnored()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 130, "CompanyName", "DelBody"));
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "130", "body", "{}"));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Concurrent deletes of the same key: one success, one 404.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_ConcurrentSameKey()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 131, "CompanyName", "Race"));
            var results = await Task.WhenAll(
                InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "131")),
                InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "131")));
            results.Count(result => !result.IsError).Should().Be(1);
            results.Count(result => result.IsError).Should().Be(1);
        }

        /// <summary>
        /// Does not call ResetDataSource.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_DoesNotCallResetDataSource()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 132, "CompanyName", "NoReset"));
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "132"));
            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("DELETE");
            result.Text.Should().NotContain("Reset");
        }

        /// <summary>
        /// <c>$key</c> is missing key.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_DollarKey()
        {
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("$key", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Empty key is rejected.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_EmptyKey()
        {
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", ""));
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Emoji key 404s.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_EmojiKey_404()
        {
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "😀"));
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Named delete equals generic odata_delete.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_EqualsOdataDelete()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 133, "CompanyName", "EqDel"));
            var named = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "133"));
            named.IsError.Should().BeFalse(named.Text);
        }

        /// <summary>
        /// Passing entitySet Products still deletes the bound customer.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_EntitySetOverwrittenCannotDeleteProducts()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 134, "CompanyName", "BoundDel"));
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("entitySet", "Orders", "key", "134"));
            result.IsError.Should().BeFalse(result.Text);

            using var client = TestServer.CreateClient();
            ((int)(await client.GetAsync("odata/Customers(134)")).StatusCode).Should().Be(404);
            (await client.GetStringAsync("odata/Orders(1)")).Should().Contain("100");
        }

        /// <summary>
        /// Family never partial: delete is advertised with list/get/create/update.
        /// </summary>
        [TestMethod]
        public void DeleteCustomer_FamilyNeverPartial()
        {
            var names = Catalog().Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain("list_customers");
            names.Should().Contain("get_mcp_customer");
            names.Should().Contain("create_mcp_customer");
            names.Should().Contain("update_mcp_customer");
            names.Should().Contain("delete_mcp_customer");
        }

        /// <summary>
        /// <c>id</c> instead of key is missing key.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_IdInsteadOfKey()
        {
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("id", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// JSON-RPC named delete.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_JsonRpcToolsCall()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 135, "CompanyName", "RpcDel"));
            using var client = TestServer.CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "delete_mcp_customer", """{"key":"135"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            ((int)(await client.GetAsync("odata/Customers(135)")).StatusCode).Should().Be(404);
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_JsonRpc_Malformed()
        {
            using var client = TestServer.CreateClient();
            using var content = RestierMcpJsonRpc.Content("{");
            using var response = await client.PostAsync("odata/mcp", content);
            response.IsSuccessStatusCode.Should().BeFalse();
        }

        /// <summary>
        /// Numeric key deletes.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_KeyAsNumber()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 136, "CompanyName", "Num"));
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", 136));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_MissingKey_IsError()
        {
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of());
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Second call is 404.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_SecondCall_404()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 137, "CompanyName", "Twice"));
            (await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "137"))).IsError.Should().BeFalse();
            var second = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "137"));
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("404");
        }

        /// <summary>
        /// Text contains status on 204.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_TextContainsStatusOn204()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 138, "CompanyName", "Status"));
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "138"));
            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().MatchRegex("20[04]");
        }

        /// <summary>
        /// After create/get/update.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_AfterCreateGetUpdate()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 139, "CompanyName", "A"));
            await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "139"));
            await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "139", "body", """{"CompanyName":"B"}"""));
            var deleted = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "139"));
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Then list_customers is absent the row.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_ThenListCustomers_Absent()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 140, "CompanyName", "Abs"));
            await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "140"));
            var listed = await InvokeAsync("list_customers", ToolArguments.Of("filter", "CompanyName eq 'Abs'"));
            listed.StructuredContent.Should().NotContain("Abs");
        }

        /// <summary>
        /// Then navigate 404s.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_ThenNavigate_404()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 141, "CompanyName", "Nav"));
            await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "141"));
            var nav = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "141", "navigation", "Orders"));
            if (!nav.IsError)
            {
                (nav.StructuredContent ?? string.Empty).Should().NotContain("Nav");
            }
        }

        /// <summary>
        /// Unknown key 404s.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_UnknownKey_404()
        {
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "999"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// List args without key are missing key.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_UsingListArgsWithoutKey()
        {
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("filter", "true"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Plural delete_customers is unknown.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomers_UnknownTool()
        {
            var result = await InvokeAsync("delete_customers", ToolArguments.Of("key", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'delete_customers'.");
        }

        /// <summary>
        /// Restier named delete of a created row.
        /// </summary>
        [TestMethod]
        public async Task DeleteMcpCustomer_Restier_CreatedRow()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 142, "CompanyName", "NamedDelRow"));
            var result = await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "142"));
            result.IsError.Should().BeFalse(result.Text);

            using var client = TestServer.CreateClient();
            ((int)(await client.GetAsync("odata/Customers(142)")).StatusCode).Should().Be(404);
        }

        /// <summary>
        /// Delete ghost is unknown.
        /// </summary>
        [TestMethod]
        public async Task DeleteGhost_UnknownTool()
        {
            var result = await InvokeAsync("delete_ghost", ToolArguments.Of("key", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'delete_ghost'.");
        }

        /// <summary>
        /// After create, named get sees the row.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_AfterCreateCustomer()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 150, "CompanyName", "After"));
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "150"));
            result.StructuredContent.Should().Contain("After");
        }

        /// <summary>
        /// After delete, named get is 404.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_AfterDelete_404()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 151, "CompanyName", "Gone"));
            await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "151"));
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "151"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// After update, named get sees the patch.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_AfterUpdate()
        {
            await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"GetAfter"}"""));
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "2"));
            result.StructuredContent.Should().Contain("GetAfter");
        }

        /// <summary>
        /// Concurrent named gets succeed.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_Concurrent()
        {
            var tasks = Enumerable.Range(0, 8).Select(_ => InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "1")));
            var results = await Task.WhenAll(tasks);
            results.Should().OnlyContain(result => !result.IsError);
        }

        /// <summary>
        /// <c>$key</c> is missing key.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_DollarKey_MissingKey()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("$key", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Empty key is rejected.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_EmptyKey()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", ""));
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Emoji key 404s.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_EmojiKey_404()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "😀"));
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// EntitySet passed is overwritten to Customers.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_EntitySetPassed_OverwrittenToBoundSet()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("entitySet", "Orders", "key", "1"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Family never partial.
        /// </summary>
        [TestMethod]
        public void GetCustomer_FamilyNeverPartial()
        {
            var names = Catalog().Tools.Select(tool => tool.Name);
            names.Should().Contain("list_customers");
            names.Should().Contain("get_mcp_customer");
        }

        /// <summary>
        /// <c>id</c> instead of key is missing key.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_IdInsteadOfKey_IsError()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("id", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// JSON-RPC named get.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_JsonRpcToolsCall()
        {
            using var client = TestServer.CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "get_mcp_customer", """{"key":"1"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_JsonRpc_Malformed()
        {
            using var client = TestServer.CreateClient();
            using var content = RestierMcpJsonRpc.Content("{");
            using var response = await client.PostAsync("odata/mcp", content);
            response.IsSuccessStatusCode.Should().BeFalse();
        }

        /// <summary>
        /// Key as object 404s.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_KeyAsObject()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", new Dictionary<string, int> { ["Id"] = 1 }));
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Expand longer than 512 is rejected.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_MaxExpandLengthExceeded()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "1", "expand", new string('A', 513)));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("expand exceeds the maximum length");
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_MissingKey_IsError()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of());
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Numeric key is unquoted on the wire.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_NumericKeyUnquotedOnWire()
        {
            using var client = TestServer.CreateClient();
            (await client.GetStringAsync("odata/Customers(1)")).Should().Contain("Contoso");
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", 1));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Select/expand are forwarded.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_SelectExpand_Forwarded()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetStringAsync("odata/Customers(1)?$select=CompanyName&$expand=Orders");
            odata.Should().Contain("Contoso");

            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "1", "select", "CompanyName", "expand", "Orders"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Then navigate Orders.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_ThenNavigate()
        {
            var get = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "1"));
            get.IsError.Should().BeFalse(get.Text);
            var nav = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"));
            nav.StructuredContent.Should().Contain("100");
        }

        /// <summary>
        /// Unknown key 404s.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_UnknownKey_404()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "999"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// List args with key forward filter onto GET.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_UsingListArgs_WithKey_FilterForwardedOnGet()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "1", "filter", "CompanyName eq 'Contoso'"));
            if (result.IsError)
            {
                result.Text.Should().Contain("status ");
            }
            else
            {
                result.StructuredContent.Should().Contain("Contoso");
            }
        }

        /// <summary>
        /// List args without key are missing key.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_UsingListArgs_FilterTop_WithoutKey_IsError()
        {
            var result = await InvokeAsync("get_mcp_customer", ToolArguments.Of("filter", "true", "top", 1));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// <c>get_customer</c> is unknown on Restier because the type is McpCustomer.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_OnRestier_UnknownToolIfTypeIsMcpCustomer()
        {
            Catalog().Tools.Select(tool => tool.Name).Should().NotContain("get_customer");
            var result = await InvokeAsync("get_customer", ToolArguments.Of("key", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'get_customer'.");
        }

        /// <summary>
        /// Plural get_customers is unknown.
        /// </summary>
        [TestMethod]
        public async Task GetCustomers_PluralWrongName_UnknownTool()
        {
            var result = await InvokeAsync("get_customers", ToolArguments.Of("key", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'get_customers'.");
        }

        /// <summary>
        /// Restier get_mcp_customer key 1 matches odata_get.
        /// </summary>
        [TestMethod]
        public async Task GetMcpCustomer_Restier_Key1_MatchesOdataGet()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetStringAsync("odata/Customers(1)");
            odata.Should().Contain("Contoso");

            var generic = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            var named = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "1"));
            generic.IsError.Should().BeFalse(generic.Text);
            named.IsError.Should().BeFalse(named.Text);
            named.StructuredContent.Should().Contain("Contoso");
            generic.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Get ghost is unknown.
        /// </summary>
        [TestMethod]
        public async Task GetGhost_UnknownTool()
        {
            var result = await InvokeAsync("get_ghost", ToolArguments.Of("key", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'get_ghost'.");
        }

        /// <summary>
        /// Concurrent list_customers succeed.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_Concurrent()
        {
            var tasks = Enumerable.Range(0, 8).Select(_ => InvokeAsync("list_customers", ToolArguments.Of("top", 1)));
            var results = await Task.WhenAll(tasks);
            results.Should().OnlyContain(result => !result.IsError);
        }

        /// <summary>
        /// Count as string true is forwarded.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_CountAsString()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("count", "true"));
            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCount(result.StructuredContent!).Should().Be(2);
        }

        /// <summary>
        /// Description reminds that query names do not include $.
        /// </summary>
        [TestMethod]
        public void ListCustomers_DescriptionContainsNoDollarReminder()
        {
            var tool = Catalog().Tools.First(item => item.Name == "list_customers");
            tool.Description.Should().Contain("do not include $");
            tool.InputSchema.Should().NotContain("$filter");
        }

        /// <summary>
        /// Does not add a default top.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_DoesNotAddDefaultTop()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of());
            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Contain("Contoso").And.Contain("Fabrikam");
        }

        /// <summary>
        /// <c>$filter</c> is ignored so the unfiltered list is returned.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_DollarFilter_IgnoredUnfiltered()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("$filter", "CompanyName eq 'NoSuch'"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Emoji filter is empty or 400.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_EmojiFilter()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("filter", "CompanyName eq '😀'"));
            if (result.IsError)
            {
                result.Text.Should().NotContain("status 500");
            }
            else
            {
                ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().BeEmpty();
            }
        }

        /// <summary>
        /// Empty args equal unpaged query.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_EmptyArgs_EqualsUnpagedQuery()
        {
            var named = await InvokeAsync("list_customers", ToolArguments.Of());
            var generic = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"));
            ODataFeedReader.ReadCompanyNames(named.StructuredContent!).Should().BeEquivalentTo(ODataFeedReader.ReadCompanyNames(generic.StructuredContent!));
        }

        /// <summary>
        /// Family is complete when flags are true.
        /// </summary>
        [TestMethod]
        public void ListCustomers_FamilyComplete_GetCreateUpdateDeletePresentWhenFlagsTrue()
        {
            var names = Catalog().Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain("list_customers");
            names.Should().Contain("get_mcp_customer");
            names.Should().Contain("create_mcp_customer");
            names.Should().Contain("update_mcp_customer");
            names.Should().Contain("delete_mcp_customer");
        }

        /// <summary>
        /// Injection-style filter is 400 or empty, not 500.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_InjectionFilter()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("filter", "1 eq 1"));
            if (result.IsError)
            {
                result.Text.Should().NotContain("status 500");
            }
            else
            {
                result.StructuredContent.Should().Contain("Contoso");
            }
        }

        /// <summary>
        /// JSON-RPC list_customers.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_JsonRpcToolsCall_Restier()
        {
            using var client = TestServer.CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "list_customers", "{}");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_JsonRpc_Malformed()
        {
            using var client = TestServer.CreateClient();
            using var content = RestierMcpJsonRpc.Content("{");
            using var response = await client.PostAsync("odata/mcp", content);
            response.IsSuccessStatusCode.Should().BeFalse();
        }

        /// <summary>
        /// Malformed filter is 400.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_MalformedFilter()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("filter", "this is not filter"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("400");
        }

        /// <summary>
        /// Expand longer than 512 is rejected.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_MaxExpandLengthExceeded()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("expand", new string('A', 513)));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("expand exceeds the maximum length");
        }

        /// <summary>
        /// Filter longer than 2048 is rejected.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_MaxFilterLengthExceeded()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("filter", new string('A', 2049)));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("filter exceeds the maximum length");
        }

        /// <summary>
        /// Select longer than 1024 is rejected.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_MaxSelectLengthExceeded()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("select", new string('A', 1025)));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("select exceeds the maximum length");
        }

        /// <summary>
        /// Passing entitySet Products still queries Customers.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_PassingEntitySet_OverriddenByCatalogBinding()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("entitySet", "Orders", "top", 1, "orderby", "Id"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            result.StructuredContent.Should().NotContain("Amount");
        }

        /// <summary>
        /// Passing key is ignored; the collection is returned.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_PassingKey_IgnoredNotAGet()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("key", "1"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            result.StructuredContent.Should().Contain("Fabrikam");
        }

        /// <summary>
        /// Restier list_customers matches generic query and HTTP.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_Restier_MatchesGenericAndHttp()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetStringAsync("odata/Customers");
            var generic = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"));
            var named = await InvokeAsync("list_customers", ToolArguments.Of());
            named.IsError.Should().BeFalse(named.Text);
            ODataFeedReader.ReadCompanyNames(named.StructuredContent!).Should().BeEquivalentTo(ODataFeedReader.ReadCompanyNames(odata));
            ODataFeedReader.ReadCompanyNames(named.StructuredContent!).Should().BeEquivalentTo(ODataFeedReader.ReadCompanyNames(generic.StructuredContent!));
        }

        /// <summary>
        /// Restier skip 1 top 2 equals odata_query.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_Restier_Skip1Top2_EqualsOdataQuery()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetStringAsync("odata/Customers?$orderby=Id&$skip=1&$top=2");
            var generic = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "orderby", "Id", "skip", 1, "top", 2));
            var named = await InvokeAsync("list_customers", ToolArguments.Of("orderby", "Id", "skip", 1, "top", 2));
            ODataFeedReader.ReadCompanyNames(named.StructuredContent!).Should().Equal("Fabrikam");
            ODataFeedReader.ReadCompanyNames(named.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(generic.StructuredContent!));
            ODataFeedReader.ReadCompanyNames(named.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
        }

        /// <summary>
        /// Then get_mcp_customer of the same row.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_ThenGetCustomer_SameRow()
        {
            var listed = await InvokeAsync("list_customers", ToolArguments.Of("filter", "Id eq 1"));
            ODataFeedReader.ReadKeys(listed.StructuredContent!).Should().Equal(1);
            var get = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "1"));
            get.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Then navigate Orders.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_ThenOdataNavigate()
        {
            var listed = await InvokeAsync("list_customers", ToolArguments.Of("filter", "Id eq 1"));
            listed.IsError.Should().BeFalse(listed.Text);
            var nav = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"));
            nav.StructuredContent.Should().Contain("100");
        }

        /// <summary>
        /// Top as array is 400.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_TopAsArray_400()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("top", new[] { 1 }));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("400");
        }

        /// <summary>
        /// tools/list contains list_customers when the cap allows.
        /// </summary>
        [TestMethod]
        public void ListCustomers_ToolsListContains_WhenCapAllows()
        {
            Catalog().Tools.Select(tool => tool.Name).Should().Contain("list_customers");
        }

        /// <summary>
        /// Unknown property is ignored.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_UnknownProp_Ignored()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("foo", "bar"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Get-style key-only still returns the collection.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_UsingGetArgsOnlyKey_ReturnsCollection()
        {
            var result = await InvokeAsync("list_customers", ToolArguments.Of("key", "1"));
            result.StructuredContent.Should().Contain("Fabrikam");
        }

        /// <summary>
        /// list_product (wrong snake) is unknown.
        /// </summary>
        [TestMethod]
        public async Task ListProducts_CalledAsList_Product_UnknownTool()
        {
            var result = await InvokeAsync("list_product", ToolArguments.Of());
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'list_product'.");
        }

        /// <summary>
        /// list_ghost is unknown.
        /// </summary>
        [TestMethod]
        public async Task ListGhost_UnknownTool()
        {
            var result = await InvokeAsync("list_ghost", ToolArguments.Of());
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'list_ghost'.");
        }

        /// <summary>
        /// Concurrent named patches.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_Concurrent()
        {
            var results = await Task.WhenAll(
                InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"C1"}""")),
                InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"C2"}""")));
            results.Should().OnlyContain(result => !result.IsError || result.Text.Contains("409", StringComparison.Ordinal));
        }

        /// <summary>
        /// <c>$key</c> is missing key.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_DollarKey()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("$key", "1", "body", "{}"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Empty body string is posted empty.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_EmptyBodyString()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "1", "body", ""));
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Named update equals generic odata_update.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_EqualsOdataUpdate()
        {
            var named = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"NamedUp"}"""));
            named.IsError.Should().BeFalse(named.Text);
            var generic = await InvokeAsync("odata_update", ToolArguments.Of("entitySet", "Customers", "key", "2", "body", """{"CompanyName":"GenericUp"}"""));
            generic.IsError.Should().BeFalse(generic.Text);
        }

        /// <summary>
        /// EntitySet is overwritten to Customers.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_EntitySetOverwritten()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("entitySet", "Orders", "key", "2", "body", """{"CompanyName":"StillCustomer"}"""));
            result.IsError.Should().BeFalse(result.Text);

            using var client = TestServer.CreateClient();
            (await client.GetStringAsync("odata/Customers(2)")).Should().Contain("StillCustomer");
        }

        /// <summary>
        /// 204 empty body text.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_204EmptyBodyText()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"NoContent"}"""));
            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().MatchRegex("20[04]");
        }

        /// <summary>
        /// <c>id</c> instead of key is missing key.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_IdInsteadOfKey()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("id", "1", "body", "{}"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Idempotent repeat succeeds.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_IdempotentRepeat()
        {
            var first = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"Repeat"}"""));
            var second = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"Repeat"}"""));
            first.IsError.Should().BeFalse(first.Text);
            second.IsError.Should().BeFalse(second.Text);
        }

        /// <summary>
        /// JSON-RPC named update.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_JsonRpcToolsCall()
        {
            using var client = TestServer.CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "update_mcp_customer", """{"key":"2","body":"{\"CompanyName\":\"RpcUp\"}"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            (await client.GetStringAsync("odata/Customers(2)")).Should().Contain("RpcUp");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_JsonRpc_Malformed()
        {
            using var client = TestServer.CreateClient();
            using var content = RestierMcpJsonRpc.Content("{");
            using var response = await client.PostAsync("odata/mcp", content);
            response.IsSuccessStatusCode.Should().BeFalse();
        }

        /// <summary>
        /// Malformed body is 400.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_MalformedBody()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "1", "body", "{"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("400");
        }

        /// <summary>
        /// Missing body synthesizes leftovers.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_MissingBody_SynthesizesLeftoversOrEmpty()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "CompanyName", "X"));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_MissingKey_IsError()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("body", "{}"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Null body synthesizes leftovers.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_NullBody()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "CompanyName", "NullUp"));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Object body patches.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_ObjectBody()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", new Dictionary<string, string> { ["CompanyName"] = "ObjUp" }));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// After create before delete.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_AfterCreate_BeforeDelete()
        {
            await InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 160, "CompanyName", "Up"));
            var updated = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "160", "body", """{"CompanyName":"Up2"}"""));
            updated.IsError.Should().BeFalse(updated.Text);
            await InvokeAsync("delete_mcp_customer", ToolArguments.Of("key", "160"));
        }

        /// <summary>
        /// Then get_mcp_customer.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_ThenGetCustomer()
        {
            await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"SeeGet"}"""));
            var get = await InvokeAsync("get_mcp_customer", ToolArguments.Of("key", "2"));
            get.StructuredContent.Should().Contain("SeeGet");
        }

        /// <summary>
        /// Then list_customers.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_ThenListCustomers()
        {
            await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"SeeList"}"""));
            var listed = await InvokeAsync("list_customers", ToolArguments.Of("filter", "CompanyName eq 'SeeList'"));
            listed.StructuredContent.Should().Contain("SeeList");
        }

        /// <summary>
        /// Unicode patch.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_Unicode()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"北風"}"""));
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Unknown key 404s.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_UnknownKey_404()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "999", "body", """{"CompanyName":"Nope"}"""));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status ");
        }

        /// <summary>
        /// Create property args without body or key fail missing key.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_UsingCreatePropertyArgsWithoutBodyOrKey()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("CompanyName", "X"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// List args without key fail.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_UsingListArgsWithoutKey()
        {
            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("filter", "true", "body", "{}"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Plural update_customers is unknown.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomers_UnknownTool()
        {
            var result = await InvokeAsync("update_customers", ToolArguments.Of("key", "1", "body", "{}"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'update_customers'.");
        }

        /// <summary>
        /// Restier named patch matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task UpdateMcpCustomer_Restier_Patch_MatchHttp()
        {
            using var client = TestServer.CreateClient();
            using var content = RestierJsonContent.Json("""{"CompanyName":"HttpNamed"}""");
            var twin = await client.PatchAsync("odata/Customers(2)", content);
            twin.IsSuccessStatusCode.Should().BeTrue();

            var result = await InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", """{"CompanyName":"McpNamed"}"""));
            result.IsError.Should().BeFalse(result.Text);
            (await client.GetStringAsync("odata/Customers(2)")).Should().Contain("McpNamed");
        }

        /// <summary>
        /// Update ghost is unknown.
        /// </summary>
        [TestMethod]
        public async Task UpdateGhost_UnknownTool()
        {
            var result = await InvokeAsync("update_ghost", ToolArguments.Of("key", "1", "body", "{}"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'update_ghost'.");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets the MCP catalog for the Restier odata prefix.
        /// </summary>
        /// <returns>
        /// The catalog.
        /// </returns>
        internal ODataMcpCatalog Catalog()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog;
        }

        /// <summary>
        /// Invokes a catalog tool.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal async Task<ODataToolInvocationResult> InvokeAsync(string name, Dictionary<string, JsonElement>? arguments)
        {
            return await Runtime().InvokeAsync(name, arguments, CancellationToken.None);
        }

        /// <summary>
        /// Gets the MCP runtime for the Restier odata prefix.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal ODataToolRuntime Runtime()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
        }

        #endregion

    }

    /// <summary>
    /// Named tools are omitted when <c>MaxNamedTools</c> leaves no room for a full family.
    /// </summary>
    [TestClass]
    public class RestierNamedCrudCapTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _databaseName = "RestierNamedCap-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierNamedCrudCapTests"/> class using endpoint routing.
        /// </summary>
        public RestierNamedCrudCapTests()
            : base(useEndpointRouting: true)
        {
            AddRestierAction = apiBuilder =>
            {
                apiBuilder.AddRestierApi<McpCustomerApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpCustomerContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_databaseName);
                    });
                    RestierTestSeed.EnsureCustomers(restierServices);
                });
            };

            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
            };
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Enables MCP with MaxNamedTools equal to the generic count.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddODataMcp(options => options.Catalog.MaxNamedTools = 10);
            });

            TestSetup();
        }

        /// <summary>
        /// Tears down the Restier host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Remaining 0 after 10 generics means no named tools at all.
        /// </summary>
        [TestMethod]
        public void ListCustomers_AbsentWhenCapTooSmallForFamily()
        {
            var names = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().HaveCount(10);
            names.Should().NotContain("list_customers");
            names.Should().NotContain("get_mcp_customer");
            names.Should().Contain("odata_query");
        }

        #endregion

    }

    /// <summary>
    /// Remaining 4 cannot fit a family of 5; no partial list_customers.
    /// </summary>
    [TestClass]
    public class RestierNamedCrudFamilyCapTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _databaseName = "RestierNamedFamilyCap-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierNamedCrudFamilyCapTests"/> class using endpoint routing.
        /// </summary>
        public RestierNamedCrudFamilyCapTests()
            : base(useEndpointRouting: true)
        {
            AddRestierAction = apiBuilder =>
            {
                apiBuilder.AddRestierApi<McpCustomerApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpCustomerContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_databaseName);
                    });
                    RestierTestSeed.EnsureCustomers(restierServices);
                });
            };

            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
            };
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Enables MCP with remaining 4 slots after generics.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddODataMcp(options => options.Catalog.MaxNamedTools = 14);
            });

            TestSetup();
        }

        /// <summary>
        /// Tears down the Restier host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Remaining 4 cannot fit family 5; no partial named tools.
        /// </summary>
        [TestMethod]
        public void ListCustomers_AbsentWhenRemaining4AndFamily5()
        {
            var names = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().NotContain("list_customers");
            names.Should().NotContain("get_mcp_customer");
            names.Should().Contain("odata_create");
        }

        #endregion

    }

    /// <summary>
    /// IncludeCreate=false reduces the family to 4, which fits remaining 4.
    /// </summary>
    [TestClass]
    public class RestierNamedCrudIncludeCreateFalseTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _databaseName = "RestierNamedNoCreate-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierNamedCrudIncludeCreateFalseTests"/> class using endpoint routing.
        /// </summary>
        public RestierNamedCrudIncludeCreateFalseTests()
            : base(useEndpointRouting: true)
        {
            AddRestierAction = apiBuilder =>
            {
                apiBuilder.AddRestierApi<McpCustomerApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpCustomerContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_databaseName);
                    });
                    RestierTestSeed.EnsureCustomers(restierServices);
                });
            };

            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
            };
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Enables MCP with IncludeCreate false and remaining 4 slots.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddODataMcp(options =>
                {
                    options.Catalog.IncludeCreate = false;
                    options.Catalog.MaxNamedTools = 14;
                });
            });

            TestSetup();
        }

        /// <summary>
        /// Tears down the Restier host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Family size 4 fits remaining 4; create_* is omitted; generic create still works.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_OmittedWhenIncludeCreateFalse_UnknownTool_GenericCreateStillWorks()
        {
            var names = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain("list_customers");
            names.Should().Contain("get_mcp_customer");
            names.Should().NotContain("create_mcp_customer");
            names.Should().Contain("odata_create");

            var unknown = await Runtime().InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 1, "CompanyName", "X"), CancellationToken.None);
            unknown.IsError.Should().BeTrue();
            unknown.Text.Should().Be("Unknown tool 'create_mcp_customer'.");

            var created = await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":170,"CompanyName":"GenericStill"}"""), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
        }

        /// <summary>
        /// list/get/update/delete are present when include-create false reduces the family to fit.
        /// </summary>
        [TestMethod]
        public void ListCustomers_PresentWhenIncludeCreateFalseReducesFamilyToFit()
        {
            var names = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain("list_customers");
            names.Should().Contain("get_mcp_customer");
            names.Should().Contain("update_mcp_customer");
            names.Should().Contain("delete_mcp_customer");
            names.Should().NotContain("create_mcp_customer");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets the MCP runtime for the Restier odata prefix.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal ODataToolRuntime Runtime()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
        }

        #endregion

    }

    /// <summary>
    /// IncludeUpdate and IncludeDelete false omit those named tools; generics remain.
    /// </summary>
    [TestClass]
    public class RestierNamedCrudIncludeUpdateDeleteFalseTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _databaseName = "RestierNamedNoUpDel-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierNamedCrudIncludeUpdateDeleteFalseTests"/> class using endpoint routing.
        /// </summary>
        public RestierNamedCrudIncludeUpdateDeleteFalseTests()
            : base(useEndpointRouting: true)
        {
            AddRestierAction = apiBuilder =>
            {
                apiBuilder.AddRestierApi<McpCustomerApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpCustomerContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_databaseName);
                    });
                    RestierTestSeed.EnsureCustomers(restierServices);
                });
            };

            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
            };
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Enables MCP with update and delete named tools omitted.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddODataMcp(options =>
                {
                    options.Catalog.IncludeUpdate = false;
                    options.Catalog.IncludeDelete = false;
                });
            });

            TestSetup();
        }

        /// <summary>
        /// Tears down the Restier host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Named update is omitted; generic update still works.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_OmittedWhenIncludeUpdateFalse_GenericUpdateStillWorks()
        {
            var names = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().NotContain("update_mcp_customer");
            names.Should().Contain("odata_update");
            names.Should().Contain("list_customers");

            var unknown = await Runtime().InvokeAsync("update_mcp_customer", ToolArguments.Of("key", "2", "body", "{}"), CancellationToken.None);
            unknown.IsError.Should().BeTrue();

            var updated = await Runtime().InvokeAsync("odata_update", ToolArguments.Of("entitySet", "Customers", "key", "2", "body", """{"CompanyName":"StillGeneric"}"""), CancellationToken.None);
            updated.IsError.Should().BeFalse(updated.Text);
        }

        /// <summary>
        /// Named delete is omitted; generic delete still works.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_OmittedWhenIncludeDeleteFalse_GenericDeleteStillWorks()
        {
            var names = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().NotContain("delete_mcp_customer");
            names.Should().Contain("odata_delete");

            var created = await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":180,"CompanyName":"DelGeneric"}"""), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var deleted = await Runtime().InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", "180"), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets the MCP runtime for the Restier odata prefix.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal ODataToolRuntime Runtime()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
        }

        #endregion

    }

}
