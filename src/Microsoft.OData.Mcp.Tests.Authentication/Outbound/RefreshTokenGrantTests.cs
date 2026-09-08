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
    /// Exercises <see cref="RefreshTokenGrant"/> against <see cref="LocalAuthorizationServer"/> over real HTTP
    /// through the <c>"OAuth"</c> named client, with no mocking.
    /// </summary>
    /// <remarks>
    /// Every test that needs a refresh token acquires one through a real device code grant with
    /// <c>offline_access</c>, because the fixture only mints a refresh token for that scope.
    /// </remarks>
    [TestClass]
    public class RefreshTokenGrantTests
    {

        #region Public Methods

        /// <summary>
        /// A container carrying no refresh token cannot be refreshed, and the grant says so before touching the
        /// network.
        /// </summary>
        [TestMethod]
        public async Task RefreshAsync_NoRefreshToken_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server.Handler);
            var metadata = DeviceCodeGrantTests.CreateMetadata(server);
            var current = new SdkAuth.TokenContainer
            {
                AccessToken = "access-abc",
                ClientId = "cli",
                ObtainedAt = DateTimeOffset.UtcNow,
                TokenType = "Bearer"
            };

            Func<Task> act = () => grant.RefreshAsync(metadata, current, null, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeRefreshToken}").Should().Be(0);
        }

        /// <summary>
        /// An authorization server that answers a refresh with no <c>refresh_token</c> at all leaves the client
        /// holding the refresh token it already had, per RFC 6749 section 6.
        /// </summary>
        [TestMethod]
        public async Task RefreshAsync_OmittedRefreshToken_KeepsPrevious()
        {
            var options = new LocalAuthorizationServerOptions
            {
                OmitRotatedRefreshToken = true,
                PendingPollsBeforeSuccess = 0,
                RequireApproval = false
            };
            using var server = new LocalAuthorizationServer(options);
            var grant = CreateGrant(server.Handler);
            var metadata = DeviceCodeGrantTests.CreateMetadata(server);
            var current = await AcquireAsync(server, metadata);

            var refreshed = await grant.RefreshAsync(metadata, current, null, CancellationToken.None);

            refreshed.RefreshToken.Should().Be(current.RefreshToken);
            refreshed.AccessToken.Should().NotBeNullOrWhiteSpace();
            refreshed.AccessToken.Should().NotBe(current.AccessToken);
        }

        /// <summary>
        /// The client identity, authorization server, and token endpoint authentication method the container
        /// carried survive the refresh, so a cold start can refresh again without re-discovering anything.
        /// </summary>
        [TestMethod]
        public async Task RefreshAsync_PreservesClientAndServer()
        {
            var options = new LocalAuthorizationServerOptions
            {
                PendingPollsBeforeSuccess = 0,
                RequireApproval = false
            };
            using var server = new LocalAuthorizationServer(options);
            var grant = CreateGrant(server.Handler);
            var metadata = DeviceCodeGrantTests.CreateMetadata(server);
            var current = await AcquireAsync(server, metadata);

            var refreshed = await grant.RefreshAsync(metadata, current, null, CancellationToken.None);

            refreshed.AuthorizationServer.Should().Be(server.Issuer.AbsoluteUri);
            refreshed.ClientId.Should().Be("cli");
            refreshed.TokenEndpointAuthMethod.Should().Be(TokenEndpointClient.AuthMethodNone);
            refreshed.Scope.Should().Contain(ODataMcpAuthConstants.OfflineAccessScope);
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeRefreshToken}").Should().Be(1);
        }

        /// <summary>
        /// A rotating authorization server hands back a new access token and a new refresh token, and rejects
        /// the refresh token it just replaced.
        /// </summary>
        [TestMethod]
        public async Task RefreshAsync_RotatesAndOldTokenRejected()
        {
            var options = new LocalAuthorizationServerOptions
            {
                PendingPollsBeforeSuccess = 0,
                RequireApproval = false
            };
            using var server = new LocalAuthorizationServer(options);
            var grant = CreateGrant(server.Handler);
            var metadata = DeviceCodeGrantTests.CreateMetadata(server);
            var current = await AcquireAsync(server, metadata);

            var refreshed = await grant.RefreshAsync(metadata, current, null, CancellationToken.None);

            refreshed.AccessToken.Should().NotBe(current.AccessToken);
            refreshed.RefreshToken.Should().NotBeNullOrWhiteSpace();
            refreshed.RefreshToken.Should().NotBe(current.RefreshToken);

            Func<Task> act = () => grant.RefreshAsync(metadata, current, null, CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OAuthTokenException>();
            assertion.Which.Error.Should().Be("invalid_grant");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Runs a real device code grant with <c>offline_access</c> and maps the result onto a container.
        /// </summary>
        /// <param name="server">The authorization server to acquire tokens from.</param>
        /// <param name="metadata">The metadata describing <paramref name="server"/>.</param>
        /// <returns>
        /// A container carrying an access token, a refresh token, and the client identity that acquired them.
        /// </returns>
        internal static async Task<SdkAuth.TokenContainer> AcquireAsync(LocalAuthorizationServer server, AuthorizationServerMetadata metadata)
        {
            var deviceCodeGrant = DeviceCodeGrantTests.CreateGrant(server.Handler);
            var device = await deviceCodeGrant.StartAsync(metadata, "cli", $"read {ODataMcpAuthConstants.OfflineAccessScope}", null, CancellationToken.None);
            var response = await deviceCodeGrant.PollAsync(metadata, device, "cli", null, CancellationToken.None);

            return response.ToTokenContainer(null, "cli", null, server.Issuer.AbsoluteUri, TokenEndpointClient.AuthMethodNone);
        }

        /// <summary>
        /// Builds a <see cref="RefreshTokenGrant"/> whose <c>"OAuth"</c> named client dispatches straight into
        /// <paramref name="handler"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="handler">The primary handler the <c>"OAuth"</c> named client is configured with.</param>
        /// <returns>
        /// The grant under test.
        /// </returns>
        internal static RefreshTokenGrant CreateGrant(HttpMessageHandler handler)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddLogging();
            var provider = services.BuildServiceProvider();
            var tokenEndpointClient = new TokenEndpointClient(
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<ILogger<TokenEndpointClient>>());

            return new RefreshTokenGrant(tokenEndpointClient, provider.GetRequiredService<ILogger<RefreshTokenGrant>>());
        }

        #endregion

    }

}
