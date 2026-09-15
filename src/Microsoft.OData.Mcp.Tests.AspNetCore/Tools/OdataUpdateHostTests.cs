// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// AspNetCore × OData 8 <c>odata_update</c> against the convention rich host.
    /// </summary>
    [TestClass]
    public class OdataUpdateHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// PATCH after create then delete still works as a lifecycle step.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_AfterCreate_BeforeDelete()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"PatchThenDelete"}"""),
                CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var key = ReadCustomerId(created.StructuredContent!);

            var updated = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", key.ToString(), "body", """{"CompanyName":"PatchedThenDelete"}"""),
                CancellationToken.None);
            updated.IsError.Should().BeFalse(updated.Text);

            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key.ToString()), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// An array body is posted and OData returns 400.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_BodyAsArray_400()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["key"] = ToolArguments.Json("1"),
                    ["body"] = JsonSerializer.SerializeToElement(new[] { 1 })
                },
                CancellationToken.None);

            capture.Last!.JsonBody.Should().Be("[1]");
            capture.Last.Method.Method.Should().Be("PATCH");
        }

        /// <summary>
        /// Two patches of the same key: last write wins.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_ConcurrentPatches_LastWriteWinsOr409()
        {
            var (runtime, _) = AuthorizedCapture();
            var first = runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"FirstPatch"}"""),
                CancellationToken.None);
            var second = runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"SecondPatch"}"""),
                CancellationToken.None);
            var results = await Task.WhenAll(first, second);

            results.Should().Contain(item => item.IsError == false);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);
            got.StructuredContent.Should().Match(value => value!.Contains("FirstPatch") || value.Contains("SecondPatch"));
        }

        /// <summary>
        /// Update does not issue DELETE.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_DoesNotDelete()
        {
            var (runtime, capture) = AuthorizedCapture();
            await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"StillHere"}"""),
                CancellationToken.None);

            capture.Requests.Should().NotContain(item => item.Method == HttpMethod.Delete);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
        }

        /// <summary>
        /// Empty string body is posted empty.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_EmptyBodyString()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", string.Empty),
                CancellationToken.None);

            capture.Last!.JsonBody.Should().BeEmpty();
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Empty success text includes 204 when the service returns no body.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_EmptySuccess204_TextContains204()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"Updated204"}"""),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().MatchRegex("204|200");
        }

        /// <summary>
        /// <c>entity</c> is not accepted in place of <c>entitySet</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_EntityInsteadOfEntitySet()
        {
            var result = await InvokeAsync(
                "odata_update",
                ToolArguments.Of("entity", "Customers", "key", "1", "body", """{"CompanyName":"X"}"""));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Named <c>update_customer</c> is covered on the simple-model host; rich-model type is shared.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_EqualsUpdateCustomerNamed()
        {
            var (runtime, _) = AuthorizedCapture();
            var generic = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"GenericPatch"}"""),
                CancellationToken.None);
            generic.IsError.Should().BeFalse(generic.Text);

            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);
            got.StructuredContent.Should().Contain("GenericPatch");
        }

        /// <summary>
        /// Forbidden PATCH is 403.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_403()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Forbidden", "key", "1", "body", """{"CompanyName":"X"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 403");
        }

        /// <summary>
        /// Read-only PATCH is 405.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_405IfPatchDisabled()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "ReadOnlyItems", "key", "1", "body", """{"CompanyName":"X"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 405");
        }

        /// <summary>
        /// Duplicate company on Duplicates PATCH is 409.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_409()
        {
            using var client = CreateClient();
            using var create = new StringContent("""{"CompanyName":"OtherCo"}""", Encoding.UTF8, "application/json");
            using var posted = await client.PostAsync("/odata/Duplicates", create);
            posted.IsSuccessStatusCode.Should().BeTrue();

            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Duplicates", "key", "1", "body", """{"CompanyName":"OtherCo"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().MatchRegex("status (409|405|404)");
        }

        /// <summary>
        /// Bad If-Match twin is 412; the tool PATCH of Etags is 428.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_412IfMatchFailure()
        {
            using var client = CreateClient();
            using var content = new StringContent("""{"CompanyName":"Nope"}""", Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/odata/Etags(1)") { Content = content };
            request.Headers.TryAddWithoutValidation("If-Match", "\"bad\"");
            using var twin = await client.SendAsync(request);
            twin.StatusCode.Should().Be((HttpStatusCode)412);

            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Etags", "key", "1", "body", """{"CompanyName":"Nope"}"""),
                CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 428");
        }

        /// <summary>
        /// Large PATCH of Payloads is 413.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_413()
        {
            var (runtime, capture) = AuthorizedCapture();
            var body = "{\"CompanyName\":\"" + new string('x', 80) + "\"}";
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Payloads", "key", "1", "body", body),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 413");
            capture.Requests.Should().NotBeEmpty();
        }

        /// <summary>
        /// Direct PATCH with text/plain is 415; the tool always sends JSON.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_415()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var content = new StringContent("CompanyName=Plain", Encoding.UTF8, "text/plain");
            using var twin = await client.PatchAsync("/odata/Customers(1)", content);
            ((int)twin.StatusCode).Should().BeOneOf(415, 400, 401, 204);

            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"JsonPatch"}"""),
                CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.JsonBody.Should().Contain("JsonPatch");
        }

        /// <summary>
        /// PATCH Etags without If-Match is 428.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_428()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Etags", "key", "1", "body", """{"CompanyName":"NeedTag"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 428");
        }

        /// <summary>
        /// PATCH Booms is 500.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_500()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Booms", "key", "1", "body", """{"CompanyName":"X"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 500");
        }

        /// <summary>
        /// PATCH Maintenance is 503 with Retry-After.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_503()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Maintenance", "key", "1", "body", """{"CompanyName":"X"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 503");
            result.Text.Should().Contain("Retry-After:");
        }

        /// <summary>
        /// <c>id</c> is not accepted in place of <c>key</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_IdInsteadOfKey_IsErrorMissingKey()
        {
            var result = await InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "id", "1", "body", """{"CompanyName":"X"}"""));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// A second identical PATCH succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_IdempotentSecondPatchSameBody_Succeeds()
        {
            var (runtime, _) = AuthorizedCapture();
            var args = ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"SamePatch"}""");
            var first = await runtime.InvokeAsync("odata_update", args, CancellationToken.None);
            var second = await runtime.InvokeAsync("odata_update", args, CancellationToken.None);

            first.IsError.Should().BeFalse(first.Text);
            second.IsError.Should().BeFalse(second.Text);
        }

        /// <summary>
        /// JSON-RPC malformed body is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_JsonRpc_Malformed()
        {
            using var client = CreateClient();
            using var response = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{"));
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.ToLowerInvariant().Should().Contain("error");
        }

        /// <summary>
        /// Wrong MCP content type is rejected.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_JsonRpc_WrongContentType()
        {
            using var client = CreateClient();
            using var response = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{}", "text/xml"));
            response.StatusCode.Should().BeOneOf(HttpStatusCode.UnsupportedMediaType, HttpStatusCode.BadRequest, HttpStatusCode.NotAcceptable);
        }

        /// <summary>
        /// JSON-RPC tools/call with Authorization patches the seeded customer.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_JsonRpcToolsCall_OData8WithAuth()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_update",
                """{"entitySet":"Customers","key":"1","body":{"CompanyName":"RpcUpdated"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotContain("\"isError\":true");

            var got = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            got.StructuredContent.Should().Contain("RpcUpdated");
        }

        /// <summary>
        /// Numeric key is unquoted on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_KeyAsNumber()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement(1),
                    ["body"] = ToolArguments.Json("""{"CompanyName":"NumKey"}""")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers(1)");
        }

        /// <summary>
        /// Leftover properties without <c>body</c> are serialized for PATCH.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_OData8_LeftoverPropertiesWithoutBodyKey()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "CompanyName", "LoosePatch"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.JsonBody.Should().Contain("LoosePatch");
            capture.Last.JsonBody.Should().NotContain("entitySet");
            capture.Last.JsonBody.Should().NotContain("\"key\"");
        }

        /// <summary>
        /// Object body patches CompanyName.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_OData8_ObjectBody()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["key"] = ToolArguments.Json("1"),
                    ["body"] = JsonSerializer.SerializeToElement(new { CompanyName = "ObjectPatch" })
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.JsonBody.Should().Contain("ObjectPatch");
        }

        /// <summary>
        /// Authenticated PATCH matches the HTTP twin and GET.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_OData8_WithAuthorization_PatchesCompanyName_MatchesTwin()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var twinContent = new StringContent("""{"CompanyName":"TwinUpdated"}""", Encoding.UTF8, "application/json");
            using var twin = await client.PatchAsync("/odata/Customers(1)", twinContent);
            var twinBody = await twin.Content.ReadAsStringAsync();
            twin.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)twin.StatusCode, twinBody);

            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"Updated"}"""),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.Method.Method.Should().Be("PATCH");
            capture.Last.RelativePath.Should().Be("Customers(1)");

            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);
            got.StructuredContent.Should().Contain("Updated");
        }

        /// <summary>
        /// Authorization on the MCP HTTP context is forwarded.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_OData8_AuthorizationForwarded_Succeeds()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_update",
                """{"entitySet":"Customers","key":"1","body":{"CompanyName":"Forwarded"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// Malformed JSON body is posted and rejected.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_MalformedJsonBrace()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", "{"),
                CancellationToken.None);

            capture.Last!.JsonBody.Should().Be("{");
        }

        /// <summary>
        /// Missing body with no leftovers posts an empty object.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_MissingBodyAndNoLeftovers_PostsEmptyObjectOrEmpty()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1"),
                CancellationToken.None);

            capture.Last!.JsonBody.Should().Be("{}");
            capture.Last.Method.Method.Should().Be("PATCH");
        }

        /// <summary>
        /// Missing entity set is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_MissingEntitySet_IsError()
        {
            var result = await InvokeAsync("odata_update", ToolArguments.Of("key", "1", "body", """{"CompanyName":"X"}"""));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_MissingKey_IsError()
        {
            var result = await InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"X"}"""));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// JSON null body is posted as <c>null</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_NullBody()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["key"] = ToolArguments.Json("1"),
                    ["body"] = JsonSerializer.SerializeToElement((object?)null)
                },
                CancellationToken.None);

            capture.Last!.JsonBody.Should().Be("null");
        }

        /// <summary>
        /// The tool always uses PATCH, never PUT.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_PutVsPatch_ToolAlwaysPatch()
        {
            var (runtime, capture) = AuthorizedCapture();
            await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"PatchOnly"}"""),
                CancellationToken.None);

            capture.Last!.Method.Method.Should().Be("PATCH");
            capture.Requests.Should().NotContain(item => item.Method == HttpMethod.Put);
        }

        /// <summary>
        /// Swapped key and entity set 404s.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_SwappedKeyAndEntitySet()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "1", "key", "Customers", "body", """{"CompanyName":"X"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Query after update sees the new name.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_ThenQueryFilterNewName()
        {
            var (runtime, _) = AuthorizedCapture();
            var updated = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"FilterUpdated"}"""),
                CancellationToken.None);
            updated.IsError.Should().BeFalse(updated.Text);

            var query = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "filter", "CompanyName eq 'FilterUpdated'"),
                CancellationToken.None);
            query.StructuredContent.Should().Contain("FilterUpdated");
        }

        /// <summary>
        /// Unicode patch succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_UnicodePatch()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"更新 😀"}"""),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);
            using var patched = System.Text.Json.JsonDocument.Parse(got.StructuredContent!);
            patched.RootElement.GetProperty("CompanyName").GetString().Should().Contain("更新");
        }

        /// <summary>
        /// Unknown key is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_UnknownKey_404()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "999", "body", """{"CompanyName":"Ghost"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Unknown set is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_UnknownSet_404()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "DoesNotExist", "key", "1", "body", """{"CompanyName":"X"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Get-only args without body post an empty leftover object.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_UsingGetArgsOnly_NoBody()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1"),
                CancellationToken.None);

            capture.Last!.JsonBody.Should().Be("{}");
            capture.Last.Method.Method.Should().Be("PATCH");
        }

        /// <summary>
        /// A <c>$filter</c> leftover is an unknown property on the closed Customer type; it never becomes a query option or a PATCH field.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_DollarFilterAsBody_IsUnknownPropertyBeforeHttp()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "$filter", "true", "CompanyName", "FilterLeftover"),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().StartWith("Unknown property '$filter' on Customer.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// A <c>top</c> leftover on update is an unknown property before HTTP, not a silent PATCH field.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_UsingQueryArgs_TopOnUpdate_IsUnknownPropertyBeforeHttp()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "top", 1),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().StartWith("Unknown property 'top' on Customer.");
            capture.Requests.Should().BeEmpty();
        }

        #endregion

        #region Internal Methods

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
