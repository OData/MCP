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

}
