// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The GET-with-retry mechanics and pre-flight URL guards shared by every outbound OAuth well-known client:
    /// <see cref="ProtectedResourceMetadataClient"/> and <see cref="AuthorizationServerMetadataClient"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// OutboundDiscoveryHttp.EnsureDiscoveryUri(url);
    /// var client = httpClientFactory.CreateClient(ODataMcpAuthConstants.OAuthHttpClientName);
    /// using var response = await OutboundDiscoveryHttp.GetWithRetryAsync(client, url, "AS metadata", logger, cancellationToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>AUTH-14</c>: a 401, 403, 404, or any other non-success status below 500 is logged at Information and
    /// reported as a <see langword="null"/> response, never an exception; a timeout, transport failure, or 5xx is
    /// retried exactly once before becoming a fatal <see cref="OutboundDiscoveryException"/>. Response bodies are
    /// never logged.
    /// </remarks>
    internal static class OutboundDiscoveryHttp
    {

        #region Internal Methods

        /// <summary>
        /// Validates that a well-known discovery URL is safe to send a request to.
        /// </summary>
        /// <param name="url">The candidate discovery URL.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="url"/> is not an absolute URI, or does not satisfy
        /// <see cref="IsSecureOrLoopback(Uri)"/>.
        /// </exception>
        /// <remarks>
        /// Both product clients call this before creating an <see cref="HttpClient"/> or sending a request, so a
        /// relative or non-loopback <c>http</c> URL never reaches the network.
        /// </remarks>
        internal static void EnsureDiscoveryUri(Uri url)
        {
            ArgumentNullException.ThrowIfNull(url);

            if (!url.IsAbsoluteUri)
            {
                throw new ArgumentException($"The discovery URL must be absolute: {url}", nameof(url));
            }

            if (!IsSecureOrLoopback(url))
            {
                throw new ArgumentException($"Discovery is only allowed over https, or over http for loopback addresses: {url}", nameof(url));
            }
        }

        /// <summary>
        /// Sends a GET request for a well-known document, retrying exactly once on a timeout, transport failure,
        /// or 5xx status before that failure becomes fatal.
        /// </summary>
        /// <param name="client">The <c>"OAuth"</c> named client to send the request on.</param>
        /// <param name="url">The absolute well-known URL to GET.</param>
        /// <param name="logCategory">A short prefix identifying the caller in non-fatal log messages, such as <c>"PRM"</c> or <c>"AS metadata"</c>.</param>
        /// <param name="logger">The logger non-fatal outcomes are recorded to.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// The successful response, ready for the caller to read and dispose; or <see langword="null"/> for a
        /// non-retryable non-success status (401, 403, 404, or any other status below 500).
        /// </returns>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when the request times out, fails at the transport, or returns a 5xx status, after one retry.
        /// </exception>
        /// <remarks>
        /// The caller owns disposal of the returned response. A response that is not returned to the caller
        /// (every non-success path) is disposed here.
        /// </remarks>
        internal static Task<HttpResponseMessage?> GetWithRetryAsync(HttpClient client, Uri url, string logCategory, ILogger logger, CancellationToken cancellationToken)
        {
            return SendAsync(client, url, logCategory, logger, attempt: 1, cancellationToken);
        }

        /// <summary>
        /// Determines whether a URL is one this package is allowed to send a user, or a request, to.
        /// </summary>
        /// <param name="url">The candidate URL.</param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="url"/> is an absolute <c>https</c> URI, or an absolute
        /// <c>http</c> URI whose host is loopback; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// OutboundDiscoveryHttp.IsSecureOrLoopback(new Uri("https://login.example.com/authorize")); // true
        /// OutboundDiscoveryHttp.IsSecureOrLoopback(new Uri("http://127.0.0.1:5000/callback/"));     // true
        /// OutboundDiscoveryHttp.IsSecureOrLoopback(new Uri("file:///C:/Windows/System32/calc.exe"));// false
        /// </code>
        /// </example>
        /// <remarks>
        /// This is the single policy every server-controlled URL is measured against: the well-known documents
        /// this class fetches, the authorization endpoint a browser is sent to, and the consent URLs that reach
        /// <c>ShellExecute</c> or an MCP elicitation. A malicious resource can name any authorization server it
        /// likes, so a <c>file:</c>, <c>ms-</c>, or <c>vscode:</c> scheme must never survive this predicate.
        /// </remarks>
        internal static bool IsSecureOrLoopback(Uri url)
        {
            ArgumentNullException.ThrowIfNull(url);

            if (!url.IsAbsoluteUri)
            {
                return false;
            }

            if (string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && url.IsLoopback;
        }

        /// <summary>
        /// Sends one GET attempt and, for a timeout, transport failure, or 5xx, retries exactly once before
        /// throwing <see cref="OutboundDiscoveryException"/>.
        /// </summary>
        /// <param name="client">The <c>"OAuth"</c> named client to send the request on.</param>
        /// <param name="url">The absolute well-known URL to GET.</param>
        /// <param name="logCategory">A short prefix identifying the caller in non-fatal log messages.</param>
        /// <param name="logger">The logger non-fatal outcomes are recorded to.</param>
        /// <param name="attempt">The 1-based attempt number; attempt 2 is final and never retries again.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// The successful response, or <see langword="null"/> for a non-retryable non-success status.
        /// </returns>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown on the final attempt when the request times out, fails at the transport, or returns a 5xx status.
        /// </exception>
        internal static async Task<HttpResponseMessage?> SendAsync(HttpClient client, Uri url, string logCategory, ILogger logger, int attempt, CancellationToken cancellationToken)
        {
            var isFinalAttempt = attempt >= 2;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

                var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                using (response)
                {
                    var statusCode = (int)response.StatusCode;

                    if (statusCode is >= 500 and < 600)
                    {
                        if (isFinalAttempt)
                        {
                            throw new OutboundDiscoveryException(url, statusCode);
                        }
                    }
                    else
                    {
                        logger.LogInformation("{Category} {Status} at {Url}, continuing", logCategory, statusCode, url);

                        return null;
                    }
                }

                return await SendAsync(client, url, logCategory, logger, attempt + 1, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (isFinalAttempt)
                {
                    throw new OutboundDiscoveryException($"Discovery request to {url} failed.", ex);
                }

                return await SendAsync(client, url, logCategory, logger, attempt + 1, cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

    }

}
