// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// URL elicitation for annotated Restier actions: CSDL, one-shot without opt-in, and Tasks wrapping.
    /// </summary>
    [TestClass]
    public class RestierMcpElicitationTests : RestierToolTestBase
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierMcpElicitationTests"/> class.
        /// </summary>
        public RestierMcpElicitationTests()
            : base(nameof(RestierMcpElicitationTests))
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// One-shot <c>Authorize</c> returns <c>connectUri</c> and does not call the waiter.
        /// </summary>
        [TestMethod]
        public async Task Authorize_WithoutTasksOptIn_ReturnsConnectUriAndSkipsWaiter()
        {
            var result = await Runtime().InvokeAsync(
                "action_mcpcustomer_authorize",
                ToolArguments.Of("key", "1"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            var payload = result.StructuredContent;
            payload.Should().Contain("https://login.example.test/connect");
            payload!.Contains("connectUri", StringComparison.OrdinalIgnoreCase).Should().BeTrue(payload);
            ((RestierAuthorizeWaiter)TestServer.Services.GetRequiredService<IMcpAnnotatedActionWaiter>()).WaitCalls.Should().Be(0);
        }

        /// <summary>
        /// Accepting the URL elicitation calls the waiter and completes with its terminal JSON.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_ElicitAccept_WaiterReturnsSucceeded()
        {
            using var client = CreateClient();
            var waiter = (RestierAuthorizeWaiter)TestServer.Services.GetRequiredService<IMcpAnnotatedActionWaiter>();
            waiter.Completion.SetResult(new CallToolResult
            {
                Content = [new TextContentBlock { Text = """{"status":"Succeeded","message":"The grant completed."}""" }]
            });

            var taskId = await CallAuthorizeAsTaskAsync(client);
            var required = await PollTaskUntilAsync(client, taskId, "input_required");
            var requestId = ReadFirstInputRequestId(required);
            requestId.Should().NotBeNullOrWhiteSpace(required);

            using var update = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.TasksUpdatePayload(taskId, "{\"" + requestId + "\":{\"action\":\"accept\"}}", RestierMcpJsonRpc.TasksMetaJson),
                "tasks/update",
                taskId);
            var updateBody = await RestierMcpJsonRpc.ReadBodyAsync(update);
            update.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)update.StatusCode, updateBody);

            var completed = await PollTaskUntilAsync(client, taskId, "completed");
            completed.Should().Contain("The grant completed.");
            waiter.WaitCalls.Should().Be(1);
        }

        /// <summary>
        /// Declining the URL elicitation completes with an error and does not call the waiter.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_ElicitDecline_SkipsWaiter()
        {
            using var client = CreateClient();
            var waiter = (RestierAuthorizeWaiter)TestServer.Services.GetRequiredService<IMcpAnnotatedActionWaiter>();
            var taskId = await CallAuthorizeAsTaskAsync(client);
            var required = await PollTaskUntilAsync(client, taskId, "input_required");
            var requestId = ReadFirstInputRequestId(required);
            requestId.Should().NotBeNullOrWhiteSpace(required);

            using var update = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.TasksUpdatePayload(taskId, "{\"" + requestId + "\":{\"action\":\"decline\"}}", RestierMcpJsonRpc.TasksMetaJson),
                "tasks/update",
                taskId);
            var updateBody = await RestierMcpJsonRpc.ReadBodyAsync(update);
            update.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)update.StatusCode, updateBody);

            var completed = await PollTaskUntilAsync(client, taskId, "completed");
            completed.Contains("declined", StringComparison.OrdinalIgnoreCase).Should().BeTrue(completed);
            waiter.WaitCalls.Should().Be(0);
        }

        /// <summary>
        /// URL elicitation without the Tasks extension uses MRTR: accept then the waiter.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_ElicitWithoutTasks_Accept_WaiterReturnsSucceeded()
        {
            using var client = CreateClient();
            var waiter = (RestierAuthorizeWaiter)TestServer.Services.GetRequiredService<IMcpAnnotatedActionWaiter>();
            waiter.Completion.SetResult(new CallToolResult
            {
                Content = [new TextContentBlock { Text = """{"status":"Succeeded","message":"The grant completed."}""" }]
            });

            var required = await CallAuthorizeAsElicitAsync(client);
            var requestState = ReadRequestState(required);
            requestState.Should().NotBeNullOrWhiteSpace(required);

            using var retry = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.CallToolRetryPayload(
                    "action_mcpcustomer_authorize",
                    """{"key":"1"}""",
                    "{\"" + ODataMcpAnnotatedActionTasks.ElicitInputKey + "\":{\"action\":\"accept\"}}",
                    requestState!,
                    RestierMcpJsonRpc.ElicitationMetaJson),
                "tools/call",
                "action_mcpcustomer_authorize");
            var retryBody = await RestierMcpJsonRpc.ReadBodyAsync(retry);
            retry.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)retry.StatusCode, retryBody);
            retryBody.Should().Contain("The grant completed.");
            retryBody.Should().NotContain("\"resultType\":\"task\"");
            waiter.WaitCalls.Should().Be(1);
        }

        /// <summary>
        /// URL elicitation without the Tasks extension: decline skips the waiter.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_ElicitWithoutTasks_Decline_SkipsWaiter()
        {
            using var client = CreateClient();
            var waiter = (RestierAuthorizeWaiter)TestServer.Services.GetRequiredService<IMcpAnnotatedActionWaiter>();
            var required = await CallAuthorizeAsElicitAsync(client);
            var requestState = ReadRequestState(required);
            requestState.Should().NotBeNullOrWhiteSpace(required);

            using var retry = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.CallToolRetryPayload(
                    "action_mcpcustomer_authorize",
                    """{"key":"1"}""",
                    "{\"" + ODataMcpAnnotatedActionTasks.ElicitInputKey + "\":{\"action\":\"decline\"}}",
                    requestState!,
                    RestierMcpJsonRpc.ElicitationMetaJson),
                "tools/call",
                "action_mcpcustomer_authorize");
            var retryBody = await RestierMcpJsonRpc.ReadBodyAsync(retry);
            retry.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)retry.StatusCode, retryBody);
            retryBody.Contains("declined", StringComparison.OrdinalIgnoreCase).Should().BeTrue(retryBody);
            retryBody.Should().NotContain("\"resultType\":\"task\"");
            waiter.WaitCalls.Should().Be(0);
        }

        /// <summary>
        /// URL elicitation without the Tasks extension returns MRTR <c>inputRequests</c>, not a task handle.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_ElicitWithoutTasks_InputRequired()
        {
            using var client = CreateClient();
            var required = await CallAuthorizeAsElicitAsync(client);

            required.Should().Contain("elicitation/create");
            required.Should().Contain("https://login.example.test/connect");
            required.Should().Contain("\"mode\":\"url\"");
            required.Should().Contain(ODataMcpAnnotatedActionTasks.ElicitInputKey);
            required.Should().NotContain("\"resultType\":\"task\"");
            required.Contains("token", StringComparison.OrdinalIgnoreCase).Should().BeFalse(required);
            ((RestierAuthorizeWaiter)TestServer.Services.GetRequiredService<IMcpAnnotatedActionWaiter>()).WaitCalls.Should().Be(0);
        }

        /// <summary>
        /// Protocol <c>2026-07-28</c> without the per-request Tasks extension stays one-shot.
        /// Claude Code omits the extension when the call cannot become a task; a task handle
        /// would surface as <c>Unsupported result type 'task' for tools/call</c>.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_July2026WithoutTasksMeta_IsOneShot()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.CallToolPayload(
                    "action_mcpcustomer_authorize",
                    """{"key":"1"}""",
                    RestierMcpJsonRpc.July2026MetaJson),
                "tools/call",
                "action_mcpcustomer_authorize");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Contains("connectUri", StringComparison.OrdinalIgnoreCase).Should().BeTrue(body);
            body.Should().Contain("https://login.example.test/connect");
            body.Should().NotContain("\"resultType\":\"task\"");
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            ((RestierAuthorizeWaiter)TestServer.Services.GetRequiredService<IMcpAnnotatedActionWaiter>()).WaitCalls.Should().Be(0);
        }

        /// <summary>
        /// Generic <c>odata_call</c> of annotated Authorize without the Tasks extension stays one-shot.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_ODataCall_WithoutTasksMeta_IsOneShot()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.CallToolPayload(
                    "odata_call",
                    """{"name":"Authorize","entitySet":"Customers","key":"1"}""",
                    RestierMcpJsonRpc.July2026MetaJson),
                "tools/call",
                "odata_call");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Contains("connectUri", StringComparison.OrdinalIgnoreCase).Should().BeTrue(body);
            body.Should().NotContain("\"resultType\":\"task\"");
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
        }

        /// <summary>
        /// <c>tasks/cancel</c> during URL elicitation does not call the waiter.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_TasksCancel_DuringElicit_SkipsWaiter()
        {
            using var client = CreateClient();
            var waiter = (RestierAuthorizeWaiter)TestServer.Services.GetRequiredService<IMcpAnnotatedActionWaiter>();
            var taskId = await CallAuthorizeAsTaskAsync(client);
            await PollTaskUntilAsync(client, taskId, "input_required");

            using var cancel = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.TasksCancelPayload(taskId, RestierMcpJsonRpc.TasksMetaJson),
                "tasks/cancel",
                taskId);
            var cancelBody = await RestierMcpJsonRpc.ReadBodyAsync(cancel);
            cancel.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)cancel.StatusCode, cancelBody);

            waiter.WaitCalls.Should().Be(0);
        }

        /// <summary>
        /// <c>tasks/get</c> surfaces URL elicitation for <c>connectUri</c> with no tokens in the payload.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_TasksGet_InputRequiredHasUrlNoTokens()
        {
            using var client = CreateClient();
            var taskId = await CallAuthorizeAsTaskAsync(client);
            var required = await PollTaskUntilAsync(client, taskId, "input_required");

            required.Should().Contain("elicitation/create");
            required.Should().Contain("https://login.example.test/connect");
            required.Should().Contain("\"mode\":\"url\"");
            required.Contains("token", StringComparison.OrdinalIgnoreCase).Should().BeFalse(required);
            required.Contains("secret", StringComparison.OrdinalIgnoreCase).Should().BeFalse(required);
            required.Contains("password", StringComparison.OrdinalIgnoreCase).Should().BeFalse(required);
            required.Contains("connect_code", StringComparison.OrdinalIgnoreCase).Should().BeFalse(required);
        }

        /// <summary>
        /// JSON-RPC <c>tools/call</c> without the Tasks extension stays one-shot.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_WithoutTasksMeta_IsOneShot()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.CallToolAsync(
                client,
                "odata/mcp",
                "action_mcpcustomer_authorize",
                """{"key":"1"}""");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Contains("connectUri", StringComparison.OrdinalIgnoreCase).Should().BeTrue(body);
            body.Should().Contain("https://login.example.test/connect");
            body.Should().NotContain("\"resultType\":\"task\"");
            RestierMcpJsonRpc.ReadIsError(body).Should().BeFalse();
            ((RestierAuthorizeWaiter)TestServer.Services.GetRequiredService<IMcpAnnotatedActionWaiter>()).WaitCalls.Should().Be(0);
        }

        /// <summary>
        /// Client opt-in plus annotated Authorize returns a task handle.
        /// </summary>
        [TestMethod]
        public async Task CallAuthorize_WithTasksOptIn_ReturnsTask()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.CallToolPayload(
                    "action_mcpcustomer_authorize",
                    """{"key":"1"}""",
                    RestierMcpJsonRpc.TasksMetaJson),
                "tools/call",
                "action_mcpcustomer_authorize");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            var json = RestierMcpJsonRpc.ExtractJson(body);
            using var document = JsonDocument.Parse(json);
            document.RootElement.TryGetProperty("result", out var result).Should().BeTrue("body {0}", json);
            AssertClaudeCodeTaskHandle(result);
        }

        /// <summary>
        /// CRUD stays one-shot even when the client opted into tasks.
        /// </summary>
        [TestMethod]
        public async Task CallListCustomers_WithTasksOptIn_StaysOneShot()
        {
            using var client = CreateClient();
            using var response = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.CallToolPayload("list_customers", "{}", RestierMcpJsonRpc.TasksMetaJson),
                "tools/call",
                "list_customers");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().NotContain("\"resultType\":\"task\"");
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// HTTP <c>$metadata</c> nests <c>ElicitUrlProperties</c> on Authorize.
        /// </summary>
        [TestMethod]
        public async Task HttpMetadata_ExportsElicitUrlPropertiesOnAuthorize()
        {
            using var client = CreateClient();
            using var response = await client.GetAsync("odata/$metadata");
            var xml = await response.Content.ReadAsStringAsync();

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, xml);
            AssertExportsElicitUrl(xml);
        }

        /// <summary>
        /// Named Authorize tool is tasked and lists <c>connectUri</c>.
        /// </summary>
        [TestMethod]
        public void NamedAuthorizeTool_IsTaskWithElicitUrl()
        {
            var authorize = Catalog().Tools.Single(tool => tool.Name == "action_mcpcustomer_authorize");

            authorize.IsTask.Should().BeTrue();
            authorize.ElicitUrlProperties.Should().Equal("connectUri");
            authorize.Description.Should().Contain(McpVocabulary.TaskToolDescription);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Asserts a <c>CreateTaskResult</c> has the fields Claude Code requires on a task handle.
        /// Missing <c>taskId</c>, <c>status</c>, <c>createdAt</c>, <c>lastUpdatedAt</c>, or
        /// <c>ttlMs</c> surfaces as <c>Unsupported result type 'task' for tools/call</c>.
        /// </summary>
        /// <param name="result">The JSON-RPC <c>result</c> object.</param>
        internal static void AssertClaudeCodeTaskHandle(JsonElement result)
        {
            result.ValueKind.Should().Be(JsonValueKind.Object);
            result.TryGetProperty("resultType", out var resultType).Should().BeTrue(result.GetRawText());
            resultType.GetString().Should().Be("task");

            var handle = result;
            if ((!result.TryGetProperty("taskId", out var topId) || topId.ValueKind != JsonValueKind.String)
                && result.TryGetProperty("task", out var nested)
                && nested.ValueKind == JsonValueKind.Object)
            {
                handle = nested;
            }

            handle.TryGetProperty("taskId", out var taskId).Should().BeTrue(handle.GetRawText());
            taskId.GetString().Should().NotBeNullOrWhiteSpace();

            handle.TryGetProperty("status", out var status).Should().BeTrue(handle.GetRawText());
            status.GetString().Should().NotBeNullOrWhiteSpace();

            handle.TryGetProperty("createdAt", out var createdAt).Should().BeTrue(handle.GetRawText());
            createdAt.GetString().Should().NotBeNullOrWhiteSpace();

            handle.TryGetProperty("lastUpdatedAt", out var lastUpdated).Should().BeTrue(handle.GetRawText());
            lastUpdated.GetString().Should().NotBeNullOrWhiteSpace();

            handle.TryGetProperty("ttlMs", out var ttl).Should().BeTrue("Claude Code requires ttlMs. Handle: {0}", handle.GetRawText());
            (ttl.ValueKind is JsonValueKind.Number or JsonValueKind.Null).Should().BeTrue("ttlMs was {0}", ttl.ValueKind);
            if (ttl.ValueKind == JsonValueKind.Number)
            {
                ttl.GetInt64().Should().BeGreaterThan(0);
            }
        }

        /// <summary>
        /// Asserts Authorize carries inline Task and ElicitUrlProperties.
        /// </summary>
        /// <param name="xml">Service CSDL.</param>
        internal static void AssertExportsElicitUrl(string? xml)
        {
            xml.Should().NotBeNullOrWhiteSpace();
            var parsed = new CsdlParser().ParseFromString(xml!);
            var authorize = parsed.Actions.Single(action => action.Name == "Authorize");
            authorize.IsTask.Should().BeTrue();
            authorize.ElicitUrlProperties.Should().Equal("connectUri");

            var document = XDocument.Parse(xml!);
            XNamespace edm = "http://docs.oasis-open.org/odata/ns/edm";
            var action = document.Descendants(edm + "Action")
                .Single(element => element.Attribute("Name")?.Value == "Authorize");
            action.Elements(edm + "Annotation")
                .Select(element => element.Attribute("Term")?.Value)
                .Should().Contain("Mcp.Task")
                .And.Contain("Mcp.ElicitUrlProperties");
        }

        /// <summary>
        /// Calls annotated Authorize with URL elicitation and no Tasks extension.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <returns>
        /// The JSON-RPC body of the MRTR <c>InputRequiredResult</c>.
        /// </returns>
        internal async Task<string> CallAuthorizeAsElicitAsync(HttpClient client)
        {
            using var response = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.CallToolPayload(
                    "action_mcpcustomer_authorize",
                    """{"key":"1"}""",
                    RestierMcpJsonRpc.ElicitationMetaJson),
                "tools/call",
                "action_mcpcustomer_authorize");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            var json = RestierMcpJsonRpc.ExtractJson(body);
            json.Should().Contain("elicitation/create", json);
            return json;
        }

        /// <summary>
        /// Calls annotated Authorize with Tasks opt-in and returns the task id.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <returns>
        /// The created task id.
        /// </returns>
        internal async Task<string> CallAuthorizeAsTaskAsync(HttpClient client)
        {
            using var response = await RestierMcpJsonRpc.PostJuly2026Async(
                client,
                "odata/mcp",
                RestierMcpJsonRpc.CallToolPayload(
                    "action_mcpcustomer_authorize",
                    """{"key":"1"}""",
                    RestierMcpJsonRpc.TasksMetaJson),
                "tools/call",
                "action_mcpcustomer_authorize");
            var body = await RestierMcpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);

            using var document = JsonDocument.Parse(RestierMcpJsonRpc.ExtractJson(body));
            var taskId = ReadTaskId(document.RootElement.GetProperty("result"));
            taskId.Should().NotBeNullOrWhiteSpace(body);
            return taskId!;
        }

        /// <summary>
        /// Registers Tasks wrapping and the Restier waiter.
        /// </summary>
        /// <param name="services">The service collection.</param>
        internal override void ConfigureServices(IServiceCollection services)
        {
            services.AddODataMcp(ConfigureODataMcp, mcp =>
            {
                mcp.WithTasks();
                mcp.WaitForAnnotatedAction<RestierAuthorizeWaiter>();
            });
        }

        /// <summary>
        /// Polls <c>tasks/get</c> until the task reports <paramref name="status"/>.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="taskId">The task id.</param>
        /// <param name="status">The expected status wire value.</param>
        /// <returns>
        /// The <c>tasks/get</c> JSON when the status matches.
        /// </returns>
        internal async Task<string> PollTaskUntilAsync(HttpClient client, string taskId, string status)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
            ArgumentException.ThrowIfNullOrWhiteSpace(status);

            string? last = null;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                using var response = await RestierMcpJsonRpc.PostJuly2026Async(
                    client,
                    "odata/mcp",
                    RestierMcpJsonRpc.TasksGetPayload(taskId, RestierMcpJsonRpc.TasksMetaJson),
                    "tasks/get",
                    taskId);
                last = RestierMcpJsonRpc.ExtractJson(await RestierMcpJsonRpc.ReadBodyAsync(response));
                using var document = JsonDocument.Parse(last);
                if (document.RootElement.TryGetProperty("result", out var result)
                    && result.TryGetProperty("status", out var wire)
                    && string.Equals(wire.GetString(), status, StringComparison.OrdinalIgnoreCase))
                {
                    return last;
                }

                await Task.Delay(50);
            }

            throw new AssertFailedException($"Task {taskId} never reached '{status}'. Last: {last}");
        }

        /// <summary>
        /// Reads the first <c>inputRequests</c> key from a <c>tasks/get</c> body.
        /// </summary>
        /// <param name="json">The JSON-RPC body.</param>
        /// <returns>
        /// The request id, or <c>null</c>.
        /// </returns>
        internal static string? ReadFirstInputRequestId(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("result", out var result)
                || !result.TryGetProperty("inputRequests", out var requests)
                || requests.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in requests.EnumerateObject())
            {
                return property.Name;
            }

            return null;
        }

        /// <summary>
        /// Reads MRTR <c>requestState</c> from a JSON-RPC result.
        /// </summary>
        /// <param name="json">The JSON-RPC body.</param>
        /// <returns>
        /// The echoed state, or <c>null</c>.
        /// </returns>
        internal static string? ReadRequestState(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("result", out var result)
                && result.TryGetProperty("requestState", out var state)
                && state.ValueKind == JsonValueKind.String)
            {
                return state.GetString();
            }

            return null;
        }

        /// <summary>
        /// Reads a task id from a tools/call result object.
        /// </summary>
        /// <param name="result">The JSON-RPC result.</param>
        /// <returns>
        /// The task id, or <c>null</c>.
        /// </returns>
        internal static string? ReadTaskId(JsonElement result)
        {
            if (result.TryGetProperty("taskId", out var taskId) && taskId.ValueKind == JsonValueKind.String)
            {
                return taskId.GetString();
            }

            if (result.TryGetProperty("task", out var task)
                && task.ValueKind == JsonValueKind.Object
                && task.TryGetProperty("taskId", out var nested)
                && nested.ValueKind == JsonValueKind.String)
            {
                return nested.GetString();
            }

            return null;
        }

        #endregion

    }

}
