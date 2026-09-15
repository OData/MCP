// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Latchkey;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="OutboundOAuthClient"/> end to end against <see cref="LocalAuthorizationServer"/>
    /// over real HTTP through the <c>"OAuth"</c> named client, with a real
    /// <see cref="LatchkeyTokenCache"/> on the process-local in-memory backend and no mocking.
    /// </summary>
    /// <remarks>
    /// Every test drives the same shape a first start does: a <c>Bearer</c> challenge naming the fixture's RFC
    /// 9728 document, discovery, then the device code grant an <see cref="AutoApproveConsentPresenter"/> stands
    /// in for. The fixture advertises a one second poll interval, so an approved grant completes in about a
    /// second.
    /// </remarks>
    [TestClass]
    public class OutboundOAuthClientTests
    {

        #region Properties

        /// <summary>
        /// Gets the OData service root every test's tokens authorize.
        /// </summary>
        internal static Uri ServiceRoot { get; } = new("http://localhost/odata/");

        /// <summary>
        /// Gets the throwaway directory the credential store round-trip check runs in, so these tests never
        /// touch the operator's own token cache.
        /// </summary>
        internal static string TokenCacheDirectory { get; } = Path.Combine(Path.GetTempPath(), $"odata-mcp-client-{Guid.NewGuid():N}");

        #endregion

        #region Public Methods

        /// <summary>
        /// The authorization code grant runs end to end over a real loopback socket: the authorize redirect is
        /// followed onto this process's own <c>HttpListener</c>, the code is exchanged, and the token lands in
        /// the cache with the refresh token <c>offline_access</c> earned.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_AuthorizationCodeGrant_AcquiresThroughLoopback()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ConsentPresenter = presenter.PresentAsync;
            options.Grant = OutboundGrantKind.AuthorizationCode;
            var cache = CreateCache();
            var client = CreateClient(server, options, cache);

            var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            await presenter.LoopbackCompletion!;

            token.AccessToken.Should().NotBeNullOrWhiteSpace();
            token.RefreshToken.Should().NotBeNullOrWhiteSpace();
            token.ClientId.Should().Be("cli");
            token.Scope.Should().Contain(ODataMcpAuthConstants.OfflineAccessScope);
            presenter.Presentations.Should().Be(1);
            presenter.LoopbackStatusCode.Should().Be(HttpStatusCode.OK);
            presenter.LastRequest!.Kind.Should().Be(OutboundGrantKind.AuthorizationCode);
            presenter.LastRequest.UserCode.Should().BeNull();
            server.HitCount("authorize").Should().Be(1);
            server.HitCount("devicecode").Should().Be(0);
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeAuthorizationCode}").Should().Be(1);
            client._pendingLoopback.Should().BeNull();
            (await cache.GetTokensAsync(CancellationToken.None))!.AccessToken.Should().Be(token.AccessToken);
        }

        /// <summary>
        /// An operator's <c>--redirect-uri</c> is what the authorization request actually carries, and a
        /// presenter that throws mid-sign-in releases that port rather than leaking a listener for the life of
        /// the process: the same prefix can be bound again immediately afterwards.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_AuthorizationCodePresenterThrows_HonorsRedirectOverrideAndReleasesPort()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var redirectUri = new Uri($"http://127.0.0.1:{LoopbackAuthorizationCallbackTests.FindFreePort()}/callback/");
            OutboundConsentRequest? presented = null;
            var options = CreateOptions();
            options.ConsentPresenter = (request, _) =>
            {
                presented = request;

                throw new InvalidOperationException("no browser here");
            };
            options.Grant = OutboundGrantKind.AuthorizationCode;
            options.RedirectUri = redirectUri;
            var client = CreateClient(server, options, CreateCache());

            Func<Task> failing = () => client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            await failing.Should().ThrowAsync<InvalidOperationException>().WithMessage("no browser here");

            var query = LoopbackAuthorizationCallback.ParseQuery(presented!.Url.Query);

            query[ODataMcpAuthConstants.RedirectUriParameter].Should().Be(redirectUri.AbsoluteUri);
            client._pendingLoopback.Should().BeNull();

            using var rebound = LoopbackAuthorizationCallback.Start(redirectUri);

            rebound.RedirectUri.Should().Be(redirectUri);
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeAuthorizationCode}").Should().Be(0);
        }

        /// <summary>
        /// A daemon with <c>--grant client_credentials</c> and an explicit client secret authenticates as
        /// itself: no presenter runs, no device code is started, and the container keeps everything a silent
        /// re-acquisition needs.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_ClientCredentialsGrant_StoresTokenWithoutPrompting()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ClientId = "daemon";
            options.ClientSecret = "daemon-secret";
            options.ConsentPresenter = presenter.PresentAsync;
            options.Grant = OutboundGrantKind.ClientCredentials;
            var client = CreateClient(server, options, CreateCache());

            var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: false, CancellationToken.None);

            token.AccessToken.Should().NotBeNullOrWhiteSpace();
            token.RefreshToken.Should().BeNull();
            token.ClientId.Should().Be("daemon");
            token.ClientSecret.Should().Be("daemon-secret");
            token.TokenEndpointAuthMethod.Should().Be(TokenEndpointClient.AuthMethodClientSecretPost);
            presenter.Presentations.Should().Be(0);
            server.HitCount("devicecode").Should().Be(0);
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeClientCredentials}").Should().Be(1);
        }

        /// <summary>
        /// A daemon with no <c>--client-id</c> at all registers itself as a confidential client, keeps the
        /// <c>client_secret</c> the registration returned, and authenticates with it — no human, no presenter,
        /// and exactly one registration.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_ClientCredentialsWithDynamicRegistration_RegistersConfidentialClient()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                EnableDynamicClientRegistration = true
            });
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ClientId = null;
            options.ConsentPresenter = presenter.PresentAsync;
            options.Grant = OutboundGrantKind.ClientCredentials;
            var client = CreateClient(server, options, CreateCache());

            var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: false, CancellationToken.None);

            token.AccessToken.Should().NotBeNullOrWhiteSpace();
            token.ClientId.Should().StartWith("dcr-");
            token.ClientSecret.Should().NotBeNullOrWhiteSpace();
            token.TokenEndpointAuthMethod.Should().Be(TokenEndpointClient.AuthMethodClientSecretPost);
            presenter.Presentations.Should().Be(0);
            server.HitCount("register").Should().Be(1);
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeClientCredentials}").Should().Be(1);
        }

        /// <summary>
        /// A wrong client secret surfaces as the authorization server's own <c>invalid_client</c>, so an
        /// operator knows to fix the secret rather than the discovery settings.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_ClientCredentialsWrongSecret_ThrowsInvalidClient()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var options = CreateOptions();
            options.ClientId = "daemon";
            options.ClientSecret = "not-the-secret";
            options.Grant = OutboundGrantKind.ClientCredentials;
            var client = CreateClient(server, options, CreateCache());

            Func<Task> act = () => client.AcquireAsync(Challenge(server), 401, interactiveAllowed: false, CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OAuthTokenException>();

            assertion.Which.Error.Should().Be("invalid_client");
        }

        /// <summary>
        /// An authorization server that accepts a client id metadata document, and an operator who hosts one,
        /// need no registration at all: the document URI is sent as the client id.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_ClientIdMetadataDocument_SendsDocumentUriAsClientId()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                AdvertiseClientIdMetadataDocument = true
            });
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ClientId = null;
            options.ClientMetadataDocumentUri = new Uri("https://cli.example/client.json");
            options.ConsentPresenter = presenter.PresentAsync;
            var client = CreateClient(server, options, CreateCache());

            var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            token.ClientId.Should().Be("https://cli.example/client.json");
            server.LastDeviceAuthorizationRequest![ODataMcpAuthConstants.ClientIdParameter].Should().Be("https://cli.example/client.json");
            server.HitCount("register").Should().Be(0);
        }

        /// <summary>
        /// A first acquisition with an auto-approving presenter walks discovery, the device code grant, and the
        /// cache, and lands a token that carries the refresh token <c>offline_access</c> earns.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_DeviceCode_StoresTokenWithRefreshToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ConsentPresenter = presenter.PresentAsync;
            var cache = CreateCache();
            var client = CreateClient(server, options, cache);

            var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            token.AccessToken.Should().NotBeNullOrWhiteSpace();
            token.RefreshToken.Should().NotBeNullOrWhiteSpace();
            token.ClientId.Should().Be("cli");
            token.Scope.Should().Contain(ODataMcpAuthConstants.OfflineAccessScope);
            presenter.Presentations.Should().Be(1);
            presenter.LastRequest!.UserCode.Should().NotBeNullOrWhiteSpace();
            server.HitCount("devicecode").Should().Be(1);

            var cached = await cache.GetTokensAsync(CancellationToken.None);

            cached!.AccessToken.Should().Be(token.AccessToken);
        }

        /// <summary>
        /// An authorization server that offers registration lets an operator start with no <c>--client-id</c> at
        /// all: the first start registers once, persists the identifier alongside the tokens, and a restart over
        /// the same store reuses both instead of registering a second client.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_DynamicClientRegistration_ReusesPersistedClientIdAcrossRestart()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                EnableDynamicClientRegistration = true
            });
            var presenter = new AutoApproveConsentPresenter(server);
            var store = CreateStore();
            var options = CreateOptions();
            options.ClientId = null;
            options.ConsentPresenter = presenter.PresentAsync;
            var client = CreateClient(server, options, CreateCache(store, "anonymous"));

            var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            token.ClientId.Should().StartWith("dcr-");
            token.RefreshToken.Should().NotBeNullOrWhiteSpace();
            server.HitCount("register").Should().Be(1);
            server.HitCount("devicecode").Should().Be(1);

            var restartOptions = CreateOptions();
            restartOptions.ClientId = null;
            restartOptions.ConsentPresenter = presenter.PresentAsync;
            var restartCache = CreateCache(store, "anonymous");
            var restarted = CreateClient(server, restartOptions, restartCache);
            var cached = await restarted.GetValidTokenAsync(CancellationToken.None);

            cached!.AccessToken.Should().Be(token.AccessToken);
            server.HitCount("register").Should().Be(1);
            server.HitCount("devicecode").Should().Be(1);

            await restartCache.StoreTokensAsync(new SdkAuth.TokenContainer
            {
                AccessToken = token.AccessToken,
                AuthorizationServer = token.AuthorizationServer,
                ClientId = token.ClientId,
                ExpiresIn = 3600,
                ObtainedAt = DateTimeOffset.UtcNow.AddHours(-2),
                RefreshToken = token.RefreshToken,
                Scope = token.Scope,
                TokenEndpointAuthMethod = token.TokenEndpointAuthMethod,
                TokenType = token.TokenType
            }, CancellationToken.None);

            var refreshed = await restarted.GetValidTokenAsync(CancellationToken.None);

            refreshed.Should().NotBeNull();
            refreshed!.ClientId.Should().Be(token.ClientId);
            server.HitCount("register").Should().Be(1);
            server.HitCount("devicecode").Should().Be(1);
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeRefreshToken}").Should().Be(1);
        }

        /// <summary>
        /// The enterprise identity assertion grant runs without a human: the id token on disk is exchanged at
        /// the identity provider for an identity assertion, and that assertion is redeemed at the authorization
        /// server's RFC 7523 JWT bearer grant.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_IdentityAssertionGrant_ExchangesIdTokenForAccessToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var presenter = new AutoApproveConsentPresenter(server);
            var idTokenFile = Path.Combine(Path.GetTempPath(), $"odata-mcp-id-token-{Guid.NewGuid():N}.jwt");

            await File.WriteAllTextAsync(idTokenFile, server.IssueIdToken("alice@contoso.example"), CancellationToken.None);

            try
            {
                var options = CreateOptions();
                options.ConsentPresenter = presenter.PresentAsync;
                options.Grant = OutboundGrantKind.IdentityAssertion;
                options.IdpClientId = LocalAuthorizationServer.IdpClientId;
                options.IdpClientSecret = LocalAuthorizationServer.IdpClientSecret;
                options.IdpIdTokenFile = idTokenFile;
                options.IdpTokenEndpoint = server.IdpTokenEndpoint;
                options.IdpUrl = server.IdpIssuer;
                var cache = CreateCache();
                var client = CreateClient(server, options, cache);

                var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: false, CancellationToken.None);

                token.AccessToken.Should().NotBeNullOrWhiteSpace();
                token.ClientId.Should().Be("cli");
                presenter.Presentations.Should().Be(0);
                server.HitCount("devicecode").Should().Be(0);
                server.HitCount("idp-token").Should().Be(1);
                server.HitCount($"token:{ODataMcpAuthConstants.JwtBearerGrantType}").Should().Be(1);
                (await cache.GetTokensAsync(CancellationToken.None))!.AccessToken.Should().Be(token.AccessToken);
            }
            finally
            {
                File.Delete(idTokenFile);
            }
        }

        /// <summary>
        /// An authorization server that neither registers clients nor accepts a metadata document leaves the
        /// operator one action, and the message names it.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_NoClientIdAndNoRegistration_ThrowsRemediation()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var options = CreateOptions();
            options.ClientId = null;
            options.ConsentPresenter = (_, _) => Task.CompletedTask;
            var client = CreateClient(server, options, CreateCache());

            Func<Task> act = () => client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            await act.Should().ThrowAsync<OutboundDiscoveryException>()
                .WithMessage("Authorization server does not advertise dynamic client registration; pass --client-id.");
            server.HitCount("devicecode").Should().Be(0);
        }

        /// <summary>
        /// With no presenter configured the grant is started but never awaited: the caller receives the consent
        /// request as an exception carrying a correlation id and the user code, and nothing that could identify
        /// the device code.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_NoPresenter_ThrowsConsentRequiredWithoutSecrets()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var options = CreateOptions();
            var client = CreateClient(server, options, CreateCache());

            Func<Task> act = () => client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            var assertion = await act.Should().ThrowAsync<OAuthConsentRequiredException>();
            var request = assertion.Which.Request;
            var deviceCode = client._pendingDevice!.DeviceCode;

            Guid.TryParseExact(request.ElicitationId, "D", out _).Should().BeTrue();
            request.UserCode.Should().NotBeNullOrWhiteSpace();
            request.Kind.Should().Be(OutboundGrantKind.DeviceCode);
            request.Message.Should().NotContain(deviceCode);
            request.Url.ToString().Should().NotContain(deviceCode);
            server.HitCount($"token:{ODataMcpAuthConstants.DeviceCodeGrantType}").Should().Be(0);
        }

        /// <summary>
        /// When no protected resource metadata document was found and no <c>--resource</c> was passed, neither
        /// the device authorization request nor the token request carries an RFC 8707 <c>resource</c> at all:
        /// per step 8b the grant relies on <c>scope</c> rather than asking for an audience nobody published.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_NoPublishedResource_SendsNoResourceIndicator()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.NotFound
            });
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ConsentPresenter = presenter.PresentAsync;
            var client = CreateClient(server, options, CreateCache());

            var token = await client.AcquireAsync(
                [$"Bearer realm=\"\", authorization_uri=\"{server.AuthorizationEndpoint}\""],
                401,
                interactiveAllowed: true,
                CancellationToken.None);

            token.AccessToken.Should().NotBeNullOrWhiteSpace();
            server.LastDeviceAuthorizationRequest.Should().NotBeNull();
            server.LastDeviceAuthorizationRequest!.Should().NotContainKey(ODataMcpAuthConstants.ResourceParameter);
            server.LastTokenRequest.Should().NotBeNull();
            server.LastTokenRequest!.Should().NotContainKey(ODataMcpAuthConstants.ResourceParameter);
        }

        /// <summary>
        /// A presenter that throws leaves no pending grant behind: the next acquisition starts a fresh device
        /// code rather than failing forever with "an interactive sign-in is already pending".
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_PresenterThrows_ClearsPendingGrant()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var options = CreateOptions();
            options.ConsentPresenter = (_, _) => throw new InvalidOperationException("no browser here");
            var client = CreateClient(server, options, CreateCache());

            Func<Task> failing = () => client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            await failing.Should().ThrowAsync<InvalidOperationException>().WithMessage("no browser here");
            client._pendingDevice.Should().BeNull();

            var presenter = new AutoApproveConsentPresenter(server);
            options.ConsentPresenter = presenter.PresentAsync;

            var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            token.AccessToken.Should().NotBeNullOrWhiteSpace();
            server.HitCount("devicecode").Should().Be(2);
        }

        /// <summary>
        /// A protected resource metadata document that publishes <c>resource</c> puts it on both the device
        /// authorization request and the token request, spelled exactly as the document did — no trailing slash
        /// <see cref="Uri"/> would have added, because the authorization server compares audiences as strings.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_PrmPublishesResource_SendsResourceIndicatorVerbatim()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ConsentPresenter = presenter.PresentAsync;
            var client = CreateClient(server, options, CreateCache());

            var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            token.AccessToken.Should().NotBeNullOrWhiteSpace();
            server.Options.ResourceUri.Should().Be("http://localhost");
            server.LastDeviceAuthorizationRequest.Should().NotBeNull();
            server.LastDeviceAuthorizationRequest![ODataMcpAuthConstants.ResourceParameter].Should().Be(server.Options.ResourceUri);
            server.LastTokenRequest.Should().NotBeNull();
            server.LastTokenRequest![ODataMcpAuthConstants.ResourceParameter].Should().Be(server.Options.ResourceUri);
        }

        /// <summary>
        /// A sign-in nobody completes gives up after <see cref="OutboundOAuthOptions.AuthTimeout"/> and also
        /// releases the pending grant, so a later attempt is not poisoned by the abandoned one.
        /// </summary>
        [TestMethod]
        public async Task AcquireAsync_SignInTimesOut_ThrowsExpiredTokenAndClearsPendingGrant()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var options = CreateOptions();
            options.AuthTimeout = TimeSpan.FromSeconds(1);
            options.ConsentPresenter = (_, _) => Task.CompletedTask;
            var client = CreateClient(server, options, CreateCache());

            Func<Task> timingOut = () => client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            var assertion = await timingOut.Should().ThrowAsync<OAuthTokenException>();

            assertion.Which.Error.Should().Be(ODataMcpAuthConstants.ErrorExpiredToken);
            assertion.Which.ErrorDescription.Should().Be("Sign-in timed out.");
            client._pendingDevice.Should().BeNull();

            var presenter = new AutoApproveConsentPresenter(server);
            options.AuthTimeout = TimeSpan.FromSeconds(60);
            options.ConsentPresenter = presenter.PresentAsync;

            var token = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            token.AccessToken.Should().NotBeNullOrWhiteSpace();
            server.HitCount("devicecode").Should().Be(2);
        }

        /// <summary>
        /// Removes the throwaway credential store directory the class created.
        /// </summary>
        [ClassCleanup]
        public static void Cleanup()
        {
            if (Directory.Exists(TokenCacheDirectory))
            {
                Directory.Delete(TokenCacheDirectory, recursive: true);
            }
        }

        /// <summary>
        /// A forced refresh replaces the access token even though the cached one still has an hour left, which
        /// is what the <c>401 invalid_token</c> path needs when the resource server disagrees with
        /// <c>expires_in</c>.
        /// </summary>
        [TestMethod]
        public async Task ForceRefreshAsync_ValidToken_ReturnsNewAccessToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ConsentPresenter = presenter.PresentAsync;
            var client = CreateClient(server, options, CreateCache());
            var original = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            var refreshed = await client.ForceRefreshAsync(CancellationToken.None);

            refreshed.Should().NotBeNull();
            refreshed!.AccessToken.Should().NotBe(original.AccessToken);
            refreshed.RefreshToken.Should().NotBeNullOrWhiteSpace();
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeRefreshToken}").Should().Be(1);
        }

        /// <summary>
        /// Five callers that all observe a token inside the refresh skew share exactly one token POST: the
        /// first through the gate refreshes, the rest re-read the cache and reuse what it stored.
        /// </summary>
        [TestMethod]
        public async Task GetValidTokenAsync_ConcurrentInsideSkew_RefreshesOnce()
        {
            var serverOptions = new LocalAuthorizationServerOptions
            {
                AccessTokenLifetime = TimeSpan.FromSeconds(20)
            };
            using var server = new LocalAuthorizationServer(serverOptions);
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ConsentPresenter = presenter.PresentAsync;
            var client = CreateClient(server, options, CreateCache());

            await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            var results = await Task.WhenAll(Enumerable
                .Range(0, 5)
                .Select(_ => client.GetValidTokenAsync(CancellationToken.None)));

            results.Should().OnlyContain(token => token != null);
            results.Select(token => token!.AccessToken).Distinct(StringComparer.Ordinal).Should().HaveCount(1);
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeRefreshToken}").Should().Be(1);
        }

        /// <summary>
        /// A refresh token the authorization server has already rotated away is not retried forever: the
        /// failure is reported, the slot is dropped, and the caller is told there is nothing to attach.
        /// </summary>
        [TestMethod]
        public async Task GetValidTokenAsync_RefreshRejected_ClearsCacheAndReturnsNull()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ConsentPresenter = presenter.PresentAsync;
            var cache = CreateCache();
            var logs = new CapturingLoggerProvider();
            var client = CreateClient(server, options, cache, logs);
            var original = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);

            await client.ForceRefreshAsync(CancellationToken.None);
            await cache.StoreTokensAsync(new SdkAuth.TokenContainer
            {
                AccessToken = original.AccessToken,
                AuthorizationServer = original.AuthorizationServer,
                ClientId = original.ClientId,
                ExpiresIn = 3600,
                ObtainedAt = DateTimeOffset.UtcNow.AddHours(-2),
                RefreshToken = original.RefreshToken,
                Scope = original.Scope,
                TokenEndpointAuthMethod = original.TokenEndpointAuthMethod,
                TokenType = original.TokenType
            }, CancellationToken.None);

            var token = await client.GetValidTokenAsync(CancellationToken.None);

            token.Should().BeNull();
            (await cache.GetTokensAsync(CancellationToken.None)).Should().BeNull();
            logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning);
            logs.AllText.Should().Contain("Refresh failed");
            logs.AllText.Should().NotContain(original.RefreshToken!);
        }

        /// <summary>
        /// A client credentials token has no refresh token to spend, so a stale one is re-acquired silently —
        /// a daemon must never be pushed onto the interactive path just because its access token aged out.
        /// </summary>
        [TestMethod]
        public async Task GetValidTokenAsync_StaleClientCredentialsToken_ReacquiresSilently()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ClientId = "daemon";
            options.ClientSecret = "daemon-secret";
            options.ConsentPresenter = presenter.PresentAsync;
            options.Grant = OutboundGrantKind.ClientCredentials;
            var cache = CreateCache();
            var client = CreateClient(server, options, cache);
            var original = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: false, CancellationToken.None);

            await cache.StoreTokensAsync(new SdkAuth.TokenContainer
            {
                AccessToken = original.AccessToken,
                AuthorizationServer = original.AuthorizationServer,
                ClientId = original.ClientId,
                ClientSecret = original.ClientSecret,
                ExpiresIn = 3600,
                ObtainedAt = DateTimeOffset.UtcNow.AddHours(-2),
                Scope = original.Scope,
                TokenEndpointAuthMethod = original.TokenEndpointAuthMethod,
                TokenType = original.TokenType
            }, CancellationToken.None);

            var token = await client.GetValidTokenAsync(CancellationToken.None);

            token.Should().NotBeNull();
            token!.AccessToken.Should().NotBe(original.AccessToken);
            token.ClientId.Should().Be("daemon");
            presenter.Presentations.Should().Be(0);
            server.HitCount("devicecode").Should().Be(0);
            server.HitCount($"token:{ODataMcpAuthConstants.GrantTypeClientCredentials}").Should().Be(2);
        }

        /// <summary>
        /// A cached token with an hour left is attached as-is: no discovery, no device code, and no token POST.
        /// </summary>
        [TestMethod]
        public async Task GetValidTokenAsync_TokenWellInsideLifetime_SendsNoTokenRequest()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ConsentPresenter = presenter.PresentAsync;
            var client = CreateClient(server, options, CreateCache());
            var acquired = await client.AcquireAsync(Challenge(server), 401, interactiveAllowed: true, CancellationToken.None);
            var tokenHitsAfterAcquire = server.HitCount("token");

            var token = await client.GetValidTokenAsync(CancellationToken.None);

            token.Should().NotBeNull();
            token!.AccessToken.Should().Be(acquired.AccessToken);
            server.HitCount("token").Should().Be(tokenHitsAfterAcquire);
        }

        /// <summary>
        /// A second interactive start while one is already pending is refused rather than silently abandoning
        /// the device code a human may already be typing.
        /// </summary>
        [TestMethod]
        public async Task StartInteractiveGrantAsync_AlreadyPending_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var client = CreateClient(server, CreateOptions(), CreateCache());

            var first = await client.StartInteractiveGrantAsync(Challenge(server), 401, CancellationToken.None);
            Func<Task> act = () => client.StartInteractiveGrantAsync(Challenge(server), 401, CancellationToken.None);

            first.UserCode.Should().NotBeNullOrWhiteSpace();
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("An interactive sign-in is already pending.");
            server.HitCount("devicecode").Should().Be(1);
        }

        /// <summary>
        /// A second authorization code start is refused while a loopback listener is already bound, so a second
        /// port is never opened behind the operator's back; abandoning the first releases it.
        /// </summary>
        [TestMethod]
        public async Task StartInteractiveGrantAsync_AuthorizationCodeAlreadyPending_Throws()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var options = CreateOptions();
            options.Grant = OutboundGrantKind.AuthorizationCode;
            var client = CreateClient(server, options, CreateCache());

            var first = await client.StartInteractiveGrantAsync(Challenge(server), 401, CancellationToken.None);
            Func<Task> act = () => client.StartInteractiveGrantAsync(Challenge(server), 401, CancellationToken.None);

            first.Kind.Should().Be(OutboundGrantKind.AuthorizationCode);
            first.UserCode.Should().BeNull();
            first.Url.Query.Should().Contain("code_challenge_method=S256");
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("An interactive sign-in is already pending.");
            server.HitCount("authorize").Should().Be(0);

            await client.ClearPendingGrantAsync();

            client._pendingLoopback.Should().BeNull();
        }

        /// <summary>
        /// A device authorization response that names a <c>file:</c> verification URI is refused before any
        /// presenter, browser, or MCP elicitation could open it, and leaves no pending grant behind: a
        /// malicious resource cannot turn a sign-in prompt into a local <c>ShellExecute</c>.
        /// </summary>
        [TestMethod]
        public async Task StartInteractiveGrantAsync_NonPresentableVerificationUri_ThrowsAndLeavesNoPendingState()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                DeviceVerificationUriScheme = "file"
            });
            var presenter = new AutoApproveConsentPresenter(server);
            var options = CreateOptions();
            options.ConsentPresenter = presenter.PresentAsync;
            var client = CreateClient(server, options, CreateCache());

            Func<Task> act = () => client.StartInteractiveGrantAsync(Challenge(server), 401, CancellationToken.None);

            await act.Should().ThrowAsync<OutboundDiscoveryException>()
                .WithMessage("Consent URL must use https (or http on loopback): file");
            presenter.Presentations.Should().Be(0);
            client._pendingDevice.Should().BeNull();
            client._pendingDiscovery.Should().BeNull();
            client._pendingLoopback.Should().BeNull();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the <c>WWW-Authenticate</c> challenge a protected resource backed by <paramref name="server"/>
        /// answers an unauthenticated request with.
        /// </summary>
        /// <param name="server">The authorization server whose RFC 9728 document the challenge names.</param>
        /// <returns>
        /// A single-element list holding the raw header value.
        /// </returns>
        internal static IReadOnlyList<string> Challenge(LocalAuthorizationServer server)
        {
            return [$"Bearer resource_metadata=\"{server.ProtectedResourceMetadataUri}\""];
        }

        /// <summary>
        /// Builds a <see cref="LatchkeyTokenCache"/> over a private, process-local in-memory store, so the test
        /// needs neither an OS keyring nor the disk.
        /// </summary>
        /// <returns>
        /// A cache whose backing store is private to this call.
        /// </returns>
        internal static LatchkeyTokenCache CreateCache()
        {
            return CreateCache(CreateStore(), "cli");
        }

        /// <summary>
        /// Builds a <see cref="LatchkeyTokenCache"/> over an existing store, so two clients can share one slot
        /// the way a restart shares the operator's credential store.
        /// </summary>
        /// <param name="store">The backing store both caches read and write.</param>
        /// <param name="clientId">The client id the Latchkey key is derived from, <c>anonymous</c> when dynamic client registration supplies one.</param>
        /// <returns>
        /// A cache addressing the single slot for <see cref="ServiceRoot"/> and <paramref name="clientId"/>.
        /// </returns>
        internal static LatchkeyTokenCache CreateCache(ILatchkey store, string clientId)
        {
            return new LatchkeyTokenCache(store, LatchkeyTokenCache.ComputeKey(ServiceRoot, clientId), NullLogger<LatchkeyTokenCache>.Instance);
        }

        /// <summary>
        /// Builds an <see cref="OutboundOAuthClient"/> whose <c>"OAuth"</c> named client dispatches straight
        /// into <paramref name="server"/>, via a real dependency injection container.
        /// </summary>
        /// <param name="server">The authorization server the client talks to.</param>
        /// <param name="options">The operator's outbound OAuth settings.</param>
        /// <param name="cache">The token cache the client persists into.</param>
        /// <returns>
        /// The client under test.
        /// </returns>
        internal static OutboundOAuthClient CreateClient(LocalAuthorizationServer server, OutboundOAuthOptions options, LatchkeyTokenCache cache)
        {
            return CreateClient(server, options, cache, null);
        }

        /// <summary>
        /// Builds an <see cref="OutboundOAuthClient"/> whose <c>"OAuth"</c> named client dispatches straight
        /// into <paramref name="server"/> and whose logging is optionally captured.
        /// </summary>
        /// <param name="server">The authorization server the client talks to.</param>
        /// <param name="options">The operator's outbound OAuth settings.</param>
        /// <param name="cache">The token cache the client persists into.</param>
        /// <param name="logs">The provider every log message is captured to, or <see langword="null"/> to discard logs.</param>
        /// <returns>
        /// The client under test.
        /// </returns>
        internal static OutboundOAuthClient CreateClient(LocalAuthorizationServer server, OutboundOAuthOptions options, LatchkeyTokenCache cache, CapturingLoggerProvider? logs)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => server.Handler);
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                if (logs is not null)
                {
                    builder.AddProvider(logs);
                }
            });
            var provider = services.BuildServiceProvider();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var discovery = new OAuthDiscovery(
                new ProtectedResourceMetadataClient(httpClientFactory, loggerFactory.CreateLogger<ProtectedResourceMetadataClient>()),
                new AuthorizationServerMetadataClient(httpClientFactory, loggerFactory.CreateLogger<AuthorizationServerMetadataClient>()),
                loggerFactory.CreateLogger<OAuthDiscovery>());

            return new OutboundOAuthClient(ServiceRoot, httpClientFactory, options, cache, discovery, loggerFactory);
        }

        /// <summary>
        /// Builds the options every test starts from: the fixture's seeded public client and a fallback scope.
        /// </summary>
        /// <returns>
        /// The options.
        /// </returns>
        internal static OutboundOAuthOptions CreateOptions()
        {
            return new OutboundOAuthOptions
            {
                ClientId = "cli",
                Scopes = ["read"],
                TokenCachePath = TokenCacheDirectory
            };
        }

        /// <summary>
        /// Creates a private, process-local Latchkey store two caches can share.
        /// </summary>
        /// <returns>
        /// A store whose contents are visible only to the caches built over it.
        /// </returns>
        internal static ILatchkey CreateStore()
        {
            return LatchkeyFactory.Create(new LatchkeyOptions
            {
                ServiceName = ODataMcpAuthConstants.LatchkeyServiceName,
                Backend = LatchkeyBackend.InMemory
            });
        }

        #endregion

    }

}
