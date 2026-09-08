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
    /// Exercises <see cref="DynamicClientRegistrar"/> against the RFC 7591 registration endpoint
    /// <see cref="LocalAuthorizationServer"/> publishes, with no mocking.
    /// </summary>
    /// <remarks>
    /// The fixture echoes the <c>grant_types</c> and <c>redirect_uris</c> it was sent back on the registration
    /// response, so these tests assert on what actually went over the wire rather than on what the registrar
    /// intended to send.
    /// </remarks>
    [TestClass]
    public class DynamicClientRegistrarTests
    {

        #region Public Methods

        /// <summary>
        /// An authorization code registration declares the loopback redirect it will actually be redirected to,
        /// and asks for a public client.
        /// </summary>
        [TestMethod]
        public async Task RegisterAsync_AuthorizationCodeGrant_RegistersRedirectUriAndPublicClient()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                EnableDynamicClientRegistration = true
            });
            var registrar = CreateRegistrar(server);
            var redirectUri = new Uri("http://127.0.0.1:56789/callback/");

            var registration = await registrar.RegisterAsync(
                CreateMetadata(server),
                new SdkAuth.DynamicClientRegistrationOptions { ClientName = ODataMcpAuthConstants.DynamicClientName },
                redirectUri,
                [ODataMcpAuthConstants.GrantTypeAuthorizationCode, ODataMcpAuthConstants.GrantTypeRefreshToken],
                CancellationToken.None);

            registration.ClientId.Should().StartWith("dcr-");
            registration.TokenEndpointAuthMethod.Should().Be(TokenEndpointClient.AuthMethodNone);
            registration.RedirectUris.Should().Equal(redirectUri.AbsoluteUri);
            registration.GrantTypes.Should().Equal(ODataMcpAuthConstants.GrantTypeAuthorizationCode, ODataMcpAuthConstants.GrantTypeRefreshToken);
            server.HitCount("register").Should().Be(1);
        }

        /// <summary>
        /// A device code registration declares no redirect URI at all, because that grant never uses one.
        /// </summary>
        [TestMethod]
        public async Task RegisterAsync_DeviceCodeGrant_RegistersNoRedirectUri()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                EnableDynamicClientRegistration = true
            });
            var registrar = CreateRegistrar(server);

            var registration = await registrar.RegisterAsync(
                CreateMetadata(server),
                new SdkAuth.DynamicClientRegistrationOptions { ClientName = ODataMcpAuthConstants.DynamicClientName },
                redirectUri: null,
                [ODataMcpAuthConstants.DeviceCodeGrantType, ODataMcpAuthConstants.GrantTypeRefreshToken],
                CancellationToken.None);

            registration.ClientId.Should().StartWith("dcr-");
            registration.RedirectUris.Should().BeEmpty();
            registration.GrantTypes.Should().Equal(ODataMcpAuthConstants.DeviceCodeGrantType, ODataMcpAuthConstants.GrantTypeRefreshToken);
        }

        /// <summary>
        /// A registration a caller sends with no grant types at all is refused before a request goes out.
        /// </summary>
        [TestMethod]
        public async Task RegisterAsync_NoGrantTypes_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                EnableDynamicClientRegistration = true
            });
            var registrar = CreateRegistrar(server);

            Func<Task> act = () => registrar.RegisterAsync(
                CreateMetadata(server),
                new SdkAuth.DynamicClientRegistrationOptions(),
                redirectUri: null,
                [],
                CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
            server.HitCount("register").Should().Be(0);
        }

        /// <summary>
        /// An endpoint that is advertised but not reachable surfaces as a token exception carrying the status,
        /// rather than as a null client id nobody can act on.
        /// </summary>
        [TestMethod]
        public async Task RegisterAsync_RegistrationDisabled_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var registrar = CreateRegistrar(server);

            Func<Task> act = () => registrar.RegisterAsync(
                CreateMetadata(server),
                new SdkAuth.DynamicClientRegistrationOptions { ClientName = ODataMcpAuthConstants.DynamicClientName },
                redirectUri: null,
                [ODataMcpAuthConstants.DeviceCodeGrantType],
                CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OAuthTokenException>();

            assertion.Which.StatusCode.Should().Be(404);
        }

        #endregion

        #region Internal Methods

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
                RegistrationEndpoint = server.RegistrationEndpoint,
                TokenEndpoint = server.TokenEndpoint
            };
        }

        /// <summary>
        /// Builds a <see cref="DynamicClientRegistrar"/> whose <c>"OAuth"</c> named client dispatches straight
        /// into <paramref name="server"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="server">The authorization server the registrar talks to.</param>
        /// <returns>
        /// The registrar under test.
        /// </returns>
        internal static DynamicClientRegistrar CreateRegistrar(LocalAuthorizationServer server)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => server.Handler);
            services.AddLogging();
            var provider = services.BuildServiceProvider();

            return new DynamicClientRegistrar(
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<ILogger<DynamicClientRegistrar>>());
        }

        #endregion

    }

}
