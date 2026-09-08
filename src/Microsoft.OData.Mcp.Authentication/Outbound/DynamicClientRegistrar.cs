// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Registers this process as an OAuth client at an authorization server's RFC 7591
    /// <c>registration_endpoint</c>, so an operator who has no pre-registered application can still sign in.
    /// </summary>
    /// <example>
    /// <code>
    /// var registration = await registrar.RegisterAsync(
    ///     metadata,
    ///     new DynamicClientRegistrationOptions { ClientName = "odata-mcp" },
    ///     redirectUri: callback.RedirectUri,
    ///     grantTypes: [ODataMcpAuthConstants.GrantTypeAuthorizationCode, ODataMcpAuthConstants.GrantTypeRefreshToken],
    ///     cancellationToken);
    ///
    /// var clientId = registration.ClientId;
    /// </code>
    /// </example>
    /// <remarks>
    /// The request goes out on the handler-free <c>"OAuth"</c> named client, the same one discovery and every
    /// token request use, so a <c>401</c> from a registration endpoint can never re-enter the outbound auth
    /// handler and recurse. The caller persists the returned identifier on the cached
    /// <c>TokenContainer</c> — per step 9 of the discovery algorithm — so the next process start reuses it
    /// instead of registering a second client. Per <c>AUTH-12</c> a returned <c>client_secret</c> is never
    /// logged.
    /// </remarks>
    public sealed class DynamicClientRegistrar
    {

        #region Fields

        /// <summary>
        /// The factory <see cref="RegisterAsync(AuthorizationServerMetadata, SdkAuth.DynamicClientRegistrationOptions, Uri, IReadOnlyList{string}, CancellationToken)"/>
        /// asks for the <c>"OAuth"</c> named client.
        /// </summary>
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// The logger this instance records each registration to, never the credentials it returns.
        /// </summary>
        private readonly ILogger<DynamicClientRegistrar> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicClientRegistrar"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The factory used to create the <c>"OAuth"</c> named client per call.</param>
        /// <param name="logger">The logger this instance records each registration to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        public DynamicClientRegistrar(IHttpClientFactory httpClientFactory, ILogger<DynamicClientRegistrar> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Registers a new client and returns the identifier the grant then runs as.
        /// </summary>
        /// <param name="metadata">The authorization server metadata whose <c>registration_endpoint</c> is posted to.</param>
        /// <param name="options">The SDK registration options carrying the client name, home page, application type, and optional initial access token.</param>
        /// <param name="redirectUri">The loopback redirect URI to register, or <see langword="null"/> for a grant that uses no redirect.</param>
        /// <param name="grantTypes">The grant types this client intends to use.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// The registration response, whose <see cref="SdkAuth.DynamicClientRegistrationResponse.ClientId"/> is
        /// guaranteed non-empty.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/>, <paramref name="options"/>, or <paramref name="grantTypes"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="grantTypes"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="metadata"/> publishes no registration endpoint.</exception>
        /// <exception cref="OAuthTokenException">
        /// Thrown when the registration endpoint answers with a non-success status, returns a body that is not a
        /// registration response, or returns one without a <c>client_id</c>.
        /// </exception>
        /// <example>
        /// <code>
        /// var registration = await registrar.RegisterAsync(
        ///     metadata,
        ///     new DynamicClientRegistrationOptions { ClientName = "odata-mcp" },
        ///     redirectUri: null,
        ///     grantTypes: [ODataMcpAuthConstants.DeviceCodeGrantType],
        ///     cancellationToken);
        /// </code>
        /// </example>
        /// <remarks>
        /// <c>response_types</c> is sent only when <paramref name="grantTypes"/> includes
        /// <c>authorization_code</c>, because RFC 7591 ties the two together and an authorization server that
        /// sees <c>["code"]</c> from a device-code-only client may reject the registration. Both <c>201</c> and
        /// <c>200</c> are accepted: RFC 7591 section 3.2.1 specifies <c>201</c>, and deployments answer
        /// <c>200</c> often enough that refusing it would strand operators for no security gain.
        /// </remarks>
        public async Task<SdkAuth.DynamicClientRegistrationResponse> RegisterAsync(
            AuthorizationServerMetadata metadata,
            SdkAuth.DynamicClientRegistrationOptions options,
            Uri? redirectUri,
            IReadOnlyList<string> grantTypes,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(grantTypes);

            if (grantTypes.Count == 0)
            {
                throw new ArgumentException("A dynamic client registration must declare at least one grant type.", nameof(grantTypes));
            }

            if (metadata.RegistrationEndpoint is null)
            {
                throw new InvalidOperationException("The authorization server metadata carries no registration endpoint.");
            }

            OutboundDiscoveryHttp.EnsureDiscoveryUri(metadata.RegistrationEndpoint);

            IList<string>? redirectUris = redirectUri is null ? null : [redirectUri.AbsoluteUri];
            IList<string>? responseTypes = grantTypes.Contains(ODataMcpAuthConstants.GrantTypeAuthorizationCode, StringComparer.Ordinal)
                ? [ODataMcpAuthConstants.ResponseTypeCode]
                : null;
            var payload = new DynamicClientRegistrationRequest
            {
                ApplicationType = options.ApplicationType,
                ClientName = options.ClientName,
                ClientUri = options.ClientUri?.AbsoluteUri,
                GrantTypes = [.. grantTypes],
                RedirectUris = redirectUris,
                ResponseTypes = responseTypes,
                TokenEndpointAuthMethod = ResolveTokenEndpointAuthMethod(grantTypes)
            };

            _logger.LogDebug("Registering a dynamic client at {Endpoint} for {GrantTypes}", metadata.RegistrationEndpoint, string.Join(' ', grantTypes));

            using var content = JsonContent.Create(payload, OutboundOAuthJsonContext.Default.DynamicClientRegistrationRequest);
            using var request = new HttpRequestMessage(HttpMethod.Post, metadata.RegistrationEndpoint)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

            if (!string.IsNullOrWhiteSpace(options.InitialAccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(ODataMcpAuthConstants.BearerScheme, options.InitialAccessToken);
            }

            var client = _httpClientFactory.CreateClient(ODataMcpAuthConstants.OAuthHttpClientName);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                throw new OAuthTokenException("transport_error", exception.Message, 0, exception);
            }

            using (response)
            {
                var statusCode = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw TokenEndpointClient.CreateErrorException(body, statusCode);
                }

                var registration = Parse(body, statusCode);

                _logger.LogInformation("Registered dynamic client {ClientId} at {Issuer}", registration.ClientId, metadata.Issuer);

                return registration;
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses a registration response body and proves it carries a client identifier.
        /// </summary>
        /// <param name="body">The success response body.</param>
        /// <param name="statusCode">The HTTP status code the response carried, reported on failure.</param>
        /// <returns>
        /// The parsed response.
        /// </returns>
        /// <exception cref="OAuthTokenException">
        /// Thrown when the body cannot be parsed, deserializes to <see langword="null"/>, or carries no
        /// <c>client_id</c>.
        /// </exception>
        internal static SdkAuth.DynamicClientRegistrationResponse Parse(string body, int statusCode)
        {
            SdkAuth.DynamicClientRegistrationResponse? parsed;

            try
            {
                parsed = string.IsNullOrWhiteSpace(body)
                    ? null
                    : JsonSerializer.Deserialize(body, OutboundOAuthJsonContext.Default.DynamicClientRegistrationResponse);
            }
            catch (JsonException exception)
            {
                throw new OAuthTokenException("invalid_response", "Registration endpoint returned a body that is not a client registration response.", statusCode, exception);
            }

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.ClientId))
            {
                throw new OAuthTokenException("invalid_response", "Registration endpoint returned no client_id.", statusCode);
            }

            return parsed;
        }

        /// <summary>
        /// Selects the <c>token_endpoint_auth_method</c> a registration declares for the grant types it asks for.
        /// </summary>
        /// <param name="grantTypes">The grant types the registration declares.</param>
        /// <returns>
        /// <see cref="TokenEndpointClient.AuthMethodClientSecretPost"/> when <paramref name="grantTypes"/>
        /// includes <c>client_credentials</c>; otherwise <see cref="TokenEndpointClient.AuthMethodNone"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="grantTypes"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A client credentials client is confidential by definition: registering it as <c>none</c> would ask
        /// the authorization server for a public client and then present a secret it never issued, which every
        /// server answers with <c>invalid_client</c>. Every other grant this client registers for is a public
        /// client completing PKCE or a device code, so it stays <c>none</c> and no secret is ever minted.
        /// </remarks>
        internal static string ResolveTokenEndpointAuthMethod(IReadOnlyList<string> grantTypes)
        {
            ArgumentNullException.ThrowIfNull(grantTypes);

            return grantTypes.Contains(ODataMcpAuthConstants.GrantTypeClientCredentials, StringComparer.Ordinal)
                ? TokenEndpointClient.AuthMethodClientSecretPost
                : TokenEndpointClient.AuthMethodNone;
        }

        #endregion

    }

}
