// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// The authenticated twin of the in-process rich convention host: the same OData 8 model, controllers, and
    /// MCP registration, but every route under <c>/odata</c> demands a bearer token from a real
    /// <see cref="LocalAuthorizationServer"/>, and the tools run through a real <c>odata-mcp start</c> process
    /// rather than in process.
    /// </summary>
    /// <example>
    /// <code>
    /// [TestClass]
    /// public class MyToolTests : ConventionRichHost
    /// {
    ///     [TestMethod]
    ///     public async Task Query_ReturnsCustomers()
    ///     {
    ///         var result = await InvokeAsync("odata_query", ToolArguments.From(("entitySet", "Customers")));
    ///
    ///         result.IsError.Should().BeFalse();
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// This class deliberately carries the same fully qualified name and member surface as
    /// <c>Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures.ConventionRichHost</c> in the in-process suite, so the
    /// tool-test files linked into this project compile against it unchanged. Per
    /// <c>specs/v3/TESTING.md</c> §8.5 a linked test is never edited to pass under authentication; this class,
    /// <see cref="OutboundToolsHostFixture"/>, or the product is what changes.
    /// <para>
    /// <see cref="HostCatalogOptions"/> is what carries a subclass's <c>AddODataMcp(o =&gt; o.Catalog…)</c> into
    /// the outbound session: the ASP.NET Core host still owns the options, and the fixture builds its catalog
    /// from the very same object.
    /// </para>
    /// </remarks>
    public abstract class ConventionRichHost : AspNetCoreBreakdanceTestBase
    {

        #region Properties

        /// <summary>
        /// Gets the authorization server protecting this host, or <see langword="null"/> before setup.
        /// </summary>
        internal LocalAuthorizationServer? AuthorizationServer { get; set; }

        /// <summary>
        /// Gets the Tools host fixture that owns the outbound session, or <see langword="null"/> before setup.
        /// </summary>
        internal OutboundToolsHostFixture? Outbound { get; set; }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Builds the secured rich convention host and the outbound Tools host that talks to it.
        /// </summary>
        /// <remarks>
        /// <see cref="MetadataController"/> is added as an application part after
        /// <see cref="ConfigureServices(IServiceCollection)"/> has run, because a subclass that replaces that
        /// method outright would otherwise leave <c>/odata/$metadata</c> unrouted — and the CLI builds its
        /// entire catalog from that one document, so the host would have nothing to serve tools from. The
        /// in-process suite never needed it: its catalog comes from the EDM model in memory.
        /// </remarks>
        [TestInitialize]
        public void Setup()
        {
            AuthorizationServer = new LocalAuthorizationServer(CreateAuthorizationServerOptions());
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSecuredResource(AuthorizationServer);
                ConfigureServices(services);
                services.AddMvcCore().AddApplicationPart(typeof(MetadataController).Assembly);
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

            Outbound = new OutboundToolsHostFixture(AuthorizationServer, TestServer, new Uri("http://localhost/odata/"));
            Outbound.Options = CreateOutboundOptions();
            Outbound.CreateSessionAsync(HostCatalogOptions(), CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Tears down the outbound host, the secured host, and the authorization server.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            Outbound?.Dispose();
            TestTearDown();
            AuthorizationServer?.Dispose();
        }

        #endregion

        #region Internal Methods

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
        /// Creates a capturing runtime over the outbound executor.
        /// </summary>
        /// <returns>
        /// Runtime and capture wrapper.
        /// </returns>
        /// <remarks>
        /// Unlike the in-process twin this builds no executor: the outbound session is already wrapped in a
        /// <see cref="CapturingODataExecutor"/>, so handing back its own runtime is what proves a tool call
        /// travelled the authenticating <c>"OData"</c> client rather than a second, unauthenticated one.
        /// <para>
        /// The ambient <see cref="IHttpContextAccessor"/> is still seeded, because the in-process twin seeds it
        /// here and linked tests reach for <c>HttpContext</c> straight afterwards to stamp headers on it.
        /// Nothing outbound reads it — the token comes from the outbound client — but leaving it
        /// <see langword="null"/> would break a linked test for a reason that has nothing to do with
        /// authentication.
        /// </para>
        /// </remarks>
        internal (ODataToolRuntime Runtime, CapturingODataExecutor Capture) CreateCapturingRuntime()
        {
            var accessor = TestServer.Services.GetRequiredService<IHttpContextAccessor>();
            if (accessor.HttpContext is null)
            {
                accessor.HttpContext = new DefaultHttpContext
                {
                    RequestServices = TestServer.Services
                };
                accessor.HttpContext.Request.Scheme = "http";
                accessor.HttpContext.Request.Host = new HostString("localhost");
            }

            return (Session().Runtime, Outbound!.Capture);
        }

        /// <summary>
        /// Builds the authorization server settings this host is protected by. Override to change the token
        /// lifetime, the protected resource metadata mode, or the grants on offer.
        /// </summary>
        /// <returns>
        /// The settings.
        /// </returns>
        internal virtual LocalAuthorizationServerOptions CreateAuthorizationServerOptions()
        {
            return new LocalAuthorizationServerOptions();
        }

        /// <summary>
        /// Creates an HTTP client for direct OData calls against the host under test, carrying a bearer token
        /// the authorization server minted.
        /// </summary>
        /// <returns>
        /// The client.
        /// </returns>
        internal virtual HttpClient CreateClient()
        {
            return Outbound!.CreateClient();
        }

        /// <summary>
        /// Builds the outbound authentication settings the Tools host signs in with. Override to pin a grant,
        /// paste an explicit token, or change the client identifier.
        /// </summary>
        /// <returns>
        /// The settings.
        /// </returns>
        /// <remarks>
        /// The base implementation returns the fixture's own defaults — the seeded public <c>"cli"</c> client,
        /// the <c>read</c> and <c>write</c> scopes, an auto-approving consent presenter, and a throwaway
        /// on-disk token cache — so an override normally mutates and returns that object rather than building a
        /// new one.
        /// </remarks>
        internal virtual OutboundOAuthOptions CreateOutboundOptions()
        {
            return Outbound!.Options;
        }

        /// <summary>
        /// Gets the catalog options the embedding host was configured with, which the outbound session's
        /// catalog is built from.
        /// </summary>
        /// <returns>
        /// The options.
        /// </returns>
        internal ODataMcpCatalogOptions HostCatalogOptions()
        {
            return TestServer.Services.GetRequiredService<IOptions<ODataMcpHostOptions>>().Value.Catalog;
        }

        /// <summary>
        /// Invokes a catalog tool on the outbound session.
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
        /// Gets the outbound MCP session.
        /// </summary>
        /// <returns>
        /// The session.
        /// </returns>
        internal ODataMcpSession Session()
        {
            return Outbound!.Session;
        }

        #endregion

    }

}
