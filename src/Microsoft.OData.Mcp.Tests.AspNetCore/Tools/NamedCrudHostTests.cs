// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.Paging;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Named <c>list_/get_/create_/update_/delete_</c> tools on convention Customers (simple model).
    /// </summary>
    [TestClass]
    public class NamedCrudHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Property-arg create with Authorization returns 201.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_OData8_PropertyArgs_WithAuth_201()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync("create_customer", ToolArguments.Of("CompanyName", "NamedCo"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().MatchRegex("201|200");
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("Customers");
            capture.Last.JsonBody.Should().Contain("NamedCo");
            capture.Last.JsonBody.Should().NotContain("filter");
        }

        /// <summary>
        /// Explicit <c>body</c> on named create is used as-is.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_OData8_ExplicitBodyString_AlsoWorks()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "create_customer",
                ToolArguments.Of("body", """{"CompanyName":"BodyCo"}"""),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.JsonBody.Should().Be("""{"CompanyName":"BodyCo"}""");
        }

        /// <summary>
        /// Named create schema includes CompanyName and omits secrets and query keys.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_SchemaContainsCompanyName_NotDollarFilter_NotInternalSecret_NotBinary()
        {
            var tool = Session().Catalog.Tools.Single(item => item.Name == "create_customer");
            tool.InputSchema.Should().Contain("CompanyName");
            tool.InputSchema.Should().NotContain("$filter");
            tool.InputSchema.Should().NotContain("InternalSecret");
            tool.InputSchema.Should().NotContain("Photo");
        }

        /// <summary>
        /// Named create equals generic create.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_EqualsGenericOdataCreate()
        {
            var (runtime, _) = AuthorizedCapture();
            var named = await runtime.InvokeAsync("create_customer", ToolArguments.Of("CompanyName", "EqNamed"), CancellationToken.None);
            var generic = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"EqGeneric"}"""),
                CancellationToken.None);

            named.IsError.Should().BeFalse(named.Text);
            generic.IsError.Should().BeFalse(generic.Text);
        }

        /// <summary>
        /// Passing entitySet on named create cannot retarget Products.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UsingGenericEntitySetArg_Overwritten()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "create_customer",
                ToolArguments.Of("entitySet", "Products", "CompanyName", "StillCustomer"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers");
        }

        /// <summary>
        /// Query option names are stripped from named create leftovers.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UsingListArgs_FilterTop_ExcludedFromBody()
        {
            var (runtime, capture) = AuthorizedCapture();
            await runtime.InvokeAsync(
                "create_customer",
                ToolArguments.Of("CompanyName", "NoFilter", "filter", "x", "top", 1),
                CancellationToken.None);

            capture.Last!.JsonBody.Should().Contain("NoFilter");
            capture.Last.JsonBody.Should().NotContain("filter");
            capture.Last.JsonBody.Should().NotContain("top");
        }

        /// <summary>
        /// <c>key</c> is excluded from named create leftovers.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_UsingGetArgs_KeyExcludedFromBody()
        {
            var (runtime, capture) = AuthorizedCapture();
            await runtime.InvokeAsync(
                "create_customer",
                ToolArguments.Of("key", "99", "CompanyName", "NoKey"),
                CancellationToken.None);

            capture.Last!.JsonBody.Should().Contain("NoKey");
            capture.Last.JsonBody.Should().NotContain("\"key\"");
        }

        /// <summary>
        /// Plural <c>create_customers</c> is unknown.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomers_Plural_UnknownTool()
        {
            var result = await InvokeAsync("create_customers", ToolArguments.Of("CompanyName", "X"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Unknown tool 'create_customers'.");
        }

        /// <summary>
        /// Empty named create fails before HTTP: CompanyName is required on create.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_EmptyArgs_FailsBeforeHttp_MissingCompanyName()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync("create_customer", new Dictionary<string, JsonElement>(), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Missing required properties on Customer: CompanyName. Required on create: CompanyName.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// JSON-RPC named create forwards Authorization.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_OData8_AuthForwardedFromJsonRpc()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "create_customer",
                """{"CompanyName":"RpcNamed"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// Missing company name fails before HTTP with the required-on-create list.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_MissingCompanyName_FailsBeforeHttp()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync("create_customer", ToolArguments.Of("City", "Seattle"), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Missing required properties on Customer: CompanyName. Required on create: CompanyName.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Ghost named create is unknown.
        /// </summary>
        [TestMethod]
        public async Task CreateGhost_UnknownTool()
        {
            var result = await InvokeAsync("create_ghost", null);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Unknown tool 'create_ghost'.");
        }

        /// <summary>
        /// Create then get then delete via named tools.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_ThenGetCustomer_ThenDeleteCustomer()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync("create_customer", ToolArguments.Of("CompanyName", "RoundTrip"), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            var got = await runtime.InvokeAsync("get_customer", ToolArguments.Of("key", key), CancellationToken.None);
            got.StructuredContent.Should().Contain("RoundTrip");
            var deleted = await runtime.InvokeAsync("delete_customer", ToolArguments.Of("key", key), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Named get of key 1 matches generic get and HTTP.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_OData8_Key1_MatchesOdataGetAndHttp()
        {
            using var client = CreateClient();
            var http = await client.GetStringAsync("/odata/Customers(1)");
            http.Should().Contain("Contoso");

            var (runtime, capture) = CreateCapturingRuntime();
            var named = await runtime.InvokeAsync("get_customer", ToolArguments.Of("key", "1"), CancellationToken.None);
            var generic = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);

            named.IsError.Should().BeFalse(named.Text);
            named.StructuredContent.Should().Contain("Contoso");
            generic.StructuredContent.Should().Contain("Contoso");
            capture.Last!.RelativePath.Should().Be("Customers(1)");
        }

        /// <summary>
        /// Select and expand are forwarded on named get.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_SelectExpand_Forwarded()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "get_customer",
                ToolArguments.Of("key", "1", "select", "CompanyName", "expand", "Orders"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().ContainKey("select").WhoseValue.Should().Be("CompanyName");
            capture.Last.QueryOptions.Should().ContainKey("expand").WhoseValue.Should().Be("Orders");
        }

        /// <summary>
        /// Numeric key is unquoted.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_NumericKeyUnquotedOnWire()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            await runtime.InvokeAsync(
                "get_customer",
                new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement(1) },
                CancellationToken.None);

            capture.Last!.RelativePath.Should().Be("Customers(1)");
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_MissingKey_IsError()
        {
            var result = await InvokeAsync("get_customer", new Dictionary<string, JsonElement>());

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// <c>id</c> is not accepted in place of <c>key</c>.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_IdInsteadOfKey_IsError()
        {
            var result = await InvokeAsync("get_customer", ToolArguments.Of("id", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Passing entitySet still GET Customers.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_EntitySetPassed_OverwrittenToBoundSet()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "get_customer",
                ToolArguments.Of("entitySet", "Products", "key", "1"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers(1)");
        }

        /// <summary>
        /// Plural get name is unknown.
        /// </summary>
        [TestMethod]
        public async Task GetCustomers_PluralWrongName_UnknownTool()
        {
            var result = await InvokeAsync("get_customers", ToolArguments.Of("key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Unknown tool 'get_customers'.");
        }

        /// <summary>
        /// Unknown key is 404.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_UnknownKey_404()
        {
            var result = await InvokeAsync("get_customer", ToolArguments.Of("key", "999"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Family is never partial: get exists with list.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_FamilyNeverPartial()
        {
            var names = Session().Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain("list_customers");
            names.Should().Contain("get_customer");
            names.Should().Contain("create_customer");
            names.Should().Contain("update_customer");
            names.Should().Contain("delete_customer");
        }

        /// <summary>
        /// Named list matches generic query with no injected top.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_OData8_MatchesOdataQueryCustomers_NoTopInjected()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var named = await runtime.InvokeAsync("list_customers", null, CancellationToken.None);
            var generic = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None);

            named.IsError.Should().BeFalse(named.Text);
            named.StructuredContent.Should().Contain("Contoso");
            generic.StructuredContent.Should().Contain("Contoso");
            capture.Last!.QueryOptions.Should().NotContainKey("top");
            capture.Last.RelativePath.Should().Be("Customers");
        }

        /// <summary>
        /// Skip/top/orderby match generic and HTTP.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_OData8_SkipTopOrderby_MatchesGenericAndHttp()
        {
            using var client = CreateClient();
            var http = await client.GetStringAsync("/odata/Customers?$orderby=CustomerId&$skip=0&$top=1");
            http.Should().Contain("Contoso");

            var args = ToolArguments.Of("orderby", "CustomerId", "skip", 0, "top", 1);
            var (runtime, capture) = CreateCapturingRuntime();
            var named = await runtime.InvokeAsync("list_customers", args, CancellationToken.None);
            var generic = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "orderby", "CustomerId", "skip", 0, "top", 1), CancellationToken.None);

            named.StructuredContent.Should().Contain("Contoso");
            generic.StructuredContent.Should().Contain("Contoso");
            capture.Last!.QueryOptions.Should().ContainKey("top");
            capture.Last.QueryOptions.Should().NotContainKey("$top");
        }

        /// <summary>
        /// Filter/select/expand/count are forwarded.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_OData8_FilterSelectExpandCount()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "list_customers",
                ToolArguments.Of("filter", "CompanyName eq 'Contoso'", "select", "CompanyName", "expand", "Orders", "count", true),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().ContainKey("filter");
            capture.Last.QueryOptions.Should().ContainKey("select");
            capture.Last.QueryOptions.Should().ContainKey("expand");
            capture.Last.QueryOptions.Should().ContainKey("count");
        }

        /// <summary>
        /// Passing entitySet Products still lists Customers.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_PassingEntitySet_OverriddenByCatalogBinding()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "list_customers",
                ToolArguments.Of("entitySet", "Products", "top", 1),
                CancellationToken.None);

            result.StructuredContent.Should().Contain("Contoso");
            capture.Last!.RelativePath.Should().Be("Customers");
        }

        /// <summary>
        /// Passing key is ignored (still a collection).
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_PassingKey_IgnoredNotAGet()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("list_customers", ToolArguments.Of("key", "1"), CancellationToken.None);

            result.StructuredContent.Should().Contain("value");
            capture.Last!.RelativePath.Should().Be("Customers");
        }

        /// <summary>
        /// <c>$filter</c> is ignored.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_DollarFilter_IgnoredUnfiltered()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "list_customers",
                ToolArguments.Of("$filter", "CompanyName eq 'NoSuch'"),
                CancellationToken.None);

            result.StructuredContent.Should().Contain("Contoso");
            capture.Last!.QueryOptions.Should().NotContainKey("filter");
        }

        /// <summary>
        /// Description reminds that query names have no $.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_DescriptionContainsNoDollarReminder()
        {
            var tool = Session().Catalog.Tools.Single(item => item.Name == "list_customers");
            tool.Description.Should().Contain("$");
            tool.InputSchema.Should().NotContain("$filter");
        }

        /// <summary>
        /// tools/list contains list_customers when the cap allows.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_ToolsListContains_WhenCapAllows()
        {
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_customers");
            using var client = CreateClient();
            using var response = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("list_customers");
        }

        /// <summary>
        /// Empty args equal unpaged query.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_EmptyArgs_EqualsUnpagedQuery()
        {
            var named = await InvokeAsync("list_customers", new Dictionary<string, JsonElement>());
            var generic = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"));

            named.StructuredContent.Should().Contain("Contoso");
            generic.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Ghost list is unknown.
        /// </summary>
        [TestMethod]
        public async Task ListGhost_UnknownTool()
        {
            var result = await InvokeAsync("list_ghost", null);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Unknown tool 'list_ghost'.");
        }

        /// <summary>
        /// Named update patches CompanyName.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_OData8_Auth_PatchCompanyName_MatchTwin()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "update_customer",
                ToolArguments.Of("key", "1", "body", """{"CompanyName":"NamedUpdated"}"""),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.Method.Method.Should().Be("PATCH");
            capture.Last.RelativePath.Should().Be("Customers(1)");
        }

        /// <summary>
        /// Missing key on named update is an error.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_MissingKey_IsError()
        {
            var result = await InvokeAsync("update_customer", ToolArguments.Of("body", """{"CompanyName":"X"}"""));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Entity set cannot retarget Products.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_EntitySetOverwritten()
        {
            var (runtime, capture) = AuthorizedCapture();
            await runtime.InvokeAsync(
                "update_customer",
                ToolArguments.Of("entitySet", "Products", "key", "1", "body", """{"CompanyName":"StillCust"}"""),
                CancellationToken.None);

            capture.Last!.RelativePath.Should().Be("Customers(1)");
        }

        /// <summary>
        /// Plural update name is unknown.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomers_UnknownTool()
        {
            var result = await InvokeAsync("update_customers", ToolArguments.Of("key", "1", "body", "{}"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Unknown tool 'update_customers'.");
        }

        /// <summary>
        /// Named delete of a created row then GET 404.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_OData8_CreatedRow_204ThenGet404()
        {
            var (runtime, capture) = AuthorizedCapture();
            var created = await runtime.InvokeAsync("create_customer", ToolArguments.Of("CompanyName", "ToDelete"), CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            var deleted = await runtime.InvokeAsync("delete_customer", ToolArguments.Of("key", key), CancellationToken.None);

            deleted.IsError.Should().BeFalse(deleted.Text);
            deleted.Text.Should().Contain("204");
            capture.Last!.Method.Should().Be(HttpMethod.Delete);
            capture.Last.RelativePath.Should().Be($"Customers({key})");

            var got = await runtime.InvokeAsync("get_customer", ToolArguments.Of("key", key), CancellationToken.None);
            got.IsError.Should().BeTrue();
            got.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Passing Products entitySet still deletes the bound customer.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_EntitySetOverwrittenCannotDeleteProducts()
        {
            var (runtime, capture) = AuthorizedCapture();
            var created = await runtime.InvokeAsync("create_customer", ToolArguments.Of("CompanyName", "KeepProducts"), CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            var deleted = await runtime.InvokeAsync(
                "delete_customer",
                ToolArguments.Of("entitySet", "Products", "key", key),
                CancellationToken.None);

            deleted.IsError.Should().BeFalse(deleted.Text);
            capture.Last!.RelativePath.Should().Be($"Customers({key})");

            var products = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None);
            products.Text.Should().NotContain("status 204");
        }

        /// <summary>
        /// Plural delete name is unknown.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomers_UnknownTool()
        {
            var result = await InvokeAsync("delete_customers", ToolArguments.Of("key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Unknown tool 'delete_customers'.");
        }

        /// <summary>
        /// JSON-RPC named list.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_JsonRpcToolsCall_Restier()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "list_customers", "{}");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// JSON-RPC named get.
        /// </summary>
        [TestMethod]
        public async Task GetCustomer_JsonRpcToolsCall()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "get_customer", """{"key":"1"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// JSON-RPC named update.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_JsonRpcToolsCall()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "update_customer",
                """{"key":"1","body":{"CompanyName":"RpcNamedUp"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// JSON-RPC named delete.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_JsonRpcToolsCall()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync("create_customer", ToolArguments.Of("CompanyName", "RpcNamedDel"), CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "delete_customer", "{\"key\":\"" + key + "\"}");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// Wrong snake <c>list_product</c> is unknown (set is Products).
        /// </summary>
        [TestMethod]
        public async Task ListProducts_CalledAsList_Product_UnknownTool()
        {
            var result = await InvokeAsync("list_product", null);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Unknown tool");
        }

        /// <summary>
        /// Family is complete when flags are true.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_FamilyComplete_GetCreateUpdateDeletePresentWhenFlagsTrue()
        {
            var names = Session().Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain(["list_customers", "get_customer", "create_customer", "update_customer", "delete_customer"]);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<CustomerStore>();
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                });
            services.AddODataMcp();
        }

        /// <summary>
        /// Reads a customer key from an OData JSON payload.
        /// </summary>
        /// <param name="json">The payload.</param>
        /// <returns>
        /// The key.
        /// </returns>
        internal static int ReadCustomerId(string json)
        {
            using var document = JsonDocument.Parse(json);
            foreach (var name in new[] { "CustomerId", "customerId" })
            {
                if (document.RootElement.TryGetProperty(name, out var value) && value.TryGetInt32(out var id))
                {
                    return id;
                }
            }

            throw new InvalidOperationException(json);
        }

        #endregion

    }

}
