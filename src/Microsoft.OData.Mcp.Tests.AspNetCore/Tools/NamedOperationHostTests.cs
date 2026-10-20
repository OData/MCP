// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Named <c>action_*</c> / <c>function_*</c> tools on the operations-only convention host.
    /// </summary>
    [TestClass]
    public class NamedOperationHostTests : OperationsOnlyToolHost
    {

        #region Public Methods

        /// <summary>
        /// Named Reset POSTs the same path as generic <c>odata_call</c>.
        /// </summary>
        [TestMethod]
        public async Task Action_Reset_MatchesODataCall()
        {
            var named = await InvokeAsync("action_reset");
            var generic = await InvokeAsync("odata_call", ToolArguments.Of("name", "Reset"));

            named.IsError.Should().BeFalse(named.Text);
            generic.IsError.Should().BeFalse(generic.Text);
        }

        /// <summary>
        /// Operations-only catalogs advertise unbound named tools after the generics.
        /// </summary>
        [TestMethod]
        public void Catalog_AdvertisesNamedUnboundTools()
        {
            var names = Session().Catalog.Tools.Select(tool => tool.Name).ToList();
            var call = names.IndexOf("odata_call");
            var status = names.IndexOf("function_getstatus");
            var most = names.IndexOf("function_mostvaluable");
            var reset = names.IndexOf("action_reset");

            names.Should().Contain("function_getstatus");
            names.Should().Contain("function_mostvaluable");
            names.Should().Contain("action_reset");
            status.Should().Be(call + 1);
            most.Should().Be(status + 1);
            reset.Should().Be(most + 1);
        }

        /// <summary>
        /// Named GetStatus matches generic <c>odata_call</c>.
        /// </summary>
        [TestMethod]
        public async Task Function_GetStatus_MatchesODataCall()
        {
            var named = await InvokeAsync("function_getstatus", ToolArguments.Of("code", "open"));
            var generic = await InvokeAsync("odata_call", ToolArguments.Of("name", "GetStatus", "parameters", new { code = "open" }));

            named.IsError.Should().BeFalse(named.Text);
            generic.IsError.Should().BeFalse(generic.Text);
            named.StructuredContent.Should().Be(generic.StructuredContent);
        }

        /// <summary>
        /// JSON-RPC <c>tools/call</c> invokes a named unbound function.
        /// </summary>
        [TestMethod]
        public async Task Function_MostValuable_JsonRpcToolsCall()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "function_mostvaluable", "{}");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.IsSuccessStatusCode.Should().BeTrue(body);
            body.Should().Contain("42");
        }

        /// <summary>
        /// Named MostValuable matches generic <c>odata_call</c>.
        /// </summary>
        [TestMethod]
        public async Task Function_MostValuable_MatchesODataCall()
        {
            var named = await InvokeAsync("function_mostvaluable");
            var generic = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));

            named.IsError.Should().BeFalse(named.Text);
            generic.IsError.Should().BeFalse(generic.Text);
            named.StructuredContent.Should().Contain("42");
            named.StructuredContent.Should().Be(generic.StructuredContent);
        }

        /// <summary>
        /// JSON-RPC <c>tools/list</c> contains the named unbound operations.
        /// </summary>
        [TestMethod]
        public async Task ToolsList_JsonRpc_ContainsNamedOperations()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.ListToolsAsync(client, "/odata/mcp");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.IsSuccessStatusCode.Should().BeTrue(body);
            body.Should().Contain("function_getstatus");
            body.Should().Contain("function_mostvaluable");
            body.Should().Contain("action_reset");
            body.Should().Contain("odata_call");
        }

        #endregion

    }

}
