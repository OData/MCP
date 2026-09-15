// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// Convention OData 8 TestServer with the rich model, MCP, and real in-process execution.
    /// </summary>
    public abstract class ConventionRichHost : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds the rich convention host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                ConfigureServices(services);
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    ConfigureApp(app);
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
        /// Marks the outer MCP context as authenticated so in-process CUD copies <see cref="HttpContext.User"/>.
        /// </summary>
        internal void Authenticate()
        {
            var accessor = TestServer.Services.GetRequiredService<McpHttpContextAccessor>();
            if (accessor.HttpContext is null)
            {
                accessor.HttpContext = new DefaultHttpContext
                {
                    RequestServices = TestServer.Services
                };
            }

            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity("test"));
        }

        /// <summary>
        /// Creates a capturing runtime and stamps an authenticated user on the current HTTP context.
        /// </summary>
        /// <returns>
        /// Runtime and capture.
        /// </returns>
        internal (ODataToolRuntime Runtime, CapturingODataExecutor Capture) AuthorizedCapture()
        {
            var pair = CreateCapturingRuntime();
            Authenticate();

            return pair;
        }

        /// <summary>
        /// Configures the application pipeline. Override to insert rate limiting.
        /// </summary>
        /// <param name="app">The application builder.</param>
        internal virtual void ConfigureApp(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
            app.UseODataMcp();
        }

        /// <summary>
        /// Configures services. Override to change MCP catalog options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        internal virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<CustomerStore>();
            services.AddSingleton<DuplicatesStore>();
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetRichModel());
                });
            services.AddODataMcp();
        }

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
        /// <remarks>
        /// Tool tests must use this instead of <c>TestServer.CreateClient()</c> so a linked copy of the
        /// test can attach credentials (see <c>specs/v3/TESTING.md</c> §8).
        /// </remarks>
        internal virtual HttpClient CreateClient()
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
