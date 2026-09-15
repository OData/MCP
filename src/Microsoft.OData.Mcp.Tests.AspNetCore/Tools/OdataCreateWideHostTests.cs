// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Generic create remains available when named families do not fit the cap.
    /// </summary>
    [TestClass]
    public class OdataCreateWideHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a 200-set host with a small named-tool cap.
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
                        options.AddRouteComponents("odata", TestModels.GetWideModel(200));
                    });
                services.AddODataMcp(options =>
                {
                    options.Catalog.MaxNamedTools = 10;
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
        /// Generic <c>odata_create</c> is advertised and can target an unnamed set.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_WideModel_CreateOnUnnamedSetViaGenericStillWorks()
        {
            var session = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"];
            session.Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_create");
            session.Catalog.Tools.Should().NotContain(tool => tool.Name.StartsWith("create_", StringComparison.Ordinal));

            var result = await session.Runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Rows000", "body", """{"Id":1}"""),
                CancellationToken.None);
            result.IsError.Should().BeTrue();
            result.Text.Should().NotContain("Unknown tool");
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
