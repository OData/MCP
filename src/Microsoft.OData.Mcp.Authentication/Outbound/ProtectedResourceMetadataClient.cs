// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Fetches an RFC 9728 protected resource metadata document over the handler-free <c>"OAuth"</c> named
    /// <see cref="HttpClient"/>, treating every well-known failure per <c>AUTH-14</c>: 401, 403, 404, and a
    /// failed parse are logged and swallowed; a timeout or 5xx is retried once before it becomes fatal.
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new ProtectedResourceMetadataClient(httpClientFactory, logger);
    /// var metadata = await client.TryGetAsync(new Uri("https://api.example.com/.well-known/oauth-protected-resource"), cancellationToken);
    /// if (metadata is not null &amp;&amp; metadata.AuthorizationServers?.Count > 0)
    /// {
    ///     // Continue discovery against metadata.AuthorizationServers[0].
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// A new <c>"OAuth"</c> client is requested from <see cref="IHttpClientFactory"/> on every call rather than
    /// cached, matching the "Two named HttpClients" table in <c>specs/v3/AUTHENTICATION.md</c>: this client
    /// carries no delegating handler and is safe to create per request. Response bodies are never logged.
    /// </remarks>
    public sealed class ProtectedResourceMetadataClient
    {

        #region Fields

        /// <summary>
        /// The factory <see cref="TryGetAsync(Uri, CancellationToken)"/> asks for the <c>"OAuth"</c> named client.
        /// </summary>
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// The logger this instance records non-fatal well-known outcomes to.
        /// </summary>
        private readonly ILogger<ProtectedResourceMetadataClient> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectedResourceMetadataClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The factory used to create the <c>"OAuth"</c> named client per call.</param>
        /// <param name="logger">The logger this instance records non-fatal well-known outcomes to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        public ProtectedResourceMetadataClient(IHttpClientFactory httpClientFactory, ILogger<ProtectedResourceMetadataClient> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to fetch and parse the RFC 9728 protected resource metadata document at a well-known URL.
        /// </summary>
        /// <param name="url">The absolute protected resource metadata URL.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// The parsed document on a <c>200</c> response with a well-formed body; otherwise <see langword="null"/>
        /// after a 401, 403, 404, unparseable body, or any other non-retryable non-success status.
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
        /// var metadata = await client.TryGetAsync(server.ProtectedResourceMetadataUri, cancellationToken);
        /// </code>
        /// </example>
        public async Task<SdkAuth.ProtectedResourceMetadata?> TryGetAsync(Uri url, CancellationToken cancellationToken)
        {
            OutboundDiscoveryHttp.EnsureDiscoveryUri(url);

            var client = _httpClientFactory.CreateClient(ODataMcpAuthConstants.OAuthHttpClientName);

            using var response = await OutboundDiscoveryHttp.GetWithRetryAsync(client, url, "PRM", _logger, cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                return null;
            }

            return await ParseAsync(response, url, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses a successful response body as protected resource metadata.
        /// </summary>
        /// <param name="response">The successful response.</param>
        /// <param name="url">The URL that was requested, for logging only.</param>
        /// <param name="cancellationToken">The token that cancels the read.</param>
        /// <returns>
        /// The parsed document, or <see langword="null"/> when the body is not well-formed JSON, or parses to
        /// <see langword="null"/>.
        /// </returns>
        internal async Task<SdkAuth.ProtectedResourceMetadata?> ParseAsync(HttpResponseMessage response, Uri url, CancellationToken cancellationToken)
        {
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var metadata = await JsonSerializer.DeserializeAsync(stream, OutboundOAuthJsonContext.Default.ProtectedResourceMetadata, cancellationToken).ConfigureAwait(false);

                if (metadata is null)
                {
                    _logger.LogInformation("PRM parse failure at {Url}, continuing", url);

                    return null;
                }

                return metadata;
            }
            catch (JsonException)
            {
                _logger.LogInformation("PRM parse failure at {Url}, continuing", url);

                return null;
            }
        }

        #endregion

    }

}
