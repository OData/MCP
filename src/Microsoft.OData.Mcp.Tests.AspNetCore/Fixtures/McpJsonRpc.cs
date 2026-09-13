// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// JSON-RPC helpers for Streamable HTTP MCP endpoints.
    /// </summary>
    public static class McpJsonRpc
    {

        #region Fields

        internal const string ProtocolVersion = "2025-06-18";

        #endregion

        #region Public Methods

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
        /// Ensures the client accepts JSON and SSE as required by Streamable HTTP.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        public static void AcceptMcp(HttpClient client)
        {
            ArgumentNullException.ThrowIfNull(client);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        }

        /// <summary>
        /// Posts <c>initialize</c> then <c>tools/call</c> to <paramref name="path"/>.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="path">The MCP path, for example <c>/odata/mcp</c>.</param>
        /// <param name="toolName">The tool name.</param>
        /// <param name="argumentsJson">JSON object of arguments, or null.</param>
        /// <returns>
        /// The tools/call response.
        /// </returns>
        public static async Task<HttpResponseMessage> CallToolAsync(HttpClient client, string path, string toolName, string? argumentsJson = "{}")
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

            AcceptMcp(client);
            using var initialize = Content(InitializePayload());
            using var initialized = await client.PostAsync(path, initialize);
            var session = initialized.Headers.TryGetValues("mcp-session-id", out var values)
                ? string.Join(",", values)
                : null;

            var args = string.IsNullOrWhiteSpace(argumentsJson) ? "null" : argumentsJson;
            using var call = Content("{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":" + args + "}}");
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = call
            };
            if (!string.IsNullOrWhiteSpace(session))
            {
                request.Headers.TryAddWithoutValidation("mcp-session-id", session);
            }

            return await client.SendAsync(request);
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

            AcceptMcp(client);
            using var initialize = Content(InitializePayload());
            using var initialized = await client.PostAsync(path, initialize);
            var session = initialized.Headers.TryGetValues("mcp-session-id", out var values)
                ? string.Join(",", values)
                : null;

            using var list = Content("""{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}""");
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = list
            };
            if (!string.IsNullOrWhiteSpace(session))
            {
                request.Headers.TryAddWithoutValidation("mcp-session-id", session);
            }

            return await client.SendAsync(request);
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

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the initialize JSON-RPC payload.
        /// </summary>
        /// <returns>
        /// The JSON.
        /// </returns>
        internal static string InitializePayload()
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":\"init\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"" + ProtocolVersion + "\",\"capabilities\":{},\"clientInfo\":{\"name\":\"odata-mcp-tests\",\"version\":\"1.0\"}}}";
        }

        #endregion

    }

}
