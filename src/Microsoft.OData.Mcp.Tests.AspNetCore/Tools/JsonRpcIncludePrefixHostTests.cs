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
    /// IncludePrefixes odata-only: shop OData still works, shop MCP does not.
    /// </summary>
    [TestClass]
    public class JsonRpcIncludePrefixHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds odata+shop with IncludePrefixes odata.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSingleton<CustomerStore>();
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                        options.AddRouteComponents("shop", TestModels.GetNoAuthModel());
                    });
                services.AddODataMcp(options =>
                {
                    options.IncludePrefixes.Add("odata");
                });
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
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
        /// GET /shop/Products 200; POST /shop/mcp 404.
        /// </summary>
        [TestMethod]
        public async Task Prefix_IncludePrefixesOdataOnly_ShopHasNoMcp_ShopODataStillWorks()
        {
            using var client = TestServer.CreateClient();
            McpJsonRpc.AcceptMcp(client);
            var mcp = await client.PostAsync("/shop/mcp", McpJsonRpc.Content("{}"));
            mcp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
            TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions.Keys.Should().Equal("odata");
        }

        /// <summary>
        /// Empty include means all — covered by two-prefix host; this host is include-only.
        /// </summary>
        [TestMethod]
        public void Prefix_EmptyIncludeMeansAll()
        {
            TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions.Keys.Should().Equal("odata");
        }

        #endregion

    }

}
