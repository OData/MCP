// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Runs the RFC 6749 authorization code grant with RFC 7636 PKCE over an RFC 8252 loopback redirect: build
    /// the authorization URL with an <c>S256</c> challenge and a bound <c>state</c>, then exchange the returned
    /// code — after the RFC 9207 <c>iss</c> check — for a token.
    /// </summary>
    /// <example>
    /// <code>
    /// using var callback = LoopbackAuthorizationCallback.Start(options.RedirectUri);
    ///
    /// var begin = grant.Begin(metadata, clientId, scope, resource, callback.RedirectUri);
    /// var result = await callback.WaitAsync(begin.State, cancellationToken);
    /// var response = await grant.ExchangeAsync(metadata, result, begin.CodeVerifier, clientId, callback.RedirectUri, resource, cancellationToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>specs/v3/AUTHENTICATION.md</c> "Grants → Authorization code + PKCE + loopback": <c>S256</c> is
    /// required and a server that publishes <c>code_challenge_methods_supported</c> without it is refused
    /// rather than downgraded to <c>plain</c>; the SDK's obsolete <c>AuthorizationRedirectDelegate</c> is never
    /// used; and an <c>iss</c> that disagrees with the selected issuer stops the grant <em>before</em> the code
    /// reaches any token endpoint, which is the RFC 9207 mix-up defense. Per <c>AUTH-12</c> the code verifier,
    /// the code, and the state never reach a log: the Debug lines carry only the endpoints.
    /// </remarks>
    public sealed class AuthorizationCodePkceGrant
    {

        #region Fields

        /// <summary>
        /// The number of random bytes the RFC 7636 code verifier is drawn from, which base64url-encodes to the
        /// 43 character minimum the specification allows.
        /// </summary>
        public const int CodeVerifierByteCount = 32;

        /// <summary>
        /// The number of random bytes the <c>state</c> is drawn from.
        /// </summary>
        public const int StateByteCount = 32;

        /// <summary>
        /// The logger this instance records the authorization and token endpoints to, never their parameters.
        /// </summary>
        private readonly ILogger<AuthorizationCodePkceGrant> _logger;

        /// <summary>
        /// The client the code exchange goes through.
        /// </summary>
        private readonly TokenEndpointClient _tokenEndpointClient;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationCodePkceGrant"/> class.
        /// </summary>
        /// <param name="tokenEndpointClient">The client the code exchange goes through.</param>
        /// <param name="logger">The logger this instance records the authorization and token endpoints to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokenEndpointClient"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        public AuthorizationCodePkceGrant(TokenEndpointClient tokenEndpointClient, ILogger<AuthorizationCodePkceGrant> logger)
        {
            ArgumentNullException.ThrowIfNull(tokenEndpointClient);
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
            _tokenEndpointClient = tokenEndpointClient;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Builds the authorization URL the operator signs in at, together with the <c>state</c> and code
        /// verifier the response is validated and exchanged against.
        /// </summary>
        /// <param name="metadata">The authorization server metadata whose <c>authorization_endpoint</c> is used.</param>
        /// <param name="clientId">The public client identifier the grant runs as.</param>
        /// <param name="scope">The space-delimited scope to request, or <see langword="null"/> to request none.</param>
        /// <param name="resource">The RFC 8707 resource indicator, sent exactly as it was published or configured, or <see langword="null"/> when the request sends none.</param>
        /// <param name="redirectUri">The loopback redirect URI the authorization response is delivered to.</param>
        /// <returns>
        /// The absolute authorization URI, the freshly generated <c>state</c>, and the code verifier.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is <see langword="null"/>, empty, or whitespace, or when <paramref name="redirectUri"/> is not absolute.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="metadata"/> carries no authorization endpoint.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when <paramref name="metadata"/>'s <c>authorization_endpoint</c> is neither an <c>https</c>
        /// URI nor an <c>http</c> URI on a loopback host.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when <paramref name="metadata"/> publishes <c>code_challenge_methods_supported</c> and it does
        /// not include <see cref="ODataMcpAuthConstants.CodeChallengeMethodS256"/>.
        /// </exception>
        /// <example>
        /// <code>
        /// var begin = grant.Begin(metadata, "cli", "read offline_access", resource, callback.RedirectUri);
        ///
        /// Console.Error.WriteLine($"Sign in at {begin.AuthorizationUri}");
        /// </code>
        /// </example>
        /// <remarks>
        /// The returned code verifier is a secret: it is the only thing that proves the process redeeming the
        /// code is the one that requested it, so it must never be logged, elicited, or placed on
        /// <see cref="OutboundConsentRequest"/>. Existing query parameters on the authorization endpoint —
        /// a tenant hint, for instance — are preserved rather than replaced.
        /// </remarks>
        public (Uri AuthorizationUri, string State, string CodeVerifier) Begin(AuthorizationServerMetadata metadata, string clientId, string? scope, Uri? resource, Uri redirectUri)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            ArgumentNullException.ThrowIfNull(redirectUri);

            if (!redirectUri.IsAbsoluteUri)
            {
                throw new ArgumentException($"The redirect URI must be absolute: {redirectUri}", nameof(redirectUri));
            }

            if (metadata.AuthorizationEndpoint is null)
            {
                throw new InvalidOperationException("The authorization server metadata carries no authorization endpoint.");
            }

            if (!OutboundDiscoveryHttp.IsSecureOrLoopback(metadata.AuthorizationEndpoint))
            {
                throw new OutboundDiscoveryException(
                    "Consent URL must use https (or http on loopback): "
                    + (metadata.AuthorizationEndpoint.IsAbsoluteUri ? metadata.AuthorizationEndpoint.Scheme : "relative URL"));
            }

            if (metadata.CodeChallengeMethodsSupported is { } methods && !methods.Contains(ODataMcpAuthConstants.CodeChallengeMethodS256))
            {
                throw new NotSupportedException($"Authorization server {metadata.Issuer} does not support the {ODataMcpAuthConstants.CodeChallengeMethodS256} PKCE code challenge method.");
            }

            var codeVerifier = CreateCodeVerifier();
            var state = CreateState();
            var parameters = new List<KeyValuePair<string, string>>
            {
                new(ODataMcpAuthConstants.ResponseTypeParameter, ODataMcpAuthConstants.ResponseTypeCode),
                new(ODataMcpAuthConstants.ClientIdParameter, clientId),
                new(ODataMcpAuthConstants.RedirectUriParameter, redirectUri.AbsoluteUri),
                new(ODataMcpAuthConstants.StateParameter, state),
                new(ODataMcpAuthConstants.CodeChallengeParameter, CreateCodeChallenge(codeVerifier)),
                new(ODataMcpAuthConstants.CodeChallengeMethodParameter, ODataMcpAuthConstants.CodeChallengeMethodS256)
            };

            if (!string.IsNullOrWhiteSpace(scope))
            {
                parameters.Add(new(ODataMcpAuthConstants.ScopeParameter, scope));
            }

            if (resource is not null)
            {
                parameters.Add(new(ODataMcpAuthConstants.ResourceParameter, resource.OriginalString));
            }

            var authorizationUri = new Uri(AppendQuery(metadata.AuthorizationEndpoint, parameters), UriKind.Absolute);

            _logger.LogDebug("Authorization code sign-in starting at {Endpoint} with a loopback redirect to {RedirectUri}", metadata.AuthorizationEndpoint, redirectUri);

            return (authorizationUri, state, codeVerifier);
        }

        /// <summary>
        /// Exchanges an authorization code for a token, after proving the response came from the authorization
        /// server this grant was started against.
        /// </summary>
        /// <param name="metadata">The authorization server metadata whose <c>token_endpoint</c> is posted to and whose issuer the RFC 9207 <c>iss</c> is matched against.</param>
        /// <param name="result">The authorization response the loopback callback captured.</param>
        /// <param name="codeVerifier">The code verifier <see cref="Begin(AuthorizationServerMetadata, string, string, Uri, Uri)"/> produced.</param>
        /// <param name="clientId">The public client identifier the grant runs as.</param>
        /// <param name="redirectUri">The redirect URI the authorization request carried, echoed back per RFC 6749 section 4.1.3.</param>
        /// <param name="resource">The RFC 8707 resource indicator, sent exactly as it was published or configured, or <see langword="null"/> when the request sends none.</param>
        /// <param name="cancellationToken">The token that cancels the exchange.</param>
        /// <returns>
        /// The successful token response.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/>, <paramref name="result"/>, or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="codeVerifier"/> or <paramref name="clientId"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="metadata"/> carries no token endpoint.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when <paramref name="result"/> carries no code, or when its RFC 9207 <c>iss</c> is present and
        /// does not equal <see cref="AuthorizationServerMetadata.Issuer"/>.
        /// </exception>
        /// <exception cref="OAuthTokenException">Thrown when the token endpoint rejects the code or the request fails at the transport.</exception>
        /// <remarks>
        /// The <c>iss</c> check runs before anything is sent, so a code minted by another authorization server
        /// is never presented to this one — the whole point of RFC 9207. An absent <c>iss</c> is not invented:
        /// the grant still relies on the <c>state</c> binding the loopback callback enforced.
        /// </remarks>
        public async Task<TokenEndpointResponse> ExchangeAsync(
            AuthorizationServerMetadata metadata,
            SdkAuth.AuthorizationResult result,
            string codeVerifier,
            string clientId,
            Uri redirectUri,
            Uri? resource,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            ArgumentNullException.ThrowIfNull(redirectUri);

            if (metadata.TokenEndpoint is null)
            {
                throw new InvalidOperationException("The authorization server metadata carries no token endpoint.");
            }

            if (string.IsNullOrWhiteSpace(result.Code))
            {
                throw new OutboundDiscoveryException("Authorization response carries no code.");
            }

            EnsureIssuerMatches(metadata, result.Iss);

            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeAuthorizationCode,
                [ODataMcpAuthConstants.CodeParameter] = result.Code,
                [ODataMcpAuthConstants.RedirectUriParameter] = redirectUri.AbsoluteUri,
                [ODataMcpAuthConstants.CodeVerifierParameter] = codeVerifier
            };

            if (resource is not null)
            {
                form[ODataMcpAuthConstants.ResourceParameter] = resource.OriginalString;
            }

            _logger.LogDebug("Exchanging the authorization code at {Endpoint}", metadata.TokenEndpoint);

            return await _tokenEndpointClient
                .PostAsync(metadata.TokenEndpoint, form, clientId, null, TokenEndpointClient.AuthMethodNone, cancellationToken)
                .ConfigureAwait(false);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Appends parameters to a URL, preserving any query string it already carries.
        /// </summary>
        /// <param name="url">The absolute URL to append to.</param>
        /// <param name="parameters">The parameters, appended in order.</param>
        /// <returns>
        /// The URL with the percent-encoded parameters appended.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
        internal static string AppendQuery(Uri url, IReadOnlyList<KeyValuePair<string, string>> parameters)
        {
            ArgumentNullException.ThrowIfNull(url);
            ArgumentNullException.ThrowIfNull(parameters);

            var builder = new StringBuilder(url.AbsoluteUri);
            var separator = string.IsNullOrEmpty(url.Query) ? '?' : '&';

            foreach (var parameter in parameters)
            {
                builder.Append(separator).Append(Uri.EscapeDataString(parameter.Key)).Append('=').Append(Uri.EscapeDataString(parameter.Value));
                separator = '&';
            }

            return builder.ToString();
        }

        /// <summary>
        /// Computes the RFC 7636 <c>S256</c> code challenge for a code verifier.
        /// </summary>
        /// <param name="codeVerifier">The code verifier.</param>
        /// <returns>
        /// The base64url encoding, without padding, of the ASCII SHA-256 hash of <paramref name="codeVerifier"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="codeVerifier"/> is <see langword="null"/>, empty, or whitespace.</exception>
        internal static string CreateCodeChallenge(string codeVerifier)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);

            return Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        }

        /// <summary>
        /// Creates a fresh RFC 7636 code verifier.
        /// </summary>
        /// <returns>
        /// A 43 character base64url string drawn from <see cref="CodeVerifierByteCount"/> cryptographically
        /// random bytes.
        /// </returns>
        internal static string CreateCodeVerifier()
        {
            return Encode(RandomNumberGenerator.GetBytes(CodeVerifierByteCount));
        }

        /// <summary>
        /// Creates a fresh authorization request <c>state</c>.
        /// </summary>
        /// <returns>
        /// A base64url string drawn from <see cref="StateByteCount"/> cryptographically random bytes.
        /// </returns>
        internal static string CreateState()
        {
            return Encode(RandomNumberGenerator.GetBytes(StateByteCount));
        }

        /// <summary>
        /// Encodes bytes as unpadded base64url.
        /// </summary>
        /// <param name="bytes">The bytes to encode.</param>
        /// <returns>
        /// The base64 encoding with <c>+</c> replaced by <c>-</c>, <c>/</c> replaced by <c>_</c>, and the
        /// <c>=</c> padding removed.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is <see langword="null"/>.</exception>
        internal static string Encode(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Enforces the RFC 9207 mix-up defense on an authorization response.
        /// </summary>
        /// <param name="metadata">The authorization server metadata the grant was started against.</param>
        /// <param name="iss">The <c>iss</c> the authorization response carried, or <see langword="null"/> when it carried none.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is <see langword="null"/>.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when <paramref name="iss"/> is present and is not an absolute URI whose spelling is exactly
        /// <see cref="AuthorizationServerMetadata.Issuer"/>, including when the metadata publishes no issuer to
        /// compare against.
        /// </exception>
        /// <remarks>
        /// The comparison is the RFC 8414 section 2 "simple string comparison" of RFC 3986 section 6.2.1, run
        /// with <see cref="StringComparison.Ordinal"/> over the raw issuer strings, not <see cref="Uri"/>
        /// equality and not over a normalized <see cref="Uri.AbsoluteUri"/>: two issuers that differ only by a
        /// trailing slash are a <em>mismatch</em>, because an authorization server's issuer identifier is the
        /// exact string it publishes and RFC 9207 exists to catch a substituted server, not to normalize one.
        /// <para>
        /// Comparing against <see cref="AuthorizationServerMetadata.Issuer"/> rather than a parsed
        /// <see cref="Uri"/> is what lets an authority-only issuer such as <c>https://accounts.google.com</c>
        /// match the <c>iss</c> its own authorization endpoint returns: parsing would have appended the path
        /// <c>/</c> to one side and not the other.
        /// </para>
        /// <para>
        /// <paramref name="iss"/> is still parsed first, so a value that is not an absolute URI at all is
        /// rejected before any comparison runs.
        /// </para>
        /// </remarks>
        internal static void EnsureIssuerMatches(AuthorizationServerMetadata metadata, string? iss)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            if (string.IsNullOrWhiteSpace(iss))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(metadata.Issuer))
            {
                throw new OutboundDiscoveryException($"Authorization response carries iss '{iss}' but the authorization server metadata publishes no issuer to match it against.");
            }

            if (!Uri.TryCreate(iss, UriKind.Absolute, out _) || !string.Equals(iss, metadata.Issuer, StringComparison.Ordinal))
            {
                throw new OutboundDiscoveryException($"Authorization response issuer '{iss}' does not match the selected authorization server '{metadata.Issuer}'.");
            }
        }

        #endregion

    }

}
