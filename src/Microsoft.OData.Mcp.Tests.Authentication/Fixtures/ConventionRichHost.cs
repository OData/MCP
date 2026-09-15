// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
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
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore;
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

        #region Fields

        /// <summary>
        /// One Breakdance + outbound Tools host per derived test class.
        /// </summary>
        internal static readonly ConcurrentDictionary<Type, OutboundConventionHostLease> HostLeases = new();

        /// <summary>
        /// A per-method host, used when the class cannot share a limiter or other process-wide budget.
        /// </summary>
        internal OutboundConventionHostLease? _ephemeralLease;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the authorization server protecting this host, or <see langword="null"/> before setup.
        /// </summary>
        internal LocalAuthorizationServer? AuthorizationServer { get; set; }

        /// <summary>
        /// Gets the Tools host fixture that owns the outbound session, or <see langword="null"/> before setup.
        /// </summary>
        internal OutboundToolsHostFixture? Outbound { get; set; }

        /// <summary>
        /// Gets a value indicating whether this class can keep one outbound host for every method.
        /// </summary>
        /// <remarks>
        /// Rate-limit suites spend a partition budget; sharing the host makes the second method start already 429.
        /// Linked tests are not edited to say so — the class name is the signal.
        /// </remarks>
        internal bool ReusesHostPerClass
        {
            get
            {
                return GetType().Name.IndexOf("RateLimit", StringComparison.OrdinalIgnoreCase) < 0;
            }
        }

        /// <summary>
        /// Gets the Breakdance <c>TestServer</c> the class host owns, even when this instance did not build it.
        /// </summary>
        internal Microsoft.AspNetCore.TestHost.TestServer SharedTestServer
        {
            get
            {
                if (_ephemeralLease is not null)
                {
                    return _ephemeralLease.Owner.TestServer;
                }

                return HostLeases.TryGetValue(GetType(), out var lease) ? lease.Owner.TestServer : TestServer;
            }
        }

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
            var lease = ReusesHostPerClass
                ? HostLeases.GetOrAdd(GetType(), _ => CreateHostLease())
                : _ephemeralLease = CreateHostLease();
            AuthorizationServer = lease.AuthorizationServer;
            Outbound = lease.Outbound;
            lease.ResetStores();
            lease.Outbound.Capture.Clear();
        }

        /// <summary>
        /// Disposes a per-method host. Class-scoped hosts live until <see cref="DisposeHostLeases"/>.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            _ephemeralLease?.Dispose();
            _ephemeralLease = null;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Marks the outer MCP context as authenticated so in-process CUD copies <see cref="HttpContext.User"/>.
        /// </summary>
        /// <remarks>
        /// Linked in-process tests call this. Outbound already sends a bearer token; this still seeds
        /// <see cref="IHttpContextAccessor"/> so those tests can stamp headers on <c>HttpContext</c>.
        /// </remarks>
        internal void Authenticate()
        {
            var accessor = SharedTestServer.Services.GetRequiredService<IHttpContextAccessor>();
            if (accessor.HttpContext is null)
            {
                accessor.HttpContext = new DefaultHttpContext
                {
                    RequestServices = SharedTestServer.Services
                };
                accessor.HttpContext.Request.Scheme = "http";
                accessor.HttpContext.Request.Host = new HostString("localhost");
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
            var accessor = SharedTestServer.Services.GetRequiredService<IHttpContextAccessor>();
            if (accessor.HttpContext is null)
            {
                accessor.HttpContext = new DefaultHttpContext
                {
                    RequestServices = SharedTestServer.Services
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
        /// Builds the secured convention host and the outbound Tools session for this derived class.
        /// </summary>
        /// <returns>
        /// The lease the rest of the class shares.
        /// </returns>
        internal OutboundConventionHostLease CreateHostLease()
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

            return new OutboundConventionHostLease(this, AuthorizationServer, Outbound);
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
        /// Disposes every shared convention + outbound host this assembly still holds.
        /// </summary>
        internal static void DisposeHostLeases()
        {
            foreach (var type in HostLeases.Keys)
            {
                if (HostLeases.TryRemove(type, out var lease))
                {
                    lease.Dispose();
                }
            }
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
            return SharedTestServer.Services.GetRequiredService<IOptions<ODataMcpHostOptions>>().Value.Catalog;
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
        /// Restores in-memory stores so write tests cannot leak into the next method.
        /// </summary>
        internal void ResetStores()
        {
            SharedTestServer.Services.GetService<CustomerStore>()?.Reset();
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

    /// <summary>
    /// The Breakdance convention host and outbound Tools session one derived test class shares.
    /// </summary>
    internal sealed class OutboundConventionHostLease : IDisposable
    {

        #region Properties

        /// <summary>
        /// Gets the authorization server protecting the host.
        /// </summary>
        internal LocalAuthorizationServer AuthorizationServer { get; }

        /// <summary>
        /// Gets the outbound Tools fixture.
        /// </summary>
        internal OutboundToolsHostFixture Outbound { get; }

        /// <summary>
        /// Gets the Breakdance instance that owns the <c>TestServer</c>.
        /// </summary>
        internal ConventionRichHost Owner { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundConventionHostLease"/> class.
        /// </summary>
        /// <param name="owner">The Breakdance instance whose <c>TestSetup</c> built the host.</param>
        /// <param name="authorizationServer">The authorization server protecting the host.</param>
        /// <param name="outbound">The outbound Tools fixture.</param>
        internal OutboundConventionHostLease(ConventionRichHost owner, LocalAuthorizationServer authorizationServer, OutboundToolsHostFixture outbound)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(authorizationServer);
            ArgumentNullException.ThrowIfNull(outbound);

            Owner = owner;
            AuthorizationServer = authorizationServer;
            Outbound = outbound;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public void Dispose()
        {
            Outbound.Dispose();
            Owner.TestTearDown();
            AuthorizationServer.Dispose();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Restores in-memory stores so write tests cannot leak into the next method.
        /// </summary>
        internal void ResetStores()
        {
            Owner.ResetStores();
        }

        #endregion

    }

}
