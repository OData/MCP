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
    /// Authenticated MCP JSON-RPC tools/call (not empty POST).
    /// </summary>
    [TestClass]
    public class JsonRpcAuthenticatedMcpHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds JWT-required MCP.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            IssuerSigningKey = new SymmetricSecurityKey(TestJwt.SigningKey),
                            ValidateAudience = false,
                            ValidateIssuer = false,
                            ValidateIssuerSigningKey = true,
                            ValidateLifetime = false
                        };
                    });
                services.AddAuthorization();
                services.AddSingleton<CustomerStore>();
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                    });
                services.AddODataMcp(options =>
                {
                    options.RequireAuthorization = true;
                });
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
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

        #region Public Methods

        /// <summary>
        /// Open MCP without token is not 401 — this host requires auth, so 401.
        /// </summary>
        [TestMethod]
        public async Task Auth_RequiredMcp_NoToken_401()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Malformed JWT is 401.
        /// </summary>
        [TestMethod]
        public async Task Auth_RequiredMcp_BadToken_401()
        {
            using var client = TestServer.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Valid JWT tools/call odata_query succeeds.
        /// </summary>
        [TestMethod]
        public async Task Auth_RequiredMcp_GoodToken_ToolsCallQuery_200()
        {
            using var client = TestServer.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken());
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// Valid JWT create forwards Authorization to OData.
        /// </summary>
        [TestMethod]
        public async Task Auth_RequiredMcp_GoodToken_CreateForwardsToOData_OData8()
        {
            using var client = TestServer.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken());
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_create",
                """{"entitySet":"Customers","body":{"CompanyName":"JwtCreate"}}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// MCP 401 is HTTP 401, not a tool IsError payload.
        /// </summary>
        [TestMethod]
        public async Task Auth_Mcp401_IsNotToolIsError()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_query", """{"entitySet":"Customers"}""");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            body.Should().NotContain("OData request failed with status");
        }

        #endregion

    }

}
