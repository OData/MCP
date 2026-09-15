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
    /// Wide catalog caps for resources and completions.
    /// </summary>
    [TestClass]
    public class JsonRpcWideCatalogHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a 200-set host.
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
        /// MaxResources 50 including metadata.
        /// </summary>
        [TestMethod]
        public void ResourcesList_MaxResources50_Wide200_CountLeq50IncludingMetadata()
        {
            var catalog = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog;
            catalog.Resources.Count.Should().BeLessThanOrEqualTo(50);
            catalog.Resources[0].Name.Should().Be("$metadata");
        }

        /// <summary>
        /// Completions cap at 50.
        /// </summary>
        [TestMethod]
        public void Complete_EntitySet_EmptyPrefix_DeclaredSetsCappedWide()
        {
            var catalog = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog;
            catalog.CompleteEntitySetNames(string.Empty).Count.Should().Be(50);
        }

        /// <summary>
        /// Two hundred sets do not emit a thousand tools.
        /// </summary>
        [TestMethod]
        public void Cap_TwoHundredSets_DoesNotEmitThousandTools()
        {
            var catalog = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog;
            catalog.Tools.Count.Should().BeLessThanOrEqualTo(150);
        }

        /// <summary>
        /// Default 150 includes generics.
        /// </summary>
        [TestMethod]
        public void Cap_Default150_IncludesGenerics()
        {
            var names = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain("odata_query");
            names.Count.Should().BeLessThanOrEqualTo(150);
        }

        #endregion

    }

}
