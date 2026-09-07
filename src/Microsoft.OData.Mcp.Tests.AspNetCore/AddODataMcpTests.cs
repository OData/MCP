// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore
{

    /// <summary>
    /// Host tests for <c>AddODataMcp</c> against OData 8 conventional routes.
    /// </summary>
    [TestClass]
    public class AddODataMcpTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a convention OData host with MCP on every route.
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
                        options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                        options.AddRouteComponents("internal", TestModels.GetMinimalModel());
                    });

                services.AddODataMcp();
            });

            AddMinimalMvc();
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
        /// Every route component gets a catalog that includes odata_query and omits shutdown_server.
        /// </summary>
        [TestMethod]
        public void AddODataMcp_Catalog_ContainsGenericQuery_OmitsShutdown()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();

            factory.Sessions.Keys.Should().Contain("odata");
            factory.Sessions.Keys.Should().Contain("internal");
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().NotContain("shutdown_server");
            factory.Sessions["internal"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_customers");
        }

        /// <summary>
        /// Catalog configuration is applied to every route.
        /// </summary>
        [TestMethod]
        public void AddODataMcp_ConfigureCatalog_LimitsNamedTools()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxNamedTools = 10;
                options.Catalog.IncludeEntitySets.Add("Customers");
            });

            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<ODataMcpSessionFactory>();

            factory.Sessions["odata"].Catalog.Tools.Should().OnlyContain(tool => tool.Name.StartsWith("odata_", StringComparison.Ordinal));
        }

        /// <summary>
        /// IncludePrefixes keeps only the listed prefix.
        /// </summary>
        [TestMethod]
        public void AddODataMcp_IncludePrefixes_DropsUnlistedRoute()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                    options.AddRouteComponents("internal", TestModels.GetMinimalModel());
                });
            services.AddODataMcp(options =>
            {
                options.IncludePrefixes.Add("odata");
            });

            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<ODataMcpSessionFactory>();

            factory.Sessions.Keys.Should().Equal("odata");
        }

        /// <summary>
        /// ExcludeRoutes hides a discovered prefix.
        /// </summary>
        [TestMethod]
        public void AddODataMcp_ExcludeRoutes_DropsListedRoute()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                    options.AddRouteComponents("internal", TestModels.GetMinimalModel());
                });
            services.AddODataMcp(options =>
            {
                options.ExcludeRoutes.Add("internal");
            });

            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<ODataMcpSessionFactory>();

            factory.Sessions.Keys.Should().Contain("odata");
            factory.Sessions.Keys.Should().NotContain("internal");
        }

        /// <summary>
        /// Explicit AddRoute is enough when no OData options exist.
        /// </summary>
        [TestMethod]
        public void AddODataMcp_ExplicitRoute_CreatesSession()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddODataMcp(options =>
            {
                options.AddRoute("manual", TestModels.GetMinimalModel());
            });

            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<ODataMcpSessionFactory>();

            factory.Sessions.Keys.Should().Equal("manual");
            factory.Sessions["manual"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
        }

        #endregion

    }

}
