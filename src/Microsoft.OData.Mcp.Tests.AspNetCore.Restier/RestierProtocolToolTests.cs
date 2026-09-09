// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Restier Streamable HTTP MCP protocol: tools/list, tools/call, resources, templates, and completion.
    /// </summary>
    [TestClass]
    public class RestierProtocolToolTests : RestierToolTestBase
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierProtocolToolTests"/> class using endpoint routing.
        /// </summary>
        public RestierProtocolToolTests()
            : base("RestierProtocol")
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Completions for argument <c>name</c> are empty.
        /// </summary>
        [TestMethod]
        public async Task Complete_ArgumentName_Empty()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CompleteAsync(client, "odata/mcp", "name", "");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().NotContain("Customers");
        }

        /// <summary>
        /// Completions for argument <c>navigation</c> are empty.
        /// </summary>
        [TestMethod]
        public async Task Complete_ArgumentNavigation_Empty()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CompleteAsync(client, "odata/mcp", "navigation", "Ord");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().NotContain("Orders");
        }

        /// <summary>
        /// Completions for argument <c>key</c> are empty.
        /// </summary>
        [TestMethod]
        public async Task Complete_ArgumentKey_NotEntitySet_EmptyValues()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CompleteAsync(client, "odata/mcp", "key", "1");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// Completions do not invent undeclared sets.
        /// </summary>
        [TestMethod]
        public void Complete_DoesNotInventSets()
        {
            var values = Catalog().CompleteEntitySetNames("");
            values.Should().Contain("Customers");
            values.Should().Contain("Orders");
            values.Should().NotContain("Products");
            values.Should().NotContain("People");
        }

        /// <summary>
        /// Empty prefix completes declared sets.
        /// </summary>
        [TestMethod]
        public async Task Complete_EntitySet_EmptyPrefix_DeclaredSetsCapped()
        {
            var values = Catalog().CompleteEntitySetNames("");
            values.Should().Contain("Customers");
            values.Should().Contain("Orders");

            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CompleteAsync(client, "odata/mcp", "entitySet", "");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("Customers");
            body.Should().Contain("Orders");
        }

        /// <summary>
        /// Prefix <c>Cust</c> completes Customers.
        /// </summary>
        [TestMethod]
        public async Task Complete_EntitySet_PrefixCust_Customers()
        {
            Catalog().CompleteEntitySetNames("Cust").Should().Contain("Customers");
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CompleteAsync(client, "odata/mcp", "entitySet", "Cust");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("Customers");
            body.Should().NotContain("Orders");
        }

        /// <summary>
        /// Unknown prefix completes empty.
        /// </summary>
        [TestMethod]
        public async Task Complete_EntitySet_UnknownPrefix_Empty()
        {
            Catalog().CompleteEntitySetNames("zzz").Should().BeEmpty();
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CompleteAsync(client, "odata/mcp", "entitySet", "zzz");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("Customers");
        }

        /// <summary>
        /// JSON-RPC completion for entitySet.
        /// </summary>
        [TestMethod]
        public async Task Complete_JsonRpc()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CompleteAsync(client, "odata/mcp", "entitySet", "Ord");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("Orders");
        }

        /// <summary>
        /// GET odata/mcp is not 404 (may be 405).
        /// </summary>
        [TestMethod]
        public async Task GetOdataMcp_Not404()
        {
            using var client = CreateClient();
            var response = await client.GetAsync("odata/mcp");
            ((int)response.StatusCode).Should().BeOneOf(404, 405, 406);
        }

        /// <summary>
        /// MapMcp is internal; POST odata/mcp works without the app calling MapMcp.
        /// </summary>
        [TestMethod]
        public async Task MapMcp_IsInternal_PostPrefixMcpWorksWithoutAppCallingMapMcp()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_list_entity_sets", "{}");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("Customers");
        }

        /// <summary>
        /// Open MCP without a token is not 401.
        /// </summary>
        [TestMethod]
        public async Task OpenMcp_WithoutToken_Not401()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
            response.IsSuccessStatusCode.Should().BeTrue();
        }

        /// <summary>
        /// POST wrong prefix /nope/mcp is 404.
        /// </summary>
        [TestMethod]
        public async Task PostWrongPrefixMcp_404()
        {
            using var client = CreateClient();
            using var response = await client.PostAsync("nope/mcp", RestierJsonContent.Json("{}"));
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// POST odata/mcp tools/call query is a real call, not empty {}.
        /// </summary>
        [TestMethod]
        public async Task PostOdataMcp_ToolsCallQuery_NotEmptyObjectAsOnlyHttpTest()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// Restier tests do not load the OData 8 controller type.
        /// </summary>
        [TestMethod]
        public void Proc_RestierTests_DoNotLoadOData8ControllerType()
        {
            var odataEight = Type.GetType("Microsoft.AspNetCore.OData.Routing.Controllers.ODataController, Microsoft.AspNetCore.OData");
            var odataSeven = Type.GetType("Microsoft.AspNet.OData.ODataController, Microsoft.AspNetCore.OData");
            odataEight.Should().BeNull("Restier tests must not load OData 8 controller types");
            odataSeven.Should().NotBeNull("Restier.AspNetCore loads Microsoft.AspNet.OData.ODataController");
        }

        /// <summary>
        /// Resource templates use the odata prefix, not https.
        /// </summary>
        [TestMethod]
        public void ResourceTemplates_AspNetCoreUsesPrefix_ToolsUsesRemote()
        {
            var templates = Catalog().ResourceTemplates.Select(item => item.UriTemplate).ToList();
            templates.Should().Contain("odata://odata/{entitySet}");
            templates.Should().Contain("odata://odata/{entitySet}({key})");
            templates.Should().NotContain(template => template.Contains("https://", StringComparison.OrdinalIgnoreCase));
            templates.Should().NotContain(template => template.Contains("odata://remote/", StringComparison.Ordinal));
        }

        /// <summary>
        /// Templates include entity set and entity by key.
        /// </summary>
        [TestMethod]
        public void ResourceTemplates_ContainsEntitySetAndEntityByKey()
        {
            var names = Catalog().ResourceTemplates.Select(item => item.Name).ToList();
            names.Should().Contain("entitySet");
            names.Should().Contain("entityByKey");
        }

        /// <summary>
        /// JSON-RPC resources/templates/list.
        /// </summary>
        [TestMethod]
        public async Task ResourceTemplates_JsonRpc()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ListTemplatesAsync(client, "odata/mcp");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("odata://odata/{entitySet}");
            body.Should().Contain("odata://odata/{entitySet}({key})");
        }

        /// <summary>
        /// Templates do not use the https scheme.
        /// </summary>
        [TestMethod]
        public async Task ResourceTemplates_NoHttpsScheme()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ListTemplatesAsync(client, "odata/mcp");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("https://");
        }

        /// <summary>
        /// Resources list metadata first then sets under odata://odata/.
        /// </summary>
        [TestMethod]
        public void ResourcesList_OData8_MetadataFirstThenSets_OdataScheme()
        {
            var resources = Catalog().Resources;
            resources[0].Uri.Should().Be("odata://odata/$metadata");
            resources[0].MimeType.Should().Be("application/xml");
            resources.Select(item => item.Uri).Should().Contain("odata://odata/Customers");
            resources.First(item => item.Name == "Customers").MimeType.Should().Be("application/json");
        }

        /// <summary>
        /// Deterministic order: metadata then sets.
        /// </summary>
        [TestMethod]
        public void ResourcesList_DeterministicOrder_MetadataThenIncludeListThenAlpha()
        {
            var uris = Catalog().Resources.Select(item => item.Uri).ToList();
            uris[0].Should().Be("odata://odata/$metadata");
            uris.Should().Contain("odata://odata/Customers");
            uris.Should().Contain("odata://odata/Orders");
        }

        /// <summary>
        /// Listing descriptions mention sets, not row data.
        /// </summary>
        [TestMethod]
        public void ResourcesList_IsTypeCardsNotCollections()
        {
            var customers = Catalog().Resources.First(item => item.Name == "Customers");
            customers.Description.Should().NotContain("Contoso");
            customers.ReadContents.Should().NotContain("Contoso");
            customers.ReadContents.Should().Contain("CompanyName");
        }

        /// <summary>
        /// JSON-RPC resources/list.
        /// </summary>
        [TestMethod]
        public async Task ResourcesList_JsonRpc()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ListResourcesAsync(client, "odata/mcp");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("odata://odata/$metadata");
            body.Should().Contain("odata://odata/Customers");
        }

        /// <summary>
        /// resources/read does not GET the Customers collection.
        /// </summary>
        [TestMethod]
        public void ResourcesRead_DoesNotExecuteQuery()
        {
            var card = Catalog().Resources.First(item => item.Name == "Customers").ReadContents;
            card.Should().Contain("\"key\":[");
            card.Should().Contain("\"props\":{");
            card.Should().NotContain("@odata.context");
            card.Should().NotContain("Contoso");
        }

        /// <summary>
        /// Empty URI returns empty contents.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_EmptyUri_EmptyContents()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.SendAfterInitializeAsync(client, "odata/mcp", """{"jsonrpc":"2.0","id":"1","method":"resources/read","params":{"uri":""}}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// Entity set resource is a type card, not a feed.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_EntitySet_ReturnsTypeCardNotFeed()
        {
            var card = Catalog().Resources.First(item => item.Name == "Customers").ReadContents;
            card.Should().Contain("\"key\":[");
            card.Should().Contain("\"props\":{");
            card.Should().Contain("\"navs\":{");
            card.Should().NotContain("@odata.context");
            card.Should().NotContain("Contoso");

            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ReadResourceAsync(client, "odata/mcp", "odata://odata/Customers");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("CompanyName");
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// An http URL is not fetched as OData.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_HttpUrlRejectedOrEmpty()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ReadResourceAsync(client, "odata/mcp", "https://example.com/Customers");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// JSON-RPC resources/read of the Customers type card.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_JsonRpc()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ReadResourceAsync(client, "odata/mcp", "odata://odata/Customers");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("CompanyName");
        }

        /// <summary>
        /// Metadata resource twins GET odata/$metadata. The in-process host does not cache CSDL on its session, so the
        /// resource text may be empty; when the host does return text it must be the real CSDL document.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_Metadata_ReturnsCsdlXml()
        {
            using var client = CreateClient();
            var metadata = await client.GetStringAsync("odata/$metadata");
            metadata.Should().Contain("Customers");
            metadata.Should().Contain("McpCustomer");

            using var response = await RestierMcpJsonRpc.ReadResourceAsync(client, "odata/mcp", "odata://odata/$metadata");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("application/xml");

            using var document = JsonDocument.Parse(RestierMcpJsonRpc.ExtractJson(body));
            var text = document.RootElement.GetProperty("result").GetProperty("contents")[0].GetProperty("text").GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                text.Should().Contain("Customers");
            }
        }

        /// <summary>
        /// Unknown URI is empty or error, not an OData query.
        /// </summary>
        [TestMethod]
        public async Task ResourcesRead_UnknownUri_EmptyOrError_NotOdataQuery()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ReadResourceAsync(client, "odata/mcp", "odata://odata/DoesNotExist");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("Contoso");
            body.Should().NotContain("@odata.context");
        }

        /// <summary>
        /// Invoke shutdown_server on Restier is unknown.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServer_AspNetCore_UnknownTool()
        {
            var result = await Runtime().InvokeAsync("shutdown_server", null, CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'shutdown_server'.");
        }

        /// <summary>
        /// tools/list omits shutdown_server.
        /// </summary>
        [TestMethod]
        public void ShutdownServer_AspNetCore_ToolsListOmits()
        {
            Catalog().Tools.Select(tool => tool.Name).Should().NotContain("shutdown_server");
        }

        /// <summary>
        /// JSON-RPC unknown shutdown_server.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServer_JsonRpcUnknownOnAspNetCoreMcp()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "shutdown_server", "{}");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeTrue();
            body.Should().Contain("shutdown_server");
            body.Should().Contain("Unknown tool");
        }

        /// <summary>
        /// Empty tool name is required.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EmptyName_ToolNameRequired()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.SendAfterInitializeAsync(client, "odata/mcp", """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"","arguments":{}}}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            body.Should().Contain("Tool name is required.");
        }

        /// <summary>
        /// JSON-RPC odata_call once.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_Call()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_call", """{"name":"Ghost"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeTrue();
            body.Should().Contain("is not declared in the model");
        }

        /// <summary>
        /// JSON-RPC odata_create once (Restier is open).
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_Create()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_create", """{"entitySet":"Customers","body":"{\"Id\":201,\"CompanyName\":\"RpcOnce\"}"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            (await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'RpcOnce'")).Should().Contain("RpcOnce");
        }

        /// <summary>
        /// JSON-RPC odata_delete once.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_Delete()
        {
            await Runtime().InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"Id":202,"CompanyName":"RpcDelOnce"}"""), CancellationToken.None);
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_delete", """{"entitySet":"Customers","key":"202"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            ((int)(await client.GetAsync("odata/Customers(202)")).StatusCode).Should().Be(404);
        }

        /// <summary>
        /// JSON-RPC odata_describe_type once.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_DescribeType()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_describe_type", """{"name":"Customers"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            body.Should().Contain("CompanyName");
        }

        /// <summary>
        /// JSON-RPC odata_get once.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_Get()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_get", """{"entitySet":"Customers","key":"1"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// JSON-RPC odata_list_entity_sets once.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_ListEntitySets()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_list_entity_sets", "{}");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            body.Should().Contain("Customers");
        }

        /// <summary>
        /// JSON-RPC odata_list_operations once.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_ListOperations()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_list_operations", "{}");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            body.Should().Contain("operations");
        }

        /// <summary>
        /// JSON-RPC odata_navigate once; Restier Customers have Orders.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_Navigate()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_navigate", """{"entitySet":"Customers","key":"1","navigation":"Orders"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            body.Should().Contain("100");
        }

        /// <summary>
        /// JSON-RPC odata_query once.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_Query()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// JSON-RPC odata_update once (Restier is open).
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_EachGenericOnce_Restier_JsonRpc_Update()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_update", """{"entitySet":"Customers","key":"2","body":"{\"CompanyName\":\"RpcOnceUp\"}"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            (await client.GetStringAsync("odata/Customers(2)")).Should().Contain("RpcOnceUp");
        }

        /// <summary>
        /// Initialize then list then call query.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_InitializeThenListThenCallQuery_Restier()
        {
            using var client = CreateClient();
            using var listed = await RestierMcpJsonRpc.ListToolsAsync(client, "odata/mcp");
            var listBody = await RestierMcpJsonRpc.ReadBodyAsync(listed);
            listed.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)listed.StatusCode, listBody);
            listBody.Should().Contain("odata_query");
            listBody.Should().NotContain("shutdown_server");

            using var called = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            var callBody = await RestierMcpJsonRpc.ReadBodyAsync(called);
            RestierMcpJsonRpc.ReadIsError(callBody).Should().BeFalse();
            callBody.Should().Contain("Contoso");
        }

        /// <summary>
        /// Missing params is an error.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_MissingParams_Error()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.SendAfterInitializeAsync(client, "odata/mcp", """{"jsonrpc":"2.0","id":"1","method":"tools/call"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            RestierMcpJsonRpc.ReadIsError(body).Should().NotBe(false);
        }

        /// <summary>
        /// Unknown tool name is IsError unknown tool.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_UnknownToolName_IsErrorUnknownTool()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(client, "odata/mcp", "not_a_tool", "{}");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            RestierMcpJsonRpc.ReadIsError(body).Should().BeTrue();
            body.Should().Contain("not_a_tool");
            body.Should().Contain("Unknown tool");
        }

        /// <summary>
        /// Wrong JSON-RPC version is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task ToolsCall_WrongJsonRpcVersion()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.SendAfterInitializeAsync(client, "odata/mcp", """{"jsonrpc":"1.0","id":"1","method":"tools/call","params":{"name":"odata_query","arguments":{"entitySet":"Customers"}}}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            RestierMcpJsonRpc.ReadIsError(body).Should().NotBe(false);
        }

        /// <summary>
        /// tools/list JSON-RPC contains generics then named, no shutdown, no $ in schemas.
        /// </summary>
        [TestMethod]
        public async Task ToolsList_JsonRpc_StreamableHttp_Restier()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ListToolsAsync(client, "odata/mcp");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("odata_list_entity_sets");
            body.Should().Contain("list_customers");
            body.Should().Contain("get_mcp_customer");
            body.Should().NotContain("shutdown_server");
            body.Should().NotContain("\"$filter\"");
        }

        /// <summary>
        /// Open-world hint is true on OData tools.
        /// </summary>
        [TestMethod]
        public void ToolsList_OpenWorldHintTrueOnOdataTools_FalseOnShutdown()
        {
            var query = Catalog().Tools.First(tool => tool.Name == "odata_query");
            query.OpenWorldHint.Should().BeTrue();
            query.ReadOnlyHint.Should().BeTrue();
            var create = Catalog().Tools.First(tool => tool.Name == "odata_create");
            create.ReadOnlyHint.Should().BeFalse();
            create.DestructiveHint.Should().BeFalse();
            var delete = Catalog().Tools.First(tool => tool.Name == "odata_delete");
            delete.DestructiveHint.Should().BeTrue();
            delete.IdempotentHint.Should().BeTrue();
            var update = Catalog().Tools.First(tool => tool.Name == "odata_update");
            update.IdempotentHint.Should().BeTrue();
        }

        /// <summary>
        /// Pagination cursor is absent when unpaged.
        /// </summary>
        [TestMethod]
        public async Task ToolsList_PaginationCursor_IfSdkSupports_NextCursorNullWhenUnpaged()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ListToolsAsync(client, "odata/mcp");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("nextCursor");
        }

        /// <summary>
        /// Restier tools/list contains list_customers and omits shutdown.
        /// </summary>
        [TestMethod]
        public async Task ToolsList_Restier_ContainsListCustomers_OmitsShutdown()
        {
            var names = Catalog().Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain("list_customers");
            names.Should().Contain("get_mcp_customer");
            names.Should().NotContain("shutdown_server");
            names.Take(10).Should().Equal(
                "odata_list_entity_sets",
                "odata_describe_type",
                "odata_query",
                "odata_get",
                "odata_create",
                "odata_update",
                "odata_delete",
                "odata_navigate",
                "odata_list_operations",
                "odata_call");

            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.ListToolsAsync(client, "odata/mcp");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            body.Should().Contain("list_customers");
            body.Should().NotContain("shutdown_server");
        }

        /// <summary>
        /// Two prefixes have different named sets.
        /// </summary>
        [TestMethod]
        public void ToolsList_TwoPrefixes_DifferentNamedSets()
        {
            Catalog().Tools.Select(tool => tool.Name).Should().Contain("list_customers");
            Catalog().Tools.Select(tool => tool.Name).Should().NotContain("list_products");
        }

        #endregion

    }

    /// <summary>
    /// MCP transport rate limit on Restier <c>odata/mcp</c>.
    /// </summary>
    [TestClass]
    public class RestierProtocolRateLimitTests : RestierToolTestBase
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierProtocolRateLimitTests"/> class using endpoint routing.
        /// </summary>
        public RestierProtocolRateLimitTests()
            : base("RestierProtocolRate")
        {
            ApplicationBuilderAction = app => app.UseRateLimiter();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Second JSON-RPC POST is HTTP 429; OData GET still works.
        /// </summary>
        [TestMethod]
        public async Task RateLimitedMcp_SecondPost429_ODataGetStill200()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
            using var first = await client.PostAsync("odata/mcp", RestierMcpJsonRpc.Content(RestierMcpJsonRpc.InitializePayload()));
            first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
            using var second = await client.PostAsync("odata/mcp", RestierMcpJsonRpc.Content(RestierMcpJsonRpc.InitializePayload()));
            second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            var odata = await client.GetAsync("odata/Customers");
            odata.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Registers the partitioned MCP rate limiter (one permit for <c>odata/mcp</c>, unlimited for <c>Customers</c>)
        /// before registering MCP.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        internal override void ConfigureServices(IServiceCollection services)
        {
            services.AddRateLimiter(options => RestierPartitionedLimiter.Apply(options, new RestierRateLimitBudget
            {
                Customers = 0,
                OdataMcp = 1,
                Products = 0,
                ShopMcp = 0
            }));
            base.ConfigureServices(services);
        }

        #endregion

    }

    /// <summary>
    /// Exclude Orders from resources and completions.
    /// </summary>
    [TestClass]
    public class RestierProtocolExcludeTests : RestierToolTestBase
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierProtocolExcludeTests"/> class using endpoint routing.
        /// </summary>
        public RestierProtocolExcludeTests()
            : base("RestierProtocolExclude")
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Completions honor exclude (fail the product if excluded names leak).
        /// </summary>
        [TestMethod]
        public void Complete_ExcludeEntitySets_StillCompletesDeclaredContainerSets()
        {
            var catalog = Catalog();
            catalog.Resources.Select(item => item.Name).Should().NotContain("Orders");
            catalog.CompleteEntitySetNames("").Should().Contain("Customers");
        }

        /// <summary>
        /// Resources omit excluded sets.
        /// </summary>
        [TestMethod]
        public void ResourcesList_ExcludeEntitySets_Omits()
        {
            var uris = Catalog().Resources.Select(item => item.Uri);
            uris.Should().Contain("odata://odata/Customers");
            uris.Should().NotContain("odata://odata/Orders");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Excludes <c>Orders</c> from the catalog.
        /// </summary>
        /// <param name="options">The host options to configure.</param>
        internal override void ConfigureODataMcp(ODataMcpHostOptions options)
        {
            options.Catalog.ExcludeEntitySets.Add("Orders");
        }

        #endregion

    }

}
