// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="ProtectedResourceMetadataClient"/> against <see cref="LocalAuthorizationServer"/> and a
    /// tiny purpose-built 5xx server, over real HTTP through the <c>"OAuth"</c> named client with no mocking.
    /// </summary>
    [TestClass]
    public class ProtectedResourceMetadataClientTests
    {

        #region Public Methods

        /// <summary>
        /// A non-loopback <c>http://</c> URL is rejected before any HTTP call is attempted.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_NonLoopbackHttpUri_ThrowsArgumentExceptionWithoutHttpCall()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);

            Func<Task> act = () => client.TryGetAsync(new Uri("http://example.com/.well-known/oauth-protected-resource"), CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
            server.HitCount("prm").Should().Be(0);
        }

        /// <summary>
        /// A 404 response is treated as non-fatal per AUTH-14: the client returns <see langword="null"/>.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_NotFound_ReturnsNull()
        {
            var options = new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.NotFound
            };
            using var server = new LocalAuthorizationServer(options);
            var client = CreateClient(server.Handler);

            var result = await client.TryGetAsync(server.ProtectedResourceMetadataUri, CancellationToken.None);

            result.Should().BeNull();
        }

        /// <summary>
        /// A successful response is parsed into a document whose <c>authorization_servers</c> and
        /// <c>resource</c> fields match what <see cref="LocalAuthorizationServer"/> published.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_Ok_ReturnsMetadataWithAuthorizationServerAndResource()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);

            var result = await client.TryGetAsync(server.ProtectedResourceMetadataUri, CancellationToken.None);

            result.Should().NotBeNull();
            result!.AuthorizationServers.Should().NotBeNullOrEmpty();
            result.AuthorizationServers![0].Should().Be(server.Issuer.ToString());
            result.Resource.Should().Be("http://localhost");
        }

        /// <summary>
        /// A relative URI is rejected before any HTTP call is attempted.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_RelativeUri_ThrowsArgumentExceptionWithoutHttpCall()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);

            Func<Task> act = () => client.TryGetAsync(new Uri("/.well-known/oauth-protected-resource", UriKind.Relative), CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
            server.HitCount("prm").Should().Be(0);
        }

        /// <summary>
        /// A 503 that persists across the one allowed retry surfaces as <see cref="OutboundDiscoveryException"/>
        /// carrying the URL and status, after exactly two attempts.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_RepeatedServerError_RetriesOnceThenThrowsOutboundDiscoveryException()
        {
            var hitCount = 0;
            using var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.Configure(app => app.Run(async ctx =>
                    {
                        hitCount++;
                        ctx.Response.StatusCode = 503;
                        await Task.CompletedTask;
                    }));
                })
                .Start();
            var testServer = host.GetTestServer();
            var client = CreateClient(testServer.CreateHandler());
            var url = new Uri("http://localhost/.well-known/oauth-protected-resource");

            Func<Task> act = () => client.TryGetAsync(url, CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OutboundDiscoveryException>();
            assertion.Which.Message.Should().Contain(url.ToString());
            assertion.Which.Message.Should().Contain("503");
            hitCount.Should().Be(2);
        }

        /// <summary>
        /// A 401 response is treated as non-fatal per AUTH-14: the client returns <see langword="null"/> after
        /// exactly one GET.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_Unauthorized_ReturnsNullAndHitsOnce()
        {
            var options = new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.Unauthorized
            };
            using var server = new LocalAuthorizationServer(options);
            var client = CreateClient(server.Handler);

            var result = await client.TryGetAsync(server.ProtectedResourceMetadataUri, CancellationToken.None);

            result.Should().BeNull();
            server.HitCount("prm").Should().Be(1);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a <see cref="ProtectedResourceMetadataClient"/> whose <c>"OAuth"</c> named client dispatches
        /// straight into <paramref name="handler"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="handler">The primary handler the <c>"OAuth"</c> named client is configured with.</param>
        /// <returns>
        /// The client under test.
        /// </returns>
        internal static ProtectedResourceMetadataClient CreateClient(HttpMessageHandler handler)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddLogging();
            var provider = services.BuildServiceProvider();

            return new ProtectedResourceMetadataClient(
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<ILogger<ProtectedResourceMetadataClient>>());
        }

        #endregion

    }

}
