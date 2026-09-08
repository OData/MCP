// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The <c>"OData"</c> named client's delegating handler: it attaches whatever credential the operator
    /// configured, and when none was configured it drives <see cref="OutboundOAuthClient"/> through the send
    /// path — attach a still-valid token, refresh one that has fallen inside the skew, or run a grant off the
    /// challenge the resource answered with.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddTransient&lt;ODataOutboundAuthHandler&gt;();
    /// services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName, client =&gt; client.BaseAddress = serviceRoot)
    ///     .AddHttpMessageHandler(sp =&gt; sp.GetRequiredService&lt;ODataOutboundAuthHandler&gt;());
    /// </code>
    /// </example>
    /// <remarks>
    /// This handler belongs on the <c>"OData"</c> client only. The <c>"OAuth"</c> client — protected resource
    /// metadata, authorization server metadata, device authorization, token, refresh — is registered without it,
    /// so a <c>401</c> on a well-known document can never re-enter discovery and recurse.
    /// <para>
    /// <see cref="OutboundOAuthOptions.ConsentPresenter"/> is read at the moment a grant is needed and never
    /// captured in the constructor, because a host swaps a blocking stderr presenter for an MCP elicitation once
    /// its session is up.
    /// </para>
    /// </remarks>
    public sealed class ODataOutboundAuthHandler : DelegatingHandler
    {

        #region Fields

        /// <summary>
        /// The outbound OAuth state machine this handler asks for tokens.
        /// </summary>
        internal readonly OutboundOAuthClient _client;

        /// <summary>
        /// The logger this instance records each attach, challenge, and retry to.
        /// </summary>
        internal readonly ILogger<ODataOutboundAuthHandler> _logger;

        /// <summary>
        /// The operator's outbound OAuth settings, read live on every send.
        /// </summary>
        internal readonly OutboundOAuthOptions _options;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataOutboundAuthHandler"/> class.
        /// </summary>
        /// <param name="client">The outbound OAuth state machine this handler asks for tokens.</param>
        /// <param name="options">The operator's outbound OAuth settings.</param>
        /// <param name="logger">The logger this instance records each attach, challenge, and retry to.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="client"/>, <paramref name="options"/>, or <paramref name="logger"/> is
        /// <see langword="null"/>.
        /// </exception>
        public ODataOutboundAuthHandler(OutboundOAuthClient client, OutboundOAuthOptions options, ILogger<ODataOutboundAuthHandler> logger)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(logger);

            _client = client;
            _logger = logger;
            _options = options;
        }

        #endregion

        #region Protected Methods

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="OAuthConsentRequiredException">
        /// Thrown when a grant needs a human and <see cref="OutboundOAuthOptions.ConsentPresenter"/> is
        /// <see langword="null"/>; the caller elicits the URL the exception carries and completes the grant.
        /// </exception>
        /// <exception cref="OutboundDiscoveryException">Thrown when the challenge cannot be turned into an authorization server.</exception>
        /// <exception cref="OAuthTokenException">Thrown when the grant itself fails.</exception>
        /// <remarks>
        /// At most one retry per send, and only when a new token could plausibly change the answer. Concretely:
        /// a 401 or 403 on a request that carried <em>no</em> credential runs the acquisition path; a 401 that
        /// names <c>invalid_token</c> on a request that carried one refreshes once — unless this same send had
        /// already refreshed on the way in, in which case a second POST would answer the same challenge twice.
        /// Everything else, a 403 on a valid token above all, is returned to the caller untouched: an
        /// insufficient-permissions answer is not a sign-in problem, and treating it as one would block the
        /// process on a consent prompt no new token can satisfy. An explicit credential short-circuits all of
        /// it: no discovery, no refresh, no retry, because the operator owns that token's lifetime.
        /// <para>
        /// When that single refresh cannot produce a token — no refresh token was ever granted, or the
        /// authorization server rejected the one that was — the send falls through to the acquisition path
        /// rather than returning the <c>401</c>. A revoked credential is exactly the case a human can fix, and
        /// refusing to ask would strand the process on a token nothing will ever renew. It is still one
        /// acquisition and one retry: the acquisition either yields a token to retry with or throws, so the
        /// send can never loop.
        /// </para>
        /// </remarks>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (_options.HasExplicitCredentials)
            {
                ApplyExplicitCredentials(request);

                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            var retry = await CloneAsync(request).ConfigureAwait(false);
            var lookup = await _client.GetTokenForSendAsync(cancellationToken).ConfigureAwait(false);
            var attached = lookup.Token is not null;

            if (lookup.Token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(ODataMcpAuthConstants.BearerScheme, lookup.Token.AccessToken);
            }

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden)
            {
                retry.Dispose();

                return response;
            }

            var challenges = ReadChallenges(response);
            SdkAuth.TokenContainer? acquired = null;

            try
            {
                if (!attached)
                {
                    _logger.LogInformation("HTTP {Status} from {Uri} with no credential attached; running the outbound OAuth acquisition path.", (int)response.StatusCode, request.RequestUri);

                    acquired = await _client.AcquireAsync(challenges, (int)response.StatusCode, IsInteractiveAllowed(_options), cancellationToken).ConfigureAwait(false);
                }
                else if (!lookup.Refreshed && response.StatusCode is HttpStatusCode.Unauthorized && IsInvalidTokenChallenge(challenges))
                {
                    _logger.LogInformation("The resource rejected the attached token as invalid_token; refreshing once.");

                    acquired = await _client.ForceRefreshAsync(cancellationToken).ConfigureAwait(false);

                    if (acquired is null)
                    {
                        _logger.LogInformation("The refresh produced no token; running the outbound OAuth acquisition path instead.");

                        acquired = await _client.AcquireAsync(challenges, (int)response.StatusCode, IsInteractiveAllowed(_options), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                response.Dispose();
                retry.Dispose();

                throw;
            }

            if (acquired is null)
            {
                _logger.LogInformation("HTTP {Status} from {Uri} stands: a new token could not fix it.", (int)response.StatusCode, request.RequestUri);
                retry.Dispose();

                return response;
            }

            response.Dispose();
            retry.Headers.Authorization = new AuthenticationHeaderValue(ODataMcpAuthConstants.BearerScheme, acquired.AccessToken);

            _logger.LogDebug("Retrying {Uri} with a freshly acquired token: {Token}", retry.RequestUri, OutboundAuthLogRedactor.DescribeToken(acquired));

            return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Attaches the explicit credential the operator configured, if any.
        /// </summary>
        /// <param name="request">The request being sent.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="OutboundOAuthOptions.ApiKey"/> is set without
        /// <see cref="OutboundOAuthOptions.ApiKeyHeader"/>, or <see cref="OutboundOAuthOptions.BasicUser"/> is
        /// set without <see cref="OutboundOAuthOptions.BasicPassword"/>.
        /// </exception>
        /// <remarks>
        /// <see cref="OutboundOAuthOptions.Validate"/> already refuses both incomplete combinations at the CLI
        /// boundary; the guards here are defense in depth, because an API key silently sent as a bearer token or
        /// a Basic header built from a missing password would both be security-relevant failures.
        /// </remarks>
        internal void ApplyExplicitCredentials(HttpRequestMessage request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!string.IsNullOrWhiteSpace(_options.AuthToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(ODataMcpAuthConstants.BearerScheme, _options.AuthToken);

                return;
            }

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                if (string.IsNullOrWhiteSpace(_options.ApiKeyHeader))
                {
                    throw new InvalidOperationException("--api-key requires --api-key-header.");
                }

                request.Headers.TryAddWithoutValidation(_options.ApiKeyHeader, _options.ApiKey);

                return;
            }

            if (string.IsNullOrWhiteSpace(_options.BasicUser))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.BasicPassword))
            {
                throw new InvalidOperationException("--basic-user requires --basic-password.");
            }

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.BasicUser}:{_options.BasicPassword}"));
            request.Headers.Authorization = new AuthenticationHeaderValue(ODataMcpAuthConstants.BasicScheme, credentials);
        }

        /// <summary>
        /// Copies a request so the same call can be sent a second time after a token is acquired.
        /// </summary>
        /// <param name="request">The request to copy.</param>
        /// <returns>
        /// A new request carrying the same method, URI, version, headers, options, and a buffered copy of the
        /// content.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// An <see cref="HttpRequestMessage"/> cannot be sent twice, so the clone is taken <em>before</em> the
        /// first send: by the time a <c>401</c> comes back the original's content stream may already be spent.
        /// </remarks>
        internal static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };

            if (request.Content is not null)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var content = new ByteArrayContent(bytes);

                foreach (var header in request.Content.Headers)
                {
                    content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                clone.Content = content;
            }

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var option in (IDictionary<string, object?>)request.Options)
            {
                clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
            }

            return clone;
        }

        /// <summary>
        /// Determines whether the acquisition this handler is about to run may prompt a human.
        /// </summary>
        /// <param name="options">The operator's outbound OAuth settings.</param>
        /// <returns>
        /// <see langword="false"/> when the operator pinned a grant that runs without a human — client
        /// credentials or identity assertion; otherwise <see langword="true"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A daemon that was told to use <c>--grant client_credentials</c> must never turn a <c>401</c> into a
        /// device code prompt on a console nobody is watching; the <c>401</c> is returned to the caller instead.
        /// When no such grant was pinned, interactive is allowed: a configured
        /// <see cref="OutboundOAuthOptions.ConsentPresenter"/> prompts, and no presenter raises
        /// <see cref="OAuthConsentRequiredException"/> so a mid-session host can elicit the URL itself.
        /// </remarks>
        internal static bool IsInteractiveAllowed(OutboundOAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return options.Grant is not OutboundGrantKind.ClientCredentials and not OutboundGrantKind.IdentityAssertion
                && (options.ConsentPresenter is not null || !options.HasExplicitCredentials);
        }

        /// <summary>
        /// Determines whether a challenge says the bearer token that was sent is no longer acceptable.
        /// </summary>
        /// <param name="challenges">The raw <c>WWW-Authenticate</c> header values the response carried.</param>
        /// <returns>
        /// <see langword="true"/> when a <c>Bearer</c> challenge names
        /// <see cref="ODataMcpAuthConstants.ErrorInvalidToken"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="challenges"/> is <see langword="null"/>.</exception>
        internal static bool IsInvalidTokenChallenge(IReadOnlyList<string> challenges)
        {
            ArgumentNullException.ThrowIfNull(challenges);

            var challenge = WwwAuthenticateParser.SelectBearer(WwwAuthenticateParser.ParseAll(challenges));

            return string.Equals(challenge?.Error, ODataMcpAuthConstants.ErrorInvalidToken, StringComparison.Ordinal);
        }

        /// <summary>
        /// Reads the raw, deduplicated <c>WWW-Authenticate</c> header values a response carried.
        /// </summary>
        /// <param name="response">The <c>401</c> or <c>403</c> response to read.</param>
        /// <returns>
        /// The distinct header values in the order received, or an empty list when the header is absent.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="response"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Deliberately <see cref="HttpHeaders.TryGetValues(string, out IEnumerable{string})"/> rather than the
        /// typed <see cref="HttpResponseHeaders.WwwAuthenticate"/> collection, matching
        /// <c>ODataExecuteResult.ReadWwwAuthenticate</c>. <see cref="HttpHeaders"/> moves a value it cannot
        /// parse as an <see cref="AuthenticationHeaderValue"/> list into its invalid bucket, so one nonstandard
        /// challenge alongside a perfectly good <c>Bearer</c> one would empty the typed collection and turn a
        /// recoverable <c>401</c> into the misleading "401 with no WWW-Authenticate" failure. This package does
        /// its own RFC 9110 parsing in <see cref="WwwAuthenticateParser"/> and needs the bytes as sent.
        /// </remarks>
        internal static IReadOnlyList<string> ReadChallenges(HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);

            if (response.Headers.TryGetValues(ODataMcpAuthConstants.WwwAuthenticateHeader, out var values))
            {
                return [.. values.Distinct(StringComparer.Ordinal)];
            }

            return [];
        }

        #endregion

    }

}
