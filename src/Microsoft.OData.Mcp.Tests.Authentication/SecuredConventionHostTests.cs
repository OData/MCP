// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.AspNetCore;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication
{

    /// <summary>
    /// Proves the secured OData 8 convention host this project's linked suite runs against is genuinely an
    /// OAuth protected resource: every route beneath <c>/odata</c> — <c>$metadata</c> and the hop-1 MCP
    /// endpoint included — answers <c>401</c> with an RFC 9728 challenge unless a token the local authorization
    /// server signed is attached.
    /// </summary>
    /// <remarks>
    /// This class wires its own host rather than deriving from the swapped
    /// <c>ConventionRichHost</c>, because it is asserting on the unauthenticated answers that base class exists
    /// to make impossible.
    /// </remarks>
    [TestClass]
    public class SecuredConventionHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Properties

        /// <summary>
        /// Gets the authorization server protecting the host, or <see langword="null"/> before setup.
        /// </summary>
        internal LocalAuthorizationServer? AuthorizationServer { get; set; }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Builds the secured rich convention host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            AuthorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSecuredResource(AuthorizationServer);
                services.AddSingleton<CustomerStore>();
                services.AddSingleton<DuplicatesStore>();
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddApplicationPart(typeof(MetadataController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetRichModel());
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
        /// An entity set read carries the same protection as <c>$metadata</c>.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Customers_WithoutToken_401()
        {
            using var client = TestServer.CreateClient();

            using var response = await client.GetAsync("odata/Customers");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// A token the authorization server signed reaches the real controller and its seeded data.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Customers_WithToken_200_ContainsContoso()
        {
            using var client = CreateAuthenticatedClient();

            using var response = await client.GetAsync("odata/Customers");
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// The hop-1 MCP endpoint lives beneath <c>/odata</c> and is protected by the same middleware, so a
        /// linked test's JSON-RPC call needs the same bearer the tool calls do.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Mcp_WithoutToken_401()
        {
            using var client = TestServer.CreateClient();
            using var content = new StringContent("""{"jsonrpc":"2.0","id":"1","method":"tools/list"}""");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await client.PostAsync("odata/mcp", content);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// A token the authorization server signed unlocks the CSDL document.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Metadata_WithAsToken_200_Xml()
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
        public async Task Metadata_WithoutToken_401_WithResourceMetadataChallenge()
        {
            using var client = TestServer.CreateClient();

            using var response = await client.GetAsync("odata/$metadata");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.ToString().Should().Contain("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/odata\"");
        }

        /// <summary>
        /// A token minted by a different authorization server instance carries a different signing key, so the
        /// resource rejects it rather than trusting any well-formed JWT.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task ForeignToken_401()
        {
            using var other = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var client = TestServer.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                ODataMcpAuthConstants.BearerScheme,
                other.IssueAccessToken("read write"));

            using var response = await client.GetAsync("odata/$metadata");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
