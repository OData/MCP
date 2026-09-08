// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.Restier.Breakdance;
using Microsoft.Restier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Shared host wiring for Restier tool tests against <see cref="McpCustomerApi"/>. Every test class in this
    /// suite derives from this base so it reaches the host only through <see cref="Session"/>, <see cref="Runtime"/>,
    /// <see cref="Catalog"/>, and <see cref="CreateClient"/>, never <c>TestServer.CreateClient()</c> directly or an
    /// inline <see cref="ODataMcpSessionFactory"/> lookup.
    /// </summary>
    /// <remarks>
    /// This member surface is normative: an authenticated test project mirrors it with a same-fully-qualified-name
    /// class so the linked tool-test files compile unchanged against a secured host.
    /// </remarks>
    public abstract class RestierToolTestBase : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        /// <summary>
        /// The name of the in-memory EF Core database backing this test class's <see cref="McpCustomerApi"/> instance.
        /// </summary>
        internal readonly string _databaseName;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes Restier endpoint routing for <see cref="McpCustomerApi"/> with a uniquely named in-memory database.
        /// </summary>
        /// <param name="databasePrefix">The prefix used to build the in-memory database name, combined with a new GUID.</param>
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
        protected RestierToolTestBase(string databasePrefix)
            : base(useEndpointRouting: true)
        {
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
        /// Registers MCP through <see cref="ConfigureServices"/> and starts the Restier host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) => ConfigureServices(services));
            TestSetup();
        }

        /// <summary>
        /// Tears down the Restier host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
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
        /// Creates an <see cref="HttpClient"/> against the Restier <see cref="RestierBreakdanceTestBase{T}.TestServer"/>.
        /// </summary>
        /// <returns>
        /// The HTTP client.
        /// </returns>
        internal HttpClient CreateClient()
        {
            return TestServer.CreateClient();
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
        /// Gets the MCP session for the Restier <c>odata</c> prefix.
        /// </summary>
        /// <returns>
        /// The session.
        /// </returns>
        internal virtual ODataMcpSession Session()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"];
        }

        #endregion

    }

}
