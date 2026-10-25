// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Sample.Examples
{

    /// <summary>
    /// Posts JSON-RPC to the sample's Streamable HTTP MCP endpoint at <c>/odata/mcp</c>.
    /// The usual client is a harness (Claude Desktop, Cursor, Claude Code). This is the raw protocol.
    /// </summary>
    public sealed class McpClientExample
    {

        #region Fields

        internal const string McpPath = "/odata/mcp";

        internal readonly HttpClient _httpClient;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an example client against the sample's launch URL.
        /// </summary>
        public McpClientExample()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:59982") };
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Calls <c>list_customers</c> for USA accounts.
        /// </summary>
        public Task<string> CallListCustomersAsync()
        {
            return PostAfterInitializeAsync(
                """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"list_customers","arguments":{"filter":"Country eq 'USA'","top":5}}}""");
        }

        /// <summary>
        /// Calls bound <c>action_product_applydiscount</c> on product 1.
        /// </summary>
        public Task<string> CallProductApplyDiscountAsync()
        {
            return PostAfterInitializeAsync(
                """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"action_product_applydiscount","arguments":{"key":1,"percentage":15}}}""");
        }

        /// <summary>
        /// Lists tools on <c>/odata/mcp</c>.
        /// </summary>
        public Task<string> ListToolsAsync()
        {
            return PostAfterInitializeAsync(
                """{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}""");
        }

        /// <summary>
        /// Runs list, query, and a named action against a running sample host.
        /// </summary>
        public async Task RunAllAsync()
        {
            Console.WriteLine(await ListToolsAsync());
            Console.WriteLine(await CallListCustomersAsync());
            Console.WriteLine(await CallProductApplyDiscountAsync());
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Builds JSON content for an MCP POST.
        /// </summary>
        /// <param name="json">The JSON payload.</param>
        /// <returns>
        /// The content.
        /// </returns>
        internal static StringContent JsonContent(string json)
        {
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        /// <summary>
        /// Initializes the Streamable HTTP session, then posts <paramref name="json"/>.
        /// </summary>
        /// <param name="json">A JSON-RPC request body.</param>
        /// <returns>
        /// The response body.
        /// </returns>
        internal async Task<string> PostAfterInitializeAsync(string json)
        {
            using var initialize = JsonContent(
                """{"jsonrpc":"2.0","id":"init","method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"odata-mcp-sample","version":"1.0"}}}""");
            using var initialized = await _httpClient.PostAsync(McpPath, initialize);
            initialized.EnsureSuccessStatusCode();

            using var request = new HttpRequestMessage(HttpMethod.Post, McpPath)
            {
                Content = JsonContent(json)
            };
            if (initialized.Headers.TryGetValues("mcp-session-id", out var values))
            {
                request.Headers.TryAddWithoutValidation("mcp-session-id", string.Join(",", values));
            }

            using var response = await _httpClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }

        #endregion

    }

}
