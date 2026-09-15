// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// DELETE that requires Authorization, isolated from <see cref="CustomersController"/>.
    /// </summary>
    [TestClass]
    public class OdataDeleteAuthFixtureHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a host whose delete action requires Authorization.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                var builder = new ODataConventionModelBuilder();
                builder.EntitySet<Customer>("AuthDeletes");
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(AuthDeletesController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", builder.GetEdmModel());
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
        /// Unauthenticated delete is 401; forwarding Authorization succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_Unauthorized_401()
        {
            var runtime = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
            var denied = await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "AuthDeletes", "key", "1"),
                CancellationToken.None);
            denied.IsError.Should().BeTrue();
            denied.Text.Should().Contain("status 401");

            var accessor = TestServer.Services.GetRequiredService<McpHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext { RequestServices = TestServer.Services };
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity("test"));
            var allowed = await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "AuthDeletes", "key", "1"),
                CancellationToken.None);
            allowed.IsError.Should().BeFalse(allowed.Text);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates an HTTP client for direct OData calls against the host under test.
        /// </summary>
        /// <returns>
        /// The client.
        /// </returns>
        internal HttpClient CreateClient()
        {
            return TestServer.CreateClient();
        }

        #endregion

    }

}
