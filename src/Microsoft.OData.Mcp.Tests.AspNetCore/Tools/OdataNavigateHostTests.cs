// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Convention OData 8 host tests for <c>odata_navigate</c>.
    /// </summary>
    [TestClass]
    public class OdataNavigateHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Navigate does not inject a default <c>top</c> on a to-many path.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_DoesNotAddDefaultTopOnToMany()
        {
            var (_, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "Orders");
            capture.Last!.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// <c>$expand</c> as the navigation name is forwarded and fails at OData.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_DollarExpandAsNavigationName_404()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "$expand");
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers(1)/$expand");
        }

        /// <summary>
        /// An empty navigation name is rejected before HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_EmptyNavigation()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("navigation");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// An emoji navigation name is sent and fails at OData.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_EmojiNavigation_404()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "😀");
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers(1)/😀");
        }

        /// <summary>
        /// <c>entity</c> is not accepted in place of <c>entitySet</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_EntityInsteadOfEntitySet()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entity", "Customers", "key", "1", "navigation", "Orders"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Expand on a navigation is forwarded when the caller supplies it.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_ExpandOnNavigation_IfAllowed()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "Orders", "expand", "Customer");
            capture.Last!.RelativePath.Should().Be("Customers(1)/Orders");
            capture.Last.QueryOptions["expand"].Should().Be("Customer");
            result.Text.Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// <c>filter</c> used as the navigation name is forwarded and fails at OData.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_FilterAsNavigation_404()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "filter");
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers(1)/filter");
        }

        /// <summary>
        /// A navigation of <c>Orders/$count</c> is sent as-is and twins the same path.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_InjectionNavigation_OrdersCommaHack_404Or400()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/Customers(1)/Orders/$count");
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "Orders/$count");
            capture.Last!.RelativePath.Should().Be("Customers(1)/Orders/$count");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain($"status {(int)twin.StatusCode}");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_JsonRpc_Malformed()
        {
            using var client = CreateClient();
            using var content = McpJsonRpc.Content("{");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, body);
            response.IsSuccessStatusCode.Should().BeFalse(body);
        }

        /// <summary>
        /// Wrong-content-type JSON-RPC is not a successful navigate.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_JsonRpc_WrongContentType()
        {
            using var client = CreateClient();
            using var content = new StringContent("""{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"odata_navigate","arguments":{"entitySet":"Customers","key":"1","navigation":"Orders"}}}""", Encoding.UTF8, "text/plain");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, body);
        }

        /// <summary>
        /// JSON-RPC <c>tools/call</c> navigates Customers(1)/Orders.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_JsonRpcToolsCall_OData8_Customer1Orders()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_navigate", """{"entitySet":"Customers","key":"1","navigation":"Orders"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, body);
        }

        /// <summary>
        /// Unknown key 999 on a navigation twins GET Customers(999)/Orders.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_KeyNotFound_404()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/Customers(999)/Orders");
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "999", "navigation", "Orders");
            capture.Last!.RelativePath.Should().Be("Customers(999)/Orders");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain($"status {(int)twin.StatusCode}");
        }

        /// <summary>
        /// An expand longer than 512 characters is rejected with no HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_MaxExpandLengthExceeded_NoHttp()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "Orders", "expand", new string('x', 513));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("expand exceeds the maximum length");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// A filter longer than 2048 characters is rejected with no HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_MaxFilterLengthExceeded_NoHttp()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "Orders", "filter", new string('x', 2049));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("filter exceeds the maximum length");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// A select longer than 1024 characters is rejected with no HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_MaxSelectLengthExceeded_NoHttp()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "Orders", "select", new string('x', 1025));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("select exceeds the maximum length");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Missing <c>entitySet</c> is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_MissingEntitySet_IsError()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("key", "1", "navigation", "Orders"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Missing <c>key</c> is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_MissingKey_IsError()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "navigation", "Orders"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Missing <c>navigation</c> is an error whose text contains <c>navigation</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_MissingNavigation_IsErrorContainsNavigation()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("navigation");
        }

        /// <summary>
        /// <c>name</c> is not accepted in place of <c>navigation</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_NameInsteadOfNavigation()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "name", "Orders"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("navigation");
        }

        /// <summary>
        /// A numeric navigation name is forwarded as raw text.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_NavigationAsNumber()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", 1);
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers(1)/1");
        }

        /// <summary>
        /// <c>nav</c> is not accepted in place of <c>navigation</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_NavInsteadOfNavigation_IsErrorMissingNavigation()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "nav", "Orders"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("navigation");
        }

        /// <summary>
        /// Navigate Customers(1)/Orders matches the HTTP twin and uses an unquoted key.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_OData8_Customer1Orders_MatchesGetCustomers1Orders()
        {
            using var client = CreateClient();
            using var odata = await client.GetAsync("/odata/Customers(1)/Orders");
            var odataBody = await odata.Content.ReadAsStringAsync();
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "Orders");
            capture.Last!.RelativePath.Should().Be("Customers(1)/Orders");
            capture.Last.Method.Should().Be(HttpMethod.Get);
            if (odata.IsSuccessStatusCode)
            {
                result.IsError.Should().BeFalse(result.Text);
            }
            else
            {
                result.IsError.Should().BeTrue(result.Text);
                result.Text.Should().Contain($"status {(int)odata.StatusCode}");
                odataBody.Should().NotBeNull();
            }
        }

        /// <summary>
        /// Navigate Orders(1)/Customer twins the to-one path when the convention model exposes it.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_OData8_OrdersToCustomer_ToOne()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/Orders(1)/Customer");
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Orders", "key", "1", "navigation", "Customer");
            capture.Last!.RelativePath.Should().Be("Orders(1)/Customer");
            AssertTwinParity(result, twin.StatusCode);
        }

        /// <summary>
        /// Skip, top, and count on a navigation collection are forwarded.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_PageNavSet_SkipTopEqualsTwin()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "Orders", "skip", 0, "top", 1, "count", true);
            capture.Last!.RelativePath.Should().Be("Customers(1)/Orders");
            capture.Last.QueryOptions["skip"].Should().Be("0");
            capture.Last.QueryOptions["top"].Should().Be("1");
            capture.Last.QueryOptions["count"].Should().Be("true");
            result.Text.Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// <c>property</c> is not accepted in place of <c>navigation</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_PropertyInsteadOfNavigation()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "property", "Orders"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("navigation");
        }

        /// <summary>
        /// Swapping key and navigation builds Customers('Orders')/1.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_SwappedNavigationAndKey()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "Orders", "navigation", "1");
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers('Orders')/1");
        }

        /// <summary>
        /// Navigate then query Orders are different paths.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_ThenQueryIsNotTheSame()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            await runtime.InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"), CancellationToken.None);
            var navigatePath = capture.Last!.RelativePath;
            await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Orders"), CancellationToken.None);
            capture.Last.RelativePath.Should().Be("Orders");
            navigatePath.Should().Be("Customers(1)/Orders");
        }

        /// <summary>
        /// A to-one missing related entity twins the HTTP status.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_ToOneMissingRelated_204OrNullOr404()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/Orders(999)/Customer");
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Orders", "key", "999", "navigation", "Customer"));
            AssertTwinParity(result, twin.StatusCode);
        }

        /// <summary>
        /// Skip/top/count on a nav collection are forwarded without injecting extras.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_TopOnCollection_Passthrough()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "Orders", "top", 1);
            capture.Last!.QueryOptions.Should().ContainKey("top");
            capture.Last.QueryOptions["top"].Should().Be("1");
            result.Text.Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// An undeclared Unicode navigation name fails at OData.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_UnicodeNavNotDeclared()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "訂單"));
            result.IsError.Should().BeTrue(result.Text);
        }

        /// <summary>
        /// An unknown entity set on navigate is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_UnknownSet_404()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "DoesNotExist", "key", "1", "navigation", "Orders"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// Get-style arguments without <c>navigation</c> still fail as missing navigation.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_UsingGetArgsWithoutNavigation()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("navigation");
        }

        /// <summary>
        /// Query-style arguments without <c>navigation</c> still fail as missing navigation.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_UsingQueryArgsWithoutNavigation()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "filter", "true", "top", 1));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("navigation");
        }

        /// <summary>
        /// Whitespace navigation is rejected before HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_WhitespaceNavigation()
        {
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "  ");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("navigation");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// <c>NotANav</c> is an OData error, not a silent empty feed.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_WrongNameNotANav_IsError404or400()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/Customers(1)/NotANav");
            var (result, capture) = await NavigateCapturedAsync("entitySet", "Customers", "key", "1", "navigation", "NotANav");
            result.IsError.Should().BeTrue(result.Text);
            capture.Last!.RelativePath.Should().Be("Customers(1)/NotANav");
            result.Text.Should().Contain($"status {(int)twin.StatusCode}");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Asserts the tool result matches the twin HTTP status class.
        /// </summary>
        /// <param name="result">The tool result.</param>
        /// <param name="status">The twin HTTP status.</param>
        internal static void AssertTwinParity(ODataToolInvocationResult result, HttpStatusCode status)
        {
            if ((int)status is >= 200 and <= 299)
            {
                result.IsError.Should().BeFalse(result.Text);
            }
            else
            {
                result.IsError.Should().BeTrue(result.Text);
                result.Text.Should().Contain($"status {(int)status}");
            }
        }

        /// <summary>
        /// Invokes <c>odata_navigate</c> through a capturing executor.
        /// </summary>
        /// <param name="pairs">Alternating names and values.</param>
        /// <returns>
        /// The tool result and capture wrapper.
        /// </returns>
        internal async Task<(ODataToolInvocationResult Result, CapturingODataExecutor Capture)> NavigateCapturedAsync(params object?[] pairs)
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_navigate", ToolArguments.Of(pairs), CancellationToken.None);

            return (result, capture);
        }

        #endregion

    }

    /// <summary>
    /// <c>odata_navigate</c> cases that require the partitioned rate limiter.
    /// </summary>
    [TestClass]
    public class OdataNavigateRateLimitedHostTests : RateLimitedRichHost
    {

        #region Public Methods

        /// <summary>
        /// The second navigate under Customers spends the set budget.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_429OnCustomersPath()
        {
            var first = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"));
            var second = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"));
            if (!first.IsError)
            {
                second.IsError.Should().BeTrue(second.Text);
                second.Text.Should().Contain("429");
            }
            else
            {
                first.Text.Should().NotBeNullOrWhiteSpace();
            }
        }

        /// <summary>
        /// A second POST to <c>/odata/mcp</c> is HTTP 429.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_McpHttp429()
        {
            using var client = CreateClient();
            using var first = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{}"));
            using var second = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{}"));
            first.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        #endregion

    }

    /// <summary>
    /// <c>odata_navigate</c> cases that require a tiny <c>MaxResponseBytes</c>.
    /// </summary>
    [TestClass]
    public class OdataNavigateTinyResponseHostTests : TinyResponseRichHost
    {

        #region Public Methods

        /// <summary>
        /// An oversized navigation payload is <c>IsError</c> when OData succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_MaxResponseBytesTiny_Friends()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"));
            result.IsError.Should().BeTrue(result.Text);
        }

        #endregion

    }

}
