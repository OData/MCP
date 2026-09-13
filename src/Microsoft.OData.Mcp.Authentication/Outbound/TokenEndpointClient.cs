// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Posts an RFC 6749 token request on the handler-free <c>"OAuth"</c> named <see cref="HttpClient"/> and
    /// turns the answer into either a <see cref="TokenEndpointResponse"/> or an
    /// <see cref="OAuthTokenException"/>. Every grant in this package — device code, refresh, authorization
    /// code, client credentials — sends its token request through this one class.
    /// </summary>
    /// <example>
    /// <code>
    /// var form = new Dictionary&lt;string, string&gt;(StringComparer.Ordinal)
    /// {
    ///     [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeRefreshToken,
    ///     [ODataMcpAuthConstants.RefreshTokenProperty] = refreshToken
    /// };
    ///
    /// var response = await tokenEndpointClient.PostAsync(
    ///     metadata.TokenEndpoint!,
    ///     form,
    ///     clientId,
    ///     clientSecret,
    ///     TokenEndpointClient.AuthMethodClientSecretPost,
    ///     cancellationToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// A new <c>"OAuth"</c> client is requested from <see cref="IHttpClientFactory"/> on every call rather than
    /// cached, matching the "Two named HttpClients" table in <c>specs/v3/AUTHENTICATION.md</c>. Per
    /// <c>AUTH-12</c> neither the request form, the response body, nor any credential is ever logged: the Debug
    /// entries carry only the endpoint, the <c>grant_type</c>, the token type, the lifetime, and whether a
    /// refresh token came back. Unlike well-known discovery, a token request is never retried here — a token
    /// POST is not idempotent and a failed grant is a decision for the caller, not a transport retry.
    /// </remarks>
    public sealed class TokenEndpointClient
    {

        #region Fields

        /// <summary>
        /// RFC 6749 section 2.3.1 <c>client_secret_basic</c> client authentication: the client identifier and
        /// secret travel in an HTTP <c>Basic</c> <c>Authorization</c> header and never in the form.
        /// </summary>
        public const string AuthMethodClientSecretBasic = "client_secret_basic";

        /// <summary>
        /// RFC 6749 section 2.3.1 <c>client_secret_post</c> client authentication: the client identifier and
        /// secret travel as <c>client_id</c> and <c>client_secret</c> form fields.
        /// </summary>
        public const string AuthMethodClientSecretPost = "client_secret_post";

        /// <summary>
        /// Public client authentication: only <c>client_id</c> travels in the form and no secret exists.
        /// </summary>
        public const string AuthMethodNone = "none";

        /// <summary>
        /// The factory <see cref="PostAsync(Uri, IReadOnlyDictionary{string, string}, string?, string?, string, CancellationToken)"/>
        /// asks for the <c>"OAuth"</c> named client.
        /// </summary>
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// The logger this instance records token request and response shapes to, never their values.
        /// </summary>
        private readonly ILogger<TokenEndpointClient> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenEndpointClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The factory used to create the <c>"OAuth"</c> named client per call.</param>
        /// <param name="logger">The logger this instance records token request and response shapes to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        public TokenEndpointClient(IHttpClientFactory httpClientFactory, ILogger<TokenEndpointClient> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Posts a token request and returns the RFC 6749 section 5.1 success response.
        /// </summary>
        /// <param name="tokenEndpoint">The absolute token endpoint to post to.</param>
        /// <param name="form">The grant-specific form fields, such as <c>grant_type</c> and <c>device_code</c>. Client credentials are added by this method, not by the caller.</param>
        /// <param name="clientId">The client identifier, or <see langword="null"/> when the grant carries none.</param>
        /// <param name="clientSecret">The client secret, or <see langword="null"/> for a public client.</param>
        /// <param name="authMethod">The token endpoint authentication method: <see cref="AuthMethodClientSecretBasic"/>, <see cref="AuthMethodClientSecretPost"/>, or <see cref="AuthMethodNone"/>.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// The parsed success response, whose <see cref="TokenEndpointResponse.AccessToken"/> is guaranteed
        /// non-empty.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokenEndpoint"/> or <paramref name="form"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="authMethod"/> is <see langword="null"/>, empty, whitespace, or a method
        /// this client does not implement; when <paramref name="tokenEndpoint"/> is relative or is an
        /// <c>http</c> URI whose host is not loopback; or when <paramref name="authMethod"/> requires
        /// credentials that <paramref name="clientId"/> or <paramref name="clientSecret"/> does not supply.
        /// </exception>
        /// <exception cref="OAuthTokenException">
        /// Thrown when the server answers with a non-success status, when the success body carries no
        /// <c>access_token</c> or cannot be parsed, or when the request fails at the transport.
        /// </exception>
        /// <remarks>
        /// The RFC 8707 <c>resource</c> indicator and <c>scope</c> are the caller's to place in
        /// <paramref name="form"/>; this method never invents either, so a grant that must omit <c>resource</c>
        /// simply leaves it out. Cancellation requested through <paramref name="cancellationToken"/> propagates
        /// as <see cref="OperationCanceledException"/> rather than being wrapped.
        /// </remarks>
        public async Task<TokenEndpointResponse> PostAsync(
            Uri tokenEndpoint,
            IReadOnlyDictionary<string, string> form,
            string? clientId,
            string? clientSecret,
            string authMethod,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tokenEndpoint);
            ArgumentNullException.ThrowIfNull(form);
            ArgumentException.ThrowIfNullOrWhiteSpace(authMethod);

            OutboundDiscoveryHttp.EnsureDiscoveryUri(tokenEndpoint);

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in form)
            {
                fields[field.Key] = field.Value;
            }

            var authorization = ApplyClientAuthentication(fields, clientId, clientSecret, authMethod);
            var grantType = fields.TryGetValue(ODataMcpAuthConstants.GrantTypeParameter, out var value) ? value : "none";

            _logger.LogDebug("Token request to {Endpoint} grant_type={GrantType}", tokenEndpoint, grantType);

            using var content = new FormUrlEncodedContent(fields);
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

            if (authorization is not null)
            {
                request.Headers.Authorization = authorization;
            }

            var client = _httpClientFactory.CreateClient(ODataMcpAuthConstants.OAuthHttpClientName);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new OAuthTokenException("transport_error", ex.Message, 0, ex);
            }

            using (response)
            {
                var statusCode = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw CreateErrorException(body, statusCode);
                }

                var parsed = Parse(body, statusCode);

                _logger.LogDebug(
                    "Token response: type={TokenType} expiresIn={ExpiresIn} hasRefresh={HasRefresh}",
                    parsed.TokenType,
                    parsed.ExpiresIn,
                    !string.IsNullOrWhiteSpace(parsed.RefreshToken));

                return parsed;
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Adds the client credentials the requested authentication method places in the form, and returns the
        /// <c>Authorization</c> header it places outside the form.
        /// </summary>
        /// <param name="fields">The form fields, mutated in place.</param>
        /// <param name="clientId">The client identifier, or <see langword="null"/> when the grant carries none.</param>
        /// <param name="clientSecret">The client secret, or <see langword="null"/> for a public client.</param>
        /// <param name="authMethod">The token endpoint authentication method.</param>
        /// <returns>
        /// The <c>Basic</c> header for <see cref="AuthMethodClientSecretBasic"/>; otherwise
        /// <see langword="null"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fields"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="authMethod"/> is not a method this client implements, or when
        /// <see cref="AuthMethodClientSecretBasic"/> is requested without both a client identifier and a secret.
        /// </exception>
        /// <remarks>
        /// Per RFC 6749 section 2.3.1 the <c>Basic</c> credentials are form-url-encoded before they are joined
        /// with <c>:</c> and base64-encoded, and <c>client_id</c> is deliberately <em>not</em> duplicated into
        /// the form, because an authorization server that sees both may reject the request as ambiguous.
        /// </remarks>
        internal static AuthenticationHeaderValue? ApplyClientAuthentication(IDictionary<string, string> fields, string? clientId, string? clientSecret, string authMethod)
        {
            ArgumentNullException.ThrowIfNull(fields);
            ArgumentException.ThrowIfNullOrWhiteSpace(authMethod);

            if (string.Equals(authMethod, AuthMethodClientSecretBasic, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    throw new ArgumentException($"The '{AuthMethodClientSecretBasic}' authentication method requires both a client id and a client secret.", nameof(authMethod));
                }

                var credentials = $"{Uri.EscapeDataString(clientId)}:{Uri.EscapeDataString(clientSecret)}";

                return new AuthenticationHeaderValue(ODataMcpAuthConstants.BasicScheme, Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
            }

            if (string.Equals(authMethod, AuthMethodClientSecretPost, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    throw new ArgumentException($"The '{AuthMethodClientSecretPost}' authentication method requires a client id.", nameof(authMethod));
                }

                fields[ODataMcpAuthConstants.ClientIdParameter] = clientId;

                if (!string.IsNullOrWhiteSpace(clientSecret))
                {
                    fields[ODataMcpAuthConstants.ClientSecretParameter] = clientSecret;
                }

                return null;
            }

            if (string.Equals(authMethod, AuthMethodNone, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    fields[ODataMcpAuthConstants.ClientIdParameter] = clientId;
                }

                return null;
            }

            throw new ArgumentException($"Unsupported token endpoint authentication method: {authMethod}", nameof(authMethod));
        }

        /// <summary>
        /// Turns a non-success response body into the exception the caller sees.
        /// </summary>
        /// <param name="body">The response body, which may or may not be an RFC 6749 section 5.2 error payload.</param>
        /// <param name="statusCode">The HTTP status code the request failed with.</param>
        /// <returns>
        /// An exception carrying the payload's <c>error</c> and <c>error_description</c>, or the error code
        /// <c>unknown_error</c> when the body is not a parsable error payload.
        /// </returns>
        /// <remarks>
        /// A body that is not JSON, or is JSON without an <c>error</c> member, is never echoed into the message,
        /// because a token endpoint that fails mid-response can put an access token in the body it did send.
        /// </remarks>
        internal static OAuthTokenException CreateErrorException(string body, int statusCode)
        {
            OAuthErrorPayload? payload = null;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    payload = JsonSerializer.Deserialize(body, OutboundOAuthJsonContext.Default.OAuthErrorPayload);
                }
                catch (JsonException)
                {
                    payload = null;
                }
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.Error))
            {
                return new OAuthTokenException("unknown_error", null, statusCode);
            }

            return new OAuthTokenException(payload.Error, payload.ErrorDescription, statusCode);
        }

        /// <summary>
        /// Parses a success response body and proves it carries an access token.
        /// </summary>
        /// <param name="body">The success response body.</param>
        /// <param name="statusCode">The HTTP status code the response carried, reported on failure.</param>
        /// <returns>
        /// The parsed response.
        /// </returns>
        /// <exception cref="OAuthTokenException">
        /// Thrown when the body cannot be parsed, deserializes to <see langword="null"/>, or carries no
        /// <c>access_token</c>.
        /// </exception>
        internal static TokenEndpointResponse Parse(string body, int statusCode)
        {
            TokenEndpointResponse? parsed;

            try
            {
                parsed = string.IsNullOrWhiteSpace(body)
                    ? null
                    : JsonSerializer.Deserialize(body, OutboundOAuthJsonContext.Default.TokenEndpointResponse);
            }
            catch (JsonException ex)
            {
                throw new OAuthTokenException("invalid_response", "Token endpoint returned a body that is not a token response.", statusCode, ex);
            }

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.AccessToken))
            {
                throw new OAuthTokenException("invalid_response", "Token endpoint returned no access_token.", statusCode);
            }

            return parsed;
        }

        #endregion

    }

}
