// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="OAuthDiscovery"/> end to end against a real <see cref="LocalAuthorizationServer"/>,
    /// over real HTTP through the <c>"OAuth"</c> named client, with no mocking anywhere.
    /// </summary>
    /// <remarks>
    /// Every discovery test uses the same service root, <c>http://localhost/odata/</c>, so the path-prefixed
    /// protected resource metadata candidate and the origin-only resource fallback are both exercised against a
    /// service root whose path is not <c>/</c>.
    /// </remarks>
    [TestClass]
    public class OAuthDiscoveryTests
    {

        #region Fields

        /// <summary>
        /// The OData service root every discovery test starts from.
        /// </summary>
        internal static readonly Uri ServiceRoot = new("http://localhost/odata/");

        #endregion

        #region Public Methods

        /// <summary>
        /// The Microsoft Graph authorize URL from the spec's worked example strips to two bases, in order;
        /// protected resource metadata entries precede them; an operator override replaces all of them.
        /// </summary>
        [TestMethod]
        public void AuthorizationServerBases_GraphAuthorizeUri_YieldsCommonThenOAuth2()
        {
            var challenge = WwwAuthenticateParser.Parse(
                "Bearer realm=\"\", authorization_uri=\"https://login.microsoftonline.com/common/oauth2/authorize\", client_id=\"00000003-0000-0000-c000-000000000000\"");

            var bases = OAuthDiscovery.AuthorizationServerBases(challenge, null, null);

            bases.Should().HaveCount(2);
            bases[0].Should().Be(new Uri("https://login.microsoftonline.com/common"));
            bases[1].Should().Be(new Uri("https://login.microsoftonline.com/common/oauth2"));

            var protectedResource = new SdkAuth.ProtectedResourceMetadata
            {
                AuthorizationServers = ["https://as.example/"]
            };

            var withProtectedResource = OAuthDiscovery.AuthorizationServerBases(challenge, protectedResource, null);

            withProtectedResource.Should().HaveCount(3);
            withProtectedResource[0].Should().Be(new Uri("https://as.example/"));
            withProtectedResource[1].Should().Be(new Uri("https://login.microsoftonline.com/common"));
            withProtectedResource[2].Should().Be(new Uri("https://login.microsoftonline.com/common/oauth2"));

            var overridden = OAuthDiscovery.AuthorizationServerBases(challenge, protectedResource, new Uri("https://override.example/tenant"));

            overridden.Should().ContainSingle().Which.Should().Be(new Uri("https://override.example/tenant"));
        }

        /// <summary>
        /// An <c>--auth-server</c> override is the only authorization server candidate, but protected resource
        /// metadata is still probed because it may still carry the resource identifier and scopes.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_AuthorizationServerOverride_IsOnlyCandidate()
        {
            var options = new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.NotFound
            };
            using var server = new LocalAuthorizationServer(options);
            var discovery = CreateDiscovery(server.Handler);
            var authorizationServerOverride = new Uri("http://localhost/oauth");

            var result = await discovery.DiscoverAsync(ServiceRoot, [], 401, authorizationServerOverride, null, CancellationToken.None);

            result.Should().NotBeNull();
            result.AuthorizationServerBase.Should().Be(authorizationServerOverride);
            result.AuthorizationServer.TokenEndpoint.Should().Be(server.TokenEndpoint);
            result.Challenge.Should().BeNull();
            server.HitCount("prm").Should().Be(2);
            server.HitCount("metadata-rfc8414-v2").Should().Be(1);
        }

        /// <summary>
        /// A 401 with no <c>WWW-Authenticate</c> at all, and no override, fails first with the spec's message.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_EmptyWwwAuthenticate_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var discovery = CreateDiscovery(server.Handler);

            Func<Task> act = () => discovery.DiscoverAsync(ServiceRoot, [], 401, null, null, CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OutboundDiscoveryException>();
            assertion.Which.Message.Should().Be("401 with no WWW-Authenticate; pass --auth-token / --api-key-header / --auth-server.");
            server.HitCount("prm").Should().Be(0);
        }

        /// <summary>
        /// A challenge carrying nothing usable, with no protected resource metadata and no override, leaves no
        /// authorization server candidate at all and fails loud with the spec's message.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_PrmNotFound_NoChallenge_NoOverride_Throws()
        {
            var options = new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.NotFound
            };
            using var server = new LocalAuthorizationServer(options);
            var discovery = CreateDiscovery(server.Handler);

            Func<Task> act = () => discovery.DiscoverAsync(ServiceRoot, ["Bearer"], 401, null, null, CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OutboundDiscoveryException>();
            assertion.Which.Message.Should().Be("Could not discover an authorization server; pass --auth-server.");
            server.HitCount("prm").Should().Be(2);
        }

        /// <summary>
        /// A 404 on both well-known protected resource metadata candidates is non-fatal per AUTH-14: discovery
        /// continues from the challenge <c>authorization_uri</c> and succeeds — and, with nothing having
        /// published an RFC 8707 <c>resource</c> and no <c>--resource</c> passed, resolves no resource
        /// indicator at all rather than inventing the service root's origin.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_PrmNotFound_WithAuthorizationUri_Succeeds()
        {
            var options = new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.NotFound
            };
            using var server = new LocalAuthorizationServer(options);
            var discovery = CreateDiscovery(server.Handler);
            var header = $"Bearer realm=\"\", authorization_uri=\"{server.AuthorizationEndpoint}\"";

            var result = await discovery.DiscoverAsync(ServiceRoot, [header], 401, null, null, CancellationToken.None);

            result.Should().NotBeNull();
            result.ProtectedResource.Should().BeNull();
            result.AuthorizationServerBase.Should().Be(server.Issuer);
            result.AuthorizationServer.Issuer.Should().Be(server.PublishedIssuer);
            result.Resource.Should().BeNull();
            server.HitCount("prm").Should().Be(2);
        }

        /// <summary>
        /// The happy path: the challenge points at protected resource metadata, the metadata names the
        /// authorization server, and every field of the result comes from a real document.
        /// </summary>
        /// <remarks>
        /// The published <c>resource</c> is the string <c>http://localhost</c>. The result keeps that spelling
        /// on <see cref="Uri.OriginalString"/>, because a token request has to send the resource identifier the
        /// document actually published rather than the trailing-slash form <see cref="Uri"/> normalizes to.
        /// </remarks>
        [TestMethod]
        public async Task DiscoverAsync_PrmOk_SelectsAuthorizationServerFromPrm()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var discovery = CreateDiscovery(server.Handler);
            var header = $"Bearer resource_metadata=\"{server.ProtectedResourceMetadataUri}\", scope=\"read\"";

            var result = await discovery.DiscoverAsync(ServiceRoot, [header], 401, null, null, CancellationToken.None);

            result.Should().NotBeNull();
            result.ProtectedResource.Should().NotBeNull();
            result.AuthorizationServerBase.Should().Be(server.Issuer);
            result.AuthorizationServer.TokenEndpoint.Should().NotBeNull();
            result.AuthorizationServer.TokenEndpoint!.ToString().Should().Contain("/v2.0/");
            result.ChallengeScope.Should().Be("read");
            result.Resource.Should().NotBeNull();
            result.Resource!.OriginalString.Should().Be("http://localhost");
            result.AdvertisedGrants.Should().Contain(ODataMcpAuthConstants.DeviceCodeGrantType);
            server.HitCount("prm").Should().Be(1);
        }

        /// <summary>
        /// The Graph trap: the challenge advertises protected resource metadata that answers 401 with another
        /// challenge. That is one non-fatal GET, never a recursion, and discovery continues from the challenge
        /// <c>authorization_uri</c>.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_PrmUnauthorized_GraphTrap_ContinuesFromAuthorizationUri()
        {
            var options = new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.Unauthorized
            };
            using var server = new LocalAuthorizationServer(options);
            var discovery = CreateDiscovery(server.Handler);
            var header = "Bearer realm=\"\", authorization_uri=\"http://localhost/oauth/v2.0/authorize\", "
                + $"client_id=\"{ODataMcpAuthConstants.MicrosoftGraphResourceAppId}\", "
                + $"resource_metadata=\"{server.ProtectedResourceMetadataUri}\"";

            var result = await discovery.DiscoverAsync(ServiceRoot, [header], 401, null, null, CancellationToken.None);

            result.Should().NotBeNull();
            result.ProtectedResource.Should().BeNull();
            result.AuthorizationServerBase.Should().Be(new Uri("http://localhost/oauth/v2.0"));
            result.Challenge.Should().NotBeNull();
            result.Challenge!.ResourceClientId.Should().Be(ODataMcpAuthConstants.MicrosoftGraphResourceAppId);
            result.AuthorizationServer.TokenEndpoint.Should().Be(server.TokenEndpoint);
            server.HitCount("prm").Should().Be(1);
        }

        /// <summary>
        /// A <c>--resource</c> override supplies the resource indicator when no protected resource metadata
        /// published one, but published protected resource metadata beats the override.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_ResourceOverride_UsedWhenPrmPublishesNone_ButPrmWins()
        {
            var resourceOverride = new Uri("http://res.example");
            var notFoundOptions = new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.NotFound
            };

            using (var server = new LocalAuthorizationServer(notFoundOptions))
            {
                var discovery = CreateDiscovery(server.Handler);
                var header = $"Bearer realm=\"\", authorization_uri=\"{server.AuthorizationEndpoint}\"";

                var result = await discovery.DiscoverAsync(ServiceRoot, [header], 401, null, resourceOverride, CancellationToken.None);

                result.ProtectedResource.Should().BeNull();
                result.Resource.Should().Be(resourceOverride);
            }

            using (var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions()))
            {
                var discovery = CreateDiscovery(server.Handler);
                var header = $"Bearer resource_metadata=\"{server.ProtectedResourceMetadataUri}\"";

                var result = await discovery.DiscoverAsync(ServiceRoot, [header], 401, null, resourceOverride, CancellationToken.None);

                result.ProtectedResource.Should().NotBeNull();
                result.Resource.Should().Be(new Uri(server.Options.ResourceUri));
                result.Resource.Should().NotBe(resourceOverride);
            }
        }

        /// <summary>
        /// A service root whose path is not <c>/</c> yields the origin well-known candidate followed by the
        /// RFC 9728 path-prefixed one; a challenge that names <c>resource_metadata</c> replaces both.
        /// </summary>
        [TestMethod]
        public void PrmCandidates_ServiceRootWithPath_YieldsOriginThenPathPrefixed()
        {
            var candidates = OAuthDiscovery.PrmCandidates(ServiceRoot, null);

            candidates.Should().HaveCount(2);
            candidates[0].Should().Be(new Uri("http://localhost/.well-known/oauth-protected-resource"));
            candidates[1].Should().Be(new Uri("http://localhost/.well-known/oauth-protected-resource/odata"));

            var challenge = WwwAuthenticateParser.Parse("Bearer resource_metadata=\"https://api.example.com/.well-known/oauth-protected-resource\"");

            var fromChallenge = OAuthDiscovery.PrmCandidates(ServiceRoot, challenge);

            fromChallenge.Should().ContainSingle().Which.Should().Be(new Uri("https://api.example.com/.well-known/oauth-protected-resource"));
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an <see cref="OAuthDiscovery"/> whose <c>"OAuth"</c> named client dispatches straight into
        /// <paramref name="handler"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="handler">The primary handler the <c>"OAuth"</c> named client is configured with.</param>
        /// <returns>
        /// The orchestrator under test.
        /// </returns>
        internal static OAuthDiscovery CreateDiscovery(HttpMessageHandler handler)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddLogging();
            var provider = services.BuildServiceProvider();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

            return new OAuthDiscovery(
                new ProtectedResourceMetadataClient(httpClientFactory, provider.GetRequiredService<ILogger<ProtectedResourceMetadataClient>>()),
                new AuthorizationServerMetadataClient(httpClientFactory, provider.GetRequiredService<ILogger<AuthorizationServerMetadataClient>>()),
                provider.GetRequiredService<ILogger<OAuthDiscovery>>());
        }

        #endregion

    }

}
