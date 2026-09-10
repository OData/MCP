// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.AspNetCore.Restier;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.Restier.Breakdance;
using Microsoft.Restier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Restier
{

    /// <summary>
    /// Proves the secured OData 7 Restier host this project's linked suite runs against is genuinely an OAuth
    /// protected resource: the Restier route answers <c>401</c> with an RFC 9728 challenge unless a token the
    /// local authorization server signed is attached.
    /// </summary>
    /// <remarks>
    /// This class wires its own host rather than deriving from the swapped <c>RestierToolTestBase</c>, because
    /// it is asserting on the unauthenticated answers that base class exists to make impossible. It also proves
    /// the startup filter alone is enough: nothing here adds <c>UseAuthentication</c> to Restier's pipeline.
    /// </remarks>
    [TestClass]
    public class SecuredRestierHostTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        /// <summary>
        /// The name of the in-memory EF Core database backing this test class's API instance.
        /// </summary>
        internal readonly string _databaseName = "SecuredRestierHost-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Properties

        /// <summary>
        /// Gets the authorization server protecting the host, or <see langword="null"/> before setup.
        /// </summary>
        internal LocalAuthorizationServer? AuthorizationServer { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecuredRestierHostTests"/> class using endpoint routing.
        /// </summary>
        public SecuredRestierHostTests()
            : base(useEndpointRouting: true)
        {
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
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Builds the secured Restier host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            AuthorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSecuredResource(AuthorizationServer);
                services.AddODataMcp();
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host and the authorization server.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
            AuthorizationServer?.Dispose();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// A token the authorization server signed reaches the real Restier API and its seeded data.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Customers_WithToken_200_Contoso()
        {
            using var client = CreateAuthenticatedClient();

            using var response = await client.GetAsync("odata/Customers");
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// A token the authorization server signed unlocks the CSDL document.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Metadata_WithToken_200()
        {
            using var client = CreateAuthenticatedClient();

            using var response = await client.GetAsync("odata/$metadata");
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotBeNullOrWhiteSpace();
            body.Should().Contain("EntitySet");
        }

        /// <summary>
        /// An unauthenticated <c>$metadata</c> GET is the request the outbound client's whole discovery chain
        /// hangs off, so it must answer <c>401</c> and point at the protected resource metadata document.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Metadata_WithoutToken_401_WithChallenge()
        {
            using var client = TestServer.CreateClient();

            using var response = await client.GetAsync("odata/$metadata");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.ToString().Should().Contain("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/odata\"");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates a client carrying a bearer token this host's authorization server signed.
        /// </summary>
        /// <returns>
        /// The client; the caller disposes it.
        /// </returns>
        internal HttpClient CreateAuthenticatedClient()
        {
            var client = TestServer.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                ODataMcpAuthConstants.BearerScheme,
                AuthorizationServer!.IssueAccessToken("read write"));

            return client;
        }

        #endregion

    }

}
