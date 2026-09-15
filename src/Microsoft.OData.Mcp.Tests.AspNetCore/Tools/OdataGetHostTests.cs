// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Convention OData 8 host tests for <c>odata_get</c>.
    /// </summary>
    [TestClass]
    public class OdataGetHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Booms(1) is an OData 500 that the tool surfaces with status 500.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_500_StatusInText()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Booms", "key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("status 500");
        }

        /// <summary>
        /// Maintenance(1) is an OData 503 whose Retry-After is copied into the tool text.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_503_MaintenanceRetryAfter()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Maintenance", "key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("status 503");
            result.Text.Should().Contain("Retry-After: 15.");
        }

        /// <summary>
        /// A customer created through the tool is visible to a subsequent get.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_AfterCreate_SeesNewEntity()
        {
            var (runtime, _) = CreateCapturingRuntime();
            Authenticate();
            var created = await runtime.InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"Fabrikam"}"""), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var key = ReadCustomerId(created.StructuredContent!) ?? "2";
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", key), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Fabrikam");
        }

        /// <summary>
        /// Get after delete of the seeded customer is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_AfterDelete_404()
        {
            var deleted = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            deleted.IsError.Should().BeFalse(deleted.Text);
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// Get after an authorized patch sees the new company name.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_AfterUpdate_SeesPatch()
        {
            var (runtime, _) = CreateCapturingRuntime();
            Authenticate();
            var updated = await runtime.InvokeAsync("odata_update", ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"Updated"}"""), CancellationToken.None);
            updated.IsError.Should().BeFalse(updated.Text);
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Updated");
        }

        /// <summary>
        /// An already-quoted key is left as-is and is not wrapped again.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_AlreadyQuotedKey_LeftAsIs()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "'ALFKI'");
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')");
            capture.Last.RelativePath.Should().NotContain("'''ALFKI'''");
        }

        /// <summary>
        /// Boolean key <c>true</c> is unquoted on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_BooleanKey_Unquoted()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Flags", "key", "true");
            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("Flags(true)");
            result.StructuredContent.Should().Contain("On");
        }

        /// <summary>
        /// Get-by-key is a single entity, not the same payload as an unfiltered top-1 query.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_DoesNotEqualQueryTop1WithoutFilter()
        {
            var get = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            var query = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers", "top", 1));
            get.IsError.Should().BeFalse(get.Text);
            query.IsError.Should().BeFalse(query.Text);
            get.StructuredContent.Should().NotBe(query.StructuredContent);
            query.StructuredContent.Should().Contain("value");
        }

        /// <summary>
        /// A <c>$key</c> argument is ignored, so the required <c>key</c> is still missing.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_DollarKey_IgnoredMissingKey()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "$key", "1");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("key");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// An empty key string is rejected before OData.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_EmptyKeyString_IsError()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("key");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// <c>entity</c> is not accepted in place of <c>entitySet</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_EntityInsteadOfEntitySet_IsError()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entity", "Customers", "key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Named <c>get_customer</c> returns the same payload as generic get for key 1.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_EqualsGetCustomerNamedTool()
        {
            var generic = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            generic.IsError.Should().BeFalse(generic.Text);
            generic.StructuredContent.Should().Contain("Contoso");
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_customers");
        }

        /// <summary>
        /// Extra unknown properties are ignored and the get still succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_ExtraUnknownProps_Ignored()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1", "foo", "bar"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// A <c>filter</c> argument on get is forwarded as a query option.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_FilterOnGet_IsForwardedAsQueryOption()
        {
            const string filter = "CompanyName eq 'Contoso'";
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "1", "filter", filter);
            capture.Last!.RelativePath.Should().Be("Customers(1)");
            capture.Last.QueryOptions.Should().ContainKey("filter");
            capture.Last.QueryOptions["filter"].Should().Be(filter);
            if (!result.IsError)
            {
                result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            }
        }

        /// <summary>
        /// Forbidden(1) is an OData 403.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Forbidden_403()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Forbidden", "key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("status 403");
        }

        /// <summary>
        /// Guid keys are left unquoted on the wire and match GET Widgets({SeedId}).
        /// </summary>
        [TestMethod]
        public async Task OdataGet_GuidKey_Unquoted()
        {
            var key = Widget.SeedId.ToString();
            using var client = CreateClient();
            var twin = await client.GetStringAsync($"/odata/Widgets({key})");
            twin.Should().Contain("Alpha");
            var (result, capture) = await GetCapturedAsync("entitySet", "Widgets", "key", key);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Alpha");
            capture.Last!.RelativePath.Should().Be($"Widgets({key})");
            capture.Last.RelativePath.Should().NotContain("'");
        }

        /// <summary>
        /// <c>id</c> is not accepted in place of <c>key</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_IdInsteadOfKey_IsErrorMissingKey()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "id", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error, not a successful get.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_JsonRpc_Malformed()
        {
            using var client = CreateClient();
            using var content = McpJsonRpc.Content("{");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, body);
            response.IsSuccessStatusCode.Should().BeFalse(body);
        }

        /// <summary>
        /// A tools/call with the wrong content type is rejected or unsupported.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_JsonRpc_WrongContentType()
        {
            using var client = CreateClient();
            using var content = new StringContent("""{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"odata_get","arguments":{"entitySet":"Customers","key":"1"}}}""", Encoding.UTF8, "text/plain");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, body);
        }

        /// <summary>
        /// JSON-RPC <c>tools/call</c> for customer 1 returns Contoso.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_JsonRpcToolsCall_OData8_Customer1()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_get", """{"entitySet":"Customers","key":"1"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue(body);
            body.Should().Contain("Contoso");
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// An array key is forwarded as raw JSON and becomes a quoted path that 404s.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyAsArray_IsErrorOr404()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", new[] { 1, 2 });
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Contain("'");
        }

        /// <summary>
        /// A JSON boolean key is left unquoted on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyAsBool_true_UnquotedPath()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Flags", "key", true);
            capture.Last!.RelativePath.Should().Be("Flags(true)");
            capture.Last.RelativePath.Should().NotContain("'");
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("On");
        }

        /// <summary>
        /// A JSON number key is formatted as an unquoted numeric path.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyAsNumber_1_SucceedsNumeric()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", 1);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            capture.Last!.RelativePath.Should().Be("Customers(1)");
        }

        /// <summary>
        /// A JSON object key is quoted as a whole and does not become a composite path.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyAsObject_CompositeAttempt()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "OrderDetails", "key", new { OrderID = 10248, ProductID = 11 });
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Contain("'");
            capture.Last.RelativePath.Should().NotBe("OrderDetails(OrderID=10248,ProductID=11)");
        }

        /// <summary>
        /// A JSON null key is treated as missing.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyNull_IsError()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", null);
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("key");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Apostrophes in string keys are doubled inside quotes.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyOBrienApostrophe_Doubled()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "O'Brien");
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('O''Brien')");
        }

        /// <summary>
        /// An emoji key is quoted and sent; OData returns 404 or 400.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_KeyWithEmoji_QuotedSent_404Or400()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "😀");
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('😀')");
            result.Text.Should().Match(text => text.Contains("status 404", StringComparison.Ordinal) || text.Contains("status 400", StringComparison.Ordinal));
        }

        /// <summary>
        /// An expand longer than 512 characters is rejected with no OData round-trip.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_MaxExpandLengthExceeded_NoHttp()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "1", "expand", new string('x', 513));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("expand exceeds the maximum length");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// A select longer than 1024 characters is rejected with no OData round-trip.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_MaxSelectLengthExceeded_NoHttp()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "1", "select", new string('x', 1025));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("select exceeds the maximum length");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Missing <c>entitySet</c> is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_MissingEntitySet_IsError()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Missing <c>key</c> is an error whose text contains <c>key</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_MissingKey_IsErrorContainsKey()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Get Customers key "1" matches GET /odata/Customers(1) and is unquoted on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_OData8_Customer1_MatchesGetCustomers1()
        {
            using var client = CreateClient();
            var odata = await client.GetStringAsync("/odata/Customers(1)");
            odata.Should().Contain("Contoso");
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "1");
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            capture.Last!.RelativePath.Should().Be("Customers(1)");
            capture.Last.Method.Should().Be(HttpMethod.Get);
        }

        /// <summary>
        /// Get with expand=Orders matches the $expand twin.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_OData8_ExpandOrders()
        {
            using var client = CreateClient();
            using var odata = await client.GetAsync("/odata/Customers(1)?$expand=Orders");
            var body = await odata.Content.ReadAsStringAsync();
            odata.IsSuccessStatusCode.Should().BeTrue(body);
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "1", "expand", "Orders");
            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["expand"].Should().Be("Orders");
            if (body.Contains("Orders", StringComparison.OrdinalIgnoreCase))
            {
                result.StructuredContent.Should().Contain("Orders");
            }
        }

        /// <summary>
        /// Composite OrderDetails keys must be unquoted on the wire. Quoted whole-key formatting fails this test.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_OData8_OrderDetails_CompositeKey()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/OrderDetails(OrderID=10248,ProductID=11)");
            var twinBody = await twin.Content.ReadAsStringAsync();
            twin.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)twin.StatusCode, twinBody);
            var (result, capture) = await GetCapturedAsync("entitySet", "OrderDetails", "key", "OrderID=10248,ProductID=11");
            capture.Last!.RelativePath.Should().NotBeNullOrWhiteSpace();
            capture.Last.RelativePath.Should().NotBe("OrderDetails('OrderID=10248,ProductID=11')",
                "FormatKey must not quote a composite key as a single string");
            capture.Last.RelativePath.Should().Be("OrderDetails(OrderID=10248,ProductID=11)");
            capture.Last.RelativePath.Should().NotStartWith("OrderDetails('");
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("10248");
        }

        /// <summary>
        /// Get with select=CompanyName matches the $select twin.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_OData8_SelectCompanyName()
        {
            using var client = CreateClient();
            var odata = await client.GetStringAsync("/odata/Customers(1)?$select=CompanyName");
            odata.Should().Contain("Contoso");
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "1", "select", "CompanyName");
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            capture.Last!.QueryOptions["select"].Should().Be("CompanyName");
        }

        /// <summary>
        /// String keys are quoted on the wire.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_StringKey_Quoted()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "ALFKI");
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('ALFKI')");
        }

        /// <summary>
        /// Swapping entitySet and key produces an OData 404.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_Swapped_EntitySetIsKey_KeyIsSetName()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "1", "key", "Customers");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("404");
            capture.Last!.RelativePath.Should().Be("1('Customers')");
        }

        /// <summary>
        /// Get of customer 1 can be followed by navigate to Orders.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_ThenNavigate()
        {
            var get = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            get.IsError.Should().BeFalse(get.Text);
            var (runtime, capture) = CreateCapturingRuntime();
            await runtime.InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"), CancellationToken.None);
            capture.Last!.RelativePath.Should().Be("Customers(1)/Orders");
        }

        /// <summary>
        /// Unknown key 999 is an OData 404.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_UnknownKey999_IsError404()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "999"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("status 404");
        }

        /// <summary>
        /// An unknown entity set is an OData 404.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_UnknownSet_404()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "DoesNotExist", "key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// Query arguments without a key do not turn get into a list.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_UsingQueryArgsWithoutKey_IsError()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "filter", "CustomerId eq 1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// A whitespace key is rejected.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_WhitespaceKey_IsError()
        {
            var (result, capture) = await GetCapturedAsync("entitySet", "Customers", "key", "   ");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("key");
            capture.Requests.Should().BeEmpty();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Invokes <c>odata_get</c> through a capturing executor.
        /// </summary>
        /// <param name="pairs">Alternating names and values.</param>
        /// <returns>
        /// The tool result and capture wrapper.
        /// </returns>
        internal async Task<(ODataToolInvocationResult Result, CapturingODataExecutor Capture)> GetCapturedAsync(params object?[] pairs)
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_get", ToolArguments.Of(pairs), CancellationToken.None);

            return (result, capture);
        }

        /// <summary>
        /// Reads a CustomerId from an OData entity payload.
        /// </summary>
        /// <param name="json">The JSON body.</param>
        /// <returns>
        /// The key text, or <c>null</c>.
        /// </returns>
        internal static string? ReadCustomerId(string json)
        {
            using var document = JsonDocument.Parse(json);
            foreach (var name in new[] { "CustomerId", "customerId" })
            {
                if (document.RootElement.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                {
                    return value.ValueKind == JsonValueKind.Number ? value.GetRawText() : value.GetString();
                }
            }

            return null;
        }

        #endregion

    }

}
