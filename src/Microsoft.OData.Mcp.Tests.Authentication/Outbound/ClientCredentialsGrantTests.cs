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

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="ClientCredentialsGrant"/> against <see cref="LocalAuthorizationServer"/> over real
    /// HTTP through the <c>"OAuth"</c> named client, with no mocking.
    /// </summary>
    /// <remarks>
    /// The fixture seeds the confidential client <c>daemon</c> with the secret <c>daemon-secret</c> and answers
    /// anything else with <c>401 invalid_client</c>, so a grant that sent the wrong credential — or sent it in
    /// the wrong place — is caught rather than silently issued a token.
    /// </remarks>
    [TestClass]
    public class ClientCredentialsGrantTests
    {

        #region Public Methods

        /// <summary>
        /// A correct secret yields an access token, and no refresh token: there is no user session to renew.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_CorrectSecret_ReturnsTokenWithoutRefreshToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server);

            var response = await grant.AcquireAsync(CreateMetadata(server), "daemon", "daemon-secret", "read offline_access", null, CancellationToken.None);

            response.AccessToken.Should().NotBeNullOrWhiteSpace();
            response.RefreshToken.Should().BeNull();
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeClientCredentials}").Should().Be(1);
            server.LastTokenRequest![ODataMcpAuthConstants.ClientIdParameter].Should().Be("daemon");
        }

        /// <summary>
        /// A wrong secret is reported as the RFC 6749 <c>invalid_client</c> the authorization server returned,
        /// not as a generic transport failure.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_WrongSecret_ThrowsInvalidClient()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server);

            Func<Task> act = () => grant.AcquireAsync(CreateMetadata(server), "daemon", "not-the-secret", "read", null, CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OAuthTokenException>();

            assertion.Which.Error.Should().Be("invalid_client");
            assertion.Which.StatusCode.Should().Be(401);
        }

        /// <summary>
        /// A server that advertises only <c>client_secret_basic</c> gets the credentials in the
        /// <c>Authorization</c> header instead of the form.
        /// </summary>
        [TestMethod]
        public void ResolveAuthMethod_OnlyBasicAdvertised_SelectsBasic()
        {
            var metadata = new AuthorizationServerMetadata
            {
                TokenEndpointAuthMethodsSupported = [TokenEndpointClient.AuthMethodClientSecretBasic]
            };

            ClientCredentialsGrant.ResolveAuthMethod(metadata).Should().Be(TokenEndpointClient.AuthMethodClientSecretBasic);
        }

        /// <summary>
        /// A server that advertises nothing at all, or advertises <c>client_secret_post</c>, gets the form
        /// method RFC 6749 leaves as the interoperable default.
        /// </summary>
        [TestMethod]
        public void ResolveAuthMethod_PostAdvertisedOrUnstated_SelectsPost()
        {
            var unstated = new AuthorizationServerMetadata();
            var advertised = new AuthorizationServerMetadata
            {
                TokenEndpointAuthMethodsSupported = [TokenEndpointClient.AuthMethodClientSecretPost, TokenEndpointClient.AuthMethodClientSecretBasic]
            };

            ClientCredentialsGrant.ResolveAuthMethod(unstated).Should().Be(TokenEndpointClient.AuthMethodClientSecretPost);
            ClientCredentialsGrant.ResolveAuthMethod(advertised).Should().Be(TokenEndpointClient.AuthMethodClientSecretPost);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a <see cref="ClientCredentialsGrant"/> whose <c>"OAuth"</c> named client dispatches straight
        /// into <paramref name="server"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="server">The authorization server the grant talks to.</param>
        /// <returns>
        /// The grant under test.
        /// </returns>
        internal static ClientCredentialsGrant CreateGrant(LocalAuthorizationServer server)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => server.Handler);
            services.AddLogging();
            var provider = services.BuildServiceProvider();
            var tokenEndpointClient = new TokenEndpointClient(
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<ILogger<TokenEndpointClient>>());

            return new ClientCredentialsGrant(tokenEndpointClient, provider.GetRequiredService<ILogger<ClientCredentialsGrant>>());
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
                Issuer = server.PublishedIssuer,
                TokenEndpoint = server.TokenEndpoint,
                TokenEndpointAuthMethodsSupported = [TokenEndpointClient.AuthMethodClientSecretPost, TokenEndpointClient.AuthMethodClientSecretBasic]
            };
        }

        #endregion

    }

}
