// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
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
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.CatalogShapes
{

    /// <summary>
    /// In-process host with functions and actions and no entity sets.
    /// </summary>
    [TestClass]
    public class OperationsOnlyHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds an operations-only MCP host.
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
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetOperationsOnlyModel());
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
        /// Operations-only catalogs advertise call/list tools and no named CRUD families.
        /// </summary>
        [TestMethod]
        public async Task OperationsOnly_ListsDeclaredOperations()
        {
            var session = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"];
            session.Catalog.Tools.Where(tool => tool.EntitySetName is not null).Should().BeEmpty();
            session.Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_call");

            var result = await session.Runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);
            result.IsError.Should().BeFalse();
            result.StructuredContent.Should().Contain("MostValuable").And.Contain("Reset").And.Contain("GetStatus");
        }

        #endregion

    }

}
