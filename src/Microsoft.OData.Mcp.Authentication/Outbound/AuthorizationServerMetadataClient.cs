// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Discovers an RFC 8414 authorization server metadata document (or its OpenID Connect discovery document
    /// counterpart) over the handler-free <c>"OAuth"</c> named <see cref="HttpClient"/>, probing the well-known
    /// candidates for a base URI in the order that prefers a Microsoft identity platform v2 document over its
    /// v1 counterpart.
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new AuthorizationServerMetadataClient(httpClientFactory, logger);
    /// var metadata = await client.DiscoverAsync(new Uri("https://login.microsoftonline.com/common"), cancellationToken);
    /// if (metadata is not null)
    /// {
    ///     // metadata.TokenEndpoint is guaranteed non-null and absolute.
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// A new <c>"OAuth"</c> client is requested from <see cref="IHttpClientFactory"/> on every call rather than
    /// cached, matching the "Two named HttpClients" table in <c>specs/v3/AUTHENTICATION.md</c>. Per <c>AUTH-14</c>,
    /// an Entra-style authorization server publishes both a v1 (OpenID Connect only) and a v2 (RFC 8414 and OpenID
    /// Connect) document at the same base URI; <see cref="CandidateDocuments(Uri)"/> orders the v2 candidates
    /// first so discovery never selects the v1 issuer when a v2 one is available. Response bodies are never
    /// logged.
    /// </remarks>
    public sealed class AuthorizationServerMetadataClient
    {

        #region Fields

        /// <summary>
        /// The factory <see cref="TryGetAsync(Uri, CancellationToken)"/> asks for the <c>"OAuth"</c> named client.
        /// </summary>
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// The logger this instance records non-fatal well-known outcomes and probe attempts to.
        /// </summary>
        private readonly ILogger<AuthorizationServerMetadataClient> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationServerMetadataClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The factory used to create the <c>"OAuth"</c> named client per call.</param>
        /// <param name="logger">The logger this instance records non-fatal well-known outcomes and probe attempts to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        public AuthorizationServerMetadataClient(IHttpClientFactory httpClientFactory, ILogger<AuthorizationServerMetadataClient> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Builds the well-known authorization server metadata candidates for a base URI, in probe order.
        /// </summary>
        /// <param name="baseUri">The absolute authorization server base URI, such as an issuer or an authorization endpoint's origin and path.</param>
        /// <returns>
        /// Five candidates when <paramref name="baseUri"/> carries a path beyond <c>/</c>: the versioned (v2)
        /// RFC 8414 document, the versioned OpenID Connect document, the unversioned RFC 8414 document, the RFC
        /// 8414 §3.1 path-prefixed document, and the unversioned OpenID Connect document, in that order. The
        /// path-prefixed candidate is omitted, leaving four candidates, when <paramref name="baseUri"/> has no
        /// path beyond <c>/</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseUri"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="baseUri"/> is not an absolute URI.</exception>
        /// <remarks>
        /// A trailing <c>/</c> on <paramref name="baseUri"/> is trimmed before any suffix is appended, so no
        /// candidate carries a doubled path separator. The versioned candidates are listed first so that an
        /// Entra-style authorization server, which publishes a v1 (OpenID Connect only) document at the
        /// unversioned base and a v2 (RFC 8414 and OpenID Connect) document under <c>/v2.0</c>, is discovered
        /// through its v2 issuer.
        /// </remarks>
        public static IReadOnlyList<Uri> CandidateDocuments(Uri baseUri)
        {
            ArgumentNullException.ThrowIfNull(baseUri);

            if (!baseUri.IsAbsoluteUri)
            {
                throw new ArgumentException($"The authorization server base URI must be absolute: {baseUri}", nameof(baseUri));
            }

            var trimmedBase = baseUri.AbsoluteUri.TrimEnd('/');
            var origin = $"{baseUri.Scheme}://{baseUri.Authority}";
            var path = baseUri.AbsolutePath.Length > 1 ? baseUri.AbsolutePath.TrimEnd('/') : baseUri.AbsolutePath;

            var candidates = new List<Uri>
            {
                new Uri($"{trimmedBase}{ODataMcpAuthConstants.V2Segment}{ODataMcpAuthConstants.OAuthAuthorizationServerSuffix}"),
                new Uri($"{trimmedBase}{ODataMcpAuthConstants.V2Segment}{ODataMcpAuthConstants.OpenIdConfigurationSuffix}"),
                new Uri($"{trimmedBase}{ODataMcpAuthConstants.OAuthAuthorizationServerSuffix}")
            };

            if (path != "/")
            {
                candidates.Add(new Uri($"{origin}{ODataMcpAuthConstants.OAuthAuthorizationServerSuffix}{path}"));
            }

            candidates.Add(new Uri($"{trimmedBase}{ODataMcpAuthConstants.OpenIdConfigurationSuffix}"));

            return candidates;
        }

        /// <summary>
        /// Probes <see cref="CandidateDocuments(Uri)"/> in order and returns the first candidate that resolves to
        /// a usable metadata document.
        /// </summary>
        /// <param name="baseUri">The absolute authorization server base URI to discover metadata for.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// The first candidate's parsed document, or <see langword="null"/> when every candidate is non-fatally
        /// unusable (missing, unparseable, or lacking a <c>token_endpoint</c>).
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseUri"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="baseUri"/> is not an absolute URI.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when a probe times out, fails at the transport, or returns a 5xx status, after one retry.
        /// </exception>
        /// <example>
        /// <code>
        /// var metadata = await client.DiscoverAsync(new Uri("https://login.microsoftonline.com/common"), cancellationToken);
        /// </code>
        /// </example>
        /// <remarks>
        /// Every probe is attempted over <c>http</c> only when its host is loopback; discovery stops at the first
        /// candidate with a <c>token_endpoint</c>, so a later candidate (such as the legacy v1 document) is never
        /// requested once an earlier one has already succeeded.
        /// </remarks>
        public async Task<AuthorizationServerMetadata?> DiscoverAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            foreach (var candidate in CandidateDocuments(baseUri))
            {
                _logger.LogInformation("Probing authorization server metadata at {Url}", candidate);

                var metadata = await TryGetAsync(candidate, cancellationToken).ConfigureAwait(false);
                if (metadata is not null)
                {
                    return metadata;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to fetch and parse the authorization server metadata document at a well-known URL.
        /// </summary>
        /// <param name="url">The absolute authorization server metadata URL.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// The parsed document on a <c>200</c> response with a well-formed body carrying an absolute
        /// <c>token_endpoint</c>; otherwise <see langword="null"/> after a 401, 403, 404, unparseable body, a
        /// document without a usable <c>token_endpoint</c>, or any other non-retryable non-success status.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="url"/> is not an absolute URI, or is an <c>http</c> URI whose host is not loopback.
        /// </exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when the request times out, fails at the transport, or returns a 5xx status, after one retry.
        /// </exception>
        /// <example>
        /// <code>
        /// var metadata = await client.TryGetAsync(new Uri("https://login.microsoftonline.com/common/v2.0/.well-known/oauth-authorization-server"), cancellationToken);
        /// </code>
        /// </example>
        public async Task<AuthorizationServerMetadata?> TryGetAsync(Uri url, CancellationToken cancellationToken)
        {
            OutboundDiscoveryHttp.EnsureDiscoveryUri(url);

            var client = _httpClientFactory.CreateClient(ODataMcpAuthConstants.OAuthHttpClientName);

            using var response = await OutboundDiscoveryHttp.GetWithRetryAsync(client, url, "AS metadata", _logger, cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                return null;
            }

            var metadata = await ParseAsync(response, url, cancellationToken).ConfigureAwait(false);
            if (metadata is null)
            {
                return null;
            }

            if (metadata.TokenEndpoint is null || !metadata.TokenEndpoint.IsAbsoluteUri)
            {
                _logger.LogInformation("AS metadata at {Url} has no token_endpoint, continuing", url);

                return null;
            }

            return metadata;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses a successful response body as authorization server metadata.
        /// </summary>
        /// <param name="response">The successful response.</param>
        /// <param name="url">The URL that was requested, for logging only.</param>
        /// <param name="cancellationToken">The token that cancels the read.</param>
        /// <returns>
        /// The parsed document, or <see langword="null"/> when the body is not well-formed JSON, or parses to
        /// <see langword="null"/>.
        /// </returns>
        internal async Task<AuthorizationServerMetadata?> ParseAsync(HttpResponseMessage response, Uri url, CancellationToken cancellationToken)
        {
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var metadata = await JsonSerializer.DeserializeAsync(stream, OutboundOAuthJsonContext.Default.AuthorizationServerMetadata, cancellationToken).ConfigureAwait(false);

                if (metadata is null)
                {
                    _logger.LogInformation("AS metadata parse failure at {Url}, continuing", url);

                    return null;
                }

                return metadata;
            }
            catch (JsonException)
            {
                _logger.LogInformation("AS metadata parse failure at {Url}, continuing", url);

                return null;
            }
        }

        #endregion

    }

}
