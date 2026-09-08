// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
    /// Exercises <see cref="TokenEndpointClient"/> against <see cref="LocalAuthorizationServer"/> over real HTTP
    /// through the <c>"OAuth"</c> named client, with no mocking.
    /// </summary>
    [TestClass]
    public class TokenEndpointClientTests
    {

        #region Public Methods

        /// <summary>
        /// <c>client_secret_basic</c> authenticates through the <c>Authorization</c> header alone: the token is
        /// issued and neither <c>client_id</c> nor <c>client_secret</c> is duplicated into the form.
        /// </summary>
        [TestMethod]
        public async Task PostAsync_ClientCredentialsBasic_SendsAuthorizationHeaderOnly()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeClientCredentials,
                [ODataMcpAuthConstants.ScopeParameter] = "read"
            };

            var response = await client.PostAsync(server.TokenEndpoint, form, "daemon", "daemon-secret", TokenEndpointClient.AuthMethodClientSecretBasic, CancellationToken.None);

            response.AccessToken.Should().NotBeNullOrWhiteSpace();
            response.TokenType.Should().Be("Bearer");
            server.LastTokenRequest.Should().NotBeNull();
            server.LastTokenRequest!.Should().NotContainKey("client_secret");
            server.LastTokenRequest.Should().NotContainKey(ODataMcpAuthConstants.ClientIdParameter);
        }

        /// <summary>
        /// <c>client_secret_post</c> puts both credentials in the form and the server issues a token.
        /// </summary>
        [TestMethod]
        public async Task PostAsync_ClientCredentialsPost_ReturnsToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeClientCredentials,
                [ODataMcpAuthConstants.ScopeParameter] = "read"
            };

            var response = await client.PostAsync(server.TokenEndpoint, form, "daemon", "daemon-secret", TokenEndpointClient.AuthMethodClientSecretPost, CancellationToken.None);

            response.AccessToken.Should().NotBeNullOrWhiteSpace();
            response.ExpiresIn.Should().Be(3600);
            server.LastTokenRequest.Should().NotBeNull();
            server.LastTokenRequest!["client_secret"].Should().Be("daemon-secret");
            server.LastTokenRequest[ODataMcpAuthConstants.ClientIdParameter].Should().Be("daemon");
        }

        /// <summary>
        /// A 400 error payload becomes an <see cref="OAuthTokenException"/> carrying the RFC 6749 error code and
        /// the HTTP status, and never the credentials that were sent.
        /// </summary>
        [TestMethod]
        public async Task PostAsync_InvalidGrant_ThrowsOAuthTokenException()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.DeviceCodeGrantType,
                [ODataMcpAuthConstants.DeviceCodeParameter] = "not-a-real-device-code"
            };

            Func<Task> act = () => client.PostAsync(server.TokenEndpoint, form, "cli", null, TokenEndpointClient.AuthMethodNone, CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OAuthTokenException>();
            assertion.Which.Error.Should().Be("invalid_grant");
            assertion.Which.StatusCode.Should().Be(400);
            assertion.Which.Message.Should().Contain("invalid_grant");
            assertion.Which.Message.Should().NotContain("not-a-real-device-code");
        }

        /// <summary>
        /// A non-loopback <c>http://</c> token endpoint is rejected before any HTTP call is attempted.
        /// </summary>
        [TestMethod]
        public async Task PostAsync_NonLoopbackHttpEndpoint_ThrowsWithoutHttpCall()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeClientCredentials
            };

            Func<Task> act = () => client.PostAsync(new Uri("http://example.com/token"), form, "daemon", "daemon-secret", TokenEndpointClient.AuthMethodClientSecretPost, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
            server.HitCount("token").Should().Be(0);
        }

        /// <summary>
        /// The RFC 8707 <c>resource</c> indicator reaches the server verbatim when the caller puts it in the form.
        /// </summary>
        [TestMethod]
        public async Task PostAsync_ResourceInForm_SendsResource()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeClientCredentials,
                [ODataMcpAuthConstants.ResourceParameter] = "http://localhost",
                [ODataMcpAuthConstants.ScopeParameter] = "read"
            };

            await client.PostAsync(server.TokenEndpoint, form, "daemon", "daemon-secret", TokenEndpointClient.AuthMethodClientSecretPost, CancellationToken.None);

            server.LastTokenRequest.Should().NotBeNull();
            server.LastTokenRequest![ODataMcpAuthConstants.ResourceParameter].Should().Be("http://localhost");
        }

        /// <summary>
        /// The client never invents a <c>resource</c> indicator the caller did not supply.
        /// </summary>
        [TestMethod]
        public async Task PostAsync_ResourceOmittedFromForm_DoesNotSendResource()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeClientCredentials,
                [ODataMcpAuthConstants.ScopeParameter] = "read"
            };

            await client.PostAsync(server.TokenEndpoint, form, "daemon", "daemon-secret", TokenEndpointClient.AuthMethodClientSecretPost, CancellationToken.None);

            server.LastTokenRequest.Should().NotBeNull();
            server.LastTokenRequest!.Should().NotContainKey(ODataMcpAuthConstants.ResourceParameter);
        }

        /// <summary>
        /// An authentication method this client does not implement is rejected rather than silently downgraded
        /// to an unauthenticated request.
        /// </summary>
        [TestMethod]
        public async Task PostAsync_UnknownAuthMethod_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server.Handler);
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeClientCredentials
            };

            Func<Task> act = () => client.PostAsync(server.TokenEndpoint, form, "daemon", "daemon-secret", "private_key_jwt", CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
            server.HitCount("token").Should().Be(0);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a <see cref="TokenEndpointClient"/> whose <c>"OAuth"</c> named client dispatches straight into
        /// <paramref name="handler"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="handler">The primary handler the <c>"OAuth"</c> named client is configured with.</param>
        /// <returns>
        /// The client under test.
        /// </returns>
        internal static TokenEndpointClient CreateClient(HttpMessageHandler handler)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddLogging();
            var provider = services.BuildServiceProvider();

            return new TokenEndpointClient(
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<ILogger<TokenEndpointClient>>());
        }

        #endregion

    }

}
