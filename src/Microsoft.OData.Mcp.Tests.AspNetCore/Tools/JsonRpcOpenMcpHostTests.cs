// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Open MCP: query without token 200; create without token is OData 401 through the tool.
    /// </summary>
    [TestClass]
    public class JsonRpcOpenMcpHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Open MCP tools/call query without token is 200.
        /// </summary>
        [TestMethod]
        public async Task Auth_OpenMcp_ToolsCallQueryWithoutToken_200()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// Open MCP create without token is tool IsError 401 (OData still requires Authorization).
        /// </summary>
        [TestMethod]
        public async Task Auth_OpenMcp_CreateWithoutToken_ODataStill401OnCustomersController()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_create",
                """{"entitySet":"Customers","body":{"CompanyName":"OpenCreate"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("401");
        }

        /// <summary>
        /// Open MCP GET customers is anonymous 200.
        /// </summary>
        [TestMethod]
        public async Task Auth_OpenMcp_GetWithoutToken_200()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_get",
                """{"entitySet":"Customers","key":"1"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("Contoso");
        }

        #endregion

    }

}
