// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// <c>UseODataMcp</c> is the on switch for the in-process pipeline capture.
    /// </summary>
    [TestClass]
    public class ODataMcpPipelineTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a convention OData host with MCP turned on.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddOData(options => options.AddRouteComponents("odata", TestModels.GetSimpleModel()));

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
        /// Tears down the test host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// <c>UseODataMcp</c> turns the host on and the filter captures the pipeline at start.
        /// </summary>
        [TestMethod]
        public void UseODataMcp_EnablesAndCapturesPipeline()
        {
            var pipeline = TestServer.Services.GetRequiredService<ODataMcpPipeline>();

            pipeline.IsEnabled.Should().BeTrue();
            pipeline.Pipeline.Should().NotBeNull();
        }

        #endregion

    }

}
