// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// A8 host tests for <c>odata_list_entity_sets</c> on the rich convention model.
    /// </summary>
    [TestClass]
    public class OdataListEntitySetsHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Every entity-set resource name except <c>$metadata</c> appears in the list.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_AgreesWithResourcesList_SetNames()
        {
            var result = await InvokeAsync("odata_list_entity_sets");
            result.IsError.Should().BeFalse(result.Text);
            var listed = ReadEntitySetNames(result.StructuredContent);
            var resources = Session().Catalog.Resources
                .Where(resource => resource.Name is not "$metadata")
                .Select(resource => resource.Name)
                .ToList();

            resources.Should().NotBeEmpty();
            listed.Should().Contain(resources);
        }

        /// <summary>
        /// AspNetCore <c>tools/list</c> advertises this tool and omits <c>shutdown_server</c>.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_AspNetCore_ToolsListContainsThisTool_OmitsShutdown()
        {
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_list_entity_sets");
            Session().Catalog.Tools.Select(tool => tool.Name).Should().NotContain("shutdown_server");

            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            body.Should().Contain("odata_list_entity_sets");
            body.Should().NotContain("shutdown_server");
        }

        /// <summary>
        /// Twenty concurrent catalog list calls all succeed.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ConcurrentTwentyCalls_AllSucceed()
        {
            var tasks = Enumerable.Range(0, 20).Select(_ => InvokeAsync("odata_list_entity_sets"));
            var results = await Task.WhenAll(tasks);

            results.Should().OnlyContain(result => result.IsError == false, "every concurrent list should succeed");
            results.Should().OnlyContain(result => result.StructuredContent!.Contains("Customers", StringComparison.Ordinal));
        }

        /// <summary>
        /// Listing never issues OData HTTP, so it cannot surface an OData 404.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_DoesNotSurfaceOData404()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().NotContain("status 404");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// A <c>$filter</c> argument is ignored; the catalog list still succeeds.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_DollarFilterArgument_IsIgnoredAndStillSucceeds()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_list_entity_sets",
                ToolArguments.Of("$filter", "x", "entitySet", "Customers"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            ReadEntitySetNames(result.StructuredContent).Should().Contain("Customers");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// An empty argument object succeeds.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_EmptyObject_Succeeds()
        {
            var result = await InvokeAsync("odata_list_entity_sets", new Dictionary<string, JsonElement>());

            result.IsError.Should().BeFalse(result.Text);
            ReadEntitySetNames(result.StructuredContent).Should().Contain("Customers");
        }

        /// <summary>
        /// JSON-RPC <c>tools/call</c> lists Customers over Streamable HTTP.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_JsonRpcToolsCall_AspNetCoreStreamableHttp()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_list_entity_sets", "{}");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("Customers");
            JsonRpcIndicatesToolSuccess(body).Should().BeTrue("body {0}", body);
        }

        /// <summary>
        /// A binary MCP body is rejected as a protocol error.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_JsonRpc_BinaryBody_Rejected()
        {
            using var client = TestServer.CreateClient();
            using var content = new ByteArrayContent([0x00, 0x01, 0x02, 0xFF, 0xFE]);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            IsSuccessfulListToolResult(response, body).Should().BeFalse("body {0}", body);
        }

        /// <summary>
        /// POST <c>{}</c> is a protocol error, not a successful list.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_JsonRpc_EmptyBody_IsProtocolErrorNotToolSuccess()
        {
            using var client = TestServer.CreateClient();
            using var response = await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject());
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            IsSuccessfulListToolResult(response, body).Should().BeFalse("body {0}", body);
        }

        /// <summary>
        /// Invalid JSON is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_JsonRpc_InvalidJson_IsProtocolError()
        {
            using var client = TestServer.CreateClient();
            using var content = new StringContent("{", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            IsSuccessfulListToolResult(response, body).Should().BeFalse("body {0}", body);
        }

        /// <summary>
        /// <c>text/plain</c> is rejected or unsupported.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_JsonRpc_WrongContentTypeTextPlain_RejectedOrUnsupported()
        {
            using var client = TestServer.CreateClient();
            using var content = new StringContent(
                """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"odata_list_entity_sets","arguments":{}}}""",
                Encoding.UTF8,
                "text/plain");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            IsSuccessfulListToolResult(response, body).Should().BeFalse("body {0}", body);
        }

        /// <summary>
        /// A <c>name</c> argument meant for describe is ignored; the full list is returned.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_NameArgumentMeantForDescribe_Ignored()
        {
            var result = await InvokeAsync("odata_list_entity_sets", ToolArguments.Of("name", "Customer"));

            result.IsError.Should().BeFalse(result.Text);
            var names = ReadEntitySetNames(result.StructuredContent);
            names.Should().Contain("Customers");
            names.Count.Should().BeGreaterThan(1);
            result.StructuredContent.Should().NotContain("navigations");
        }

        /// <summary>
        /// Null arguments succeed.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_NullArguments_Succeeds()
        {
            var result = await InvokeAsync("odata_list_entity_sets");

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().StartWith("Declared entity sets:");
            ReadEntitySetNames(result.StructuredContent).Should().Contain("Customers");
        }

        /// <summary>
        /// The rich convention catalog lists Customers, Orders, Products, and OrderItems with Customer keys.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_OData8_ReturnsCustomersOrdersProductsOrderItems()
        {
            using var client = TestServer.CreateClient();
            var metadata = await client.GetAsync("/odata/$metadata");
            var metadataBody = await metadata.Content.ReadAsStringAsync();
            if (metadata.IsSuccessStatusCode)
            {
                metadataBody.Should().Contain("Customers");
            }

            var service = await client.GetAsync("/odata");
            var serviceBody = await service.Content.ReadAsStringAsync();
            var result = await InvokeAsync("odata_list_entity_sets");
            result.IsError.Should().BeFalse(result.Text);
            var listed = ReadEntitySetNames(result.StructuredContent);
            listed.Should().Contain(["Customers", "Orders", "Products", "OrderItems"]);
            result.Text.Should().Be($"Declared entity sets: {listed.Count}.");
            ReadCustomerKeys(result.StructuredContent).Should().Contain("CustomerId");
            if (service.IsSuccessStatusCode)
            {
                serviceBody.Should().Contain("Customers");
                listed.Count.Should().Be(ReadServiceDocumentSets(serviceBody).Count);
            }
        }

        /// <summary>
        /// Every listed set name can be described.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ThenDescribeType_EachName_SucceedsOrIsExcluded()
        {
            var listed = await InvokeAsync("odata_list_entity_sets");
            listed.IsError.Should().BeFalse(listed.Text);
            var names = ReadEntitySetNames(listed.StructuredContent);
            names.Should().NotBeEmpty();

            foreach (var name in names)
            {
                var described = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", name));
                described.IsError.Should().BeFalse(described.Text);
                described.StructuredContent.Should().Match(text => text.Contains(name, StringComparison.Ordinal) || text.Contains("keys", StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Querying the first listed set succeeds on this read surface.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ThenQueryFirstSet_SucceedsOnReadSurfaces()
        {
            var listed = await InvokeAsync("odata_list_entity_sets");
            listed.IsError.Should().BeFalse(listed.Text);
            var names = ReadEntitySetNames(listed.StructuredContent);
            names.Should().NotBeEmpty();
            var first = names[0];

            var query = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", first));
            query.IsError.Should().BeFalse(query.Text);
            query.StructuredContent.Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// Get-style <c>entitySet</c> and <c>key</c> arguments are ignored; the full list is returned.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_UsingGetArgs_EntitySetAndKey_Ignored()
        {
            var result = await InvokeAsync("odata_list_entity_sets", ToolArguments.Of("entitySet", "Customers", "key", "1"));

            result.IsError.Should().BeFalse(result.Text);
            var names = ReadEntitySetNames(result.StructuredContent);
            names.Should().Contain("Customers");
            names.Count.Should().BeGreaterThan(1);
        }

        /// <summary>
        /// Query arguments do not cause an OData GET.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_UsingQueryArgs_FilterTop_DoesNotQueryOData()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_list_entity_sets",
                ToolArguments.Of("filter", "true", "top", 1),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            ReadEntitySetNames(result.StructuredContent).Should().Contain("Customers");
            capture.Requests.Should().BeEmpty();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Returns whether the HTTP response is a successful <c>odata_list_entity_sets</c> tool result.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <param name="body">The response body.</param>
        /// <returns>
        /// <c>true</c> when the tool succeeded.
        /// </returns>
        internal static bool IsSuccessfulListToolResult(HttpResponseMessage response, string body)
        {
            ArgumentNullException.ThrowIfNull(response);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            return JsonRpcIndicatesToolSuccess(body)
                && body.Contains("Declared entity sets", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns whether a JSON-RPC payload is a successful tool result.
        /// </summary>
        /// <param name="body">The HTTP body.</param>
        /// <returns>
        /// <c>true</c> when <c>result.isError</c> is not true and no protocol error is present.
        /// </returns>
        internal static bool JsonRpcIndicatesToolSuccess(string body)
        {
            var payload = ReadJsonRpcPayload(body);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (root.TryGetProperty("error", out _))
                {
                    return false;
                }

                if (!root.TryGetProperty("result", out var result))
                {
                    return body.Contains("Declared entity sets", StringComparison.Ordinal)
                        && !body.Contains("\"isError\":true", StringComparison.Ordinal);
                }

                if (result.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True)
                {
                    return false;
                }

                return true;
            }
            catch (JsonException)
            {
                return body.Contains("Declared entity sets", StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Reads Customer keys from a list payload.
        /// </summary>
        /// <param name="json">The structured content.</param>
        /// <returns>
        /// Key names.
        /// </returns>
        internal static IReadOnlyList<string> ReadCustomerKeys(string? json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);
            foreach (var set in document.RootElement.GetProperty("entitySets").EnumerateArray())
            {
                if (!set.TryGetProperty("name", out var name) || name.GetString() != "Customers")
                {
                    continue;
                }

                if (!set.TryGetProperty("keys", out var keys) || keys.ValueKind != JsonValueKind.Array)
                {
                    return [];
                }

                return [.. keys.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item))];
            }

            return [];
        }

        /// <summary>
        /// Reads entity-set names from list structured content.
        /// </summary>
        /// <param name="json">The structured content.</param>
        /// <returns>
        /// Set names in payload order.
        /// </returns>
        internal static IReadOnlyList<string> ReadEntitySetNames(string? json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("entitySets", out var sets) || sets.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return [.. sets.EnumerateArray()
                .Select(item => item.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)];
        }

        /// <summary>
        /// Extracts a JSON-RPC object from a raw JSON or SSE body.
        /// </summary>
        /// <param name="body">The HTTP body.</param>
        /// <returns>
        /// JSON text.
        /// </returns>
        internal static string ReadJsonRpcPayload(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            var trimmed = body.TrimStart();
            if (trimmed.StartsWith('{'))
            {
                return body;
            }

            foreach (var line in body.Split('\n'))
            {
                var data = line.Trim();
                if (!data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var json = data["data:".Length..].Trim();
                if (json.StartsWith('{'))
                {
                    return json;
                }
            }

            return body;
        }

        /// <summary>
        /// Reads entity-set names from an OData service document.
        /// </summary>
        /// <param name="json">The service document JSON.</param>
        /// <returns>
        /// Entity-set names.
        /// </returns>
        internal static IReadOnlyList<string> ReadServiceDocumentSets(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var names = new List<string>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.TryGetProperty("kind", out var kind)
                    && kind.ValueKind == JsonValueKind.String
                    && !string.Equals(kind.GetString(), "EntitySet", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    var text = name.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        names.Add(text);
                    }
                }
            }

            return names;
        }

        #endregion

    }

    /// <summary>
    /// Lists entity sets when Orders is excluded from a simple model.
    /// </summary>
    [TestClass]
    public class ListEntitySetsExcludeHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Excluded Orders is omitted from the list and from named tools.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ExcludeEntitySets_OmitsPeopleFromList()
        {
            var result = await InvokeAsync("odata_list_entity_sets");
            result.IsError.Should().BeFalse(result.Text);
            var names = OdataListEntitySetsHostTests.ReadEntitySetNames(result.StructuredContent);
            names.Should().Contain("Customers");
            names.Should().NotContain("Orders");
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_list_entity_sets");
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
            Session().Catalog.Tools.Select(tool => tool.Name).Should().NotContain("list_orders");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.ExcludeEntitySets.Add("Orders");
            });
        }

        #endregion

    }

    /// <summary>
    /// Lists entity sets on an operations-only model.
    /// </summary>
    [TestClass]
    public class ListEntitySetsOperationsOnlyHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Operations-only catalogs return an empty entity-set array.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_OperationsOnlyModel_ReturnsEmptyArray()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Be("Declared entity sets: 0.");
            OdataListEntitySetsHostTests.ReadEntitySetNames(result.StructuredContent).Should().BeEmpty();
            capture.Requests.Should().BeEmpty();
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetOperationsOnlyModel());
                });
            services.AddODataMcp();
        }

        #endregion

    }

    /// <summary>
    /// MCP transport and OData set rate limits around <c>odata_list_entity_sets</c>.
    /// </summary>
    [TestClass]
    public class ListEntitySetsRateLimitHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A second MCP POST is HTTP 429; the tool result is not produced.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_McpTransportRateLimit_SecondPost429()
        {
            using var client = TestServer.CreateClient();
            using var firstContent = McpJsonRpc.Content(
                """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"odata_list_entity_sets","arguments":{}}}""");
            using var first = await client.PostAsync("/odata/mcp", firstContent);
            using var secondContent = McpJsonRpc.Content(
                """{"jsonrpc":"2.0","id":"2","method":"tools/call","params":{"name":"odata_list_entity_sets","arguments":{}}}""");
            using var second = await client.PostAsync("/odata/mcp", secondContent);

            first.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        /// <summary>
        /// Spending the Customers OData budget does not affect this catalog tool.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ODataSetRateLimit_DoesNotAffectThisTool()
        {
            using var client = TestServer.CreateClient();
            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            var result = await InvokeAsync("odata_list_entity_sets");
            result.IsError.Should().BeFalse(result.Text);
            OdataListEntitySetsHostTests.ReadEntitySetNames(result.StructuredContent).Should().Contain("Customers");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureApp(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseRateLimiter();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
            app.UseODataMcp();
        }

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services.AddRateLimiter(options => ODataPartitionedLimiter.Apply(options, new RateLimitBudget
            {
                Customers = 1,
                Function = 1,
                Mcp = 1,
                Products = 2
            }));
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetRateLimitModel());
                });
            services.AddODataMcp();
        }

        #endregion

    }

    /// <summary>
    /// Lists entity sets when the response size guard is tiny.
    /// </summary>
    [TestClass]
    public class ListEntitySetsTinyResponseHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A tiny <c>MaxResponseBytes</c> rejects the list and suggests select and top.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_MaxResponseBytesTiny_IsErrorSuggestingSelectTop()
        {
            var result = await InvokeAsync("odata_list_entity_sets");

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("select");
            result.Text.Should().Contain("top");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxResponseBytes = 10;
            });
        }

        #endregion

    }

    /// <summary>
    /// Isolated catalogs on two OData prefixes.
    /// </summary>
    [TestClass]
    public class ListEntitySetsTwoPrefixHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Each prefix lists only its own exclusive sets.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_TwoPrefixes_IsolatedCatalogs()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            var odata = await factory.Sessions["odata"].Runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            var shop = await factory.Sessions["shop"].Runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            odata.IsError.Should().BeFalse(odata.Text);
            shop.IsError.Should().BeFalse(shop.Text);
            var odataNames = OdataListEntitySetsHostTests.ReadEntitySetNames(odata.StructuredContent);
            var shopNames = OdataListEntitySetsHostTests.ReadEntitySetNames(shop.StructuredContent);
            odataNames.Should().Contain("Customers");
            odataNames.Should().NotContain("Products");
            shopNames.Should().Contain("Products");
            shopNames.Should().NotContain("Customers");
            shopNames.Should().NotContain("Orders");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetMinimalModel());
                    options.AddRouteComponents("shop", TestModels.GetNoAuthModel());
                });
            services.AddODataMcp();
        }

        #endregion

    }

    /// <summary>
    /// Lists all 200 sets of a wide model even when named tools and resources are capped.
    /// </summary>
    [TestClass]
    public class ListEntitySetsWideModelHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// The list returns every declared set, not only named-tool sets.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_WideModel200_ReturnsAllDeclaredSetsNotJustNamed()
        {
            var result = await InvokeAsync("odata_list_entity_sets");
            result.IsError.Should().BeFalse(result.Text);
            var names = OdataListEntitySetsHostTests.ReadEntitySetNames(result.StructuredContent);
            names.Should().HaveCount(200);
            names.Should().Contain("Rows000");
            names.Should().Contain("Rows199");
            result.Text.Should().Be("Declared entity sets: 200.");
            Session().Catalog.Tools.Count.Should().BeLessThanOrEqualTo(150);
            Session().Catalog.Resources.Count.Should().BeLessThanOrEqualTo(50);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.AddRouteComponents("odata", TestModels.GetWideModel(200));
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxNamedTools = 150;
                options.Catalog.MaxResources = 50;
            });
        }

        #endregion

    }

}
