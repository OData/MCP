// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The single exception type an outbound token, device authorization, or polling request throws when the
    /// authorization server answers with an RFC 6749 section 5.2 error payload, or when the request never
    /// reached one.
    /// </summary>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     var response = await tokenEndpointClient.PostAsync(tokenEndpoint, form, clientId, clientSecret, authMethod, cancellationToken);
    /// }
    /// catch (OAuthTokenException ex) when (ex.Error == ODataMcpAuthConstants.ErrorAuthorizationPending)
    /// {
    ///     // Keep polling.
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>AUTH-12</c> no member of this type ever carries an access token, refresh token, device code, or
    /// client secret: <see cref="Error"/> and <see cref="ErrorDescription"/> are copied straight from the
    /// server's error payload, and the composed <see cref="Exception.Message"/> repeats only those two values
    /// and the HTTP status. RFC 8628 device code polling reuses this type for its <c>authorization_pending</c>,
    /// <c>slow_down</c>, <c>expired_token</c>, and <c>access_denied</c> answers, which is why
    /// <see cref="Error"/> is exposed as a value a caller can branch on rather than only formatted into the
    /// message.
    /// </remarks>
    public class OAuthTokenException : Exception
    {

        #region Properties

        /// <summary>
        /// Gets the RFC 6749 error code the authorization server returned, such as <c>invalid_grant</c>, or
        /// <c>transport_error</c> when the request never reached a server.
        /// </summary>
        /// <value>
        /// <see cref="string.Empty"/> when this instance was constructed with only a message and an inner
        /// exception.
        /// </value>
        public string Error { get; }

        /// <summary>
        /// Gets the human readable <c>error_description</c> the authorization server returned, or
        /// <see langword="null"/> when it supplied none.
        /// </summary>
        public string? ErrorDescription { get; }

        /// <summary>
        /// Gets the HTTP status code the failing request returned.
        /// </summary>
        /// <value>
        /// <c>0</c> when the request never produced a response, such as a transport failure.
        /// </value>
        public int StatusCode { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthTokenException"/> class for an error payload the
        /// authorization server returned.
        /// </summary>
        /// <param name="error">The RFC 6749 error code, such as <c>invalid_grant</c>.</param>
        /// <param name="errorDescription">The human readable description, or <see langword="null"/> when the server supplied none.</param>
        /// <param name="statusCode">The HTTP status code the request failed with, or <c>0</c> when there was no response.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> is <see langword="null"/>, empty, or whitespace.</exception>
        public OAuthTokenException(string error, string? errorDescription, int statusCode)
            : base(FormatMessage(error, errorDescription, statusCode))
        {
            Error = error;
            ErrorDescription = errorDescription;
            StatusCode = statusCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthTokenException"/> class for an error payload the
        /// authorization server returned, or a transport failure, that another exception caused.
        /// </summary>
        /// <param name="error">The RFC 6749 error code, or <c>transport_error</c> for a request that never reached a server.</param>
        /// <param name="errorDescription">The human readable description, or <see langword="null"/> when there is none.</param>
        /// <param name="statusCode">The HTTP status code the request failed with, or <c>0</c> when there was no response.</param>
        /// <param name="inner">The exception that caused this failure.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> is <see langword="null"/>, empty, or whitespace.</exception>
        public OAuthTokenException(string error, string? errorDescription, int statusCode, Exception inner)
            : base(FormatMessage(error, errorDescription, statusCode), inner)
        {
            Error = error;
            ErrorDescription = errorDescription;
            StatusCode = statusCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthTokenException"/> class with a message and the
        /// exception that caused this failure.
        /// </summary>
        /// <param name="message">The error message. Must never carry a token, device code, or client secret.</param>
        /// <param name="inner">The exception that caused this failure.</param>
        /// <remarks>
        /// <see cref="Error"/> reads as <see cref="string.Empty"/> and <see cref="StatusCode"/> as <c>0</c> for
        /// an instance built this way, because neither an error code nor a response status is known.
        /// </remarks>
        public OAuthTokenException(string message, Exception inner)
            : base(message, inner)
        {
            Error = string.Empty;
            StatusCode = 0;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the message the error payload constructors pass to the base constructor.
        /// </summary>
        /// <param name="error">The RFC 6749 error code.</param>
        /// <param name="errorDescription">The human readable description, or <see langword="null"/> when there is none.</param>
        /// <param name="statusCode">The HTTP status code the request failed with.</param>
        /// <returns>
        /// <c>Token endpoint returned {statusCode}: {error}</c>, with <c>: {errorDescription}</c> appended when
        /// a description is present.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <remarks>
        /// Validating <paramref name="error"/> here, before the base constructor runs, keeps the
        /// <c>: base(...)</c> call the single place the message is composed.
        /// </remarks>
        internal static string FormatMessage(string error, string? errorDescription, int statusCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);

            var message = $"Token endpoint returned {statusCode}: {error}";

            return string.IsNullOrWhiteSpace(errorDescription) ? message : $"{message}: {errorDescription}";
        }

        #endregion

    }

}
