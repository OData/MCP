// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
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
        /// Registers MCP through <see cref="ConfigureServices"/>, starts the secured Restier host, and builds
        /// the outbound Tools host that talks to it.
        /// </summary>
        [TestInitialize]
        public void Setup()
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
        }

        /// <summary>
        /// Tears down the outbound host, the secured Restier host, and the authorization server.
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

}
