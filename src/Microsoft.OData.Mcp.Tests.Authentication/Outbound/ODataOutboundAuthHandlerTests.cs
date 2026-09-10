// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Latchkey;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="ODataOutboundAuthHandler"/> as the <c>"OData"</c> named client's delegating handler
    /// against a real <see cref="SecuredStaticResourceServer"/> and a real
    /// <see cref="LocalAuthorizationServer"/>, with no mocking anywhere in the chain.
    /// </summary>
    /// <remarks>
    /// The <c>"OAuth"</c> named client is deliberately registered without this handler, so the discovery and
    /// token traffic the handler triggers can never re-enter the handler and recurse.
    /// </remarks>
    [TestClass]
    public class ODataOutboundAuthHandlerTests
    {

        #region Properties

        /// <summary>
        /// Gets the service root the header-echoing test server is addressed by.
        /// </summary>
        internal static Uri EchoRoot { get; } = new("http://localhost/echo/");

        /// <summary>
        /// Gets the throwaway directory the credential store round-trip check runs in, so these tests never
        /// touch the operator's own token cache.
        /// </summary>
        internal static string TokenCacheDirectory { get; } = Path.Combine(Path.GetTempPath(), $"odata-mcp-handler-{Guid.NewGuid():N}");

        #endregion

        #region Public Methods

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
        /// The interactive decision the handler hands the acquisition path is derived from the options rather
        /// than hard-coded: a grant the operator pinned to a non-interactive kind never prompts, an explicit
        /// credential with no presenter never prompts, and everything else may.
        /// </summary>
        [TestMethod]
        public void IsInteractiveAllowed_OptionsTable_DerivesFromGrantAndPresenter()
        {
            Func<OutboundConsentRequest, CancellationToken, Task> presenter = (_, _) => Task.CompletedTask;

            ODataOutboundAuthHandler.IsInteractiveAllowed(new OutboundOAuthOptions()).Should().BeTrue();
            ODataOutboundAuthHandler.IsInteractiveAllowed(new OutboundOAuthOptions { Grant = OutboundGrantKind.DeviceCode }).Should().BeTrue();
            ODataOutboundAuthHandler.IsInteractiveAllowed(new OutboundOAuthOptions { Grant = OutboundGrantKind.AuthorizationCode }).Should().BeTrue();
            ODataOutboundAuthHandler.IsInteractiveAllowed(new OutboundOAuthOptions { Grant = OutboundGrantKind.ClientCredentials }).Should().BeFalse();
            ODataOutboundAuthHandler.IsInteractiveAllowed(new OutboundOAuthOptions { Grant = OutboundGrantKind.IdentityAssertion }).Should().BeFalse();
            ODataOutboundAuthHandler.IsInteractiveAllowed(new OutboundOAuthOptions { AuthToken = "pasted" }).Should().BeFalse();
            ODataOutboundAuthHandler.IsInteractiveAllowed(new OutboundOAuthOptions { AuthToken = "pasted", ConsentPresenter = presenter }).Should().BeTrue();
        }

        /// <summary>
        /// An API key is sent under the operator's header name and nothing about OAuth runs: no discovery, no
        /// device authorization, no token endpoint.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_ApiKey_SendsConfiguredHeaderAndSkipsOAuth()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var echo = CreateEchoServer(headers);
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var options = new OutboundOAuthOptions
            {
                ApiKey = "k-123",
                ApiKeyHeader = "X-Api-Key"
            };
            using var provider = CreateProvider(echo.CreateHandler(), EchoRoot, authorizationServer, options, CreateCache());

            using var response = await CreateODataClient(provider).GetAsync("ping");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            headers.Should().ContainKey("X-Api-Key").WhoseValue.Should().Be("k-123");
            headers.Should().NotContainKey("Authorization");
            authorizationServer.HitCount("prm").Should().Be(0);
            authorizationServer.HitCount("token").Should().Be(0);
        }

        /// <summary>
        /// RFC 7617 Basic credentials are base64-encoded onto the <c>Authorization</c> header and, like every
        /// explicit credential, bypass the OAuth path entirely.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_BasicCredentials_SendsAuthorizationBasic()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var echo = CreateEchoServer(headers);
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var options = new OutboundOAuthOptions
            {
                BasicUser = "ada",
                BasicPassword = "lovelace"
            };
            using var provider = CreateProvider(echo.CreateHandler(), EchoRoot, authorizationServer, options, CreateCache());

            using var response = await CreateODataClient(provider).GetAsync("ping");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            headers.Should().ContainKey("Authorization").WhoseValue.Should().Be("Basic YWRhOmxvdmVsYWNl");
            authorizationServer.HitCount("token").Should().Be(0);
        }

        /// <summary>
        /// A <c>403</c> on a request that already carried a valid token is an authorization answer, not a
        /// sign-in problem: it is returned to the caller untouched rather than triggering discovery and a
        /// device code grant that would block the process on a prompt no new token can satisfy.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_ForbiddenWithValidToken_ReturnsForbiddenWithoutAcquiring()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var options = CreateOAuthOptions(presenter);
            using var provider = CreateProvider(resource.Server.CreateHandler(), resource.ServiceRoot, authorizationServer, options, CreateCache());
            var client = CreateODataClient(provider);

            using (var primed = await client.GetAsync("$metadata"))
            {
                primed.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            var tokenHitsAfterPriming = authorizationServer.HitCount("token");

            using var response = await client.GetAsync("Forbidden");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            resource.HitCount("forbidden").Should().Be(1);
            authorizationServer.HitCount("devicecode").Should().Be(1);
            authorizationServer.HitCount("token").Should().Be(tokenHitsAfterPriming);
            presenter.Presentations.Should().Be(1);
        }

        /// <summary>
        /// A token this send already refreshed on the way in is not refreshed a second time when the resource
        /// still answers <c>invalid_token</c>: one challenge costs one token POST, and the <c>401</c> is
        /// returned rather than answered forever.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_InvalidTokenAfterSendPathRefresh_DoesNotRefreshTwice()
        {
            var serverOptions = new LocalAuthorizationServerOptions();
            using var authorizationServer = new LocalAuthorizationServer(serverOptions);
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var options = CreateOAuthOptions(presenter);
            var cache = CreateCache();
            using var provider = CreateProvider(resource.Server.CreateHandler(), resource.ServiceRoot, authorizationServer, options, cache);
            var client = CreateODataClient(provider);

            using (var primed = await client.GetAsync("$metadata"))
            {
                primed.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            var acquired = await cache.GetTokensAsync(CancellationToken.None);
            await cache.StoreTokensAsync(new SdkAuth.TokenContainer
            {
                AccessToken = acquired!.AccessToken,
                AuthorizationServer = acquired.AuthorizationServer,
                ClientId = acquired.ClientId,
                ExpiresIn = 3600,
                ObtainedAt = DateTimeOffset.UtcNow.AddHours(-2),
                RefreshToken = acquired.RefreshToken,
                Scope = acquired.Scope,
                TokenEndpointAuthMethod = acquired.TokenEndpointAuthMethod,
                TokenType = acquired.TokenType
            }, CancellationToken.None);
            serverOptions.RevokeAllTokens = true;

            using var response = await client.GetAsync("Customers");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            authorizationServer.HitCount($"token:{ODataMcpAuthConstants.GrantTypeRefreshToken}").Should().Be(1);
            authorizationServer.HitCount("devicecode").Should().Be(1);
            resource.HitCount("customers").Should().Be(1);
        }

        /// <summary>
        /// When the single refresh the send path allows itself cannot produce a token — here because the
        /// authorization server has ended the session behind the refresh token — the send falls through to the
        /// acquisition path rather than returning the <c>401</c>: one new grant, one retry, and the data call
        /// succeeds.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_InvalidTokenAndRefreshRejected_AcquiresOnceAndRetries()
        {
            var serverOptions = new LocalAuthorizationServerOptions();
            using var authorizationServer = new LocalAuthorizationServer(serverOptions);
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var options = CreateOAuthOptions(presenter);
            using var provider = CreateProvider(resource.Server.CreateHandler(), resource.ServiceRoot, authorizationServer, options, CreateCache());
            var client = CreateODataClient(provider);

            using (var primed = await client.GetAsync("$metadata"))
            {
                primed.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            serverOptions.RejectRefresh = true;
            authorizationServer.RevokeIssuedTokens();

            using var response = await client.GetAsync("Customers");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Contoso");
            authorizationServer.HitCount($"token:{ODataMcpAuthConstants.GrantTypeRefreshToken}").Should().Be(1);
            authorizationServer.HitCount("devicecode").Should().Be(2);
            presenter.Presentations.Should().Be(2);
        }

        /// <summary>
        /// A cached token that the resource server has stopped accepting produces one <c>refresh_token</c> POST
        /// and one retry, not a fresh device code grant.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_InvalidTokenChallenge_RefreshesOnceAndRetries()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var options = CreateOAuthOptions(presenter);
            using var provider = CreateProvider(resource.Server.CreateHandler(), resource.ServiceRoot, authorizationServer, options, CreateCache());
            var client = CreateODataClient(provider);

            using (var primed = await client.GetAsync("$metadata"))
            {
                primed.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            authorizationServer.RevokeIssuedTokens();

            using var response = await client.GetAsync("Customers");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Contoso");
            resource.HitCount("customers").Should().Be(2);
            authorizationServer.HitCount($"token:{ODataMcpAuthConstants.GrantTypeRefreshToken}").Should().Be(1);
            authorizationServer.HitCount("devicecode").Should().Be(1);
        }

        /// <summary>
        /// A daemon pinned to <c>--grant client_credentials</c> never prompts: a resource that rejects even a
        /// freshly issued token surfaces as the <c>401</c> it is, and no device code is ever started.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_ClientCredentialsGrant_NeverPromptsOnRejection()
        {
            var serverOptions = new LocalAuthorizationServerOptions
            {
                RevokeAllTokens = true
            };
            using var authorizationServer = new LocalAuthorizationServer(serverOptions);
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var options = CreateOAuthOptions(presenter);
            options.ClientId = "daemon";
            options.ClientSecret = "daemon-secret";
            options.Grant = OutboundGrantKind.ClientCredentials;
            using var provider = CreateProvider(resource.Server.CreateHandler(), resource.ServiceRoot, authorizationServer, options, CreateCache());
            var client = CreateODataClient(provider);

            using var response = await client.GetAsync("$metadata");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            presenter.Presentations.Should().Be(0);
            authorizationServer.HitCount("devicecode").Should().Be(0);
            authorizationServer.HitCount($"token:{ODataMcpAuthConstants.GrantTypeClientCredentials}").Should().Be(1);
        }

        /// <summary>
        /// An empty cache costs one unauthenticated probe, one device code grant, and one authenticated retry —
        /// and nothing more on the next request, because the token is now cached with an hour of life left.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_NoCachedToken_RunsDeviceFlowThenReusesToken()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var options = CreateOAuthOptions(presenter);
            using var provider = CreateProvider(resource.Server.CreateHandler(), resource.ServiceRoot, authorizationServer, options, CreateCache());
            var client = CreateODataClient(provider);

            using var first = await client.GetAsync("$metadata");
            var body = await first.Content.ReadAsStringAsync();
            var tokenHitsAfterFirst = authorizationServer.HitCount("token");

            using var second = await client.GetAsync("$metadata");

            first.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("EntitySet");
            resource.HitCount("metadata").Should().Be(3);
            authorizationServer.HitCount("devicecode").Should().Be(1);
            presenter.Presentations.Should().Be(1);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            authorizationServer.HitCount("token").Should().Be(tokenHitsAfterFirst);
        }

        /// <summary>
        /// A <c>401</c> whose <c>WWW-Authenticate</c> value <see cref="System.Net.Http.Headers.HttpHeaders"/>
        /// refuses to parse still discovers and acquires: the handler reads the raw header rather than the
        /// typed collection, which such a value empties.
        /// </summary>
        /// <remarks>
        /// The challenge names a second scheme spelled <c>Negotiate/SPNEGO</c>. A scheme may not carry
        /// <c>/</c>, so the typed parser rejects the whole value and moves it to the invalid bucket — taking the
        /// perfectly good <c>Bearer</c> challenge with it — while the raw header is unchanged. Reading the typed
        /// collection here would report "401 with no WWW-Authenticate" and strand the operator.
        /// </remarks>
        [TestMethod]
        public async Task SendAsync_NonstandardChallengeValue_StillDiscoversAndAcquires()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            var challenge = $"Bearer resource_metadata=\"{authorizationServer.ProtectedResourceMetadataUri}\", Negotiate/SPNEGO";
            using var resource = new SecuredStaticResourceServer(
                authorizationServer,
                SecuredStaticResourceServer.DefaultCsdl,
                challengeOverride: challenge);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var options = CreateOAuthOptions(presenter);
            using var provider = CreateProvider(resource.Server.CreateHandler(), resource.ServiceRoot, authorizationServer, options, CreateCache());
            var client = CreateODataClient(provider);

            using var probeClient = resource.Server.CreateClient();
            using var probe = await probeClient.GetAsync("odata/$metadata");

            probe.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            probe.Headers.WwwAuthenticate.Count.Should().Be(0);
            probe.Headers.TryGetValues(ODataMcpAuthConstants.WwwAuthenticateHeader, out var raw).Should().BeTrue();
            raw.Should().ContainSingle().Which.Should().Be(challenge);

            using var response = await client.GetAsync("$metadata");
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("EntitySet");
            presenter.Presentations.Should().Be(1);
            authorizationServer.HitCount("prm").Should().Be(1);
            authorizationServer.HitCount("devicecode").Should().Be(1);
        }

        /// <summary>
        /// The <c>"OAuth"</c> named client carries no outbound handler, so a Graph-shaped protected resource
        /// metadata document that answers <c>401</c> costs exactly one GET and never re-enters discovery.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_OAuthClient_HasNoHandlerSoAChallengeNeverRecurses()
        {
            var serverOptions = new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.Unauthorized
            };
            using var authorizationServer = new LocalAuthorizationServer(serverOptions);
            using var echo = CreateEchoServer(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var options = CreateOAuthOptions(presenter);
            using var provider = CreateProvider(echo.CreateHandler(), EchoRoot, authorizationServer, options, CreateCache());
            var oauthClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ODataMcpAuthConstants.OAuthHttpClientName);

            using var response = await oauthClient.GetAsync(authorizationServer.ProtectedResourceMetadataUri);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            authorizationServer.HitCount("prm").Should().Be(1);
            authorizationServer.HitCount("devicecode").Should().Be(0);
            authorizationServer.HitCount("token").Should().Be(0);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a <see cref="LatchkeyTokenCache"/> over a private, process-local in-memory store.
        /// </summary>
        /// <returns>
        /// A cache whose backing store is private to this call.
        /// </returns>
        internal static LatchkeyTokenCache CreateCache()
        {
            var store = LatchkeyFactory.Create(new LatchkeyOptions
            {
                ServiceName = ODataMcpAuthConstants.LatchkeyServiceName,
                Backend = LatchkeyBackend.InMemory
            });

            return new LatchkeyTokenCache(store, LatchkeyTokenCache.ComputeKey(new Uri("http://localhost/odata/"), "cli"), NullLogger<LatchkeyTokenCache>.Instance);
        }

        /// <summary>
        /// Starts a test server that records the headers of every request it receives and answers <c>200</c>.
        /// </summary>
        /// <param name="headers">The dictionary each request's headers are copied into.</param>
        /// <returns>
        /// The running test server; the caller disposes it.
        /// </returns>
        internal static TestServer CreateEchoServer(Dictionary<string, string> headers)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.Configure(app => app.Run(context =>
                    {
                        headers.Clear();
                        foreach (var header in context.Request.Headers)
                        {
                            headers[header.Key] = header.Value.ToString();
                        }

                        return context.Response.WriteAsync("ok");
                    }));
                })
                .Start();

            return host.GetTestServer();
        }

        /// <summary>
        /// Resolves the <c>"OData"</c> named client, whose chain ends in the handler under test.
        /// </summary>
        /// <param name="provider">The provider the client is created from.</param>
        /// <returns>
        /// The client.
        /// </returns>
        internal static HttpClient CreateODataClient(ServiceProvider provider)
        {
            return provider.GetRequiredService<IHttpClientFactory>().CreateClient(ODataMcpAuthConstants.ODataHttpClientName);
        }

        /// <summary>
        /// Builds the options an OAuth-authenticated test starts from.
        /// </summary>
        /// <param name="presenter">The presenter that stands in for a human approving the device code grant.</param>
        /// <returns>
        /// The options.
        /// </returns>
        internal static OutboundOAuthOptions CreateOAuthOptions(AutoApproveConsentPresenter presenter)
        {
            return new OutboundOAuthOptions
            {
                ClientId = "cli",
                ConsentPresenter = presenter.PresentAsync,
                Scopes = ["read"],
                TokenCachePath = TokenCacheDirectory
            };
        }

        /// <summary>
        /// Wires the two named clients exactly the way the Tools host does: <c>"OData"</c> with the handler
        /// under test in front of a real resource, and <c>"OAuth"</c> with no handler at all.
        /// </summary>
        /// <param name="odataHandler">The primary handler the <c>"OData"</c> named client dispatches into.</param>
        /// <param name="serviceRoot">The base address of the <c>"OData"</c> named client.</param>
        /// <param name="authorizationServer">The authorization server the <c>"OAuth"</c> named client dispatches into.</param>
        /// <param name="options">The operator's outbound OAuth settings.</param>
        /// <param name="cache">The token cache the outbound client persists into.</param>
        /// <returns>
        /// The built provider; the caller disposes it.
        /// </returns>
        internal static ServiceProvider CreateProvider(
            HttpMessageHandler odataHandler,
            Uri serviceRoot,
            LocalAuthorizationServer authorizationServer,
            OutboundOAuthOptions options,
            LatchkeyTokenCache cache)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace));
            services.AddSingleton(options);
            services.AddSingleton<SdkAuth.ITokenCache>(cache);
            services.AddSingleton<ProtectedResourceMetadataClient>();
            services.AddSingleton<AuthorizationServerMetadataClient>();
            services.AddSingleton<OAuthDiscovery>();
            services.AddSingleton(provider => new OutboundOAuthClient(
                serviceRoot,
                provider.GetRequiredService<IHttpClientFactory>(),
                options,
                provider.GetRequiredService<SdkAuth.ITokenCache>(),
                provider.GetRequiredService<OAuthDiscovery>(),
                provider.GetRequiredService<ILoggerFactory>()));
            services.AddTransient<ODataOutboundAuthHandler>();
            services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName, client => client.BaseAddress = serviceRoot)
                .ConfigurePrimaryHttpMessageHandler(() => odataHandler)
                .AddHttpMessageHandler(provider => provider.GetRequiredService<ODataOutboundAuthHandler>());
            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => authorizationServer.Handler);

            return services.BuildServiceProvider();
        }

        #endregion

    }

}
