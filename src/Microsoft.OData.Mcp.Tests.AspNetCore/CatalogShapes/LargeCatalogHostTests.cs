// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.CatalogShapes
{

    /// <summary>
    /// In-process host with two hundred entity sets.
    /// </summary>
    [TestClass]
    public class LargeCatalogHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a 200-set MCP host.
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
                services.AddODataMcp();
            });
            AddMinimalMvc();
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
        /// Every entity set gets a named family. Resources and completions stay capped.
        /// </summary>
        [TestMethod]
        public void LargeCatalog_EmitsEveryNamedFamily_ResourcesStayCapped()
        {
            var catalog = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog;

            catalog.Tools.Count.Should().Be(1_011);
            catalog.Tools.Select(tool => tool.Name).Should().Contain("list_rows199");
            catalog.Resources.Count.Should().BeLessThanOrEqualTo(50);
            catalog.CompleteEntitySetNames(string.Empty).Count.Should().BeLessThanOrEqualTo(50);
        }

        #endregion

    }

}
