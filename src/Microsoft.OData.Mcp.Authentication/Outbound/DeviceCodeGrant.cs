// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Runs the RFC 8628 device authorization grant: start the grant at the device authorization endpoint, then
    /// poll the token endpoint honoring <c>interval</c>, <c>slow_down</c>, and <c>authorization_pending</c>
    /// until a token is issued, the code expires, or the end user denies it.
    /// </summary>
    /// <example>
    /// <code>
    /// var device = await deviceCodeGrant.StartAsync(metadata, clientId, "read offline_access", resource, cancellationToken);
    ///
    /// Console.Error.WriteLine($"Enter {device.UserCode} at {device.VerificationUri}");
    ///
    /// var response = await deviceCodeGrant.PollAsync(metadata, device, clientId, resource, cancellationToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>specs/v3/AUTHENTICATION.md</c> "Grants → Device code (RFC 8628)" the
    /// <c>device_authorization_endpoint</c> from metadata is always preferred; <c>{issuer}/devicecode</c> is a
    /// last-resort fallback, logged at Warning, used only when the server advertises the device code grant but
    /// publishes no endpoint for it. Per <c>AUTH-12</c> the device code, user code, and every issued token stay
    /// out of the log: poll entries carry only the attempt number and the RFC error code. The device
    /// authorization request goes out on the <c>"OAuth"</c> named client directly; every token POST goes
    /// through <see cref="TokenEndpointClient"/> so that all token traffic in this package shares one code path.
    /// </remarks>
    public sealed class DeviceCodeGrant
    {

        #region Fields

        /// <summary>
        /// The RFC 8628 default poll interval, in seconds, used when the server advertises none.
        /// </summary>
        public const int DefaultIntervalSeconds = 5;

        /// <summary>
        /// The path segment appended to the issuer when a server advertises the device code grant but publishes
        /// no <c>device_authorization_endpoint</c>.
        /// </summary>
        public const string DeviceAuthorizationFallbackSegment = "/devicecode";

        /// <summary>
        /// The number of seconds a <c>slow_down</c> answer adds to the poll interval, per RFC 8628 section 3.5.
        /// </summary>
        public const int SlowDownIncrementSeconds = 5;

        /// <summary>
        /// The factory <see cref="StartAsync(AuthorizationServerMetadata, string, string?, Uri?, CancellationToken)"/>
        /// asks for the <c>"OAuth"</c> named client.
        /// </summary>
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// The logger this instance records the endpoint fallback and each poll outcome to.
        /// </summary>
        private readonly ILogger<DeviceCodeGrant> _logger;

        /// <summary>
        /// The client every token POST this grant sends goes through.
        /// </summary>
        private readonly TokenEndpointClient _tokenEndpointClient;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceCodeGrant"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The factory used to create the <c>"OAuth"</c> named client for the device authorization request.</param>
        /// <param name="tokenEndpointClient">The client every token POST this grant sends goes through.</param>
        /// <param name="logger">The logger this instance records the endpoint fallback and each poll outcome to.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="httpClientFactory"/>, <paramref name="tokenEndpointClient"/>, or
        /// <paramref name="logger"/> is <see langword="null"/>.
        /// </exception>
        public DeviceCodeGrant(IHttpClientFactory httpClientFactory, TokenEndpointClient tokenEndpointClient, ILogger<DeviceCodeGrant> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(tokenEndpointClient);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _tokenEndpointClient = tokenEndpointClient;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Polls the token endpoint until the end user completes the grant, the device code expires, or the
        /// request is cancelled.
        /// </summary>
        /// <param name="metadata">The authorization server metadata whose <c>token_endpoint</c> is polled.</param>
        /// <param name="device">The device authorization response <see cref="StartAsync(AuthorizationServerMetadata, string, string?, Uri?, CancellationToken)"/> returned.</param>
        /// <param name="clientId">The public client identifier the grant was started with.</param>
        /// <param name="resource">The RFC 8707 resource indicator, sent exactly as it was published or configured, or <see langword="null"/> when the grant sends none.</param>
        /// <param name="cancellationToken">The token that cancels both the poll requests and the waits between them.</param>
        /// <returns>
        /// The successful token response.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> or <paramref name="device"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="clientId"/> is <see langword="null"/>, empty, or whitespace, when
        /// <paramref name="device"/> carries no device code, or when it carries no positive <c>expires_in</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="metadata"/> carries no token endpoint.</exception>
        /// <exception cref="OAuthTokenException">
        /// Thrown with <see cref="ODataMcpAuthConstants.ErrorExpiredToken"/> when the device code's lifetime
        /// runs out, with <see cref="ODataMcpAuthConstants.ErrorAccessDenied"/> when the end user refuses, and
        /// with whatever the server returned for any other error.
        /// </exception>
        /// <example>
        /// <code>
        /// using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        /// timeout.CancelAfter(TimeSpan.FromSeconds(300));
        ///
        /// var response = await deviceCodeGrant.PollAsync(metadata, device, clientId, resource, timeout.Token);
        /// var container = response.ToTokenContainer(null, clientId, null, metadata.Issuer!, TokenEndpointClient.AuthMethodNone);
        /// </code>
        /// </example>
        /// <remarks>
        /// The first wait is <see cref="DeviceAuthorizationResponse.Interval"/> seconds, floored at one second so
        /// a server that advertises zero cannot turn this into a hot loop; each <c>slow_down</c> adds
        /// <see cref="SlowDownIncrementSeconds"/> seconds for the rest of the grant, per RFC 8628 section 3.5.
        /// The deadline is computed once from <see cref="DeviceAuthorizationResponse.ExpiresIn"/> at the first
        /// poll, so a slow server cannot extend the code's life.
        /// </remarks>
        public async Task<TokenEndpointResponse> PollAsync(AuthorizationServerMetadata metadata, DeviceAuthorizationResponse device, string clientId, Uri? resource, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(device);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

            if (string.IsNullOrWhiteSpace(device.DeviceCode))
            {
                throw new ArgumentException("The device authorization response carries no device code.", nameof(device));
            }

            if (device.ExpiresIn <= 0)
            {
                throw new ArgumentException("The device authorization response carries no expires_in.", nameof(device));
            }

            if (metadata.TokenEndpoint is null)
            {
                throw new InvalidOperationException("The authorization server metadata carries no token endpoint.");
            }

            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.DeviceCodeGrantType,
                [ODataMcpAuthConstants.DeviceCodeParameter] = device.DeviceCode
            };

            if (resource is not null)
            {
                form[ODataMcpAuthConstants.ResourceParameter] = resource.OriginalString;
            }

            var deadline = DateTimeOffset.UtcNow.AddSeconds(device.ExpiresIn);
            var interval = Math.Max(1, device.Interval ?? DefaultIntervalSeconds);
            var attempt = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new OAuthTokenException(ODataMcpAuthConstants.ErrorExpiredToken, "Device code expired before authorization completed.", 0);
                }

                attempt++;

                try
                {
                    return await _tokenEndpointClient
                        .PostAsync(metadata.TokenEndpoint, form, clientId, null, TokenEndpointClient.AuthMethodNone, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OAuthTokenException ex) when (ex.Error is ODataMcpAuthConstants.ErrorAuthorizationPending or ODataMcpAuthConstants.ErrorSlowDown)
                {
                    _logger.LogDebug("Device code poll {Attempt}: {Error}", attempt, ex.Error);

                    interval = NextInterval(interval, ex.Error);

                    await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Starts a device authorization grant and returns the codes the end user needs.
        /// </summary>
        /// <param name="metadata">The authorization server metadata whose device authorization endpoint is used.</param>
        /// <param name="clientId">The public client identifier the grant is started for.</param>
        /// <param name="scope">The space-delimited scope to request, or <see langword="null"/> to request none.</param>
        /// <param name="resource">The RFC 8707 resource indicator, sent exactly as it was published or configured, or <see langword="null"/> when the grant sends none.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// The device authorization response, whose device code, user code, and verification URI are guaranteed
        /// non-empty.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when <paramref name="metadata"/> publishes no device authorization endpoint and does not
        /// advertise the device code grant either.
        /// </exception>
        /// <exception cref="OAuthTokenException">
        /// Thrown when the server answers with a non-success status, when the response cannot be parsed, or
        /// when it omits the device code, user code, or verification URI.
        /// </exception>
        /// <remarks>
        /// <paramref name="scope"/> and <paramref name="resource"/> are sent only when supplied, so a server
        /// that rejects an empty <c>scope</c> or an unexpected <c>resource</c> never sees either.
        /// </remarks>
        public async Task<DeviceAuthorizationResponse> StartAsync(AuthorizationServerMetadata metadata, string clientId, string? scope, Uri? resource, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

            var endpoint = ResolveDeviceAuthorizationEndpoint(metadata);

            OutboundDiscoveryHttp.EnsureDiscoveryUri(endpoint);

            var fields = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.ClientIdParameter] = clientId
            };

            if (!string.IsNullOrWhiteSpace(scope))
            {
                fields[ODataMcpAuthConstants.ScopeParameter] = scope;
            }

            if (resource is not null)
            {
                fields[ODataMcpAuthConstants.ResourceParameter] = resource.OriginalString;
            }

            _logger.LogDebug("Device authorization request to {Endpoint}", endpoint);

            using var content = new FormUrlEncodedContent(fields);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

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
                    throw TokenEndpointClient.CreateErrorException(body, statusCode);
                }

                return ParseDeviceAuthorization(body, statusCode);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Computes the poll interval to wait before the next attempt.
        /// </summary>
        /// <param name="currentSeconds">The interval in force for the attempt that just failed.</param>
        /// <param name="error">The RFC 8628 error code the attempt returned.</param>
        /// <returns>
        /// <paramref name="currentSeconds"/> plus <see cref="SlowDownIncrementSeconds"/> for
        /// <see cref="ODataMcpAuthConstants.ErrorSlowDown"/>; otherwise <paramref name="currentSeconds"/>. The
        /// result is never below one second.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <remarks>
        /// The one second floor applies before the increment, so a server advertising <c>interval: 0</c> and
        /// then <c>slow_down</c> lands on six seconds rather than five, and a client that ignores the floor can
        /// never busy-poll a token endpoint.
        /// </remarks>
        internal static int NextInterval(int currentSeconds, string error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);

            var floored = currentSeconds < 1 ? 1 : currentSeconds;

            return string.Equals(error, ODataMcpAuthConstants.ErrorSlowDown, StringComparison.Ordinal)
                ? floored + SlowDownIncrementSeconds
                : floored;
        }

        /// <summary>
        /// Parses a device authorization response body and proves it carries the codes the end user needs.
        /// </summary>
        /// <param name="body">The success response body.</param>
        /// <param name="statusCode">The HTTP status code the response carried, reported on failure.</param>
        /// <returns>
        /// The parsed response.
        /// </returns>
        /// <exception cref="OAuthTokenException">
        /// Thrown when the body cannot be parsed, deserializes to <see langword="null"/>, or omits
        /// <c>device_code</c>, <c>user_code</c>, or <c>verification_uri</c>.
        /// </exception>
        internal static DeviceAuthorizationResponse ParseDeviceAuthorization(string body, int statusCode)
        {
            DeviceAuthorizationResponse? parsed;

            try
            {
                parsed = string.IsNullOrWhiteSpace(body)
                    ? null
                    : JsonSerializer.Deserialize(body, OutboundOAuthJsonContext.Default.DeviceAuthorizationResponse);
            }
            catch (JsonException ex)
            {
                throw new OAuthTokenException("invalid_response", "Device authorization endpoint returned a body that is not a device authorization response.", statusCode, ex);
            }

            if (parsed is null
                || string.IsNullOrWhiteSpace(parsed.DeviceCode)
                || string.IsNullOrWhiteSpace(parsed.UserCode)
                || string.IsNullOrWhiteSpace(parsed.VerificationUri))
            {
                throw new OAuthTokenException("invalid_response", "Device authorization endpoint returned no device_code, user_code, or verification_uri.", statusCode);
            }

            return parsed;
        }

        /// <summary>
        /// Selects the device authorization endpoint to POST to.
        /// </summary>
        /// <param name="metadata">The authorization server metadata to read.</param>
        /// <returns>
        /// <see cref="AuthorizationServerMetadata.DeviceAuthorizationEndpoint"/> when it is published; otherwise
        /// <c>{issuer}/devicecode</c> when the server advertises the device code grant.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is <see langword="null"/>.</exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when no endpoint is published and either the grant is not advertised or there is no issuer to
        /// derive a fallback from.
        /// </exception>
        /// <remarks>
        /// The fallback is logged at Warning because guessing an endpoint is a documented last resort, not
        /// normal operation: an operator seeing that line knows their authorization server's metadata is
        /// incomplete.
        /// </remarks>
        internal Uri ResolveDeviceAuthorizationEndpoint(AuthorizationServerMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            if (metadata.DeviceAuthorizationEndpoint is not null)
            {
                return metadata.DeviceAuthorizationEndpoint;
            }

            if (!string.IsNullOrWhiteSpace(metadata.Issuer) && metadata.AdvertisesGrant(ODataMcpAuthConstants.DeviceCodeGrantType))
            {
                var fallback = new Uri($"{metadata.Issuer.TrimEnd('/')}{DeviceAuthorizationFallbackSegment}");

                _logger.LogWarning(
                    "Authorization server {Issuer} advertises the device code grant but publishes no device_authorization_endpoint; falling back to {Endpoint}",
                    metadata.Issuer,
                    fallback);

                return fallback;
            }

            throw new NotSupportedException("Authorization server does not advertise a device authorization endpoint.");
        }

        #endregion

    }

}
