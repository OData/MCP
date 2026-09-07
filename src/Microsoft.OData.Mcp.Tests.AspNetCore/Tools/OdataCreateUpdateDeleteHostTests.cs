// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Convention OData 8 host tests for create, update, and delete.
    /// </summary>
    [TestClass]
    public class OdataCreateUpdateDeleteHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Create with leftover properties posts JSON when body is omitted.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_LeftoverProperties_PostsJson()
        {
            Authorize();
            var created = await InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "CompanyName", "LeftoverCo"));

            created.IsError.Should().BeFalse(created.Text);
        }

        /// <summary>
        /// Object body is accepted.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ObjectBody_Succeeds()
        {
            Authorize();
            var created = await InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", new { CompanyName = "ObjectCo" }));

            created.IsError.Should().BeFalse(created.Text);
        }

        /// <summary>
        /// String body is accepted.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_StringBody_Succeeds()
        {
            Authorize();
            var created = await InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"StringCo"}"""));

            created.IsError.Should().BeFalse(created.Text);
        }

        /// <summary>
        /// Unauthenticated create is 401.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_WithoutAuthorization_IsError401()
        {
            var created = await InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NoAuth"}"""));

            created.IsError.Should().BeTrue();
            created.Text.Should().Contain("status 401");
        }

        /// <summary>
        /// Duplicate create is 409.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_Duplicate_IsError409()
        {
            var created = await InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Duplicates", "body", """{"CompanyName":"Contoso"}"""));

            created.IsError.Should().BeTrue();
            created.Text.Should().Contain("status 409");
        }

        /// <summary>
        /// Read-only POST is 405.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ReadOnlyItems_IsError405()
        {
            var created = await InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "ReadOnlyItems", "body", """{"CompanyName":"X"}"""));

            created.IsError.Should().BeTrue();
            created.Text.Should().Contain("status 405");
        }

        /// <summary>
        /// Oversized MCP body is rejected without OData.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_MaxRequestBodyBytes_IsErrorNoHttp()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var huge = "{\"CompanyName\":\"" + new string('A', 300_000) + "\"}";
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", huge),
                CancellationToken.None);

            created.IsError.Should().BeTrue();
            created.Text.Should().Contain("exceeds the maximum size");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// OData 413 on Payloads.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_Payloads_IsError413()
        {
            var created = await InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Payloads", "body", "{\"CompanyName\":\"" + new string('B', 80) + "\"}"));

            created.IsError.Should().BeTrue();
            created.Text.Should().Contain("status 413");
        }

        /// <summary>
        /// JSON-RPC create with Authorization.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_JsonRpcToolsCall_WithAuthorization()
        {
            using var client = TestServer.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_create",
                "{\"entitySet\":\"Customers\",\"body\":\"{\\\"CompanyName\\\":\\\"RpcCo\\\"}\"}");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        }

        /// <summary>
        /// Missing entitySet is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_MissingEntitySet_IsError()
        {
            var created = await InvokeAsync("odata_create", ToolArguments.Of("body", "{}"));

            created.IsError.Should().BeTrue();
            created.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Update without auth is 401.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_WithoutAuthorization_IsError401()
        {
            var updated = await InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"Nope"}"""));

            updated.IsError.Should().BeTrue();
            updated.Text.Should().Contain("status 401");
        }

        /// <summary>
        /// Authenticated update succeeds and matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_WithAuthorization_Succeeds()
        {
            Authorize();
            var updated = await InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"UpdatedCo"}"""));

            updated.IsError.Should().BeFalse(updated.Text);
            var got = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            got.StructuredContent.Should().Contain("UpdatedCo");
        }

        /// <summary>
        /// Update missing key 404.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_UnknownKey_IsError404()
        {
            Authorize();
            var updated = await InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "99", "body", """{"CompanyName":"X"}"""));

            updated.IsError.Should().BeTrue();
            updated.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Etag missing If-Match is 428.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_Etags_MissingIfMatch_428()
        {
            var updated = await InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Etags", "key", "1", "body", """{"CompanyName":"X"}"""));

            updated.IsError.Should().BeTrue();
            updated.Text.Should().Contain("status 428");
        }

        /// <summary>
        /// Delete missing key 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_UnknownKey_IsError404()
        {
            var deleted = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", "99"));

            deleted.IsError.Should().BeTrue();
            deleted.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Create then delete is visible on HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_AfterCreate_RemovesEntity()
        {
            Authorize();
            var created = await InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"Doomed"}"""));
            created.IsError.Should().BeFalse(created.Text);
            var listed = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "filter", "CompanyName eq 'Doomed'"));
            var key = ODataFeedReader.ReadKeys(listed.StructuredContent!).Single();
            var deleted = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key.ToString()));

            deleted.IsError.Should().BeFalse(deleted.Text);
            using var client = TestServer.CreateClient();
            var missing = await client.GetAsync($"/odata/Customers({key})");
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_MissingKey_IsError()
        {
            var deleted = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers"));

            deleted.IsError.Should().BeTrue();
            deleted.Text.Should().Contain("key");
        }

        /// <summary>
        /// Named create_customer leftover properties.
        /// </summary>
        [TestMethod]
        public async Task NamedCreateCustomer_LeftoverProperties_Succeeds()
        {
            Authorize();
            var created = await InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "CompanyName", "NamedCo"));

            created.IsError.Should().BeFalse(created.Text);
        }

        /// <summary>
        /// Named get bound to Customers key 1.
        /// </summary>
        [TestMethod]
        public async Task NamedGetCustomer_Key1_MatchesGeneric()
        {
            var generic = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            var listed = await InvokeAsync("list_customers", ToolArguments.Of("filter", "CustomerId eq 1"));

            generic.IsError.Should().BeFalse(generic.Text);
            listed.IsError.Should().BeFalse(listed.Text);
            generic.StructuredContent.Should().Contain("Contoso");
            listed.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Named list_customers matches odata_query.
        /// </summary>
        [TestMethod]
        public async Task NamedListCustomers_MatchesGeneric()
        {
            var named = await InvokeAsync("list_customers", ToolArguments.Of("top", 1));
            var generic = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "top", 1));

            named.StructuredContent.Should().Be(generic.StructuredContent);
        }

        /// <summary>
        /// Named family is complete.
        /// </summary>
        [TestMethod]
        public void NamedFamily_DoesNotSplit()
        {
            var names = Session().Catalog.Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("list_customers");
            names.Should().Contain("get_customer");
            names.Should().Contain("create_customer");
            names.Should().Contain("update_customer");
            names.Should().Contain("delete_customer");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Puts a Bearer token on the in-process HTTP context so CUD forwards Authorization.
        /// </summary>
        internal void Authorize()
        {
            var accessor = TestServer.Services.GetRequiredService<IHttpContextAccessor>();
            if (accessor.HttpContext is null)
            {
                accessor.HttpContext = new DefaultHttpContext
                {
                    RequestServices = TestServer.Services
                };
                accessor.HttpContext.Request.Scheme = "http";
                accessor.HttpContext.Request.Host = new HostString("localhost");
            }

            accessor.HttpContext.Request.Headers.Authorization = "Bearer test";
        }

        #endregion

    }

}
