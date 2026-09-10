// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Security
{

    /// <summary>
    /// MCP endpoints with no authentication.
    /// </summary>
    [TestClass]
    public class OpenMcpHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds an unauthenticated MCP host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddOData(options =>
                    {
                        options.AddRouteComponents("odata", TestModels.GetMinimalModel());
                    });
                services.AddODataMcp();
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
        /// Open MCP endpoints are reachable without a bearer token.
        /// </summary>
        [TestMethod]
        public async Task Mcp_WithoutToken_IsNotUnauthorized()
        {
            using var client = TestServer.CreateClient();
            var response = await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject());

            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        #endregion

    }

}
