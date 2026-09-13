// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The single exception type outbound discovery throws when a well-known request fails in a way that
    /// <c>AUTH-14</c> does not treat as a non-fatal fallback: a timeout, a transport failure, or a repeated
    /// server error after the one retry discovery allows.
    /// </summary>
    /// <example>
    /// <code>
    /// throw new OutboundDiscoveryException(url, (int)response.StatusCode);
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>specs/v3/AUTHENTICATION.md</c> AUTH-14, well-known 401/403/404 and a failed parse are not fatal
    /// and never throw this type; only a timeout or a repeated 5xx after retrying once does. Every discovery
    /// failure in this package throws this exception, so a caller can catch one type regardless of which
    /// well-known request failed.
    /// </remarks>
    public class OutboundDiscoveryException : Exception
    {

        #region Properties

        /// <summary>
        /// Gets the HTTP status code the failing request returned, or <see langword="null"/> when the failure
        /// was a transport exception with no response.
        /// </summary>
        public int? StatusCode { get; }

        /// <summary>
        /// Gets the well-known URL the failing request was sent to, or <see langword="null"/> when this
        /// instance was constructed with only a message.
        /// </summary>
        public Uri? Url { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundDiscoveryException"/> class with a message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public OutboundDiscoveryException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundDiscoveryException"/> class with a message and
        /// the exception that caused this failure.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="inner">The exception that caused this failure.</param>
        public OutboundDiscoveryException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundDiscoveryException"/> class for a well-known
        /// request that failed with an HTTP status code.
        /// </summary>
        /// <param name="url">The well-known URL that was requested.</param>
        /// <param name="statusCode">The HTTP status code the request failed with.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
        public OutboundDiscoveryException(Uri url, int statusCode)
            : base(FormatStatusMessage(url, statusCode))
        {
            Url = url;
            StatusCode = statusCode;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the message for the <see cref="OutboundDiscoveryException(Uri, int)"/> constructor.
        /// </summary>
        /// <param name="url">The well-known URL that was requested.</param>
        /// <param name="statusCode">The HTTP status code the request failed with.</param>
        /// <returns>
        /// A message of the form <c>Discovery request to {url} failed with HTTP {statusCode}.</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Validating <paramref name="url"/> here, before the base constructor runs, keeps the base
        /// <c>: base(...)</c> call the single place the message is composed.
        /// </remarks>
        internal static string FormatStatusMessage(Uri url, int statusCode)
        {
            ArgumentNullException.ThrowIfNull(url);

            return $"Discovery request to {url} failed with HTTP {statusCode}.";
        }

        #endregion

    }

}
