// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Operations-only convention host for list/call tool tests.
    /// </summary>
    public abstract class OperationsOnlyToolHost : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds an operations-only MCP host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetOperationsOnlyModel());
                    });
                services.AddODataMcp();
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                    app.UseODataMcp();
                });
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates a capturing runtime over the real in-process executor.
        /// </summary>
        /// <returns>
        /// Runtime and capture wrapper.
        /// </returns>
        internal (ODataToolRuntime Runtime, CapturingODataExecutor Capture) CreateCapturingRuntime()
        {
            var session = Session();
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

            var inner = new InProcessODataExecutor(
                TestServer.Services.GetRequiredService<IHttpClientFactory>(),
                accessor,
                "odata");
            var capture = new CapturingODataExecutor(inner);

            return (new ODataToolRuntime(session.Catalog, capture), capture);
        }

        /// <summary>
        /// Creates an HTTP client for direct OData calls against the host under test.
        /// </summary>
        /// <returns>
        /// The client.
        /// </returns>
        internal HttpClient CreateClient()
        {
            return TestServer.CreateClient();
        }

        /// <summary>
        /// Invokes a catalog tool on the odata session.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal Task<ODataToolInvocationResult> InvokeAsync(string name, IEnumerable<KeyValuePair<string, JsonElement>>? arguments = null)
        {
            return Session().Runtime.InvokeAsync(name, arguments, CancellationToken.None);
        }

        /// <summary>
        /// Gets the odata MCP session.
        /// </summary>
        /// <returns>
        /// The session.
        /// </returns>
        internal ODataMcpSession Session()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"];
        }

        #endregion

    }

    /// <summary>
    /// Minimal Customers host with no operations.
    /// </summary>
    public abstract class FewTablesToolHost : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a few-tables MCP host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetMinimalModel());
                    });
                services.AddODataMcp();
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                    app.UseODataMcp();
                });
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates an HTTP client for direct OData calls against the host under test.
        /// </summary>
        /// <returns>
        /// The client.
        /// </returns>
        internal HttpClient CreateClient()
        {
            return TestServer.CreateClient();
        }

        /// <summary>
        /// Invokes a catalog tool on the odata session.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal Task<ODataToolInvocationResult> InvokeAsync(string name, IEnumerable<KeyValuePair<string, JsonElement>>? arguments = null)
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime.InvokeAsync(name, arguments, CancellationToken.None);
        }

        #endregion

    }

    /// <summary>
    /// Wide model host with two hundred entity sets and no operations.
    /// </summary>
    public abstract class WideModelToolHost : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a 200-set MCP host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddOData(options =>
                    {
                        options.AddRouteComponents("odata", TestModels.GetWideModel(200));
                    });
                services.AddODataMcp();
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                    app.UseODataMcp();
                });
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates an HTTP client for direct OData calls against the host under test.
        /// </summary>
        /// <returns>
        /// The client.
        /// </returns>
        internal HttpClient CreateClient()
        {
            return TestServer.CreateClient();
        }

        /// <summary>
        /// Invokes a catalog tool on the odata session.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal Task<ODataToolInvocationResult> InvokeAsync(string name, IEnumerable<KeyValuePair<string, JsonElement>>? arguments = null)
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime.InvokeAsync(name, arguments, CancellationToken.None);
        }

        #endregion

    }

    /// <summary>
    /// Convention OData 8 host tests for <c>odata_list_operations</c>.
    /// </summary>
    [TestClass]
    public class OdataListOperationsHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Listing operations agrees with <c>$metadata</c> function and action names.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_AgreesWithMetadataFunctionsAndActions()
        {
            using var client = CreateClient();
            using var metadata = await client.GetAsync("/odata/$metadata");
            var metadataBody = await metadata.Content.ReadAsStringAsync();
            if (metadata.IsSuccessStatusCode)
            {
                metadataBody.Should().Contain("MostValuable");
                metadataBody.Should().Contain("GetStatus");
                metadataBody.Should().Contain("Reset");
            }

            var result = await InvokeAsync("odata_list_operations");
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("MostValuable").And.Contain("GetStatus").And.Contain("Reset");
        }

        /// <summary>
        /// AspNetCore tools/list contains this tool and omits <c>shutdown_server</c>.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_AspNetCore_NoShutdownInToolsList()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue(body);
            body.Should().Contain("odata_list_operations");
            body.Should().NotContain("shutdown_server");
        }

        /// <summary>
        /// Concurrent list-operations calls all succeed.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_Concurrent()
        {
            var tasks = Enumerable.Range(0, 10).Select(_ => InvokeAsync("odata_list_operations")).ToArray();
            var results = await Task.WhenAll(tasks);
            results.Should().OnlyContain(result => !result.IsError);
        }

        /// <summary>
        /// Listing does not issue an OData HTTP request, so it cannot surface 404.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_DoesNotHitOData_SoNo404()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_list_operations", null, default);
            result.IsError.Should().BeFalse(result.Text);
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Extra <c>entitySet</c> is ignored and no HTTP is issued.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_ExtraEntitySet_IgnoredNoHttp()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_list_operations", ToolArguments.Of("entitySet", "Customers"), default);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("MostValuable");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Filter and top are ignored.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_FilterTop_Ignored()
        {
            var result = await InvokeAsync("odata_list_operations", ToolArguments.Of("filter", "true", "top", 1));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("MostValuable").And.Contain("Reset");
        }

        /// <summary>
        /// A huge extra property is ignored and the list still succeeds.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_HugeExtraPayload_StillListsOrMcp413()
        {
            var result = await InvokeAsync("odata_list_operations", ToolArguments.Of("padding", new string('x', 4096)));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("MostValuable");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_JsonRpc_Malformed()
        {
            using var client = CreateClient();
            using var content = McpJsonRpc.Content("{");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, body);
            response.IsSuccessStatusCode.Should().BeFalse(body);
        }

        /// <summary>
        /// Wrong content type is not a successful list.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_JsonRpc_WrongContentType()
        {
            using var client = CreateClient();
            using var content = new StringContent("""{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"odata_list_operations","arguments":{}}}""", Encoding.UTF8, "text/plain");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, body);
        }

        /// <summary>
        /// JSON-RPC <c>tools/call</c> lists MostValuable on the rich host.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_JsonRpcToolsCall_RichHost()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_list_operations", "{}");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue(body);
            body.Should().Contain("MostValuable");
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// A <c>name</c> argument does not filter the list.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_NameArg_IgnoredDoesNotFilter()
        {
            var result = await InvokeAsync("odata_list_operations", ToolArguments.Of("name", "MostValuable"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("MostValuable").And.Contain("GetStatus").And.Contain("Reset");
        }

        /// <summary>
        /// Null arguments succeed.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_NullArgs_Succeeds()
        {
            var result = await InvokeAsync("odata_list_operations");
            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().StartWith("Declared operations:");
        }

        /// <summary>
        /// The rich (rate-limit) model lists MostValuable as an unbound function returning int.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_OData8_RateLimitModel_ContainsMostValuableFunction()
        {
            var result = await InvokeAsync("odata_list_operations");
            result.IsError.Should().BeFalse(result.Text);
            using var document = JsonDocument.Parse(result.StructuredContent!);
            var most = document.RootElement.GetProperty("operations").GetProperty("MostValuable").GetString();
            most.Should().Be("() -> int", "unbound function with no arguments returning Edm.Int32; no kind or isBound keys");
        }

        /// <summary>
        /// After listing, each unbound action can be called with POST.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_ThenCallEachUnboundAction_Post()
        {
            var listed = await InvokeAsync("odata_list_operations");
            listed.IsError.Should().BeFalse(listed.Text);
            var (runtime, capture) = CreateCapturingRuntime();
            var reset = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Reset", "body", "{}"), default);
            reset.IsError.Should().BeFalse(reset.Text);
            capture.Last.Should().NotBeNull();
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("Reset");
        }

        /// <summary>
        /// After listing, each unbound function can be called with GET.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_ThenCallEachUnboundFunction_Get()
        {
            var listed = await InvokeAsync("odata_list_operations");
            listed.IsError.Should().BeFalse(listed.Text);
            var most = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            most.IsError.Should().BeFalse(most.Text);
            most.StructuredContent.Should().Contain("42");

            var status = await InvokeAsync("odata_call", ToolArguments.Of("name", "GetStatus", "code", "open"));
            status.IsError.Should().BeFalse(status.Text);
        }

        /// <summary>
        /// A misspelled singular tool name is unknown.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_UnknownToolIfMisspelled_odata_list_operation()
        {
            var result = await InvokeAsync("odata_list_operation");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("Unknown tool 'odata_list_operation'.");
        }

        /// <summary>
        /// Call-style <c>body</c> and <c>name</c> arguments are ignored.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_UsingCallArgs_BodyName_Ignored()
        {
            var result = await InvokeAsync("odata_list_operations", ToolArguments.Of("name", "Reset", "body", "{}"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("MostValuable").And.Contain("GetStatus").And.Contain("Reset");
        }

        #endregion

    }

    /// <summary>
    /// <c>odata_list_operations</c> on the operations-only model.
    /// </summary>
    [TestClass]
    public class OdataListOperationsOnlyHostTests : OperationsOnlyToolHost
    {

        #region Public Methods

        /// <summary>
        /// JSON-RPC lists MostValuable, GetStatus, and Reset on the operations-only host.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_JsonRpcToolsCall_OperationsOnlyHost()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_list_operations", "{}");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue(body);
            body.Should().Contain("MostValuable");
            body.Should().Contain("GetStatus");
            body.Should().Contain("Reset");
        }

        /// <summary>
        /// Operations-only catalogs list MostValuable, GetStatus (with code), and Reset (action).
        /// </summary>
        [TestMethod]
        public async Task ListOperations_OData8_OperationsOnly_ContainsMostValuableGetStatusReset()
        {
            var result = await InvokeAsync("odata_list_operations");
            result.IsError.Should().BeFalse(result.Text);
            using var document = JsonDocument.Parse(result.StructuredContent!);
            var operations = document.RootElement.GetProperty("operations");
            var names = operations.EnumerateObject().Select(item => item.Name).ToList();
            names.Should().Contain("MostValuable").And.Contain("GetStatus").And.Contain("Reset");

            operations.GetProperty("GetStatus").GetString().Should().Be("(code?: string) -> string");
            operations.GetProperty("Reset").GetString().Should().Be("() // writes", "actions carry the writes marker instead of a kind key");
        }

        #endregion

    }

    /// <summary>
    /// <c>odata_list_operations</c> on a few-tables model with no operations.
    /// </summary>
    [TestClass]
    public class OdataListOperationsFewTablesHostTests : FewTablesToolHost
    {

        #region Public Methods

        /// <summary>
        /// Few-tables catalogs declare zero operations.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_FewTablesNoOps_DeclaredOperations0()
        {
            var result = await InvokeAsync("odata_list_operations");
            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Be("Declared operations: 0.");
            result.StructuredContent.Should().Be("{}", "an empty operations section is omitted, never an empty array");
        }

        #endregion

    }

    /// <summary>
    /// <c>odata_list_operations</c> on the wide model.
    /// </summary>
    [TestClass]
    public class OdataListOperationsWideHostTests : WideModelToolHost
    {

        #region Public Methods

        /// <summary>
        /// A wide model with no operations still lists count 0.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_WideModelNoOps_Count0()
        {
            var result = await InvokeAsync("odata_list_operations");
            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Be("Declared operations: 0.");
        }

        #endregion

    }

    /// <summary>
    /// <c>odata_list_operations</c> cases that require the partitioned rate limiter.
    /// </summary>
    [TestClass]
    public class OdataListOperationsRateLimitedHostTests : RateLimitedRichHost
    {

        #region Public Methods

        /// <summary>
        /// Spending the MostValuable budget does not prevent listing operations.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_FunctionRateLimitSpent_StillLists()
        {
            var call = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            call.IsError.Should().BeFalse(call.Text);

            var listed = await InvokeAsync("odata_list_operations");
            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("MostValuable");
        }

        /// <summary>
        /// A second POST to <c>/odata/mcp</c> is HTTP 429.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_McpHttp429()
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
    /// <c>odata_list_operations</c> with a tiny response cap.
    /// </summary>
    [TestClass]
    public class OdataListOperationsTinyResponseHostTests : TinyResponseRichHost
    {

        #region Public Methods

        /// <summary>
        /// An oversized operations list is <c>IsError</c>.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_MaxResponseBytesTiny_IsError()
        {
            var result = await InvokeAsync("odata_list_operations");
            result.IsError.Should().BeTrue(result.Text);
        }

        #endregion

    }

}
