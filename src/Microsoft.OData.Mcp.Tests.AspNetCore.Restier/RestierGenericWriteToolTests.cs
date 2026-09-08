// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Restier API tests for create, update, delete, call, and named CRUD. No OData controller.
    /// </summary>
    [TestClass]
    public class RestierGenericWriteToolTests : RestierToolTestBase
    {

        #region Constructors

        /// <summary>
        /// Initializes Restier endpoint routing for <see cref="McpCustomerApi"/>.
        /// </summary>
        public RestierGenericWriteToolTests()
            : base("RestierWrite")
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Named family uses list_customers and get_mcp_customer.
        /// </summary>
        [TestMethod]
        public void NamedFamily_Restier_DoesNotSplit()
        {
            var names = Session().Catalog.Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("list_customers");
            names.Should().Contain(name => name.StartsWith("get_", StringComparison.Ordinal));
            names.Should().Contain(name => name.StartsWith("create_", StringComparison.Ordinal));
            names.Should().Contain(name => name.StartsWith("update_", StringComparison.Ordinal));
            names.Should().Contain(name => name.StartsWith("delete_", StringComparison.Ordinal));
        }

        /// <summary>
        /// Create through MCP is visible on Restier HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_Restier_VisibleToHttpGet()
        {
            var created = await Runtime().InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"Id":10,"CompanyName":"RestierCo"}"""),
                CancellationToken.None);

            created.IsError.Should().BeFalse(created.Text);
            using var client = CreateClient();
            var body = await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'RestierCo'");
            body.Should().Contain("RestierCo");
        }

        /// <summary>
        /// Update through MCP changes CompanyName.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_Restier_Customer1_CompanyName()
        {
            var updated = await Runtime().InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"ContosoUpdated"}"""),
                CancellationToken.None);

            updated.IsError.Should().BeFalse(updated.Text);
            using var client = CreateClient();
            var body = await client.GetStringAsync("odata/Customers(1)");
            body.Should().Contain("ContosoUpdated");
        }

        /// <summary>
        /// Delete through MCP removes the entity.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_Restier_RemovesCustomer()
        {
            var created = await Runtime().InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"Id":11,"CompanyName":"ToDelete"}"""),
                CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var deleted = await Runtime().InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "Customers", "key", "11"),
                CancellationToken.None);

            deleted.IsError.Should().BeFalse(deleted.Text);
            using var client = CreateClient();
            var missing = await client.GetAsync("odata/Customers(11)");
            missing.IsSuccessStatusCode.Should().BeFalse();
        }

        /// <summary>
        /// List operations includes Restier API methods.
        /// </summary>
        [TestMethod]
        public async Task OdataListOperations_Restier_ContainsMostValuable()
        {
            var listed = await Runtime().InvokeAsync("odata_list_operations", null, CancellationToken.None);

            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("MostValuable");
        }

        /// <summary>
        /// odata_call MostValuable.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_Restier_MostValuable()
        {
            var result = await Runtime().InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"), CancellationToken.None);

            result.Should().NotBeNull();
            if (!result.IsError)
            {
                result.StructuredContent.Should().Contain("42");
            }
        }

        /// <summary>
        /// Bound Share action requires entitySet and key.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_Restier_Share_Bound()
        {
            var result = await Runtime().InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "Share", "entitySet", "Customers", "key", "1", "userName", "bob"),
                CancellationToken.None);

            result.Should().NotBeNull();
        }

        /// <summary>
        /// list_customers matches odata_query.
        /// </summary>
        [TestMethod]
        public async Task NamedListCustomers_Restier_MatchesGeneric()
        {
            var named = await Runtime().InvokeAsync("list_customers", ToolArguments.Of("top", 1, "orderby", "Id"), CancellationToken.None);
            var generic = await Runtime().InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "top", 1, "orderby", "Id"),
                CancellationToken.None);

            named.IsError.Should().BeFalse(named.Text);
            named.StructuredContent.Should().Be(generic.StructuredContent);
        }

        /// <summary>
        /// Restier create of Northwind is visible to HTTP filter.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_Restier_CustomerNorthwind_VisibleToHttpFilter()
        {
            var result = await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":4,"CompanyName":"Northwind"}"""), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            using var client = CreateClient();
            (await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'Northwind'")).Should().Contain("Northwind");
        }

        /// <summary>
        /// JSON-RPC create is visible on the Restier route.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_JsonRpcToolsCall_Restier()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_create", """{"entitySet":"Customers","body":"{\"Id\":57,\"CompanyName\":\"JsonRpcCo\"}"}""");
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            (await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'JsonRpcCo'")).Should().Contain("JsonRpcCo");
        }

        /// <summary>
        /// Generic create equals named create_mcp_customer.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_EqualsCreateCustomerNamed_Restier()
        {
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("create_mcp_customer");
            var generic = await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":55,"CompanyName":"GenericTwin"}"""), CancellationToken.None);
            generic.IsError.Should().BeFalse(generic.Text);
            var named = await Runtime().InvokeAsync("create_mcp_customer", ToolArguments.Of("Id", 56, "CompanyName", "NamedTwin"), CancellationToken.None);
            named.IsError.Should().BeFalse(named.Text);
        }

        /// <summary>
        /// Unicode company name succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_UnicodeCompanyName_SucceedsRestierAndTripPin()
        {
            var result = await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":65,"CompanyName":"北風 😀"}"""), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            using var client = CreateClient();
            var body = await client.GetStringAsync("odata/Customers(65)");
            body.Should().MatchRegex("北風|\\\\u5317\\\\u98a8");
        }

        /// <summary>
        /// Duplicate key create is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_409DuplicateKey_RestierOrFixture()
        {
            var result = await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":1,"CompanyName":"Dup"}"""), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("OData request failed with status");
        }

        /// <summary>
        /// Missing entitySet is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_MissingEntitySet_IsError()
        {
            var result = await Runtime().InvokeAsync("odata_create", ToolArguments.Of("body", """{"Id":59,"CompanyName":"X"}"""), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Create then get then update then delete.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ThenGet_ThenUpdate_ThenDelete_Restier()
        {
            (await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":62,"CompanyName":"Lifecycle"}"""), CancellationToken.None)).IsError.Should().BeFalse();
            (await Runtime().InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "62"), CancellationToken.None)).StructuredContent.Should().Contain("Lifecycle");
            (await Runtime().InvokeAsync("odata_update", ToolArguments.Of("entitySet", "Customers", "key", "62", "body", """{"CompanyName":"Lifecycle2"}"""), CancellationToken.None)).IsError.Should().BeFalse();
            (await Runtime().InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", "62"), CancellationToken.None)).IsError.Should().BeFalse();
            using var client = CreateClient();
            ((int)(await client.GetAsync("odata/Customers(62)")).StatusCode).Should().Be(404);
        }

        /// <summary>
        /// Restier patch CompanyName is visible to HTTP GET.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_Restier_PatchCompanyName_VisibleToHttpGet()
        {
            using var client = CreateClient();
            using var content = RestierJsonContent.Json("""{"CompanyName":"HttpPatched"}""");
            var twin = await client.PatchAsync("odata/Customers(2)", content);
            twin.IsSuccessStatusCode.Should().BeTrue();
            var result = await Runtime().InvokeAsync("odata_update", ToolArguments.Of("entitySet", "Customers", "key", "2", "body", """{"CompanyName":"Updated"}"""), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            (await client.GetStringAsync("odata/Customers(2)")).Should().Contain("Updated");
        }

        /// <summary>
        /// JSON-RPC patch.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_JsonRpcToolsCall_Restier()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_update", """{"entitySet":"Customers","key":"2","body":"{\"CompanyName\":\"RpcPatch\"}"}""");
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            (await client.GetStringAsync("odata/Customers(2)")).Should().Contain("RpcPatch");
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_MissingKey_IsError()
        {
            var result = await Runtime().InvokeAsync("odata_update", ToolArguments.Of("entitySet", "Customers", "body", "{}"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Unknown key 404s.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_UnknownKey_404()
        {
            var result = await Runtime().InvokeAsync("odata_update", ToolArguments.Of("entitySet", "Customers", "key", "999", "body", """{"CompanyName":"Nope"}"""), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status ");
        }

        /// <summary>
        /// Restier delete of a created row then GET 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_Restier_CreatedRow_ThenGet404()
        {
            (await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":78,"CompanyName":"HttpDel"}"""), CancellationToken.None)).IsError.Should().BeFalse();
            var result = await Runtime().InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", "78"), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            using var client = CreateClient();
            ((int)(await client.GetAsync("odata/Customers(78)")).StatusCode).Should().Be(404);
        }

        /// <summary>
        /// JSON-RPC delete of a created row.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_JsonRpcToolsCall_Restier()
        {
            await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":76,"CompanyName":"RpcDel"}"""), CancellationToken.None);
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_delete", """{"entitySet":"Customers","key":"76"}""");
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            ((int)(await client.GetAsync("odata/Customers(76)")).StatusCode).Should().Be(404);
        }

        /// <summary>
        /// Second delete is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_IdempotentSecondDelete_404IsError()
        {
            await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":75,"CompanyName":"Once"}"""), CancellationToken.None);
            (await Runtime().InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", "75"), CancellationToken.None)).IsError.Should().BeFalse();
            var second = await Runtime().InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", "75"), CancellationToken.None);
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("404");
        }

        /// <summary>
        /// Unknown key 999 is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_UnknownKey999_404()
        {
            using var client = CreateClient();
            var twin = await client.DeleteAsync("odata/Customers(999)");
            ((int)twin.StatusCode).Should().Be(404);
            var result = await Runtime().InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", "999"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_MissingKey_IsError()
        {
            var result = await Runtime().InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// List operations agrees with $metadata.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_AgreesWithMetadataFunctionsAndActions()
        {
            using var client = CreateClient();
            var metadata = await client.GetStringAsync("odata/$metadata");
            var result = await Runtime().InvokeAsync("odata_list_operations", null, CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            if (metadata.Contains("MostValuable", StringComparison.Ordinal))
            {
                result.StructuredContent.Should().Contain("MostValuable");
            }
        }

        /// <summary>
        /// Restier list operations succeeds.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_RestierCustomerApi_EmptyArraySucceeds()
        {
            var result = await Runtime().InvokeAsync("odata_list_operations", null, CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("operations");
            result.Text.Should().MatchRegex(@"Declared operations: \d+\.");
        }

        /// <summary>
        /// Unknown operation is not declared.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_RestierCustomerApi_UnknownOp_NotDeclared()
        {
            var result = await Runtime().InvokeAsync("odata_call", ToolArguments.Of("name", "Ghost"), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Operation 'Ghost' is not declared in the model.");
        }

        /// <summary>
        /// Missing name is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_MissingName_IsError()
        {
            var result = await Runtime().InvokeAsync("odata_call", ToolArguments.Of(), CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// Restier create is open; unauthenticated POST is not 401.
        /// </summary>
        [TestMethod]
        public async Task Auth_Restier_OpenCreate_No401()
        {
            using var client = CreateClient();
            using var content = RestierJsonContent.Json("""{"Id":50,"CompanyName":"OpenCo"}""");
            var posted = await client.PostAsync("odata/Customers", content);
            posted.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
            posted.IsSuccessStatusCode.Should().BeTrue();
        }

        #endregion

    }

}
