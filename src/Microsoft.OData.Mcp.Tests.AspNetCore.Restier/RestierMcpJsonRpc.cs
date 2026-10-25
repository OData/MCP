// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// JSON-RPC helpers for Restier Streamable HTTP MCP endpoints at <c>odata/mcp</c>.
    /// </summary>
    public static class RestierMcpJsonRpc
    {

        #region Fields

        internal const string ProtocolVersion = "2025-06-18";

        /// <summary>
        /// Per-request <c>_meta</c> for protocol <c>2026-07-28</c> with URL elicitation and no Tasks extension.
        /// </summary>
        internal const string ElicitationMetaJson = """{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientInfo":{"name":"odata-mcp-tests","version":"1.0"},"io.modelcontextprotocol/clientCapabilities":{"elicitation":{"url":{}}}}""";

        /// <summary>
        /// Per-request <c>_meta</c> for protocol <c>2026-07-28</c>: version, client info, URL elicitation,
        /// and the Tasks extension opt-in.
        /// </summary>
        internal const string TasksMetaJson = """{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientInfo":{"name":"odata-mcp-tests","version":"1.0"},"io.modelcontextprotocol/clientCapabilities":{"elicitation":{"url":{}},"extensions":{"io.modelcontextprotocol/tasks":{}}}}""";

        /// <summary>
        /// Protocol version that advertises Tasks and URL elicitation.
        /// </summary>
        internal const string TasksProtocolVersion = "2026-07-28";

        #endregion

        #region Public Methods

        /// <summary>
        /// Posts <c>initialize</c> then <c>tools/call</c> to <paramref name="path"/>.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path, for example <c>odata/mcp</c>.</param>
        /// <param name="toolName">The tool name.</param>
        /// <param name="argumentsJson">JSON object of arguments, or null for <c>{}</c>.</param>
        /// <returns>
        /// The tools/call response.
        /// </returns>
        public static async Task<HttpResponseMessage> CallToolAsync(HttpClient client, string path, string toolName, string? argumentsJson = "{}")
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

            var args = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
            var payload = "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":" + args + "}}";

            return await SendAfterInitializeAsync(client, path, payload);
        }

        /// <summary>
        /// Builds a <c>tools/call</c> JSON-RPC payload.
        /// </summary>
        /// <param name="toolName">The tool name.</param>
        /// <param name="argumentsJson">JSON object of arguments.</param>
        /// <param name="metaJson">Optional <c>_meta</c> JSON object.</param>
        /// <returns>
        /// The JSON.
        /// </returns>
        public static string CallToolPayload(string toolName, string argumentsJson = "{}", string? metaJson = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

            var args = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
            var meta = string.IsNullOrWhiteSpace(metaJson)
                ? string.Empty
                : ",\"_meta\":" + metaJson;
            return "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":" + args + meta + "}}";
        }

        /// <summary>
        /// Builds a <c>tools/call</c> retry after an MRTR <c>InputRequiredResult</c>.
        /// </summary>
        /// <param name="toolName">The tool name.</param>
        /// <param name="argumentsJson">JSON object of arguments.</param>
        /// <param name="inputResponsesJson">JSON object of input responses.</param>
        /// <param name="requestState">Opaque state from the previous result.</param>
        /// <param name="metaJson">Optional <c>_meta</c> JSON object.</param>
        /// <returns>
        /// The JSON-RPC body.
        /// </returns>
        public static string CallToolRetryPayload(
            string toolName,
            string argumentsJson,
            string inputResponsesJson,
            string requestState,
            string? metaJson = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
            ArgumentException.ThrowIfNullOrWhiteSpace(inputResponsesJson);
            ArgumentException.ThrowIfNullOrWhiteSpace(requestState);

            var args = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
            var meta = string.IsNullOrWhiteSpace(metaJson)
                ? string.Empty
                : ",\"_meta\":" + metaJson;
            return "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName
                + "\",\"arguments\":" + args
                + ",\"inputResponses\":" + inputResponsesJson
                + ",\"requestState\":" + JsonSerializer.Serialize(requestState)
                + meta + "}}";
        }

        /// <summary>
        /// Posts <c>completion/complete</c> after initialize.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path.</param>
        /// <param name="argumentName">The argument name, typically <c>entitySet</c>.</param>
        /// <param name="prefix">The typed prefix.</param>
        /// <returns>
        /// The completion response.
        /// </returns>
        public static async Task<HttpResponseMessage> CompleteAsync(HttpClient client, string path, string argumentName, string prefix)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(argumentName);

            var payload = "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/resource\",\"uri\":\"odata://odata/{entitySet}\"},\"argument\":{\"name\":\"" + argumentName + "\",\"value\":" + JsonSerializer.Serialize(prefix) + "}}}";

            return await SendAfterInitializeAsync(client, path, payload);
        }

        /// <summary>
        /// Builds JSON content for an MCP POST.
        /// </summary>
        /// <param name="json">The JSON payload.</param>
        /// <param name="contentType">The content type.</param>
        /// <returns>
        /// The content.
        /// </returns>
        public static StringContent Content(string json, string contentType = "application/json")
        {
            return new StringContent(json, Encoding.UTF8, contentType);
        }

        /// <summary>
        /// Extracts the first JSON object from a JSON or SSE body.
        /// </summary>
        /// <param name="body">The HTTP body.</param>
        /// <returns>
        /// JSON text.
        /// </returns>
        public static string ExtractJson(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return body ?? string.Empty;
            }

            var start = body.IndexOf('{');
            if (start < 0)
            {
                return body;
            }

            var last = body.LastIndexOf('}');
            if (last > start)
            {
                return body[start..(last + 1)];
            }

            return body[start..];
        }

        /// <summary>
        /// Builds the initialize JSON-RPC payload.
        /// </summary>
        /// <returns>
        /// The JSON.
        /// </returns>
        public static string InitializePayload()
        {
            return InitializePayload(ProtocolVersion, "{}");
        }

        /// <summary>
        /// Builds the initialize JSON-RPC payload.
        /// </summary>
        /// <param name="protocolVersion">The MCP protocol version.</param>
        /// <param name="capabilitiesJson">JSON object of client capabilities.</param>
        /// <returns>
        /// The JSON.
        /// </returns>
        public static string InitializePayload(string protocolVersion, string capabilitiesJson)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(protocolVersion);
            ArgumentException.ThrowIfNullOrWhiteSpace(capabilitiesJson);

            return "{\"jsonrpc\":\"2.0\",\"id\":\"init\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"" + protocolVersion + "\",\"capabilities\":" + capabilitiesJson + ",\"clientInfo\":{\"name\":\"odata-mcp-tests\",\"version\":\"1.0\"}}}";
        }

        /// <summary>
        /// Posts <c>initialize</c> and returns the session id when the server sent one.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path.</param>
        /// <param name="protocolVersion">The MCP protocol version.</param>
        /// <param name="capabilitiesJson">JSON object of client capabilities.</param>
        /// <returns>
        /// The session id, or <c>null</c>.
        /// </returns>
        public static async Task<string?> InitializeSessionAsync(
            HttpClient client,
            string path,
            string? protocolVersion = null,
            string? capabilitiesJson = null)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
            using var initialize = Content(InitializePayload(
                string.IsNullOrWhiteSpace(protocolVersion) ? ProtocolVersion : protocolVersion,
                string.IsNullOrWhiteSpace(capabilitiesJson) ? "{}" : capabilitiesJson));
            using var initialized = await client.PostAsync(path, initialize);
            return initialized.Headers.TryGetValues("mcp-session-id", out var values)
                ? string.Join(",", values)
                : null;
        }

        /// <summary>
        /// Posts a JSON-RPC body, carrying the session header when present.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path.</param>
        /// <param name="json">The JSON-RPC payload.</param>
        /// <param name="sessionId">The session id from initialize, or <c>null</c>.</param>
        /// <returns>
        /// The response.
        /// </returns>
        public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string json, string? sessionId)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = Content(json)
            };
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                request.Headers.TryAddWithoutValidation("mcp-session-id", sessionId);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Posts a protocol <c>2026-07-28</c> JSON-RPC request. That revision has no initialize
        /// handshake; the MCP method and name travel as headers.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path.</param>
        /// <param name="json">The JSON-RPC payload.</param>
        /// <param name="method">The JSON-RPC method, for example <c>tools/call</c>.</param>
        /// <param name="name">The tool or resource name when the method requires <c>Mcp-Name</c>.</param>
        /// <returns>
        /// The response.
        /// </returns>
        public static async Task<HttpResponseMessage> PostJuly2026Async(
            HttpClient client,
            string path,
            string json,
            string method,
            string? name = null)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            ArgumentException.ThrowIfNullOrWhiteSpace(method);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = Content(json)
            };
            request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", TasksProtocolVersion);
            request.Headers.TryAddWithoutValidation("Mcp-Method", method);
            if (!string.IsNullOrWhiteSpace(name))
            {
                request.Headers.TryAddWithoutValidation("Mcp-Name", name);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Posts a JSON-RPC <c>resources/list</c> after initialize.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path.</param>
        /// <returns>
        /// The resources/list response.
        /// </returns>
        public static async Task<HttpResponseMessage> ListResourcesAsync(HttpClient client, string path)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return await SendAfterInitializeAsync(client, path, """{"jsonrpc":"2.0","id":"1","method":"resources/list","params":{}}""");
        }

        /// <summary>
        /// Posts a JSON-RPC <c>resources/templates/list</c> after initialize.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path.</param>
        /// <returns>
        /// The templates/list response.
        /// </returns>
        public static async Task<HttpResponseMessage> ListTemplatesAsync(HttpClient client, string path)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return await SendAfterInitializeAsync(client, path, """{"jsonrpc":"2.0","id":"1","method":"resources/templates/list","params":{}}""");
        }

        /// <summary>
        /// Posts a JSON-RPC <c>tools/list</c> after initialize.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path.</param>
        /// <returns>
        /// The tools/list response.
        /// </returns>
        public static async Task<HttpResponseMessage> ListToolsAsync(HttpClient client, string path)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return await SendAfterInitializeAsync(client, path, """{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}""");
        }

        /// <summary>
        /// Reads the response body as text.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <returns>
        /// The body.
        /// </returns>
        public static async Task<string> ReadBodyAsync(HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Reads <c>result.isError</c> from a tools/call JSON-RPC body when present.
        /// </summary>
        /// <param name="body">The HTTP body.</param>
        /// <returns>
        /// The flag, or <c>null</c> when the payload is a protocol error.
        /// </returns>
        public static bool? ReadIsError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            using var document = JsonDocument.Parse(ExtractJson(body));
            if (document.RootElement.TryGetProperty("result", out var result)
                && result.TryGetProperty("isError", out var isError)
                && (isError.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                return isError.GetBoolean();
            }

            return null;
        }

        /// <summary>
        /// Posts <c>resources/read</c> after initialize.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path.</param>
        /// <param name="uri">The resource URI.</param>
        /// <returns>
        /// The resources/read response.
        /// </returns>
        public static async Task<HttpResponseMessage> ReadResourceAsync(HttpClient client, string path, string uri)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(uri);

            var payload = "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"resources/read\",\"params\":{\"uri\":" + JsonSerializer.Serialize(uri) + "}}";

            return await SendAfterInitializeAsync(client, path, payload);
        }

        /// <summary>
        /// Posts a raw JSON-RPC body after initialize, carrying the session header when present.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path.</param>
        /// <param name="json">The JSON-RPC payload.</param>
        /// <returns>
        /// The second response.
        /// </returns>
        public static async Task<HttpResponseMessage> SendAfterInitializeAsync(HttpClient client, string path, string json)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
            using var initialize = Content(InitializePayload());
            using var initialized = await client.PostAsync(path, initialize);
            var session = initialized.Headers.TryGetValues("mcp-session-id", out var values)
                ? string.Join(",", values)
                : null;

            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = Content(json)
            };
            if (!string.IsNullOrWhiteSpace(session))
            {
                request.Headers.TryAddWithoutValidation("mcp-session-id", session);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Builds a <c>tasks/cancel</c> JSON-RPC payload.
        /// </summary>
        /// <param name="taskId">The task id.</param>
        /// <param name="metaJson">Optional <c>_meta</c> JSON object.</param>
        /// <returns>
        /// The JSON.
        /// </returns>
        public static string TasksCancelPayload(string taskId, string? metaJson = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

            var meta = string.IsNullOrWhiteSpace(metaJson)
                ? string.Empty
                : ",\"_meta\":" + metaJson;
            return "{\"jsonrpc\":\"2.0\",\"id\":\"4\",\"method\":\"tasks/cancel\",\"params\":{\"taskId\":" + JsonSerializer.Serialize(taskId) + meta + "}}";
        }

        /// <summary>
        /// Builds a <c>tasks/get</c> JSON-RPC payload.
        /// </summary>
        /// <param name="taskId">The task id.</param>
        /// <param name="metaJson">Optional <c>_meta</c> JSON object.</param>
        /// <returns>
        /// The JSON.
        /// </returns>
        public static string TasksGetPayload(string taskId, string? metaJson = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

            var meta = string.IsNullOrWhiteSpace(metaJson)
                ? string.Empty
                : ",\"_meta\":" + metaJson;
            return "{\"jsonrpc\":\"2.0\",\"id\":\"2\",\"method\":\"tasks/get\",\"params\":{\"taskId\":" + JsonSerializer.Serialize(taskId) + meta + "}}";
        }

        /// <summary>
        /// Builds a <c>tasks/update</c> JSON-RPC payload.
        /// </summary>
        /// <param name="taskId">The task id.</param>
        /// <param name="inputResponsesJson">JSON object of input responses.</param>
        /// <param name="metaJson">Optional <c>_meta</c> JSON object.</param>
        /// <returns>
        /// The JSON.
        /// </returns>
        public static string TasksUpdatePayload(string taskId, string inputResponsesJson, string? metaJson = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
            ArgumentException.ThrowIfNullOrWhiteSpace(inputResponsesJson);

            var meta = string.IsNullOrWhiteSpace(metaJson)
                ? string.Empty
                : ",\"_meta\":" + metaJson;
            return "{\"jsonrpc\":\"2.0\",\"id\":\"3\",\"method\":\"tasks/update\",\"params\":{\"taskId\":" + JsonSerializer.Serialize(taskId) + ",\"inputResponses\":" + inputResponsesJson + meta + "}}";
        }

        #endregion

    }

}
