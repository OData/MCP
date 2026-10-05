// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Streamable HTTP tools/list paging on a 200-set catalog.
    /// </summary>
    [TestClass]
    public class ToolsListPagingHostTests : WideModelToolHost
    {

        #region Public Methods

        /// <summary>
        /// The first page continues with a cursor, the next page does not repeat those tools, and every page shares cache hints.
        /// </summary>
        [TestMethod]
        public async Task ToolsList_WideModel_PagesWithCursor_SameCacheScope()
        {
            using var client = CreateClient();
            using var firstResponse = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp");
            var firstBody = await McpJsonRpc.ReadBodyAsync(firstResponse);
            using var first = JsonDocument.Parse(OdataListEntitySetsHostTests.ReadJsonRpcPayload(firstBody));
            var firstResult = first.RootElement.GetProperty("result");
            var firstNames = ReadNames(firstResult);
            var cursor = firstResult.GetProperty("nextCursor").GetString();

            firstNames.Should().NotBeEmpty();
            firstNames.Should().Contain("odata_query");
            firstNames.Should().NotContain("list_rows199");
            cursor.Should().NotBeNullOrWhiteSpace();
            firstResult.GetProperty("ttlMs").GetInt32().Should().Be(300_000);
            firstResult.GetProperty("cacheScope").GetString().Should().Be("public");
            firstResult.GetProperty("resultType").GetString().Should().Be("complete");

            using var secondResponse = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp", cursor);
            var secondBody = await McpJsonRpc.ReadBodyAsync(secondResponse);
            using var second = JsonDocument.Parse(OdataListEntitySetsHostTests.ReadJsonRpcPayload(secondBody));
            var secondResult = second.RootElement.GetProperty("result");
            var secondNames = ReadNames(secondResult);

            secondNames.Should().NotBeEmpty();
            secondNames.Should().NotIntersectWith(firstNames);
            secondResult.GetProperty("ttlMs").GetInt32().Should().Be(firstResult.GetProperty("ttlMs").GetInt32());
            secondResult.GetProperty("cacheScope").GetString().Should().Be("public");
            secondResult.GetProperty("resultType").GetString().Should().Be("complete");
        }

        /// <summary>
        /// An empty cursor and a cursor this server did not issue are JSON-RPC -32602.
        /// </summary>
        [TestMethod]
        public async Task ToolsList_InvalidCursor_IsInvalidParams()
        {
            using var client = CreateClient();
            using var empty = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp", "");
            var emptyBody = await McpJsonRpc.ReadBodyAsync(empty);
            using var emptyDocument = JsonDocument.Parse(OdataListEntitySetsHostTests.ReadJsonRpcPayload(emptyBody));
            emptyDocument.RootElement.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);

            using var garbage = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp", "not-a-cursor");
            var garbageBody = await McpJsonRpc.ReadBodyAsync(garbage);
            using var garbageDocument = JsonDocument.Parse(OdataListEntitySetsHostTests.ReadJsonRpcPayload(garbageBody));
            garbageDocument.RootElement.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Reads tool names from a tools/list result.
        /// </summary>
        /// <param name="result">The result object.</param>
        /// <returns>
        /// The names, in response order.
        /// </returns>
        private static string[] ReadNames(JsonElement result)
        {
            return [.. result.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!)];
        }

        #endregion

    }

}
