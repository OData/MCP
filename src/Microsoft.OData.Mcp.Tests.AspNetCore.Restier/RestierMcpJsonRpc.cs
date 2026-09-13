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
            return "{\"jsonrpc\":\"2.0\",\"id\":\"init\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"" + ProtocolVersion + "\",\"capabilities\":{},\"clientInfo\":{\"name\":\"odata-mcp-tests\",\"version\":\"1.0\"}}}";
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

        #endregion

    }

}
