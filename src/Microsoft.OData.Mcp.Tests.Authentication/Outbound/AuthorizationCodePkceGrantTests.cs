// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
    /// Exercises <see cref="AuthorizationCodePkceGrant"/> against <see cref="LocalAuthorizationServer"/>,
    /// including one end-to-end run whose authorization response travels over a real <c>127.0.0.1</c> socket
    /// into a real <see cref="System.Net.HttpListener"/>, with no mocking.
    /// </summary>
    /// <remarks>
    /// The fixture's authorize endpoint enforces the same rules a production authorization server does — it
    /// requires <c>response_type=code</c>, <c>code_challenge_method=S256</c>, and a <c>state</c>, and its token
    /// endpoint recomputes the challenge from the verifier — so a grant that got PKCE wrong could not pass by
    /// accident.
    /// </remarks>
    [TestClass]
    public class AuthorizationCodePkceGrantTests
    {

        #region Public Methods

        /// <summary>
        /// The authorization URL carries an <c>S256</c> challenge that is exactly the base64url SHA-256 of the
        /// returned verifier, plus the bound <c>state</c>, the loopback redirect, and the RFC 8707
        /// <c>resource</c> spelled exactly as it was supplied — no trailing slash <see cref="Uri"/> would have
        /// added.
        /// </summary>
        [TestMethod]
        public void Begin_Metadata_ProducesS256ChallengeAndState()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server);
            var redirectUri = new Uri("http://127.0.0.1:56789/callback/");

            var begin = grant.Begin(CreateMetadata(server), "cli", "read offline_access", new Uri("http://localhost"), redirectUri);

            var query = LoopbackAuthorizationCallback.ParseQuery(begin.AuthorizationUri.Query);

            begin.State.Should().NotBeNullOrWhiteSpace();
            begin.CodeVerifier.Should().NotBeNullOrWhiteSpace();
            begin.CodeVerifier.Length.Should().BeInRange(43, 128);
            query[ODataMcpAuthConstants.ResponseTypeParameter].Should().Be("code");
            query[ODataMcpAuthConstants.ClientIdParameter].Should().Be("cli");
            query[ODataMcpAuthConstants.RedirectUriParameter].Should().Be(redirectUri.AbsoluteUri);
            query[ODataMcpAuthConstants.StateParameter].Should().Be(begin.State);
            query[ODataMcpAuthConstants.CodeChallengeMethodParameter].Should().Be("S256");
            query[ODataMcpAuthConstants.CodeChallengeParameter].Should().Be(ExpectedChallenge(begin.CodeVerifier));
            query[ODataMcpAuthConstants.ScopeParameter].Should().Be("read offline_access");
            query[ODataMcpAuthConstants.ResourceParameter].Should().Be("http://localhost");
        }

        /// <summary>
        /// An authorization server that publishes its PKCE methods without <c>S256</c> is refused rather than
        /// downgraded to <c>plain</c>.
        /// </summary>
        [TestMethod]
        public void Begin_NoS256Advertised_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server);
            var metadata = CreateMetadata(server);
            metadata.CodeChallengeMethodsSupported = ["plain"];

            var act = () => grant.Begin(metadata, "cli", "read", null, new Uri("http://127.0.0.1:56789/callback/"));

            act.Should().Throw<NotSupportedException>().WithMessage("*S256*");
        }

        /// <summary>
        /// An authorization server whose issuer identifier is authority-only — <c>http://localhost</c>, the
        /// shape <c>https://accounts.google.com</c> has in production — echoes that exact string as its RFC
        /// 9207 <c>iss</c>, and the exchange succeeds. Comparing against a parsed <see cref="Uri"/> would have
        /// appended the path <c>/</c> to the metadata side only and rejected a conforming server.
        /// </summary>
        [TestMethod]
        public async Task ExchangeAsync_AuthorityOnlyIssuer_Succeeds()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                AuthorityOnlyIssuer = true
            });
            var grant = CreateGrant(server);
            var metadata = CreateMetadata(server);

            using var callback = LoopbackAuthorizationCallback.Start(null);

            var begin = grant.Begin(metadata, "cli", "read", null, callback.RedirectUri);
            var wait = callback.WaitAsync(begin.State, CancellationToken.None);

            using var authorizationClient = new HttpClient(server.Handler, disposeHandler: false);
            using var redirect = await authorizationClient.GetAsync(begin.AuthorizationUri, CancellationToken.None);

            using var loopbackClient = new HttpClient();
            using var landing = await loopbackClient.GetAsync(redirect.Headers.Location, CancellationToken.None);

            var result = await wait;
            var response = await grant.ExchangeAsync(metadata, result, begin.CodeVerifier, "cli", callback.RedirectUri, null, CancellationToken.None);

            server.PublishedIssuer.Should().Be("http://localhost");
            result.Iss.Should().Be("http://localhost");
            metadata.Issuer.Should().Be("http://localhost");
            metadata.IssuerUri!.AbsoluteUri.Should().Be("http://localhost/");
            response.AccessToken.Should().NotBeNullOrWhiteSpace();
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeAuthorizationCode}").Should().Be(1);
        }

        /// <summary>
        /// An <c>iss</c> that <see cref="Uri"/> equality would call equal — the same issuer respelled with its
        /// default port — is still a mismatch, because RFC 8414 section 2 compares issuer identifiers as
        /// strings and never normalizes them.
        /// </summary>
        [TestMethod]
        public async Task ExchangeAsync_IssuerDefaultPortSpelling_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server);
            var result = new SdkAuth.AuthorizationResult
            {
                Code = "the-code",
                Iss = $"{server.Issuer.Scheme}://{server.Issuer.Host}:80{server.Issuer.AbsolutePath}",
                State = "the-state"
            };

            Func<Task> act = () => grant.ExchangeAsync(
                CreateMetadata(server),
                result,
                AuthorizationCodePkceGrant.CreateCodeVerifier(),
                "cli",
                new Uri("http://127.0.0.1:56789/callback/"),
                null,
                CancellationToken.None);

            await act.Should().ThrowAsync<OutboundDiscoveryException>().WithMessage("*does not match*");
            server.HitCount("token").Should().Be(0);
        }

        /// <summary>
        /// An <c>iss</c> naming another authorization server stops the grant before anything is posted, which is
        /// the RFC 9207 mix-up defense: the code is never presented to a server that did not mint it.
        /// </summary>
        [TestMethod]
        public async Task ExchangeAsync_IssuerMismatch_ThrowsAndPostsNoToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server);
            var result = new SdkAuth.AuthorizationResult
            {
                Code = "the-code",
                Iss = "http://evil",
                State = "the-state"
            };

            Func<Task> act = () => grant.ExchangeAsync(
                CreateMetadata(server),
                result,
                AuthorizationCodePkceGrant.CreateCodeVerifier(),
                "cli",
                new Uri("http://127.0.0.1:56789/callback/"),
                null,
                CancellationToken.None);

            await act.Should().ThrowAsync<OutboundDiscoveryException>().WithMessage("*http://evil*");
            server.HitCount("token").Should().Be(0);
        }

        /// <summary>
        /// RFC 8414 section 2 compares issuer identifiers by simple string comparison, so an <c>iss</c> that
        /// differs from the selected authorization server only by a trailing slash is a mismatch and the code is
        /// never presented.
        /// </summary>
        [TestMethod]
        public async Task ExchangeAsync_IssuerTrailingSlashDifference_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server);
            var result = new SdkAuth.AuthorizationResult
            {
                Code = "the-code",
                Iss = server.Issuer.AbsoluteUri + "/",
                State = "the-state"
            };

            Func<Task> act = () => grant.ExchangeAsync(
                CreateMetadata(server),
                result,
                AuthorizationCodePkceGrant.CreateCodeVerifier(),
                "cli",
                new Uri("http://127.0.0.1:56789/callback/"),
                null,
                CancellationToken.None);

            await act.Should().ThrowAsync<OutboundDiscoveryException>().WithMessage("*does not match*");
            server.HitCount("token").Should().Be(0);
        }

        /// <summary>
        /// The whole grant — authorize, a real loopback redirect, then the code exchange — lands a token whose
        /// <c>offline_access</c> scope earned a refresh token.
        /// </summary>
        [TestMethod]
        public async Task ExchangeAsync_LoopbackRoundTrip_ReturnsTokenWithRefreshToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server);
            var metadata = CreateMetadata(server);

            using var callback = LoopbackAuthorizationCallback.Start(null);

            var begin = grant.Begin(metadata, "cli", "read offline_access", null, callback.RedirectUri);
            var wait = callback.WaitAsync(begin.State, CancellationToken.None);

            using var authorizationClient = new HttpClient(server.Handler, disposeHandler: false);
            using var redirect = await authorizationClient.GetAsync(begin.AuthorizationUri, CancellationToken.None);

            redirect.StatusCode.Should().Be(HttpStatusCode.Found);

            using var loopbackClient = new HttpClient();
            using var landing = await loopbackClient.GetAsync(redirect.Headers.Location, CancellationToken.None);

            var result = await wait;
            var response = await grant.ExchangeAsync(metadata, result, begin.CodeVerifier, "cli", callback.RedirectUri, null, CancellationToken.None);

            landing.StatusCode.Should().Be(HttpStatusCode.OK);
            result.Iss.Should().Be(server.Issuer.ToString());
            response.AccessToken.Should().NotBeNullOrWhiteSpace();
            response.RefreshToken.Should().NotBeNullOrWhiteSpace();
            response.Scope.Should().Contain(ODataMcpAuthConstants.OfflineAccessScope);
            server.HitCount("authorize").Should().Be(1);
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeAuthorizationCode}").Should().Be(1);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an <see cref="AuthorizationCodePkceGrant"/> whose <c>"OAuth"</c> named client dispatches
        /// straight into <paramref name="server"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="server">The authorization server the grant talks to.</param>
        /// <returns>
        /// The grant under test.
        /// </returns>
        internal static AuthorizationCodePkceGrant CreateGrant(LocalAuthorizationServer server)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => server.Handler);
            services.AddLogging();
            var provider = services.BuildServiceProvider();
            var tokenEndpointClient = new TokenEndpointClient(
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<ILogger<TokenEndpointClient>>());

            return new AuthorizationCodePkceGrant(tokenEndpointClient, provider.GetRequiredService<ILogger<AuthorizationCodePkceGrant>>());
        }

        /// <summary>
        /// Builds the authorization server metadata the fixture publishes, without running discovery.
        /// </summary>
        /// <param name="server">The authorization server whose endpoints are copied.</param>
        /// <returns>
        /// The metadata.
        /// </returns>
        internal static AuthorizationServerMetadata CreateMetadata(LocalAuthorizationServer server)
        {
            return new AuthorizationServerMetadata
            {
                AuthorizationEndpoint = server.AuthorizationEndpoint,
                AuthorizationResponseIssParameterSupported = true,
                CodeChallengeMethodsSupported = [ODataMcpAuthConstants.CodeChallengeMethodS256],
                Issuer = server.PublishedIssuer,
                TokenEndpoint = server.TokenEndpoint
            };
        }

        /// <summary>
        /// Recomputes the RFC 7636 <c>S256</c> challenge for a verifier, independently of the code under test.
        /// </summary>
        /// <param name="codeVerifier">The code verifier the grant produced.</param>
        /// <returns>
        /// The unpadded base64url SHA-256 hash.
        /// </returns>
        internal static string ExpectedChallenge(string codeVerifier)
        {
            return Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        #endregion

    }

}
