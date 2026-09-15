// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// <c>UseODataMcp</c> discovery from <c>WebApplication</c> data sources versus DI.
    /// </summary>
    [TestClass]
    public class UseODataMcpDiscoveryTests
    {

        #region Public Methods

        /// <summary>
        /// Routes mapped on the <c>WebApplication</c> are discovered without a prior empty
        /// <c>UseEndpoints</c> and without registering the data source in DI.
        /// </summary>
        [TestMethod]
        public void UseODataMcp_ApplicationDataSources_DiscoveredWithoutDi()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddODataMcp();
            var app = builder.Build();
            app.UseRouting();
            var endpoint = ODataMcpRouteDiscoveryTests.CreateEndpoint("odata/{**odataPath}", new FakeODataRoutingMetadata
            {
                Model = TestModels.GetSimpleModel(),
                Prefix = "odata"
            });
            ((IEndpointRouteBuilder)app).DataSources.Add(new StaticEndpointDataSource(endpoint));

            app.UseODataMcp();

            app.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions.Keys.Should().Contain("odata");
        }

        /// <summary>
        /// An application with no OData routes still maps MCP (empty) and does not throw.
        /// </summary>
        [TestMethod]
        public void UseODataMcp_NoODataRoutes_DoesNotThrow()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddODataMcp();
            var app = builder.Build();
            app.UseRouting();

            var act = () => app.UseODataMcp();

            act.Should().NotThrow();
            app.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions.Should().BeEmpty();
        }

        /// <summary>
        /// The no-routes warning names <c>UseODataMcp</c> and the two discovery surfaces.
        /// </summary>
        [TestMethod]
        public void WarnNoODataRoutes_Message_NamesSurfaces()
        {
            ODataMcp_AspNetCore_ApplicationBuilderExtensions.NoODataRoutesWarning.Should().Contain("UseODataMcp");
            ODataMcp_AspNetCore_ApplicationBuilderExtensions.NoODataRoutesWarning.Should().Contain("WebApplication");
            ODataMcp_AspNetCore_ApplicationBuilderExtensions.NoODataRoutesWarning.Should().Contain("DI");
        }

        #endregion

    }

}
