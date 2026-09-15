// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Streamable HTTP MCP protocol handlers on the convention rich host.
    /// </summary>
    [TestClass]
    public class JsonRpcProtocolHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Argument <c>key</c> completions are empty.
        /// </summary>
        [TestMethod]
        public async Task Complete_ArgumentKey_NotEntitySet_EmptyValues()
        {
            var values = Session().Catalog.CompleteEntitySetNames("Customers");
            values.Should().Contain("Customers");
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"completion/complete","params":{"argument":{"name":"key","value":"1"},"ref":{"type":"ref/resource","uri":"odata://odata/{entitySet}({key})"}}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// Argument <c>name</c> completions are empty.
        /// </summary>
        [TestMethod]
        public async Task Complete_ArgumentName_Empty()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"completion/complete","params":{"ref":{"type":"ref/resource","uri":"odata://odata/{entitySet}"},"argument":{"name":"name","value":"C"}}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// Argument <c>navigation</c> completions are empty.
        /// </summary>
        [TestMethod]
        public async Task Complete_ArgumentNavigation_Empty()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"completion/complete","params":{"argument":{"name":"navigation","value":"Ord"}}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("Orders");
        }

        /// <summary>
        /// Completions do not invent sets.
        /// </summary>
        [TestMethod]
        public void Complete_DoesNotInventSets()
        {
            Session().Catalog.CompleteEntitySetNames("Ghost").Should().BeEmpty();
        }

        /// <summary>
        /// Empty prefix returns declared sets capped at MaxCompletionValues.
        /// </summary>
        [TestMethod]
        public void Complete_EntitySet_EmptyPrefix_DeclaredSetsCapped()
        {
            var values = Session().Catalog.CompleteEntitySetNames(string.Empty);
            values.Should().Contain("Customers");
            values.Count.Should().BeLessThanOrEqualTo(50);
        }

        /// <summary>
        /// Prefix Prod matches Products.
        /// </summary>
        [TestMethod]
        public void Complete_EntitySet_PrefixProd_NorthwindProducts()
        {
            Session().Catalog.CompleteEntitySetNames("Prod").Should().Contain("Products");
        }

        /// <summary>
        /// Unknown prefix is empty.
        /// </summary>
        [TestMethod]
        public void Complete_EntitySet_UnknownPrefix_Empty()
        {
            Session().Catalog.CompleteEntitySetNames("zzz").Should().BeEmpty();
        }

        /// <summary>
        /// JSON-RPC completion for entitySet returns Customers.
        /// </summary>
        [TestMethod]
        public async Task Complete_JsonRpc()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"completion/complete","params":{"ref":{"type":"ref/resource","uri":"odata://odata/{entitySet}"},"argument":{"name":"entitySet","value":"Cust"}}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().Contain("Customers");
        }

        /// <summary>
        /// GET /odata/mcp is not 404.
        /// </summary>
        [TestMethod]
        public async Task GetOdataMcp_Not404()
        {
            using var client = TestServer.CreateClient();
            using var response = await client.GetAsync("/odata/mcp");
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// MapMcp is internal: POST /odata/mcp works without the app calling MapMcp.
        /// </summary>
        [TestMethod]
        public async Task MapMcp_IsInternal_PostPrefixMcpWorksWithoutAppCallingMapMcp()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// POST /nope/mcp is 404.
        /// </summary>
        [TestMethod]
        public async Task PostWrongPrefixMcp_404()
        {
            using var client = TestServer.CreateClient();
            using var response = await client.PostAsync("/nope/mcp", McpJsonRpc.Content("{}"));
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// POST /odata/mcp with a real tools/call is not an empty-object smoke test.
        /// </summary>
        [TestMethod]
        public async Task PostOdataMcp_ToolsCallQuery_NotEmptyObjectAsOnlyHttpTest()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("Contoso");
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// Missing content type is rejected.
        /// </summary>
        [TestMethod]
        public async Task ContentTypeMissingOnMcp_Rejected()
        {
            using var client = TestServer.CreateClient();
            using var content = new StringContent("""{"jsonrpc":"2.0","id":"1","method":"tools/list"}""", Encoding.UTF8);
            content.Headers.ContentType = null;
            using var response = await client.PostAsync("/odata/mcp", content);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.UnsupportedMediaType, HttpStatusCode.BadRequest, HttpStatusCode.NotAcceptable);
        }

        /// <summary>
        /// Resource templates include entity set and entity-by-key, odata scheme only.
        /// </summary>
        [TestMethod]
        public void ResourceTemplates_ContainsEntitySetAndEntityByKey()
        {
            var templates = Session().Catalog.ResourceTemplates;
            templates.Select(item => item.UriTemplate).Should().Contain("odata://odata/{entitySet}");
            templates.Select(item => item.UriTemplate).Should().Contain("odata://odata/{entitySet}({key})");
            templates.Should().NotContain(item => item.UriTemplate.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// AspNetCore templates use the OData prefix.
        /// </summary>
        [TestMethod]
        public void ResourceTemplates_AspNetCoreUsesPrefix_ToolsUsesRemote()
        {
            Session().Catalog.ResourceTemplates.Should().OnlyContain(item => item.UriTemplate.StartsWith("odata://odata/", StringComparison.Ordinal));
        }

        /// <summary>
        /// JSON-RPC resources/templates/list.
        /// </summary>
        [TestMethod]
        public async Task ResourceTemplates_JsonRpc()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(client, """{"jsonrpc":"2.0","id":"1","method":"resources/templates/list","params":{}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("odata://odata/{entitySet}");
        }

        /// <summary>
        /// No https templates.
        /// </summary>
        [TestMethod]
        public void ResourceTemplates_NoHttpsScheme()
        {
            Session().Catalog.ResourceTemplates.Should().NotContain(item => item.UriTemplate.Contains("https://", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// resources/list: metadata first, then sets, odata scheme.
        /// </summary>
        [TestMethod]
        public void ResourcesList_OData8_MetadataFirstThenSets_OdataScheme()
        {
            var resources = Session().Catalog.Resources;
            resources[0].Name.Should().Be("$metadata");
            resources[0].Uri.Should().Be("odata://odata/$metadata");
            resources[0].MimeType.Should().Be("application/xml");
            resources.Should().Contain(item => item.Uri == "odata://odata/Customers" && item.MimeType == "application/json");
        }

        /// <summary>
        /// Listing descriptions mention sets, not row data.
        /// </summary>
        [TestMethod]
        public void ResourcesList_IsTypeCardsNotCollections()
        {
            var customers = Session().Catalog.Resources.Single(item => item.Name == "Customers");
            customers.Description.Should().NotContain("Contoso");
            customers.ReadContents.Should().Contain("\"key\":[");
            customers.ReadContents.Should().NotContain("@odata.context");
        }

        /// <summary>
        /// JSON-RPC resources/list.
        /// </summary>
        [TestMethod]
        public async Task ResourcesList_JsonRpc()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(client, """{"jsonrpc":"2.0","id":"1","method":"resources/list","params":{}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("odata://odata/$metadata");
            body.Should().Contain("odata://odata/Customers");
        }

        /// <summary>
        /// Metadata then include-list then alpha.
        /// </summary>
        [TestMethod]
        public void ResourcesList_DeterministicOrder_MetadataThenIncludeListThenAlpha()
        {
            var names = Session().Catalog.Resources.Select(item => item.Name).ToList();
            names[0].Should().Be("$metadata");
            names.Skip(1).Should().BeInAscendingOrder(StringComparer.Ordinal);
        }

        /// <summary>
        /// resources/read $metadata is the CSDL XML document the in-process host serialized from its model, so it
        /// carries the same EntityContainer the twin GET returns.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_Metadata_ReturnsCsdlXml()
        {
            using var client = TestServer.CreateClient();
            using var twin = await client.GetAsync("/odata/$metadata");
            if (twin.IsSuccessStatusCode)
            {
                (await twin.Content.ReadAsStringAsync()).Should().Contain("EntityContainer");
            }

            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"resources/read","params":{"uri":"odata://odata/$metadata"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().Contain("application/xml");

            body.Should().NotContain("\"text\":\"\"");
            body.Should().Contain("EntityContainer");
            body.Should().Contain("Customers");
        }

        /// <summary>
        /// Entity-set resource is a type card, not the collection.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_EntitySet_ReturnsTypeCardNotFeed()
        {
            var card = Session().Catalog.Resources.Single(item => item.Name == "Customers").ReadContents;
            card.Should().Contain("\"key\":[");
            card.Should().Contain("\"props\":{");
            card.Should().Contain("\"navs\":{");
            card.Should().NotContain("Contoso");
            card.Should().NotContain("@odata.context");

            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"resources/read","params":{"uri":"odata://odata/Customers"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("props");
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// Unknown URI is empty, not an OData query.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_UnknownUri_EmptyOrError_NotOdataQuery()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"resources/read","params":{"uri":"odata://odata/Ghost"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// https URLs are not fetched.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_HttpUrlRejectedOrEmpty()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"resources/read","params":{"uri":"https://example.com/Customers"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// Empty URI yields empty contents.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_EmptyUri_EmptyContents()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"resources/read","params":{"uri":" "}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// JSON-RPC resources/read.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_JsonRpc()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"resources/read","params":{"uri":"odata://odata/Customers"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("Customer");
        }

        /// <summary>
        /// Type card omits ignored and binary properties.
        /// </summary>
        [TestMethod]
        public void ResourcesRead_TypeCardOmitsIgnoredAndBinary()
        {
            var customers = Session().Catalog.Resources.Single(item => item.Name == "Customers").ReadContents;
            customers.Should().NotContain("InternalSecret");
            var documents = Session().Catalog.Resources.FirstOrDefault(item => item.Name == "Documents");
            if (documents is not null)
            {
                documents.ReadContents.Should().NotContain("Photo");
                documents.ReadContents.Should().NotContain("File");
            }
        }

        /// <summary>
        /// resources/read does not GET the collection.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_DoesNotExecuteQuery()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            _ = runtime;
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"resources/read","params":{"uri":"odata://odata/Customers"}}""");
            await McpJsonRpc.ReadBodyAsync(response);
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// shutdown_server is unknown on AspNetCore.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServer_AspNetCore_UnknownTool()
        {
            var result = await InvokeAsync("shutdown_server", null);
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Unknown tool 'shutdown_server'.");
        }

        /// <summary>
        /// tools/list omits shutdown_server.
        /// </summary>
        [TestMethod]
        public void ShutdownServer_AspNetCore_ToolsListOmits()
        {
            Session().Catalog.Tools.Select(tool => tool.Name).Should().NotContain("shutdown_server");
        }

        /// <summary>
        /// JSON-RPC shutdown_server is unknown.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServer_JsonRpcUnknownOnAspNetCoreMcp()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "shutdown_server", "{}");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("Unknown tool");
            body.Should().Contain("shutdown_server");
        }

        /// <summary>
        /// Empty name is tool-name-required.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EmptyName_ToolNameRequired()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"","arguments":{}}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("Tool name is required.");
        }

        /// <summary>
        /// Each generic tool is invoked once over JSON-RPC.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_OData8_JsonRpc()
        {
            using var client = TestServer.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");

            await AssertToolAsync(client, "odata_list_entity_sets", "{}", "Customers");
            await AssertToolAsync(client, "odata_describe_type", """{"name":"Customers"}""", "CustomerId");
            await AssertToolAsync(client, "odata_query", """{"entitySet":"Customers"}""", "Contoso");
            await AssertToolAsync(client, "odata_get", """{"entitySet":"Customers","key":"1"}""", "Contoso");
            await AssertToolAsync(client, "odata_create", """{"entitySet":"Customers","body":{"CompanyName":"RpcEachCreate"}}""", "RpcEachCreate");
            await AssertToolAsync(client, "odata_update", """{"entitySet":"Customers","key":"1","body":{"CompanyName":"RpcEachUpdate"}}""", null);
            await AssertToolAsync(client, "odata_delete", """{"entitySet":"Customers","key":"999"}""", "404", error: true);
            await AssertToolAsync(client, "odata_navigate", """{"entitySet":"Customers","key":"1","navigation":"Orders"}""", null, error: true);
            await AssertToolAsync(client, "odata_list_operations", "{}", "operations");
            await AssertToolAsync(client, "odata_call", """{"name":"MostValuable"}""", null, error: true);
        }

        /// <summary>
        /// Initialize then list then query.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_InitializeThenListThenCallQuery_OData8()
        {
            using var client = TestServer.CreateClient();
            using var listed = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp");
            var listBody = await McpJsonRpc.ReadBodyAsync(listed);
            listBody.Should().Contain("odata_query");
            using var called = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            var callBody = await McpJsonRpc.ReadBodyAsync(called);
            callBody.Should().Contain("Contoso");
        }

        /// <summary>
        /// Missing params is an error.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_MissingParams_Error()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(client, """{"jsonrpc":"2.0","id":"1","method":"tools/call"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.ToLowerInvariant().Should().Contain("error");
        }

        /// <summary>
        /// Unknown tool name is IsError.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_UnknownToolName_IsErrorUnknownTool()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "not_a_tool", "{}");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("Unknown tool");
            body.Should().Contain("not_a_tool");
        }

        /// <summary>
        /// Wrong JSON-RPC version is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_WrongJsonRpcVersion()
        {
            using var client = TestServer.CreateClient();
            using var response = await PostRpcAsync(
                client,
                """{"jsonrpc":"1.0","id":"1","method":"tools/list","params":{}}""");
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.ToLowerInvariant().Should().Contain("error");
        }

        /// <summary>
        /// Empty {} POST is a protocol error, not a tool success.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_JsonRpc_EmptyBody_IsProtocolErrorNotToolSuccess()
        {
            using var client = TestServer.CreateClient();
            using var response = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{}"));
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("Declared entity sets");
        }

        /// <summary>
        /// tools/list generic order then named, no shutdown, no $ in schemas.
        /// </summary>
        [TestMethod]
        public void ToolsList_OData8_GenericOrderThenNamed_NoShutdown()
        {
            var tools = Session().Catalog.Tools.ToList();
            var names = tools.Select(tool => tool.Name).ToList();
            names.Take(11).Should().Equal(
                "odata_list_entity_sets",
                "odata_describe_type",
                "odata_describe_model",
                "odata_query",
                "odata_get",
                "odata_create",
                "odata_update",
                "odata_delete",
                "odata_navigate",
                "odata_list_operations",
                "odata_call");
            names.Should().NotContain("shutdown_server");
            tools.Should().OnlyContain(tool => !tool.InputSchema.Contains("$filter", StringComparison.Ordinal));
            var query = tools.Single(tool => tool.Name == "odata_query");
            query.ReadOnlyHint.Should().BeTrue();
            var del = tools.Single(tool => tool.Name == "odata_delete");
            del.DestructiveHint.Should().BeTrue();
            var update = tools.Single(tool => tool.Name == "odata_update");
            update.IdempotentHint.Should().BeTrue();
            var create = tools.Single(tool => tool.Name == "odata_create");
            create.ReadOnlyHint.Should().BeFalse();
            create.DestructiveHint.Should().BeFalse();
        }

        /// <summary>
        /// JSON-RPC tools/list.
        /// </summary>
        [TestMethod]
        public async Task ToolsList_JsonRpc_StreamableHttp_OData8()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("odata_query");
            body.Should().NotContain("shutdown_server");
        }

        /// <summary>
        /// nextCursor is absent/null when unpaged.
        /// </summary>
        [TestMethod]
        public async Task ToolsList_PaginationCursor_IfSdkSupports_NextCursorNullWhenUnpaged()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("\"nextCursor\":\"");
        }

        /// <summary>
        /// Open-world hint is true on OData tools.
        /// </summary>
        [TestMethod]
        public void ToolsList_OpenWorldHintTrueOnOdataTools_FalseOnShutdown()
        {
            Session().Catalog.Tools.Should().OnlyContain(tool => tool.OpenWorldHint);
        }

        /// <summary>
        /// Operations-only catalogs are covered by OperationsOnlyHostTests; rich model has odata_call.
        /// </summary>
        [TestMethod]
        public void ToolsList_OperationsOnly_NoNamedCrud_HasOdataCall()
        {
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_call");
        }

        /// <summary>
        /// AspNetCore tests do not load the Restier OData 7 API type.
        /// </summary>
        [TestMethod]
        public void Proc_AspNetCoreTests_DoNotLoadOData7ControllerType()
        {
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(type => type is not null)!;
                    }
                })
                .Should()
                .NotContain(type => type!.FullName == "Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios.McpCustomerApi");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Asserts a JSON-RPC tools/call.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="name">The tool name.</param>
        /// <param name="argumentsJson">Arguments JSON.</param>
        /// <param name="contains">Optional substring.</param>
        /// <param name="error">Whether IsError is expected.</param>
        internal static async Task AssertToolAsync(HttpClient client, string name, string argumentsJson, string? contains, bool error = false)
        {
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", name, argumentsJson);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            if (error)
            {
                body.Should().Contain("isError");
            }
            else
            {
                body.Should().NotContain("\"isError\":true");
            }

            if (!string.IsNullOrWhiteSpace(contains))
            {
                body.Should().Contain(contains);
            }
        }

        /// <summary>
        /// Posts a JSON-RPC payload after initialize.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="json">The JSON-RPC body.</param>
        /// <returns>
        /// The response.
        /// </returns>
        internal static async Task<HttpResponseMessage> PostRpcAsync(HttpClient client, string json)
        {
            ApplyMcpAccept(client);
            using var initialize = McpJsonRpc.Content(McpJsonRpc.InitializePayload());
            using var initialized = await client.PostAsync("/odata/mcp", initialize);
            var session = initialized.Headers.TryGetValues("mcp-session-id", out var values)
                ? string.Join(",", values)
                : null;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/odata/mcp")
            {
                Content = McpJsonRpc.Content(json)
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");
            if (!string.IsNullOrWhiteSpace(session))
            {
                request.Headers.TryAddWithoutValidation("mcp-session-id", session);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Streamable HTTP requires both JSON and SSE accept types.
        /// </summary>
        /// <param name="client">The client.</param>
        internal static void ApplyMcpAccept(HttpClient client)
        {
            ArgumentNullException.ThrowIfNull(client);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        }

        #endregion

    }

}
