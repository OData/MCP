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
    /// Exercises <see cref="AuthorizationServerMetadataClient"/> against <see cref="LocalAuthorizationServer"/> and a
    /// tiny purpose-built 5xx server, over real HTTP through the <c>"OAuth"</c> named client with no mocking.
    /// </summary>
    [TestClass]
    public class AuthorizationServerMetadataClientTests
    {

        #region Public Methods

        /// <summary>
        /// <see cref="AuthorizationServerMetadataClient.CandidateDocuments(Uri)"/> yields the five well-known
        /// candidates in RFC 8414 / OpenID Connect probe order for a base URI carrying a path segment.
        /// </summary>
        [TestMethod]
        public void CandidateDocuments_CommonBase_YieldsFiveInOrder()
        {
            var baseUri = new Uri("https://login.microsoftonline.com/common");

            var candidates = AuthorizationServerMetadataClient.CandidateDocuments(baseUri);

            candidates.Should().HaveCount(5);
            candidates[0].Should().Be(new Uri("https://login.microsoftonline.com/common/v2.0/.well-known/oauth-authorization-server"));
            candidates[1].Should().Be(new Uri("https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration"));
            candidates[2].Should().Be(new Uri("https://login.microsoftonline.com/common/.well-known/oauth-authorization-server"));
            candidates[3].Should().Be(new Uri("https://login.microsoftonline.com/.well-known/oauth-authorization-server/common"));
            candidates[4].Should().Be(new Uri("https://login.microsoftonline.com/common/.well-known/openid-configuration"));
        }

        /// <summary>
        /// A base URI with no path segment omits the RFC 8414 §3.1 path-prefixed candidate, since there is no
        /// path to insert after the well-known segment.
        /// </summary>
        [TestMethod]
        public void CandidateDocuments_OriginOnlyBase_OmitsPathPrefixedForm()
        {
            var baseUri = new Uri("https://as.example");

            var candidates = AuthorizationServerMetadataClient.CandidateDocuments(baseUri);

            candidates.Should().HaveCount(4);
            candidates[0].Should().Be(new Uri("https://as.example/v2.0/.well-known/oauth-authorization-server"));
            candidates[1].Should().Be(new Uri("https://as.example/v2.0/.well-known/openid-configuration"));
            candidates[2].Should().Be(new Uri("https://as.example/.well-known/oauth-authorization-server"));
            candidates[3].Should().Be(new Uri("https://as.example/.well-known/openid-configuration"));
        }

        /// <summary>
        /// A trailing slash on the base URI is trimmed before appending each well-known suffix, so no candidate
        /// carries a doubled path separator.
        /// </summary>
        [TestMethod]
        public void CandidateDocuments_TrailingSlashOnBase_DoesNotProduceDoubleSlash()
        {
            var baseUri = new Uri("https://login.microsoftonline.com/common/");

            var candidates = AuthorizationServerMetadataClient.CandidateDocuments(baseUri);

            candidates.Should().HaveCount(5);
            candidates[0].Should().Be(new Uri("https://login.microsoftonline.com/common/v2.0/.well-known/oauth-authorization-server"));
            candidates[1].Should().Be(new Uri("https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration"));
            candidates[2].Should().Be(new Uri("https://login.microsoftonline.com/common/.well-known/oauth-authorization-server"));
            candidates[3].Should().Be(new Uri("https://login.microsoftonline.com/.well-known/oauth-authorization-server/common"));
            candidates[4].Should().Be(new Uri("https://login.microsoftonline.com/common/.well-known/openid-configuration"));

            foreach (var candidate in candidates)
            {
                candidate.AbsoluteUri.Should().NotContain("common//");
            }
        }

        /// <summary>
        /// Against <see cref="LocalAuthorizationServer"/> with default options, the first candidate (the versioned
        /// RFC 8414 document) already carries a <c>token_endpoint</c>, so discovery selects it without ever
        /// probing the legacy (v1) document or the versioned OpenID Connect document.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_LocalAuthorizationServer_SelectsV2AndNeverReadsV1()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);

            var result = await client.DiscoverAsync(new Uri("http://localhost/oauth"), CancellationToken.None);

            result.Should().NotBeNull();
            result!.TokenEndpoint.Should().NotBeNull();
            result.TokenEndpoint!.AbsoluteUri.Should().Contain("/v2.0/");
            result.Issuer!.ToString().Should().EndWith("/oauth/v2.0");
            server.HitCount("metadata-rfc8414-v2").Should().Be(1);
            server.HitCount("metadata-v1").Should().Be(0);
            server.HitCount("metadata-v2").Should().Be(0);
        }

        /// <summary>
        /// When none of the five candidates resolve to a document, discovery returns <see langword="null"/>
        /// rather than throwing, since every probe fails with a non-fatal 404.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_NoDocuments_ReturnsNull()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);

            var result = await client.DiscoverAsync(new Uri("http://localhost/nothing"), CancellationToken.None);

            result.Should().BeNull();
        }

        /// <summary>
        /// A document without a <c>token_endpoint</c> is well-formed JSON but not usable metadata, so
        /// <see cref="AuthorizationServerMetadataClient.TryGetAsync(Uri, CancellationToken)"/> returns
        /// <see langword="null"/> rather than a metadata document a caller could not act on.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_DocumentWithoutTokenEndpoint_ReturnsNull()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);

            var result = await client.TryGetAsync(server.ProtectedResourceMetadataUri, CancellationToken.None);

            result.Should().BeNull();
        }

        /// <summary>
        /// The legacy (v1) discovery document is itself a valid, independently selectable document: fetching it
        /// directly proves that <see cref="AuthorizationServerMetadataClient.DiscoverAsync(Uri, CancellationToken)"/>
        /// relying on candidate ordering, not document validity, is what keeps a v1 issuer from being selected.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_LegacyDocument_ReturnsMetadata()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);

            var result = await client.TryGetAsync(new Uri("http://localhost/oauth/.well-known/openid-configuration"), CancellationToken.None);

            result.Should().NotBeNull();
            result!.TokenEndpoint.Should().NotBeNull();
            result.TokenEndpoint!.AbsoluteUri.Should().EndWith("/oauth/token");
        }

        /// <summary>
        /// A relative URI, and a non-loopback <c>http://</c> URI, are both rejected before any HTTP call is
        /// attempted.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_RelativeOrNonLoopbackHttp_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);

            Func<Task> relativeAct = () => client.TryGetAsync(new Uri("/.well-known/oauth-authorization-server", UriKind.Relative), CancellationToken.None);
            Func<Task> nonLoopbackAct = () => client.TryGetAsync(new Uri("http://example.com/.well-known/oauth-authorization-server"), CancellationToken.None);

            await relativeAct.Should().ThrowAsync<ArgumentException>();
            await nonLoopbackAct.Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// A 503 that persists across the one allowed retry surfaces as <see cref="OutboundDiscoveryException"/>
        /// carrying the URL and status, after exactly two attempts.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_ServerError_RetriesOnceThenThrows()
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
            var url = new Uri("http://localhost/.well-known/oauth-authorization-server");

            Func<Task> act = () => client.TryGetAsync(url, CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OutboundDiscoveryException>();
            assertion.Which.Message.Should().Contain(url.ToString());
            assertion.Which.Message.Should().Contain("503");
            hitCount.Should().Be(2);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an <see cref="AuthorizationServerMetadataClient"/> whose <c>"OAuth"</c> named client dispatches
        /// straight into <paramref name="handler"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="handler">The primary handler the <c>"OAuth"</c> named client is configured with.</param>
        /// <returns>
        /// The client under test.
        /// </returns>
        internal static AuthorizationServerMetadataClient CreateClient(HttpMessageHandler handler)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddLogging();
            var provider = services.BuildServiceProvider();

            return new AuthorizationServerMetadataClient(
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<ILogger<AuthorizationServerMetadataClient>>());
        }

        #endregion

    }

}
