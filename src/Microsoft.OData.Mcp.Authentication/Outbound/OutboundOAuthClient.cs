// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Latchkey;
using Microsoft.Extensions.Logging;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The outbound OAuth state machine: it owns one OData service root's cached token, decides whether that
    /// token can still be attached, refreshes it exactly once when several callers race, and runs the advertised
    /// grant when there is nothing left to refresh.
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new OutboundOAuthClient(
    ///     new Uri("https://api.example.com/odata/"),
    ///     httpClientFactory,
    ///     options,
    ///     tokenCache,
    ///     discovery,
    ///     loggerFactory);
    ///
    /// var token = await client.GetValidTokenAsync(cancellationToken)
    ///     ?? await client.AcquireAsync(challenges, 401, interactiveAllowed: true, cancellationToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// Nothing here ever logs an access token, a refresh token, a device code, an authorization code, or a PKCE
    /// code verifier: every token that reaches a log goes through
    /// <see cref="OutboundAuthLogRedactor.DescribeToken(SdkAuth.TokenContainer)"/> first. There is
    /// no background refresh — an idle process sends no token requests at all — because the send path checks
    /// remaining lifetime against <see cref="ODataMcpAuthConstants.TokenRefreshSkew"/> and a
    /// <c>401 invalid_token</c> remains the fallback for a token whose lifetime the client cannot compute.
    /// <para>
    /// The credential store is proved usable inside
    /// <see cref="AcquireAsync(IReadOnlyList{string}, int, bool, CancellationToken)"/> rather than at host
    /// construction, and only when no explicit credential was configured, so a service that needs no sign-in at
    /// all never touches the operating system keyring.
    /// </para>
    /// </remarks>
    public sealed class OutboundOAuthClient
    {

        #region Fields

        /// <summary>
        /// The RFC 6749 authorization code grant with RFC 7636 PKCE this client starts and completes over a
        /// loopback redirect.
        /// </summary>
        internal readonly AuthorizationCodePkceGrant _authorizationCodeGrant;

        /// <summary>
        /// The RFC 6749 section 4.4 client credentials grant this client runs for a daemon.
        /// </summary>
        internal readonly ClientCredentialsGrant _clientCredentialsGrant;

        /// <summary>
        /// Whether the "credential store unavailable" warning has already been written, so an unusable keyring
        /// costs one log line rather than one per OData request.
        /// </summary>
        /// <remarks>
        /// An <see cref="int"/> rather than a <see cref="bool"/> so
        /// <see cref="Interlocked.Exchange(ref int, int)"/> can claim the warning exactly once across
        /// concurrent sends.
        /// </remarks>
        internal int _credentialStoreUnavailableLogged;

        /// <summary>
        /// The RFC 8628 device authorization grant this client starts and completes.
        /// </summary>
        internal readonly DeviceCodeGrant _deviceCodeGrant;

        /// <summary>
        /// The discovery algorithm that turns a challenge into an authorization server and a resource indicator.
        /// </summary>
        internal readonly OAuthDiscovery _discovery;

        /// <summary>
        /// The RFC 7591 dynamic client registrar used when no <c>--client-id</c> was configured.
        /// </summary>
        internal readonly DynamicClientRegistrar _dynamicClientRegistrar;

        /// <summary>
        /// The factory the SDK identity assertion provider is handed the <c>"OAuth"</c> named client from.
        /// </summary>
        internal readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// The discovery result the most recent acquisition produced, reused so a refresh needs no second
        /// round of well-known probing; or <see langword="null"/> before the first acquisition.
        /// </summary>
        internal OAuthDiscoveryResult? _lastDiscovery;

        /// <summary>
        /// The logger this instance records each acquisition, refresh, and failure to.
        /// </summary>
        internal readonly ILogger<OutboundOAuthClient> _logger;

        /// <summary>
        /// The factory the SDK identity assertion provider creates its own loggers from.
        /// </summary>
        internal readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// The operator's outbound OAuth settings, read live so a caller can change
        /// <see cref="OutboundOAuthOptions.ConsentPresenter"/> between requests.
        /// </summary>
        internal readonly OutboundOAuthOptions _options;

        /// <summary>
        /// The client identifier the pending interactive grant was started for, or <see langword="null"/> when
        /// none is pending.
        /// </summary>
        internal string? _pendingClientId;

        /// <summary>
        /// The device authorization response of the pending interactive grant, or <see langword="null"/> when
        /// no device code grant is pending.
        /// </summary>
        internal DeviceAuthorizationResponse? _pendingDevice;

        /// <summary>
        /// The discovery result the pending interactive grant was started against, or <see langword="null"/>
        /// when none is pending.
        /// </summary>
        internal OAuthDiscoveryResult? _pendingDiscovery;

        /// <summary>
        /// Serializes access to the pending interactive grant fields, so a second start is refused rather than
        /// silently replacing a grant a human is already completing.
        /// </summary>
        internal readonly SemaphoreSlim _pendingGrant = new(1, 1);

        /// <summary>
        /// The live loopback listener of the pending authorization code grant, or <see langword="null"/> when
        /// no authorization code grant is pending.
        /// </summary>
        internal LoopbackAuthorizationCallback? _pendingLoopback;

        /// <summary>
        /// The redirect URI the pending authorization code grant was started with, echoed back on the code
        /// exchange; or <see langword="null"/> when no authorization code grant is pending.
        /// </summary>
        internal Uri? _pendingRedirectUri;

        /// <summary>
        /// The <c>state</c> the pending authorization code grant's response is matched against, or
        /// <see langword="null"/> when no authorization code grant is pending.
        /// </summary>
        internal string? _pendingState;

        /// <summary>
        /// The RFC 7636 code verifier of the pending authorization code grant, or <see langword="null"/> when no
        /// authorization code grant is pending.
        /// </summary>
        /// <remarks>
        /// A secret: per <c>AUTH-12</c> it never reaches a log, an elicitation, or
        /// <see cref="OutboundConsentRequest"/>.
        /// </remarks>
        internal string? _pendingVerifier;

        /// <summary>
        /// Whether this client has already proved the credential store can round-trip a value.
        /// </summary>
        internal bool _persistenceVerified;

        /// <summary>
        /// Coalesces concurrent refreshes, so several callers inside the same skew window share one token POST.
        /// </summary>
        internal readonly SemaphoreSlim _refreshGate = new(1, 1);

        /// <summary>
        /// The RFC 6749 section 6 refresh token grant.
        /// </summary>
        internal readonly RefreshTokenGrant _refreshTokenGrant;

        /// <summary>
        /// The client identifier dynamic client registration produced, or that was restored from the cache; or
        /// <see langword="null"/> when neither has happened.
        /// </summary>
        internal string? _registeredClientId;

        /// <summary>
        /// The client secret dynamic client registration produced, or that was restored from the cache; or
        /// <see langword="null"/> for a public client.
        /// </summary>
        internal string? _registeredClientSecret;

        /// <summary>
        /// The step 8 scope resolver.
        /// </summary>
        internal readonly ScopeResolver _scopeResolver;

        /// <summary>
        /// The absolute OData service root every token this client holds authorizes.
        /// </summary>
        internal readonly Uri _serviceRoot;

        /// <summary>
        /// The credential store this client's single token slot lives in.
        /// </summary>
        internal readonly SdkAuth.ITokenCache _tokenCache;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundOAuthClient"/> class.
        /// </summary>
        /// <param name="serviceRoot">The absolute OData service root every token this client holds authorizes.</param>
        /// <param name="httpClientFactory">The factory the composed grants create the <c>"OAuth"</c> named client from.</param>
        /// <param name="options">The operator's outbound OAuth settings.</param>
        /// <param name="tokenCache">The credential store this client's single token slot lives in.</param>
        /// <param name="discovery">The discovery algorithm that turns a challenge into an authorization server.</param>
        /// <param name="loggerFactory">The factory every composed collaborator's logger is created from.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceRoot"/>, <paramref name="httpClientFactory"/>,
        /// <paramref name="options"/>, <paramref name="tokenCache"/>, <paramref name="discovery"/>, or
        /// <paramref name="loggerFactory"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceRoot"/> is not an absolute URI.</exception>
        /// <remarks>
        /// The token endpoint client, every grant, the dynamic client registrar, and the scope resolver are
        /// composed here rather than injected: they are implementation detail of this state machine and
        /// registering half a dozen more services would let a caller wire an inconsistent set.
        /// </remarks>
        public OutboundOAuthClient(
            Uri serviceRoot,
            IHttpClientFactory httpClientFactory,
            OutboundOAuthOptions options,
            SdkAuth.ITokenCache tokenCache,
            OAuthDiscovery discovery,
            ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(tokenCache);
            ArgumentNullException.ThrowIfNull(discovery);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            if (!serviceRoot.IsAbsoluteUri)
            {
                throw new ArgumentException($"The OData service root must be absolute: {serviceRoot}", nameof(serviceRoot));
            }

            var tokenEndpointClient = new TokenEndpointClient(httpClientFactory, loggerFactory.CreateLogger<TokenEndpointClient>());

            _authorizationCodeGrant = new AuthorizationCodePkceGrant(tokenEndpointClient, loggerFactory.CreateLogger<AuthorizationCodePkceGrant>());
            _clientCredentialsGrant = new ClientCredentialsGrant(tokenEndpointClient, loggerFactory.CreateLogger<ClientCredentialsGrant>());
            _deviceCodeGrant = new DeviceCodeGrant(httpClientFactory, tokenEndpointClient, loggerFactory.CreateLogger<DeviceCodeGrant>());
            _discovery = discovery;
            _dynamicClientRegistrar = new DynamicClientRegistrar(httpClientFactory, loggerFactory.CreateLogger<DynamicClientRegistrar>());
            _httpClientFactory = httpClientFactory;
            _logger = loggerFactory.CreateLogger<OutboundOAuthClient>();
            _loggerFactory = loggerFactory;
            _options = options;
            _refreshTokenGrant = new RefreshTokenGrant(tokenEndpointClient, loggerFactory.CreateLogger<RefreshTokenGrant>());
            _scopeResolver = new ScopeResolver(loggerFactory.CreateLogger<ScopeResolver>());
            _serviceRoot = serviceRoot;
            _tokenCache = tokenCache;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Runs discovery and the advertised grant to obtain a brand new token, and persists it.
        /// </summary>
        /// <param name="wwwAuthenticate">The raw <c>WWW-Authenticate</c> header values the challenge arrived on.</param>
        /// <param name="statusCode">The HTTP status code the challenge arrived with.</param>
        /// <param name="interactiveAllowed">Whether this process may run an interactive grant such as device code.</param>
        /// <param name="cancellationToken">The token that cancels discovery and the grant.</param>
        /// <returns>
        /// The acquired token, already written to the cache.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="wwwAuthenticate"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when this host's credential store cannot round-trip a value.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when no authorization server can be discovered, when no client identifier is available, or
        /// when the authorization server advertises no grant this build can run.
        /// </exception>
        /// <exception cref="OAuthConsentRequiredException">
        /// Thrown when the selected grant needs a human and <see cref="OutboundOAuthOptions.ConsentPresenter"/>
        /// is <see langword="null"/>. The grant stays pending so a caller that elicits the URL can finish it
        /// with <see cref="CompleteInteractiveGrantAsync(CancellationToken)"/>.
        /// </exception>
        /// <exception cref="OAuthTokenException">Thrown when the grant fails, including when it outlives <see cref="OutboundOAuthOptions.AuthTimeout"/>.</exception>
        /// <exception cref="NotSupportedException">Thrown when the selected grant is not implemented in this build.</exception>
        /// <example>
        /// <code>
        /// var challenges = response.Headers.WwwAuthenticate.Select(x =&gt; x.ToString()).ToList();
        ///
        /// var token = await client.AcquireAsync(challenges, (int)response.StatusCode, interactiveAllowed: true, cancellationToken);
        /// </code>
        /// </example>
        /// <remarks>
        /// The whole interactive wait runs under a linked cancellation source that fires after
        /// <see cref="OutboundOAuthOptions.AuthTimeout"/>, so a human who walks away cannot pin the send path
        /// open forever; the caller's own cancellation still surfaces as
        /// <see cref="OperationCanceledException"/> rather than a timeout. The client credentials grant takes
        /// none of that path: it needs no human, so it neither starts a timeout nor consults a presenter.
        /// <para>
        /// When a presenter is configured and the wait fails for any reason — the presenter threw, the timeout
        /// fired, the end user refused — the pending grant is cleared, so the next call starts a fresh one
        /// instead of failing forever with "an interactive sign-in is already pending". The deliberate
        /// <see cref="OAuthConsentRequiredException"/> thrown when no presenter is configured is the one path
        /// that keeps the pending state, because a caller that elicits the URL finishes that same grant later.
        /// </para>
        /// </remarks>
        public async Task<SdkAuth.TokenContainer> AcquireAsync(IReadOnlyList<string> wwwAuthenticate, int statusCode, bool interactiveAllowed, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(wwwAuthenticate);

            await EnsureCredentialStoreAsync(cancellationToken).ConfigureAwait(false);

            var discovery = await DiscoverAsync(wwwAuthenticate, statusCode, cancellationToken).ConfigureAwait(false);
            var scope = _scopeResolver.Resolve(discovery, _options);
            var grant = GrantSelector.Select(discovery, _options, interactive: interactiveAllowed);

            _logger.LogInformation("Acquiring a token from {Issuer} with the {Grant} grant", discovery.AuthorizationServer.Issuer, OutboundGrantKindParser.ToWireName(grant));

            if (grant is OutboundGrantKind.ClientCredentials)
            {
                var daemonClientId = await ResolveClientIdAsync(discovery, grant, redirectUri: null, cancellationToken).ConfigureAwait(false);

                return await AcquireClientCredentialsAsync(discovery, daemonClientId, scope, cancellationToken).ConfigureAwait(false);
            }

            if (grant is OutboundGrantKind.IdentityAssertion)
            {
                return await AcquireIdentityAssertionAsync(discovery, scope, cancellationToken).ConfigureAwait(false);
            }

            if (grant is not OutboundGrantKind.AuthorizationCode and not OutboundGrantKind.DeviceCode)
            {
                throw new NotSupportedException($"Grant '{OutboundGrantKindParser.ToWireName(grant)}' is not implemented in this build.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.AuthTimeout);

            OutboundConsentRequest request;
            try
            {
                request = await StartInteractiveGrantAsync(discovery, scope, grant, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new OAuthTokenException(ODataMcpAuthConstants.ErrorExpiredToken, "Sign-in timed out.", 0);
            }

            var presenter = _options.ConsentPresenter;
            if (presenter is null)
            {
                throw new OAuthConsentRequiredException(request);
            }

            try
            {
                await presenter(request, timeout.Token).ConfigureAwait(false);

                return await CompleteInteractiveGrantAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await ClearPendingGrantAsync().ConfigureAwait(false);

                throw new OAuthTokenException(ODataMcpAuthConstants.ErrorExpiredToken, "Sign-in timed out.", 0);
            }
            catch
            {
                await ClearPendingGrantAsync().ConfigureAwait(false);

                throw;
            }
        }

        /// <summary>
        /// Finishes the interactive grant a previous
        /// <see cref="StartInteractiveGrantAsync(IReadOnlyList{string}, int, CancellationToken)"/> left pending:
        /// polls the token endpoint or waits on the loopback redirect, persists the result, and clears the
        /// pending state.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels the poll or the loopback wait and, on cancellation, abandons the pending grant.</param>
        /// <returns>
        /// The acquired token, already written to the cache.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when no interactive grant is pending.</exception>
        /// <exception cref="OutboundDiscoveryException">Thrown when the authorization response fails its <c>state</c> or RFC 9207 <c>iss</c> check.</exception>
        /// <exception cref="OAuthTokenException">Thrown when the end user refuses, the device code expires, or the token endpoint fails.</exception>
        /// <remarks>
        /// The pending state is cleared — and the loopback listener disposed — in a <c>finally</c>, so a refusal
        /// or a timeout leaves this client ready to start a fresh grant rather than wedged on a device code the
        /// authorization server has forgotten or a socket nothing is reading.
        /// </remarks>
        public async Task<SdkAuth.TokenContainer> CompleteInteractiveGrantAsync(CancellationToken cancellationToken)
        {
            await _pendingGrant.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var clientId = _pendingClientId;
                var discovery = _pendingDiscovery;

                if (discovery is null || string.IsNullOrWhiteSpace(clientId))
                {
                    throw new InvalidOperationException("No interactive sign-in is pending.");
                }

                TokenEndpointResponse response;

                if (_pendingLoopback is { } loopback)
                {
                    var result = await loopback.WaitAsync(_pendingState!, cancellationToken).ConfigureAwait(false);

                    response = await _authorizationCodeGrant
                        .ExchangeAsync(discovery.AuthorizationServer, result, _pendingVerifier!, clientId, _pendingRedirectUri!, discovery.Resource, cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (_pendingDevice is { } device)
                {
                    response = await _deviceCodeGrant
                        .PollAsync(discovery.AuthorizationServer, device, clientId, discovery.Resource, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException("No interactive sign-in is pending.");
                }

                var token = response.ToTokenContainer(
                    previousRefreshToken: null,
                    clientId: clientId,
                    clientSecret: _registeredClientSecret,
                    authorizationServer: ResolveIssuer(discovery),
                    tokenEndpointAuthMethod: string.IsNullOrWhiteSpace(_registeredClientSecret)
                        ? TokenEndpointClient.AuthMethodNone
                        : TokenEndpointClient.AuthMethodClientSecretPost);

                await _tokenCache.StoreTokensAsync(token, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Interactive sign-in completed: {Token}", OutboundAuthLogRedactor.DescribeToken(token));

                return token;
            }
            finally
            {
                _pendingLoopback?.Dispose();
                _pendingClientId = null;
                _pendingDevice = null;
                _pendingDiscovery = null;
                _pendingLoopback = null;
                _pendingRedirectUri = null;
                _pendingState = null;
                _pendingVerifier = null;
                _pendingGrant.Release();
            }
        }

        /// <summary>
        /// Refreshes the cached token even when its remaining lifetime says it is still good, for the
        /// <c>401 invalid_token</c> path where the resource server disagrees with the token's own
        /// <c>expires_in</c>.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels the refresh.</param>
        /// <returns>
        /// The refreshed token, or <see langword="null"/> when there is nothing cached to refresh or the
        /// authorization server rejected the refresh token.
        /// </returns>
        /// <example>
        /// <code>
        /// if (challenge?.Error == ODataMcpAuthConstants.ErrorInvalidToken)
        /// {
        ///     var refreshed = await client.ForceRefreshAsync(cancellationToken);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Coalesced through the same gate as the lifetime-driven refresh, so a burst of simultaneous
        /// <c>invalid_token</c> answers costs one token POST rather than one per request. A client credentials
        /// token has no refresh token to spend, so this returns <see langword="null"/> and the caller falls
        /// through to a fresh grant.
        /// </remarks>
        public async Task<SdkAuth.TokenContainer?> ForceRefreshAsync(CancellationToken cancellationToken)
        {
            var current = await _tokenCache.GetTokensAsync(cancellationToken).ConfigureAwait(false);
            if (current is null || string.IsNullOrWhiteSpace(current.RefreshToken))
            {
                return null;
            }

            return await RefreshAsync(current, force: true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a token that can be attached to the next <c>"OData"</c> request, refreshing it first when its
        /// remaining lifetime has fallen inside <see cref="ODataMcpAuthConstants.TokenRefreshSkew"/>.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels the cache read and any refresh it triggers.</param>
        /// <returns>
        /// The token to attach, or <see langword="null"/> when nothing is cached, nothing can be refreshed, or
        /// the refresh failed — in which case the caller runs
        /// <see cref="AcquireAsync(IReadOnlyList{string}, int, bool, CancellationToken)"/> off the resulting
        /// challenge.
        /// </returns>
        /// <example>
        /// <code>
        /// var token = await client.GetValidTokenAsync(cancellationToken);
        /// if (token is not null)
        /// {
        ///     request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// A token whose <c>expires_in</c> the authorization server omitted is attached as-is: the client cannot
        /// prove it has expired, and a <c>401 invalid_token</c> is a cheaper way to find out than throwing away
        /// a token that may still have hours left.
        /// </remarks>
        public async Task<SdkAuth.TokenContainer?> GetValidTokenAsync(CancellationToken cancellationToken)
        {
            return (await GetTokenForSendAsync(cancellationToken).ConfigureAwait(false)).Token;
        }

        /// <summary>
        /// Starts an interactive grant and returns the consent request a human — or an MCP elicitation — has to
        /// act on, without waiting for them to act.
        /// </summary>
        /// <param name="wwwAuthenticate">The raw <c>WWW-Authenticate</c> header values the challenge arrived on.</param>
        /// <param name="statusCode">The HTTP status code the challenge arrived with.</param>
        /// <param name="cancellationToken">The token that cancels discovery and the device authorization request.</param>
        /// <returns>
        /// The consent request, carrying the URL to open, the user code to type when the grant uses one, and a
        /// fresh <c>D</c>-format GUID <see cref="OutboundConsentRequest.ElicitationId"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="wwwAuthenticate"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an interactive sign-in is already pending, or when this host's credential store cannot round-trip a value.</exception>
        /// <exception cref="OutboundDiscoveryException">Thrown when discovery fails, no client identifier is available, or the loopback redirect cannot be bound.</exception>
        /// <exception cref="NotSupportedException">Thrown when the selected grant is not an interactive one.</exception>
        /// <exception cref="OAuthTokenException">Thrown when the device authorization request fails.</exception>
        /// <remarks>
        /// Split from <see cref="CompleteInteractiveGrantAsync(CancellationToken)"/> so an MCP host never polls
        /// a token endpoint — or blocks on a loopback socket — on the thread that is servicing an OData request.
        /// </remarks>
        public async Task<OutboundConsentRequest> StartInteractiveGrantAsync(IReadOnlyList<string> wwwAuthenticate, int statusCode, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(wwwAuthenticate);

            await EnsureCredentialStoreAsync(cancellationToken).ConfigureAwait(false);

            var discovery = await DiscoverAsync(wwwAuthenticate, statusCode, cancellationToken).ConfigureAwait(false);
            var scope = _scopeResolver.Resolve(discovery, _options);
            var grant = GrantSelector.Select(discovery, _options, interactive: true);

            return await StartInteractiveGrantAsync(discovery, scope, grant, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Runs the client credentials grant against an already-discovered authorization server and persists the
        /// result together with everything a silent re-acquisition needs.
        /// </summary>
        /// <param name="discovery">The discovery result the grant runs against.</param>
        /// <param name="clientId">The confidential client identifier.</param>
        /// <param name="scope">The resolved space-delimited scope, or <see langword="null"/> to request none.</param>
        /// <param name="cancellationToken">The token that cancels the token request.</param>
        /// <returns>
        /// The acquired token, already written to the cache.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="OutboundDiscoveryException">Thrown when no client secret is configured.</exception>
        /// <exception cref="OAuthTokenException">Thrown when the authorization server rejects the credentials.</exception>
        /// <remarks>
        /// The stored container keeps the client secret and the token endpoint authentication method, which is
        /// what lets <see cref="ReacquireClientCredentialsAsync(SdkAuth.TokenContainer, CancellationToken)"/>
        /// renew an expired token without a human — a client credentials response carries no refresh token to
        /// spend.
        /// </remarks>
        internal async Task<SdkAuth.TokenContainer> AcquireClientCredentialsAsync(OAuthDiscoveryResult discovery, string clientId, string? scope, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(discovery);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

            var clientSecret = string.IsNullOrWhiteSpace(_options.ClientSecret) ? _registeredClientSecret : _options.ClientSecret;
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new OutboundDiscoveryException($"The client credentials grant needs a client secret; set {ODataMcpAuthConstants.ClientSecretEnvironmentVariable}.");
            }

            var authMethod = ClientCredentialsGrant.ResolveAuthMethod(discovery.AuthorizationServer);
            var response = await _clientCredentialsGrant
                .AcquireAsync(discovery.AuthorizationServer, clientId, clientSecret, scope, discovery.Resource, cancellationToken)
                .ConfigureAwait(false);
            var token = response.ToTokenContainer(
                previousRefreshToken: null,
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationServer: ResolveIssuer(discovery),
                tokenEndpointAuthMethod: authMethod);

            await _tokenCache.StoreTokensAsync(token, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Client credentials sign-in completed: {Token}", OutboundAuthLogRedactor.DescribeToken(token));

            return token;
        }

        /// <summary>
        /// Runs the SDK identity assertion grant against an already-discovered authorization server and
        /// persists the result.
        /// </summary>
        /// <param name="discovery">The discovery result the grant runs against.</param>
        /// <param name="scope">The resolved space-delimited scope, or <see langword="null"/> to request none.</param>
        /// <param name="cancellationToken">The token that cancels the exchange and the token request.</param>
        /// <returns>
        /// The acquired token, already written to the cache.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> is <see langword="null"/>.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when no client identifier is configured, when no IdP client identifier is configured, or when
        /// the discovered authorization server publishes no issuer to redeem the assertion at.
        /// </exception>
        /// <exception cref="OAuthTokenException">Thrown when the RFC 8693 exchange or the RFC 7523 grant fails.</exception>
        /// <remarks>
        /// This is the one grant that needs no human and no keyring dialog: the enterprise identity provider
        /// has already authenticated the end user, and
        /// <see cref="FileIdTokenCallback"/> only reads the resulting id token off disk or out of the
        /// environment. The whole flow is delegated to
        /// <see cref="SdkAuth.IdentityAssertionGrantProvider"/>, which performs the RFC 8693 token exchange at
        /// the identity provider and the RFC 7523 JWT bearer grant at the authorization server; per
        /// <c>AUTH-12</c> neither the id token nor the assertion it produces ever reaches a log.
        /// <para>
        /// The provider's own failure type is translated to <see cref="OAuthTokenException"/> so every grant in
        /// this package fails the same way. The stored container deliberately carries no client secret and
        /// records <see cref="TokenEndpointClient.AuthMethodNone"/>, so
        /// <see cref="IsClientCredentialsContainer(SdkAuth.TokenContainer)"/> never mistakes an expired
        /// identity assertion token for one the client credentials grant could silently renew: the correct
        /// renewal is a fresh assertion built from a freshly read id token.
        /// </para>
        /// <para>
        /// This is the only caller that substitutes for an absent <see cref="OAuthDiscoveryResult.Resource"/>:
        /// the SDK provider requires a resource URL, so the service root's origin stands in when neither the
        /// protected resource metadata nor <c>--resource</c> named one. Every other grant simply omits the RFC
        /// 8707 parameter.
        /// </para>
        /// </remarks>
        internal async Task<SdkAuth.TokenContainer> AcquireIdentityAssertionAsync(OAuthDiscoveryResult discovery, string? scope, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(discovery);

            if (string.IsNullOrWhiteSpace(_options.ClientId))
            {
                throw new OutboundDiscoveryException("The identity assertion grant needs a pre-registered client; pass --client-id.");
            }

            if (string.IsNullOrWhiteSpace(_options.IdpClientId))
            {
                throw new OutboundDiscoveryException("The identity assertion grant needs an identity provider client; pass --idp-client-id.");
            }

            if (discovery.AuthorizationServer.IssuerUri is not { } issuer)
            {
                throw new OutboundDiscoveryException("The discovered authorization server publishes no issuer to redeem an identity assertion at.");
            }

            var idTokenCallback = new FileIdTokenCallback(_options.IdpIdTokenFile);
            var providerOptions = new SdkAuth.IdentityAssertionGrantProviderOptions
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret,
                IdpClientId = _options.IdpClientId,
                IdpClientSecret = _options.IdpClientSecret,
                IdpScope = _options.IdpScope,
                IdpTokenEndpoint = _options.IdpTokenEndpoint?.AbsoluteUri,
                IdpUrl = _options.IdpUrl?.AbsoluteUri,
                IdTokenCallback = idTokenCallback.ReadAsync,
                Scope = scope
            };
            var provider = new SdkAuth.IdentityAssertionGrantProvider(
                providerOptions,
                _httpClientFactory.CreateClient(ODataMcpAuthConstants.OAuthHttpClientName),
                _loggerFactory);

            SdkAuth.TokenContainer issued;
            try
            {
                issued = await provider
                    .GetAccessTokenAsync(discovery.Resource ?? new Uri(_serviceRoot.GetLeftPart(UriPartial.Authority)), issuer, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (SdkAuth.IdentityAssertionGrantException exception)
            {
                throw new OAuthTokenException(
                    string.IsNullOrWhiteSpace(exception.ErrorCode) ? "invalid_grant" : exception.ErrorCode,
                    OutboundAuthLogRedactor.Redact(exception.Message),
                    0,
                    exception);
            }

            var token = new SdkAuth.TokenContainer
            {
                AccessToken = issued.AccessToken,
                AuthorizationServer = ResolveIssuer(discovery),
                ClientId = _options.ClientId,
                ExpiresIn = issued.ExpiresIn,
                ObtainedAt = issued.ObtainedAt,
                RefreshToken = issued.RefreshToken,
                Scope = issued.Scope ?? scope,
                TokenEndpointAuthMethod = TokenEndpointClient.AuthMethodNone,
                TokenType = issued.TokenType
            };

            await _tokenCache.StoreTokensAsync(token, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Identity assertion sign-in completed: {Token}", OutboundAuthLogRedactor.DescribeToken(token));

            return token;
        }

        /// <summary>
        /// Abandons any pending interactive grant, disposing its loopback listener, so the next start is not
        /// refused.
        /// </summary>
        /// <returns>
        /// A task that completes once the pending state is gone.
        /// </returns>
        /// <remarks>
        /// Idempotent and uncancellable on purpose: it runs on the failure path, where the token that caused
        /// the failure is often the one that has already been cancelled, and leaving a dead device code or a
        /// live loopback socket behind would wedge every later sign-in for the life of the process.
        /// </remarks>
        internal async Task ClearPendingGrantAsync()
        {
            await _pendingGrant.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                _pendingLoopback?.Dispose();
                _pendingClientId = null;
                _pendingDevice = null;
                _pendingDiscovery = null;
                _pendingLoopback = null;
                _pendingRedirectUri = null;
                _pendingState = null;
                _pendingVerifier = null;
            }
            finally
            {
                _pendingGrant.Release();
            }
        }

        /// <summary>
        /// Runs the discovery algorithm for this client's service root and remembers the result.
        /// </summary>
        /// <param name="wwwAuthenticate">The raw <c>WWW-Authenticate</c> header values the challenge arrived on.</param>
        /// <param name="statusCode">The HTTP status code the challenge arrived with.</param>
        /// <param name="cancellationToken">The token that cancels the well-known probes.</param>
        /// <returns>
        /// The discovery result.
        /// </returns>
        internal async Task<OAuthDiscoveryResult> DiscoverAsync(IReadOnlyList<string> wwwAuthenticate, int statusCode, CancellationToken cancellationToken)
        {
            var discovery = await _discovery
                .DiscoverAsync(_serviceRoot, wwwAuthenticate, statusCode, _options.AuthServer, _options.Resource, cancellationToken)
                .ConfigureAwait(false);

            _lastDiscovery = discovery;

            return discovery;
        }

        /// <summary>
        /// Proves, once per client, that the credential store this process would persist tokens into can
        /// round-trip a value.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels waiting for the pending-grant gate.</param>
        /// <returns>
        /// A task that completes once the store has been verified, or immediately when verification does not
        /// apply.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the credential store cannot round-trip a value.</exception>
        /// <remarks>
        /// Skipped entirely when <see cref="OutboundOAuthOptions.HasExplicitCredentials"/> is set, because an
        /// operator who pasted a bearer token, an API key, or Basic credentials is never going to write a token
        /// to the keyring — and an unauthenticated service never reaches this method at all, so it never
        /// creates a token cache directory it would not use.
        /// <para>
        /// A round-trip check that <em>throws</em> — a DPAPI write into a directory that cannot be created, a
        /// keyring the platform refuses to open — means the same thing as one that answers <see langword="false"/>:
        /// the store is unusable. Both become the one message that names what an operator can do about it,
        /// rather than a backend stack trace surfacing out of an OData request.
        /// </para>
        /// </remarks>
        internal async Task EnsureCredentialStoreAsync(CancellationToken cancellationToken)
        {
            if (_persistenceVerified || _options.HasExplicitCredentials)
            {
                return;
            }

            await _pendingGrant.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_persistenceVerified)
                {
                    return;
                }

                bool usable;
                try
                {
                    usable = LatchkeyTokenCache.VerifyPersistence(_options.TokenCachePath);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogDebug(exception, "The credential store round-trip check failed");

                    usable = false;
                }

                if (!usable)
                {
                    throw new InvalidOperationException("Credential store is not usable on this host; unlock the OS keyring or pass --token-cache.");
                }

                _persistenceVerified = true;
            }
            finally
            {
                _pendingGrant.Release();
            }
        }

        /// <summary>
        /// Returns the token to attach to the next <c>"OData"</c> request together with whether obtaining it
        /// cost a refresh, so the send path can tell a token it just refreshed from one it read straight out of
        /// the cache.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels the cache read and any refresh it triggers.</param>
        /// <returns>
        /// The token — or <see langword="null"/> when there is nothing attachable — and the refresh flag.
        /// </returns>
        /// <remarks>
        /// <see cref="OutboundTokenResult.Refreshed"/> is <see langword="true"/> whenever the refresh branch was
        /// taken, including when the refresh was coalesced onto another caller's in-flight POST: in both cases
        /// the token in hand is as new as the authorization server can make it, so answering a
        /// <c>401 invalid_token</c> with a second refresh would only burn another round trip.
        /// <para>
        /// A stale container that carries a client secret and a real token endpoint authentication method is a
        /// client credentials token: it has no refresh token by design, so it is re-acquired silently rather
        /// than reported as "nothing to attach", which would send a daemon down the interactive path.
        /// </para>
        /// <para>
        /// An unusable credential store — a locked Secret Service keyring, a cache directory that lost its
        /// permissions — is reported as "nothing to attach" rather than propagated, and warned about exactly
        /// once. Every OData request reads this cache, including requests to services that never challenge at
        /// all, so letting the read throw would break an unauthenticated service on a host whose keyring
        /// happens to be locked. Nothing is lost by continuing: when a grant really is needed,
        /// <see cref="EnsureCredentialStoreAsync(CancellationToken)"/> fails first with an actionable message.
        /// </para>
        /// </remarks>
        internal async Task<OutboundTokenResult> GetTokenForSendAsync(CancellationToken cancellationToken)
        {
            SdkAuth.TokenContainer? cached;
            try
            {
                cached = await _tokenCache.GetTokensAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (LatchkeyBackendUnavailableException)
            {
                if (Interlocked.Exchange(ref _credentialStoreUnavailableLogged, 1) == 0)
                {
                    _logger.LogWarning("Credential store unavailable; continuing without a cached token");
                }

                return new OutboundTokenResult(null, refreshed: false);
            }

            if (cached is null || string.IsNullOrWhiteSpace(cached.AccessToken))
            {
                return new OutboundTokenResult(null, refreshed: false);
            }

            if (IsAttachable(cached))
            {
                return new OutboundTokenResult(cached, refreshed: false);
            }

            if (string.IsNullOrWhiteSpace(cached.RefreshToken))
            {
                if (IsClientCredentialsContainer(cached))
                {
                    _logger.LogInformation("Cached client credentials token is inside the refresh skew; re-running the grant.");

                    return new OutboundTokenResult(await ReacquireClientCredentialsAsync(cached, cancellationToken).ConfigureAwait(false), refreshed: true);
                }

                _logger.LogInformation("Cached token is inside the refresh skew and carries no refresh token; a fresh grant is required.");

                return new OutboundTokenResult(null, refreshed: false);
            }

            return new OutboundTokenResult(await RefreshAsync(cached, force: false, cancellationToken).ConfigureAwait(false), refreshed: true);
        }

        /// <summary>
        /// Determines whether a cached token can be attached to the next request without refreshing it first.
        /// </summary>
        /// <param name="token">The cached token.</param>
        /// <returns>
        /// <see langword="true"/> when the token carries no <c>expires_in</c> at all, or when its remaining
        /// lifetime is greater than <see cref="ODataMcpAuthConstants.TokenRefreshSkew"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is <see langword="null"/>.</exception>
        internal static bool IsAttachable(SdkAuth.TokenContainer token)
        {
            ArgumentNullException.ThrowIfNull(token);

            return RemainingLifetime(token) is not { } remaining || remaining > ODataMcpAuthConstants.TokenRefreshSkew;
        }

        /// <summary>
        /// Determines whether a cached container was issued by the client credentials grant, and can therefore
        /// be renewed without a human.
        /// </summary>
        /// <param name="token">The cached token.</param>
        /// <returns>
        /// <see langword="true"/> when the container carries a client identifier, a client secret, and a token
        /// endpoint authentication method other than <see cref="TokenEndpointClient.AuthMethodNone"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is <see langword="null"/>.</exception>
        internal static bool IsClientCredentialsContainer(SdkAuth.TokenContainer token)
        {
            ArgumentNullException.ThrowIfNull(token);

            return !string.IsNullOrWhiteSpace(token.ClientId)
                && !string.IsNullOrWhiteSpace(token.ClientSecret)
                && !string.IsNullOrWhiteSpace(token.TokenEndpointAuthMethod)
                && !string.Equals(token.TokenEndpointAuthMethod, TokenEndpointClient.AuthMethodNone, StringComparison.Ordinal);
        }

        /// <summary>
        /// Runs the client credentials grant again for a cached container whose access token has gone stale.
        /// </summary>
        /// <param name="stale">The container the caller read before it decided a re-acquisition was needed.</param>
        /// <param name="cancellationToken">The token that cancels the token request.</param>
        /// <returns>
        /// The freshly issued token; the token another caller already re-acquired; or <see langword="null"/>
        /// when the authorization server rejected the credentials and the cache entry was dropped.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stale"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Coalesced through the same gate as a refresh, and re-reads the cache after taking it, so a burst of
        /// concurrent sends costs one token POST. A rejection clears the slot rather than looping, which turns
        /// the next send into a full acquisition that reports the real error to the operator.
        /// </remarks>
        internal async Task<SdkAuth.TokenContainer?> ReacquireClientCredentialsAsync(SdkAuth.TokenContainer stale, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stale);

            await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await _tokenCache.GetTokensAsync(cancellationToken).ConfigureAwait(false);
                if (current is null || !IsClientCredentialsContainer(current))
                {
                    return null;
                }

                if (!string.Equals(current.AccessToken, stale.AccessToken, StringComparison.Ordinal))
                {
                    _logger.LogDebug("Another caller already re-acquired this token; reusing it.");

                    return current;
                }

                if (IsAttachable(current))
                {
                    return current;
                }

                var discovery = await ResolveDiscoveryAsync(current, cancellationToken).ConfigureAwait(false);

                SdkAuth.TokenContainer reacquired;
                try
                {
                    var response = await _clientCredentialsGrant
                        .AcquireAsync(discovery.AuthorizationServer, current.ClientId!, current.ClientSecret!, current.Scope, discovery.Resource, cancellationToken)
                        .ConfigureAwait(false);

                    reacquired = response.ToTokenContainer(
                        previousRefreshToken: null,
                        clientId: current.ClientId,
                        clientSecret: current.ClientSecret,
                        authorizationServer: ResolveIssuer(discovery),
                        tokenEndpointAuthMethod: current.TokenEndpointAuthMethod);
                }
                catch (OAuthTokenException exception)
                {
                    _logger.LogWarning("Client credentials re-acquisition failed ({Error}); clearing the cached token. {Detail}", exception.Error, OutboundAuthLogRedactor.Redact(exception.Message));

                    if (_tokenCache is LatchkeyTokenCache latchkey)
                    {
                        await latchkey.ClearAsync(cancellationToken).ConfigureAwait(false);
                    }

                    return null;
                }

                await _tokenCache.StoreTokensAsync(reacquired, cancellationToken).ConfigureAwait(false);

                return reacquired;
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        /// <summary>
        /// Performs one coalesced refresh of the cached token.
        /// </summary>
        /// <param name="stale">The container the caller read before it decided a refresh was needed.</param>
        /// <param name="force">Whether to refresh even when the cached token's remaining lifetime still looks healthy.</param>
        /// <param name="cancellationToken">The token that cancels the refresh.</param>
        /// <returns>
        /// The refreshed token; the token another caller already refreshed; or <see langword="null"/> when the
        /// authorization server rejected the refresh token and the cache entry was dropped.
        /// </returns>
        /// <remarks>
        /// The cache is re-read after the gate is taken, so the second of two racing callers observes the first
        /// caller's freshly stored token and returns it instead of spending a second token POST. A refresh that
        /// fails is reported at Warning and clears the slot, which turns the next
        /// <see cref="GetValidTokenAsync(CancellationToken)"/> into a <see langword="null"/> and sends the
        /// handler down its 401 acquisition path.
        /// </remarks>
        internal async Task<SdkAuth.TokenContainer?> RefreshAsync(SdkAuth.TokenContainer stale, bool force, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stale);

            await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await _tokenCache.GetTokensAsync(cancellationToken).ConfigureAwait(false);
                if (current is null || string.IsNullOrWhiteSpace(current.RefreshToken))
                {
                    return null;
                }

                if (!string.Equals(current.AccessToken, stale.AccessToken, StringComparison.Ordinal))
                {
                    _logger.LogDebug("Another caller already refreshed this token; reusing it.");

                    return current;
                }

                if (!force && IsAttachable(current))
                {
                    return current;
                }

                var discovery = await ResolveDiscoveryAsync(current, cancellationToken).ConfigureAwait(false);

                SdkAuth.TokenContainer refreshed;
                try
                {
                    refreshed = await _refreshTokenGrant
                        .RefreshAsync(discovery.AuthorizationServer, current, discovery.Resource, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OAuthTokenException exception)
                {
                    _logger.LogWarning("Refresh failed ({Error}); clearing the cached token so the next request re-runs the grant. {Detail}", exception.Error, OutboundAuthLogRedactor.Redact(exception.Message));

                    if (_tokenCache is LatchkeyTokenCache latchkey)
                    {
                        await latchkey.ClearAsync(cancellationToken).ConfigureAwait(false);
                    }

                    return null;
                }

                await _tokenCache.StoreTokensAsync(refreshed, cancellationToken).ConfigureAwait(false);

                return refreshed;
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        /// <summary>
        /// Computes how much of a cached token's advertised lifetime is left.
        /// </summary>
        /// <param name="token">The cached token.</param>
        /// <returns>
        /// <c>ObtainedAt + ExpiresIn − now</c>, or <see langword="null"/> when the token carries no
        /// <c>expires_in</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is <see langword="null"/>.</exception>
        internal static TimeSpan? RemainingLifetime(SdkAuth.TokenContainer token)
        {
            ArgumentNullException.ThrowIfNull(token);

            return token.ExpiresIn is { } expiresIn
                ? token.ObtainedAt.AddSeconds(expiresIn) - DateTimeOffset.UtcNow
                : null;
        }

        /// <summary>
        /// Resolves the client identifier the grant runs as, per step 7 of the discovery algorithm.
        /// </summary>
        /// <param name="discovery">The discovery result whose authorization server would be registered with.</param>
        /// <param name="grant">The grant kind that was selected, which decides the registered grant types and whether a redirect URI is registered at all.</param>
        /// <param name="redirectUri">The loopback redirect URI to register, or <see langword="null"/> when the selected grant uses none.</param>
        /// <param name="cancellationToken">The token that cancels the cache read and the registration request.</param>
        /// <returns>
        /// <see cref="OutboundOAuthOptions.ClientId"/>; a client identifier this client already registered or
        /// restored from the cache; a freshly registered one; or the client id metadata document URI.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> is <see langword="null"/>.</exception>
        /// <exception cref="OutboundDiscoveryException">Thrown when no client identifier can be obtained at all.</exception>
        /// <exception cref="OAuthTokenException">Thrown when dynamic client registration fails.</exception>
        /// <remarks>
        /// The cached container is consulted before registering, so a restart reuses the identifier the first
        /// start registered rather than creating a second client on every launch — that is why step 9 persists
        /// <see cref="SdkAuth.TokenContainer.ClientId"/> alongside the tokens. Client id metadata documents come
        /// last because they need an operator-hosted document, which registration does not.
        /// <para>
        /// A <see cref="OutboundGrantKind.ClientCredentials"/> registration is confidential — it declares
        /// <c>client_secret_post</c> — so a server that answers without a <c>client_secret</c> is reported here
        /// rather than one round trip later as an opaque <c>invalid_client</c> from the token endpoint.
        /// </para>
        /// </remarks>
        internal async Task<string> ResolveClientIdAsync(OAuthDiscoveryResult discovery, OutboundGrantKind grant, Uri? redirectUri, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(discovery);

            if (!string.IsNullOrWhiteSpace(_options.ClientId))
            {
                return _options.ClientId;
            }

            if (!string.IsNullOrWhiteSpace(_registeredClientId))
            {
                return _registeredClientId;
            }

            var cached = await _tokenCache.GetTokensAsync(cancellationToken).ConfigureAwait(false);
            if (cached is not null && !string.IsNullOrWhiteSpace(cached.ClientId))
            {
                _registeredClientId = cached.ClientId;
                _registeredClientSecret = cached.ClientSecret;

                _logger.LogInformation("Reusing the persisted client id {ClientId} instead of registering again.", cached.ClientId);

                return cached.ClientId;
            }

            var metadata = discovery.AuthorizationServer;

            if (metadata.RegistrationEndpoint is not null)
            {
                var registration = await _dynamicClientRegistrar
                    .RegisterAsync(
                        metadata,
                        new SdkAuth.DynamicClientRegistrationOptions { ClientName = ODataMcpAuthConstants.DynamicClientName },
                        grant is OutboundGrantKind.AuthorizationCode ? redirectUri : null,
                        ResolveRegistrationGrantTypes(grant),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (grant is OutboundGrantKind.ClientCredentials && string.IsNullOrWhiteSpace(registration.ClientSecret))
                {
                    throw new OutboundDiscoveryException("Dynamic client registration did not return a client secret; pass --client-id and --client-secret.");
                }

                _registeredClientId = registration.ClientId;
                _registeredClientSecret = registration.ClientSecret;

                return registration.ClientId;
            }

            if (metadata.ClientIdMetadataDocumentSupported == true && _options.ClientMetadataDocumentUri is not null)
            {
                _logger.LogInformation("Using the client id metadata document {Document} as the client id.", _options.ClientMetadataDocumentUri);

                return _options.ClientMetadataDocumentUri.AbsoluteUri;
            }

            throw new OutboundDiscoveryException("Authorization server does not advertise dynamic client registration; pass --client-id.");
        }

        /// <summary>
        /// Returns the discovery result a refresh should post against, probing the well-known documents only
        /// when this client has not already discovered them.
        /// </summary>
        /// <param name="current">The cached container whose recorded authorization server seeds a cold-start probe.</param>
        /// <param name="cancellationToken">The token that cancels the probes.</param>
        /// <returns>
        /// The discovery result.
        /// </returns>
        /// <remarks>
        /// A restart that finds a usable cached token never reaches this method at all; one that finds a token
        /// inside the refresh skew reaches it exactly once, and the recorded
        /// <see cref="SdkAuth.TokenContainer.AuthorizationServer"/> stands in for the challenge that is not
        /// there to read.
        /// </remarks>
        internal async Task<OAuthDiscoveryResult> ResolveDiscoveryAsync(SdkAuth.TokenContainer? current, CancellationToken cancellationToken)
        {
            if (_lastDiscovery is { } cached)
            {
                return cached;
            }

            var authorizationServerOverride = _options.AuthServer;
            if (authorizationServerOverride is null
                && current is not null
                && Uri.TryCreate(current.AuthorizationServer, UriKind.Absolute, out var issuer))
            {
                authorizationServerOverride = issuer;
            }

            var discovery = await _discovery
                .DiscoverAsync(_serviceRoot, [], 0, authorizationServerOverride, _options.Resource, cancellationToken)
                .ConfigureAwait(false);

            _lastDiscovery = discovery;

            return discovery;
        }

        /// <summary>
        /// Returns the issuer string a token container records for a discovery result.
        /// </summary>
        /// <param name="discovery">The discovery result.</param>
        /// <returns>
        /// The authorization server's <c>issuer</c>, falling back to the base URI it was discovered from.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> is <see langword="null"/>.</exception>
        internal static string ResolveIssuer(OAuthDiscoveryResult discovery)
        {
            ArgumentNullException.ThrowIfNull(discovery);

            return string.IsNullOrWhiteSpace(discovery.AuthorizationServer.Issuer)
                ? discovery.AuthorizationServerBase.AbsoluteUri
                : discovery.AuthorizationServer.Issuer;
        }

        /// <summary>
        /// Returns the grant types a dynamic client registration declares for a selected grant kind.
        /// </summary>
        /// <param name="grant">The grant kind that was selected.</param>
        /// <returns>
        /// The selected grant's wire name, plus <c>refresh_token</c> for the two interactive grants that can
        /// earn one.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="grant"/> is not a grant this client registers for.</exception>
        internal static IReadOnlyList<string> ResolveRegistrationGrantTypes(OutboundGrantKind grant)
        {
            return grant switch
            {
                OutboundGrantKind.AuthorizationCode => [ODataMcpAuthConstants.GrantTypeAuthorizationCode, ODataMcpAuthConstants.GrantTypeRefreshToken],
                OutboundGrantKind.ClientCredentials => [ODataMcpAuthConstants.GrantTypeClientCredentials],
                OutboundGrantKind.DeviceCode => [ODataMcpAuthConstants.DeviceCodeGrantType, ODataMcpAuthConstants.GrantTypeRefreshToken],
                _ => throw new ArgumentOutOfRangeException(nameof(grant), grant, "Dynamic client registration is only run for the authorization code, client credentials, and device code grants.")
            };
        }

        /// <summary>
        /// Starts the RFC 6749 authorization code grant: binds the loopback listener, resolves the client
        /// identifier the registered redirect URI belongs to, and builds the authorization URL.
        /// </summary>
        /// <param name="discovery">The discovery result the grant runs against.</param>
        /// <param name="scope">The resolved space-delimited scope, or <see langword="null"/> to request none.</param>
        /// <param name="cancellationToken">The token that cancels a dynamic client registration request.</param>
        /// <returns>
        /// The consent request a human has to act on.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> is <see langword="null"/>.</exception>
        /// <exception cref="OutboundDiscoveryException">Thrown when the loopback redirect cannot be bound or no client identifier is available.</exception>
        /// <exception cref="NotSupportedException">Thrown when the authorization server does not accept the <c>S256</c> code challenge method.</exception>
        /// <remarks>
        /// The listener is bound <em>before</em> the client identifier is resolved on purpose: a dynamic client
        /// registration has to declare the redirect URI the authorization response will actually arrive on, and
        /// that URI is not known until the ephemeral port is. Any failure after the bind disposes the listener
        /// rather than leaking a socket for the life of the process.
        /// </remarks>
        internal async Task<OutboundConsentRequest> StartAuthorizationCodeGrantAsync(OAuthDiscoveryResult discovery, string? scope, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(discovery);

            var loopback = LoopbackAuthorizationCallback.Start(_options.RedirectUri);

            try
            {
                var clientId = await ResolveClientIdAsync(discovery, OutboundGrantKind.AuthorizationCode, loopback.RedirectUri, cancellationToken).ConfigureAwait(false);
                var begin = _authorizationCodeGrant.Begin(discovery.AuthorizationServer, clientId, scope, discovery.Resource, loopback.RedirectUri);
                var consent = new OutboundConsentRequest(
                    OutboundGrantKind.AuthorizationCode,
                    begin.AuthorizationUri,
                    $"To authorize access to {_serviceRoot}, open {begin.AuthorizationUri} and complete sign-in.",
                    userCode: null,
                    verificationUri: null);

                _pendingClientId = clientId;
                _pendingDiscovery = discovery;
                _pendingLoopback = loopback;
                _pendingRedirectUri = loopback.RedirectUri;
                _pendingState = begin.State;
                _pendingVerifier = begin.CodeVerifier;

                _logger.LogInformation("Authorization code sign-in started for {ServiceRoot}; awaiting the redirect on {RedirectUri}", _serviceRoot, loopback.RedirectUri);

                return consent;
            }
            catch
            {
                loopback.Dispose();

                throw;
            }
        }

        /// <summary>
        /// Starts the selected interactive grant against an already-discovered authorization server and records
        /// the pending state a later completion needs.
        /// </summary>
        /// <param name="discovery">The discovery result the grant runs against.</param>
        /// <param name="scope">The resolved space-delimited scope, or <see langword="null"/> to request none.</param>
        /// <param name="grant">The grant kind grant selection chose.</param>
        /// <param name="cancellationToken">The token that cancels the device authorization or registration request.</param>
        /// <returns>
        /// The consent request a human has to act on.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an interactive sign-in is already pending.</exception>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="grant"/> is not an interactive grant.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when the authorization server named a consent URL that is neither <c>https</c> nor <c>http</c>
        /// on a loopback host.
        /// </exception>
        /// <remarks>
        /// The consent request is built before any pending-grant field is assigned, so a server-controlled URL
        /// that fails <see cref="OutboundConsentRequest"/>'s scheme check leaves this client with no pending
        /// state at all: the next start is accepted rather than refused with "already pending".
        /// </remarks>
        internal async Task<OutboundConsentRequest> StartInteractiveGrantAsync(OAuthDiscoveryResult discovery, string? scope, OutboundGrantKind grant, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(discovery);

            await _pendingGrant.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_pendingDevice is not null || _pendingLoopback is not null)
                {
                    throw new InvalidOperationException("An interactive sign-in is already pending.");
                }

                if (grant is OutboundGrantKind.AuthorizationCode)
                {
                    return await StartAuthorizationCodeGrantAsync(discovery, scope, cancellationToken).ConfigureAwait(false);
                }

                if (grant is not OutboundGrantKind.DeviceCode)
                {
                    throw new NotSupportedException($"Grant '{OutboundGrantKindParser.ToWireName(grant)}' is not an interactive grant.");
                }

                var clientId = await ResolveClientIdAsync(discovery, grant, redirectUri: null, cancellationToken).ConfigureAwait(false);
                var device = await _deviceCodeGrant
                    .StartAsync(discovery.AuthorizationServer, clientId, scope, discovery.Resource, cancellationToken)
                    .ConfigureAwait(false);

                var verificationUri = new Uri(device.VerificationUri, UriKind.Absolute);
                var url = string.IsNullOrWhiteSpace(device.VerificationUriComplete)
                    ? verificationUri
                    : new Uri(device.VerificationUriComplete, UriKind.Absolute);
                var consent = new OutboundConsentRequest(
                    OutboundGrantKind.DeviceCode,
                    url,
                    $"To authorize access to {_serviceRoot}, open {url} and enter the code {device.UserCode}.",
                    device.UserCode,
                    verificationUri);

                _pendingClientId = clientId;
                _pendingDevice = device;
                _pendingDiscovery = discovery;

                _logger.LogInformation("Device authorization started for {ServiceRoot}; awaiting sign-in at {Url}", _serviceRoot, verificationUri);

                return consent;
            }
            finally
            {
                _pendingGrant.Release();
            }
        }

        #endregion

    }

}
