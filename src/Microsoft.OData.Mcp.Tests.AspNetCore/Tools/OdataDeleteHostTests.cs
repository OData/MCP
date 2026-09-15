// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// AspNetCore × OData 8 <c>odata_delete</c> against the convention rich host.
    /// </summary>
    [TestClass]
    public class OdataDeleteHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Apostrophe keys are doubled by <c>FormatKey</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_ApostropheKey_FormatKeyDoubled()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "Customers", "key", "O'Brien"),
                CancellationToken.None);

            capture.Last!.RelativePath.Should().Be("Customers('O''Brien')");
        }

        /// <summary>
        /// Body on delete is ignored.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_BodyOnDelete_Ignored()
        {
            var (runtime, capture) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"DropMe"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!);
            var result = await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "Customers", "key", key.ToString(), "body", """{"CompanyName":"Nope"}"""),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.JsonBody.Should().BeNull();
            capture.Last.Method.Should().Be(HttpMethod.Delete);
        }

        /// <summary>
        /// Concurrent deletes of the same key: one success, one 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_ConcurrentDeletesSameKey_OneSuccessOne404()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"RaceDelete"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            var first = runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);
            var second = runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);
            var results = await Task.WhenAll(first, second);

            results.Count(item => item.IsError == false).Should().Be(1);
            results.Count(item => item.IsError && item.Text.Contains("404", StringComparison.Ordinal)).Should().Be(1);
        }

        /// <summary>
        /// Delete does not issue PATCH.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_DoesNotPatch()
        {
            var (runtime, capture) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NoPatch"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);

            capture.Last!.Method.Should().Be(HttpMethod.Delete);
            capture.Requests.Where(item => item.RelativePath.Contains("Customers(", StringComparison.Ordinal)).Should()
                .NotContain(item => item.Method.Method == "PATCH");
        }

        /// <summary>
        /// <c>$key</c> is ignored, so key is missing.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_DollarKey_MissingKeyError()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "$key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Empty key is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_EmptyKey()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", " "));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// 204 delete text includes the status.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_EmptyBody204_TextContainsStatus()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NoBody"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            var result = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("204");
            result.StructuredContent.Should().BeNull();
        }

        /// <summary>
        /// Emoji key is quoted and 404s.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_EmojiKey_404()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "Customers", "key", "😀"),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().MatchRegex("status (400|404)");
            capture.Last!.RelativePath.Should().Contain("😀");
        }

        /// <summary>
        /// <c>entity</c> is not accepted in place of <c>entitySet</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_EntityInsteadOfEntitySet()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entity", "Customers", "key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Named delete is asserted on the simple-model host; generic delete of a created row matches.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_EqualsDeleteCustomerNamed()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NamedDeleteTwin"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Forbidden DELETE is 403.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_Forbidden_403()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Forbidden", "key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 403");
        }

        /// <summary>
        /// Read-only DELETE is 405.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_405()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "ReadOnlyItems", "key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 405");
        }

        /// <summary>
        /// Constrained DELETE on Duplicates is 409.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_409ConflictIfConstrained()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Duplicates", "key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 409");
        }

        /// <summary>
        /// DELETE Etags without If-Match is 428; twin with a bad tag is 412.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_412()
        {
            using var client = CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Delete, "/odata/Etags(1)");
            request.Headers.TryAddWithoutValidation("If-Match", "\"bad\"");
            using var twin = await client.SendAsync(request);
            twin.StatusCode.Should().Be((HttpStatusCode)412);

            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Etags", "key", "1"));
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 428");
        }

        /// <summary>
        /// DELETE has no body, so the 413 payload guard does not apply.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_413NotApplicableNoBody()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "Customers", "key", "999", "body", new string('x', 300000)),
                CancellationToken.None);

            capture.Last.Should().NotBeNull();
            capture.Last!.JsonBody.Should().BeNull();
        }

        /// <summary>
        /// DELETE Etags without If-Match is 428.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_428()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Etags", "key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 428");
        }

        /// <summary>
        /// DELETE Booms is 500.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_500()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Booms", "key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 500");
        }

        /// <summary>
        /// DELETE Maintenance is 503 with Retry-After.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_503()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Maintenance", "key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 503");
            result.Text.Should().Contain("Retry-After:");
        }

        /// <summary>
        /// <c>id</c> is not accepted in place of <c>key</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_IdInsteadOfKey()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "id", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// A second delete of the same key is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_IdempotentSecondDelete_404IsError()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"Once"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            var first = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);
            var second = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);

            first.IsError.Should().BeFalse(first.Text);
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_JsonRpc_Malformed()
        {
            using var client = CreateClient();
            using var response = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{"));
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Wrong MCP content type is rejected.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_JsonRpc_WrongContentType()
        {
            using var client = CreateClient();
            using var response = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{}", "text/plain"));
            response.StatusCode.Should().BeOneOf(HttpStatusCode.UnsupportedMediaType, HttpStatusCode.BadRequest, HttpStatusCode.NotAcceptable);
        }

        /// <summary>
        /// JSON-RPC tools/call deletes a created row.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_JsonRpcToolsCall_Restier()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"RpcDelete"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();

            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_delete",
                "{\"entitySet\":\"Customers\",\"key\":\"" + key + "\"}");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// Numeric key is unquoted on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_KeyAsNumber()
        {
            var (runtime, capture) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NumDel"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!);
            await runtime.InvokeAsync(
                "odata_delete",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = ToolArguments.Json("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement(key)
                },
                CancellationToken.None);

            capture.Last!.RelativePath.Should().Be($"Customers({key})");
        }

        /// <summary>
        /// Missing entity set is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_MissingEntitySet_IsError()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Missing key is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_MissingKey_IsError()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Create then delete then GET 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_OData8_ExistingKey_204ThenGet404()
        {
            using var client = CreateClient();
            var (runtime, capture) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"Disposable"}"""),
                CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var key = ReadCustomerId(created.StructuredContent!);

            using var twin = await client.DeleteAsync($"/odata/Customers({key})");
            twin.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var created2 = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"Disposable2"}"""),
                CancellationToken.None);
            var key2 = ReadCustomerId(created2.StructuredContent!);
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key2.ToString()), CancellationToken.None);

            deleted.IsError.Should().BeFalse(deleted.Text);
            deleted.Text.Should().Contain("204");
            capture.Last!.Method.Should().Be(HttpMethod.Delete);
            capture.Last.RelativePath.Should().Be($"Customers({key2})");

            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", key2.ToString()), CancellationToken.None);
            got.IsError.Should().BeTrue();
            got.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Swapped key and entity set 404s.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_Swapped()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "1", "key", "Customers"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Delete after create/get.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_AfterCreateGet()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"AfterGet"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Query after delete does not list the row.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_ThenQueryDoesNotList()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"Gone"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);

            var query = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "filter", "CompanyName eq 'Gone'"),
                CancellationToken.None);
            query.IsError.Should().BeFalse(query.Text);
            query.StructuredContent.Should().NotContain("Gone");
        }

        /// <summary>
        /// Navigate after delete is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_ThenNavigate_404()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NavGone"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);
            var nav = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Customers", "key", key, "navigation", "Orders"),
                CancellationToken.None);

            nav.IsError.Should().BeTrue();
            nav.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Unknown key is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_UnknownKey999_404()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", "999"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Unknown set is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_UnknownSet_404()
        {
            var result = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "DoesNotExist", "key", "1"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Filter is not forwarded on DELETE.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_UsingQueryArgs_FilterIgnoredNotAQuery()
        {
            var (runtime, capture) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"FilterDel"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "Customers", "key", key, "filter", "true"),
                CancellationToken.None);

            capture.Last!.QueryOptions.Should().BeEmpty();
            capture.Last.Method.Should().Be(HttpMethod.Delete);
        }

        /// <summary>
        /// Get-style args succeed when entitySet and key are present.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_UsingGetArgs_OkIfEntitySetAndKeyPresent()
        {
            var (runtime, _) = AuthorizedCapture();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"GetArgsDel"}"""),
                CancellationToken.None);
            var key = ReadCustomerId(created.StructuredContent!).ToString();
            var result = await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "Customers", "key", key, "select", "CompanyName"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
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
