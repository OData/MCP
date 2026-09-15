// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.Restier.Breakdance;
using Microsoft.Restier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// The authenticated twin of the in-process Restier tool test base: the same OData 7 <see cref="McpCustomerApi"/>
    /// host, but every route under <c>/odata</c> demands a bearer token from a real
    /// <see cref="LocalAuthorizationServer"/>, and the tools run through a real <c>odata-mcp start</c> process
    /// rather than in process.
    /// </summary>
    /// <example>
    /// <code>
    /// public class MyRestierTests : RestierToolTestBase
    /// {
    ///     public MyRestierTests()
    ///         : base("MyRestierTests")
    ///     {
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// This class deliberately carries the same fully qualified name and member surface as
    /// <c>Microsoft.OData.Mcp.Tests.AspNetCore.Restier.RestierToolTestBase</c> in the in-process suite, so the
    /// tool-test files linked into this project compile against it unchanged. Per
    /// <c>specs/v3/TESTING.md</c> §8.5 a linked test is never edited to pass under authentication; this class,
    /// <see cref="OutboundToolsHostFixture"/>, or the product is what changes.
    /// <para>
    /// No <c>UseAuthentication</c> call is added to the pipeline: <c>AddSecuredResource</c> registers a startup
    /// filter that authenticates and challenges ahead of whatever pipeline Restier's Breakdance base builds, so
    /// a host that never calls it stays protected anyway.
    /// </para>
    /// </remarks>
    public abstract class RestierToolTestBase : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        /// <summary>
        /// One Breakdance + outbound Tools host per derived test class.
        /// </summary>
        internal static readonly ConcurrentDictionary<Type, OutboundRestierHostLease> HostLeases = new();

        /// <summary>
        /// The name of the in-memory EF Core database backing this test class's <see cref="McpCustomerApi"/> instance.
        /// </summary>
        internal readonly string _databaseName;

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
        /// Gets the Breakdance <c>TestServer</c> the class host owns, even when this instance did not build it.
        /// </summary>
        internal Microsoft.AspNetCore.TestHost.TestServer SharedTestServer
        {
            get
            {
                return HostLeases.TryGetValue(GetType(), out var lease) ? lease.Owner.TestServer : TestServer;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes Restier endpoint routing for <see cref="McpCustomerApi"/> with a uniquely named in-memory database.
        /// </summary>
        /// <param name="databasePrefix">The prefix used to build the in-memory database name, combined with a new GUID.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="databasePrefix"/> is <see langword="null"/>, empty, or whitespace.</exception>
        protected RestierToolTestBase(string databasePrefix)
            : base(useEndpointRouting: true)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePrefix);

            _databaseName = databasePrefix + "-" + Guid.NewGuid().ToString("N");

            AddRestierAction = apiBuilder =>
            {
                apiBuilder.AddRestierApi<McpCustomerApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpCustomerContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_databaseName);
                    });
                    RestierTestSeed.EnsureCustomers(restierServices);
                });
            };

            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
            };

            ApplicationBuilderLastAction = app =>
            {
                app.UseODataMcp();
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attaches this test method to the class's shared secured Restier host and outbound Tools session.
        /// </summary>
        /// <remarks>
        /// Breakdance's <c>TestSetup</c> and <see cref="OutboundToolsHostFixture.CreateSessionAsync"/> run the
        /// full OAuth handshake. Doing that on every method is what made this suite take minutes per TFM.
        /// The host is built once per derived class; the customer seed is reset so write tests cannot leak.
        /// </remarks>
        [TestInitialize]
        public void Setup()
        {
            var lease = HostLeases.GetOrAdd(GetType(), _ => CreateHostLease());
            AuthorizationServer = lease.AuthorizationServer;
            Outbound = lease.Outbound;
            lease.ResetCustomerStore();
            lease.Outbound.Capture.Clear();
        }

        /// <summary>
        /// Intentionally empty: the class host is disposed in <see cref="DisposeHostLeases"/>.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets the MCP catalog for the Restier <c>odata</c> prefix.
        /// </summary>
        /// <returns>
        /// The catalog.
        /// </returns>
        internal ODataMcpCatalog Catalog()
        {
            return Session().Catalog;
        }

        /// <summary>
        /// Applies test-specific <see cref="ODataMcpHostOptions"/>. The base implementation is a no-op; override
        /// to set options such as <see cref="ODataMcpCatalogOptions.MaxNamedTools"/>.
        /// </summary>
        /// <param name="options">The host options to configure.</param>
        internal virtual void ConfigureODataMcp(ODataMcpHostOptions options)
        {
        }

        /// <summary>
        /// Registers MCP services for the test host. Override to add services (for example rate limiting) before
        /// calling the base implementation.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        internal virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddODataMcp(ConfigureODataMcp);
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
        /// Creates an <see cref="HttpClient"/> against the secured Restier host, carrying a bearer token the
        /// authorization server minted.
        /// </summary>
        /// <returns>
        /// The HTTP client.
        /// </returns>
        internal HttpClient CreateClient()
        {
            return Outbound!.CreateClient();
        }

        /// <summary>
        /// Builds the secured Restier host and the outbound Tools session for this derived class.
        /// </summary>
        /// <returns>
        /// The lease the rest of the class shares.
        /// </returns>
        internal OutboundRestierHostLease CreateHostLease()
        {
            AuthorizationServer = new LocalAuthorizationServer(CreateAuthorizationServerOptions());
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSecuredResource(AuthorizationServer);
                ConfigureServices(services);
            });
            TestSetup();

            Outbound = new OutboundToolsHostFixture(AuthorizationServer, TestServer, new Uri("http://localhost/odata/"));
            Outbound.Options = CreateOutboundOptions();
            Outbound.CreateSessionAsync(HostCatalogOptions(), CancellationToken.None).GetAwaiter().GetResult();

            return new OutboundRestierHostLease(this, AuthorizationServer, Outbound);
        }

        /// <summary>
        /// Builds the outbound authentication settings the Tools host signs in with. Override to pin a grant,
        /// paste an explicit token, or change the client identifier.
        /// </summary>
        /// <returns>
        /// The settings.
        /// </returns>
        internal virtual OutboundOAuthOptions CreateOutboundOptions()
        {
            return Outbound!.Options;
        }

        /// <summary>
        /// Disposes every shared Restier + outbound host this assembly still holds.
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
        /// Restores the two-row customer seed on the shared in-memory database.
        /// </summary>
        internal void ResetCustomerStore()
        {
            var container = GetScopedRequestContainer("odata", useEndpointRouting: true);
            try
            {
                RestierTestSeed.ResetCustomers(container.GetRequiredService<McpCustomerContext>());
            }
            finally
            {
                (container as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Gets the MCP runtime for the Restier <c>odata</c> prefix.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal ODataToolRuntime Runtime()
        {
            return Session().Runtime;
        }

        /// <summary>
        /// Gets the outbound MCP session.
        /// </summary>
        /// <returns>
        /// The session.
        /// </returns>
        internal virtual ODataMcpSession Session()
        {
            return Outbound!.Session;
        }

        #endregion

    }

    /// <summary>
    /// The Breakdance Restier host and outbound Tools session one derived test class shares.
    /// </summary>
    internal sealed class OutboundRestierHostLease : IDisposable
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
        /// Gets the Breakdance instance that owns <see cref="RestierBreakdanceTestBase{TApi}.TestServer"/>.
        /// </summary>
        internal RestierToolTestBase Owner { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundRestierHostLease"/> class.
        /// </summary>
        /// <param name="owner">The Breakdance instance whose <c>TestSetup</c> built the host.</param>
        /// <param name="authorizationServer">The authorization server protecting the host.</param>
        /// <param name="outbound">The outbound Tools fixture.</param>
        internal OutboundRestierHostLease(RestierToolTestBase owner, LocalAuthorizationServer authorizationServer, OutboundToolsHostFixture outbound)
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
        /// Restores the two-row customer seed on the shared in-memory database.
        /// </summary>
        internal void ResetCustomerStore()
        {
            Owner.ResetCustomerStore();
        }

        #endregion

    }

}
