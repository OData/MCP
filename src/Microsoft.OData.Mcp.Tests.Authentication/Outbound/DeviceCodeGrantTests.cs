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
    /// Exercises <see cref="DeviceCodeGrant"/> against <see cref="LocalAuthorizationServer"/> over real HTTP
    /// through the <c>"OAuth"</c> named client, with no mocking.
    /// </summary>
    /// <remarks>
    /// The fixture advertises a one second poll interval, so a test that walks two <c>authorization_pending</c>
    /// answers costs roughly two seconds and the <c>slow_down</c> test costs roughly six.
    /// </remarks>
    [TestClass]
    public class DeviceCodeGrantTests
    {

        #region Public Methods

        /// <summary>
        /// An <c>authorization_pending</c> answer leaves the poll interval exactly as the server advertised it.
        /// </summary>
        [TestMethod]
        public void NextInterval_AuthorizationPending_KeepsInterval()
        {
            DeviceCodeGrant.NextInterval(1, ODataMcpAuthConstants.ErrorAuthorizationPending).Should().Be(1);
            DeviceCodeGrant.NextInterval(5, ODataMcpAuthConstants.ErrorAuthorizationPending).Should().Be(5);
        }

        /// <summary>
        /// A grant that requires an explicit approval issues a token on the first poll that follows
        /// <see cref="LocalAuthorizationServer.Approve(string)"/>.
        /// </summary>
        [TestMethod]
        public async Task Poll_ManualApproval_SucceedsAfterApprove()
        {
            var options = new LocalAuthorizationServerOptions
            {
                RequireApproval = true
            };
            using var server = new LocalAuthorizationServer(options);
            var grant = CreateGrant(server.Handler);
            var metadata = CreateMetadata(server);

            var device = await grant.StartAsync(metadata, "cli", "read", null, CancellationToken.None);
            server.Approve(device.UserCode);
            var response = await grant.PollAsync(metadata, device, "cli", null, CancellationToken.None);

            response.AccessToken.Should().NotBeNullOrWhiteSpace();
            server.HitCount($"token:{ODataMcpAuthConstants.DeviceCodeGrantType}").Should().Be(1);
        }

        /// <summary>
        /// A <c>slow_down</c> answer lengthens the poll interval by five seconds and the grant still completes.
        /// </summary>
        [TestMethod]
        public async Task Poll_SlowDownOnce_StillSucceeds()
        {
            DeviceCodeGrant.NextInterval(1, ODataMcpAuthConstants.ErrorSlowDown).Should().Be(6);

            var options = new LocalAuthorizationServerOptions
            {
                PendingPollsBeforeSuccess = 0,
                RequireApproval = false,
                SlowDownOnce = true
            };
            using var server = new LocalAuthorizationServer(options);
            var grant = CreateGrant(server.Handler);
            var metadata = CreateMetadata(server);

            var device = await grant.StartAsync(metadata, "cli", "read", null, CancellationToken.None);
            var response = await grant.PollAsync(metadata, device, "cli", null, CancellationToken.None);

            response.AccessToken.Should().NotBeNullOrWhiteSpace();
            server.HitCount($"token:{ODataMcpAuthConstants.DeviceCodeGrantType}").Should().Be(2);
        }

        /// <summary>
        /// An authorization server that advertises the device code grant but publishes no
        /// <c>device_authorization_endpoint</c> falls back to <c>{issuer}/devicecode</c>, and says so at Warning.
        /// </summary>
        [TestMethod]
        public async Task Start_NoDeviceEndpointButAdvertised_FallsBackToIssuerDevicecode()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var logs = new CapturingLoggerProvider();
            var grant = CreateGrant(server.Handler, logs);
            var metadata = new AuthorizationServerMetadata
            {
                DeviceAuthorizationEndpoint = null,
                GrantTypesSupported = [ODataMcpAuthConstants.GrantTypeAuthorizationCode, ODataMcpAuthConstants.DeviceCodeGrantType],
                Issuer = server.PublishedIssuer,
                TokenEndpoint = server.TokenEndpoint
            };

            var device = await grant.StartAsync(metadata, "cli", "read", null, CancellationToken.None);

            device.DeviceCode.Should().NotBeNullOrWhiteSpace();
            device.UserCode.Should().NotBeNullOrWhiteSpace();
            server.HitCount("devicecode").Should().Be(1);
            logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning);
            logs.AllText.Should().Contain("devicecode");
        }

        /// <summary>
        /// An authorization server that neither publishes a device authorization endpoint nor advertises the
        /// grant is refused up front rather than probed on a guessed path.
        /// </summary>
        [TestMethod]
        public async Task Start_NotAdvertised_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var grant = CreateGrant(server.Handler);
            var metadata = new AuthorizationServerMetadata
            {
                DeviceAuthorizationEndpoint = null,
                GrantTypesSupported = [ODataMcpAuthConstants.GrantTypeAuthorizationCode],
                Issuer = server.PublishedIssuer,
                TokenEndpoint = server.TokenEndpoint
            };

            Func<Task> act = () => grant.StartAsync(metadata, "cli", "read", null, CancellationToken.None);

            await act.Should().ThrowAsync<NotSupportedException>();
            server.HitCount("devicecode").Should().Be(0);
        }

        /// <summary>
        /// The happy path: two <c>authorization_pending</c> answers then a token, in exactly three token POSTs,
        /// with a refresh token because <c>offline_access</c> was requested.
        /// </summary>
        [TestMethod]
        public async Task StartThenPoll_AutoApprovedAfterTwoPending_IssuesTokenInThreePosts()
        {
            var options = new LocalAuthorizationServerOptions
            {
                PendingPollsBeforeSuccess = 2,
                RequireApproval = false
            };
            using var server = new LocalAuthorizationServer(options);
            var grant = CreateGrant(server.Handler);
            var metadata = CreateMetadata(server);
            var before = DateTimeOffset.UtcNow;

            var device = await grant.StartAsync(metadata, "cli", "read offline_access", null, CancellationToken.None);
            var response = await grant.PollAsync(metadata, device, "cli", null, CancellationToken.None);

            device.Interval.Should().Be(1);
            device.VerificationUri.Should().NotBeNullOrWhiteSpace();
            response.AccessToken.Should().NotBeNullOrWhiteSpace();
            response.RefreshToken.Should().NotBeNullOrWhiteSpace();
            server.HitCount($"token:{ODataMcpAuthConstants.DeviceCodeGrantType}").Should().Be(3);

            var container = response.ToTokenContainer(null, "cli", null, server.Issuer.AbsoluteUri, TokenEndpointClient.AuthMethodNone);

            container.ExpiresIn.Should().Be(3600);
            container.ObtainedAt.Should().BeCloseTo(before, TimeSpan.FromSeconds(30));
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a <see cref="DeviceCodeGrant"/> whose <c>"OAuth"</c> named client dispatches straight into
        /// <paramref name="handler"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="handler">The primary handler the <c>"OAuth"</c> named client is configured with.</param>
        /// <returns>
        /// The grant under test.
        /// </returns>
        internal static DeviceCodeGrant CreateGrant(HttpMessageHandler handler)
        {
            return CreateGrant(handler, null);
        }

        /// <summary>
        /// Builds a <see cref="DeviceCodeGrant"/> whose <c>"OAuth"</c> named client dispatches straight into
        /// <paramref name="handler"/> and whose logging is optionally captured.
        /// </summary>
        /// <param name="handler">The primary handler the <c>"OAuth"</c> named client is configured with.</param>
        /// <param name="logs">The provider every log message is captured to, or <see langword="null"/> to discard logs.</param>
        /// <returns>
        /// The grant under test.
        /// </returns>
        internal static DeviceCodeGrant CreateGrant(HttpMessageHandler handler, CapturingLoggerProvider? logs)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddLogging(builder =>
            {
                if (logs is not null)
                {
                    builder.AddProvider(logs);
                }
            });
            var provider = services.BuildServiceProvider();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var tokenEndpointClient = new TokenEndpointClient(httpClientFactory, provider.GetRequiredService<ILogger<TokenEndpointClient>>());

            return new DeviceCodeGrant(httpClientFactory, tokenEndpointClient, provider.GetRequiredService<ILogger<DeviceCodeGrant>>());
        }

        /// <summary>
        /// Builds the authorization server metadata document <paramref name="server"/> publishes, without going
        /// back through discovery.
        /// </summary>
        /// <param name="server">The local authorization server the metadata describes.</param>
        /// <returns>
        /// The metadata.
        /// </returns>
        internal static AuthorizationServerMetadata CreateMetadata(LocalAuthorizationServer server)
        {
            return new AuthorizationServerMetadata
            {
                DeviceAuthorizationEndpoint = server.DeviceAuthorizationEndpoint,
                GrantTypesSupported =
                [
                    ODataMcpAuthConstants.GrantTypeAuthorizationCode,
                    ODataMcpAuthConstants.GrantTypeClientCredentials,
                    ODataMcpAuthConstants.GrantTypeRefreshToken,
                    ODataMcpAuthConstants.DeviceCodeGrantType
                ],
                Issuer = server.PublishedIssuer,
                TokenEndpoint = server.TokenEndpoint
            };
        }

        #endregion

    }

}
