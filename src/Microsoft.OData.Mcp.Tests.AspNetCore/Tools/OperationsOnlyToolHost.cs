// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Operations-only convention host for list/call tool tests.
    /// </summary>
    public abstract class OperationsOnlyToolHost : AspNetCoreBreakdanceTestBase
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
                    .AddApplicationPart(typeof(CustomersController).Assembly)
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

        #region Internal Methods

        /// <summary>
        /// Creates a capturing runtime over the real in-process executor.
        /// </summary>
        /// <returns>
        /// Runtime and capture wrapper.
        /// </returns>
        internal (ODataToolRuntime Runtime, CapturingODataExecutor Capture) CreateCapturingRuntime()
        {
            var session = Session();
            var accessor = TestServer.Services.GetRequiredService<McpHttpContextAccessor>();
            if (accessor.HttpContext is null)
            {
                accessor.HttpContext = new DefaultHttpContext
                {
                    RequestServices = TestServer.Services
                };
            }

            var inner = new InProcessODataExecutor(
                TestServer.Services.GetRequiredService<ODataMcpPipeline>(),
                accessor,
                TestServer.Services.GetRequiredService<IServiceScopeFactory>(),
                "odata");
            var capture = new CapturingODataExecutor(inner);

            return (new ODataToolRuntime(session.Catalog, capture), capture);
        }

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

        /// <summary>
        /// Invokes a catalog tool on the odata session.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal Task<ODataToolInvocationResult> InvokeAsync(string name, IEnumerable<KeyValuePair<string, JsonElement>>? arguments = null)
        {
            return Session().Runtime.InvokeAsync(name, arguments, CancellationToken.None);
        }

        /// <summary>
        /// Gets the odata MCP session.
        /// </summary>
        /// <returns>
        /// The session.
        /// </returns>
        internal ODataMcpSession Session()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"];
        }

        #endregion

    }

}
