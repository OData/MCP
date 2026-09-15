// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// AspNetCore × OData 8 <c>odata_create</c> against the convention rich host.
    /// </summary>
    [TestClass]
    public class OdataCreateHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// POST to the read-only set is 405 through the tool.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_405OnReadOnlyController()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "ReadOnlyItems", "body", """{"CompanyName":"Nope"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 405");
        }

        /// <summary>
        /// Duplicate company on Duplicates is 409.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_409DuplicateKey_RestierOrFixture()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Duplicates", "body", """{"CompanyName":"Contoso"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 409");
        }

        /// <summary>
        /// A bad If-Match twin against Etags is 412; the tool cannot send If-Match so it sees 428.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_412Precondition()
        {
            using var client = CreateClient();
            using var content = new StringContent("""{"CompanyName":"EtagCo"}""", Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/odata/Etags")
            {
                Content = content
            };
            request.Headers.TryAddWithoutValidation("If-Match", "\"bad\"");
            using var twin = await client.SendAsync(request);
            twin.StatusCode.Should().Be((HttpStatusCode)412);

            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Etags", "body", """{"CompanyName":"EtagCo"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 428");
        }

        /// <summary>
        /// Payloads rejects a body larger than 64 bytes with OData 413.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_413ODataBodyTooLarge()
        {
            var (runtime, capture) = AuthorizedCapture();
            var body = "{\"CompanyName\":\"" + new string('x', 80) + "\"}";
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Payloads", "body", body),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 413");
            capture.Requests.Should().NotBeEmpty();
        }

        /// <summary>
        /// Direct OData POST with text/plain is 415; the tool always sends JSON.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_415UnsupportedMedia()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var content = new StringContent("CompanyName=Plain", Encoding.UTF8, "text/plain");
            using var twin = await client.PostAsync("/odata/Customers", content);
            ((int)twin.StatusCode).Should().BeOneOf(415, 400, 401);

            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"JsonCo"}"""),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.JsonBody.Should().Be("""{"CompanyName":"JsonCo"}""");
        }

        /// <summary>
        /// POST Etags without If-Match is 428.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_428PreconditionRequired()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Etags", "body", """{"CompanyName":"NeedTag"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 428");
        }

        /// <summary>
        /// POST Booms is 500 through the tool.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_500()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Booms", "body", """{"CompanyName":"Boom"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 500");
        }

        /// <summary>
        /// POST Maintenance is 503 with Retry-After in the tool text.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_503()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Maintenance", "body", """{"CompanyName":"Later"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 503");
            result.Text.Should().Contain("Retry-After:");
        }

        /// <summary>
        /// JSON-RPC tools/call forwards Authorization onto the in-process OData POST.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_AuthorizationForwardedFromMcpHttpContext_Succeeds()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_create",
                """{"entitySet":"Customers","body":{"CompanyName":"RpcAuthCo"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotContain("\"isError\":true");
            body.Should().Contain("RpcAuthCo");

            var listed = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "filter", "CompanyName eq 'RpcAuthCo'"));
            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("RpcAuthCo");
        }

        /// <summary>
        /// A binary-looking body string is posted and rejected by OData.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_BinaryGarbageBody()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", "\u0000\u0001not-json"),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            capture.Last!.JsonBody.Should().Be("\u0000\u0001not-json");
        }

        /// <summary>
        /// An array body is posted raw and OData returns 400.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_BodyAsArray_PostedArray_400()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["body"] = JsonSerializer.SerializeToElement(new[] { 1, 2 })
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 400");
            capture.Last!.JsonBody.Should().Be("[1,2]");
        }

        /// <summary>
        /// A bool body is posted raw and OData returns 400.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_BodyAsBool_400()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["body"] = JsonSerializer.SerializeToElement(true)
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 400");
            capture.Last!.JsonBody.Should().Be("true");
        }

        /// <summary>
        /// A numeric body is posted as raw text.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_BodyAsNumber_RawTextPosted()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["body"] = JsonSerializer.SerializeToElement(42)
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            capture.Last!.JsonBody.Should().Be("42");
        }

        /// <summary>
        /// The string <c>null</c> is posted as the body.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_BodyLiteralNullString()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", "null"),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            capture.Last!.JsonBody.Should().Be("null");
        }

        /// <summary>
        /// JSON null body is posted as <c>null</c> rather than leftover properties.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_BodyNullJson_TreatedMissing_SynthesizesLeftovers()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["body"] = JsonSerializer.SerializeToElement((object?)null),
                    ["CompanyName"] = ToolArguments.Json("FromLeftover")
                },
                CancellationToken.None);

            capture.Last!.JsonBody.Should().Be("null");
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// An open-brace-only body is malformed JSON and OData returns 400.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_BodyOpenBraceOnly_MalformedJson_OData400()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", "{"),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 400");
            capture.Last!.JsonBody.Should().Be("{");
        }

        /// <summary>
        /// Concurrent unique creates all succeed.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ConcurrentCreates_RestierUniqueIds()
        {
            var (runtime, _) = AuthorizedCapture();
            var tasks = Enumerable.Range(0, 5).Select(index => runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", $$"""{"CompanyName":"Concurrent{{index}}"}"""),
                CancellationToken.None));
            var results = await Task.WhenAll(tasks);

            results.Should().OnlyContain(item => item.IsError == false, string.Join(" | ", results.Select(item => item.Text)));
        }

        /// <summary>
        /// Create issues POST, not GET.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_DoesNotCallQuery()
        {
            var (runtime, capture) = AuthorizedCapture();
            await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"PostOnly"}"""),
                CancellationToken.None);

            capture.Last.Should().NotBeNull();
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("Customers");
            capture.Requests.Should().NotContain(item => item.Method == HttpMethod.Get);
        }

        /// <summary>
        /// <c>$body</c> is not <c>body</c>; it becomes a leftover property that the closed Customer type rejects before HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_DollarBody_IsUnknownPropertyBeforeHttp()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "$body", """{"CompanyName":"Dollar"}""", "CompanyName", "Synthesized"),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().StartWith("Unknown property '$body' on Customer.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// An empty string body is posted empty.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_EmptyBodyString_PostsEmpty_OData400Or415()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", string.Empty),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            capture.Last!.JsonBody.Should().BeEmpty();
            result.Text.Should().MatchRegex("status (400|415)");
        }

        /// <summary>
        /// <c>entity</c> is not accepted in place of <c>entitySet</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_EntityInsteadOfEntitySet_IsError()
        {
            var result = await InvokeAsync("odata_create", ToolArguments.Of("entity", "Customers", "body", """{"CompanyName":"X"}"""));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Named <c>create_customer</c> and generic create produce the same company.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_EqualsCreateCustomerNamed_Restier()
        {
            var (runtime, _) = AuthorizedCapture();
            var generic = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"GenericTwin"}"""),
                CancellationToken.None);
            generic.IsError.Should().BeFalse(generic.Text);

            var named = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NamedTwin"}"""),
                CancellationToken.None);
            named.IsError.Should().BeFalse(named.Text);
            named.StructuredContent.Should().Contain("NamedTwin");
        }

        /// <summary>
        /// <c>id</c> is not accepted in place of <c>entitySet</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_IdInsteadOfEntitySet_IsError()
        {
            var result = await InvokeAsync("odata_create", ToolArguments.Of("id", "Customers", "body", """{"CompanyName":"X"}"""));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Trailing-comma JSON is posted and rejected.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_InvalidJson_TrailingComma()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"X",}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            capture.Last!.JsonBody.Should().Contain("CompanyName");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error, not a tool success.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_JsonRpc_Malformed()
        {
            using var client = CreateClient();
            using var response = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{"));
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            body.Should().NotContain("odata_create");
            body.ToLowerInvariant().Should().Contain("error");
        }

        /// <summary>
        /// A non-JSON content type on MCP is rejected.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_JsonRpc_WrongContentTypeOnMcp()
        {
            using var client = CreateClient();
            using var response = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("""{"jsonrpc":"2.0","id":"1","method":"tools/call"}""", "text/plain"));

            response.StatusCode.Should().BeOneOf(HttpStatusCode.UnsupportedMediaType, HttpStatusCode.BadRequest, HttpStatusCode.NotAcceptable);
        }

        /// <summary>
        /// Leftover properties without <c>body</c> are serialized and POSTed.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_OData8_LeftoverPropertiesWithoutBody_PostsThem()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "CompanyName", "Loose"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.JsonBody.Should().Contain("Loose");
            capture.Last.JsonBody.Should().NotContain("entitySet");
        }

        /// <summary>
        /// Object <c>body</c> matches a JSON string body.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_OData8_ObjectBodyNotString_SameAsString()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["body"] = JsonSerializer.SerializeToElement(new { CompanyName = "ObjectCo" })
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("ObjectCo");
            capture.Last!.JsonBody.Should().Contain("ObjectCo");
        }

        /// <summary>
        /// Authenticated create returns 201 and the row is visible to GET/query.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_OData8_WithAuthorization_Returns201AndVisibleToGet()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var twinContent = new StringContent("""{"CompanyName":"FabrikamTwin"}""", Encoding.UTF8, "application/json");
            using var twin = await client.PostAsync("/odata/Customers", twinContent);
            var twinBody = await twin.Content.ReadAsStringAsync();
            twin.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)twin.StatusCode, twinBody);

            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"Fabrikam"}"""),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().MatchRegex("201|200");
            result.StructuredContent.Should().Contain("Fabrikam");
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("Customers");

            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "2"), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            got.StructuredContent.Should().Contain("Fabrikam");
        }

        /// <summary>
        /// POST Forbidden is 403.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_OData8_Forbidden_403()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Forbidden", "body", """{"CompanyName":"Nope"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 403");
        }

        /// <summary>
        /// Missing <c>entitySet</c> is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_MissingEntitySet_IsError()
        {
            var result = await InvokeAsync("odata_create", ToolArguments.Of("body", """{"CompanyName":"X"}"""));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Empty company name is OData 400 with auth.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_MissingRequiredCompanyName_OData8_400()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var twinContent = new StringContent("""{"CompanyName":""}""", Encoding.UTF8, "application/json");
            using var twin = await client.PostAsync("/odata/Customers", twinContent);
            twin.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":""}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 400");
        }

        /// <summary>
        /// Entity set and body swapped yields 404 or 400.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_SwappedBodyAndEntitySet()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement(new { CompanyName = "Swap" }),
                    ["body"] = ToolArguments.Json("Customers")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().MatchRegex("status (400|404|405)");
        }

        /// <summary>
        /// Create then get then update then delete on the new row.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ThenGet_ThenUpdate_ThenDelete_Restier()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"Lifecycle"}"""),
                CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var key = ReadCustomerId(created.StructuredContent!);

            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", key.ToString()), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            got.StructuredContent.Should().Contain("Lifecycle");

            var updated = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", key.ToString(), "body", """{"CompanyName":"Lifecycle2"}"""),
                CancellationToken.None);
            updated.IsError.Should().BeFalse(updated.Text);

            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key.ToString()), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Create then named get sees the row.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ThenNamedGet()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NamedGetCo"}"""),
                CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var key = ReadCustomerId(created.StructuredContent!);

            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", key.ToString()), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            got.StructuredContent.Should().Contain("NamedGetCo");
        }

        /// <summary>
        /// Create then query filter sees the row.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ThenQueryFilter_SeesRow()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"FilterCo"}"""),
                CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);

            var query = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "filter", "CompanyName eq 'FilterCo'"),
                CancellationToken.None);
            query.IsError.Should().BeFalse(query.Text);
            query.StructuredContent.Should().Contain("FilterCo");
        }

        /// <summary>
        /// Unicode company names succeed.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_UnicodeCompanyName_SucceedsRestierAndTripPin()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"北風 😀"}"""),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            using var created = System.Text.Json.JsonDocument.Parse(result.StructuredContent!);
            created.RootElement.GetProperty("CompanyName").GetString().Should().Contain("北風");
        }

        /// <summary>
        /// Unknown entity set is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_UnknownSet_404()
        {
            var (runtime, _) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "DoesNotExist", "body", """{"CompanyName":"X"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().MatchRegex("status (404|405)");
        }

        /// <summary>
        /// <c>key</c> is stripped from leftover JSON; CompanyName is posted.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_UsingGetArgs_KeyInCreate_KeyStrippedFromBody()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "key", "99", "CompanyName", "KeyStripped"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.JsonBody.Should().Contain("KeyStripped");
            capture.Last.JsonBody.Should().NotContain("\"key\"");
        }

        /// <summary>
        /// Query option names as the only leftovers on generic create fail before HTTP: CompanyName is missing.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_UsingQueryArgs_FilterTop_AsOnlyExtras_FailsBeforeHttp()
        {
            var (runtime, capture) = AuthorizedCapture();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "filter", "x", "top", 1),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Missing required properties on Customer: CompanyName. Required on create: CompanyName.");
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
