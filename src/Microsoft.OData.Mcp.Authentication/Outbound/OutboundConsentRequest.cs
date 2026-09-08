// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The consent-request payload passed to <see cref="OutboundOAuthOptions.ConsentPresenter"/>, and carried by
    /// <see cref="OAuthConsentRequiredException"/>, when interactive sign-in is required to complete an outbound
    /// OAuth grant.
    /// </summary>
    /// <example>
    /// <code>
    /// var request = new OutboundConsentRequest(
    ///     OutboundGrantKind.DeviceCode,
    ///     new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/devicecode"),
    ///     "Sign in to continue.",
    ///     userCode: "ABCD-EFGH",
    ///     verificationUri: new Uri("https://microsoft.com/devicelogin"));
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>AUTH-12</c> this type never carries a <c>device_code</c>, an access token, a refresh token, or a
    /// PKCE <c>code_verifier</c> — only what a human or an MCP client needs to complete sign-in out of band.
    /// Fields that must stay off this type (the pending grant's <c>device_code</c>, <c>interval</c>,
    /// <c>code_verifier</c>, expected <c>state</c>, and live <c>HttpListener</c>) live instead on
    /// <c>OutboundOAuthClient</c>'s internal pending-grant state.
    /// <para>
    /// Every URL on this type is server-controlled: it comes from an authorization server that a malicious
    /// protected resource could have named. Because a consent URL is handed to <c>ShellExecute</c> by the stdio
    /// presenter and to an MCP client as an elicitation URL, the constructor is the chokepoint that refuses any
    /// scheme other than <c>https</c>, or <c>http</c> on a loopback host, before either can happen.
    /// </para>
    /// </remarks>
    public sealed class OutboundConsentRequest
    {

        #region Properties

        /// <summary>
        /// Gets the identifier this consent request correlates an MCP elicitation to.
        /// </summary>
        public string ElicitationId { get; }

        /// <summary>
        /// Gets the grant kind this consent request is completing.
        /// </summary>
        public OutboundGrantKind Kind { get; }

        /// <summary>
        /// Gets the human-readable message describing what the operator or the LLM should do.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the absolute URL to open to complete sign-in: an authorization URI for interactive grants, or a
        /// device verification URI for the device code grant.
        /// </summary>
        public Uri Url { get; }

        /// <summary>
        /// Gets the short code a human types at <see cref="VerificationUri"/>, or <see langword="null"/> when
        /// the grant kind does not use one.
        /// </summary>
        public string? UserCode { get; }

        /// <summary>
        /// Gets the verification URI a human is directed to enter <see cref="UserCode"/> at, or
        /// <see langword="null"/> when the grant kind does not use one.
        /// </summary>
        public Uri? VerificationUri { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundConsentRequest"/> class.
        /// </summary>
        /// <param name="kind">The grant kind this consent request is completing.</param>
        /// <param name="url">The absolute URL to open to complete sign-in.</param>
        /// <param name="message">The human-readable message describing what to do.</param>
        /// <param name="userCode">The short code a human types at <paramref name="verificationUri"/>, or <see langword="null"/>.</param>
        /// <param name="verificationUri">The verification URI a human is directed to, or <see langword="null"/>.</param>
        /// <param name="elicitationId">The correlation identifier, or <see langword="null"/> to generate a new <c>D</c>-format GUID.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="url"/> is not an absolute URI, when <paramref name="message"/> is empty or
        /// white space, or when <paramref name="elicitationId"/> is non-<see langword="null"/> and empty or white space.
        /// </exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when <paramref name="url"/> or <paramref name="verificationUri"/> is neither an <c>https</c>
        /// URI nor an <c>http</c> URI on a loopback host.
        /// </exception>
        /// <remarks>
        /// The scheme check runs on both URLs and it runs here rather than at each presenter, because there is
        /// no presenter this instance is guaranteed to reach: it is also carried by
        /// <see cref="OAuthConsentRequiredException"/> to whatever host catches it.
        /// </remarks>
        public OutboundConsentRequest(OutboundGrantKind kind, Uri url, string message, string? userCode, Uri? verificationUri, string? elicitationId = null)
        {
            ArgumentNullException.ThrowIfNull(url);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            if (!url.IsAbsoluteUri)
            {
                throw new ArgumentException("The consent URL must be absolute.", nameof(url));
            }

            if (elicitationId is not null && string.IsNullOrWhiteSpace(elicitationId))
            {
                throw new ArgumentException("The elicitation id must not be empty or white space.", nameof(elicitationId));
            }

            EnsurePresentableUrl(url);

            if (verificationUri is not null)
            {
                EnsurePresentableUrl(verificationUri);
            }

            ElicitationId = elicitationId ?? Guid.NewGuid().ToString("D");
            Kind = kind;
            Message = message;
            Url = url;
            UserCode = userCode;
            VerificationUri = verificationUri;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Refuses a URL that must never be opened on the operator's behalf.
        /// </summary>
        /// <param name="url">The absolute URL a human or an MCP client would be sent to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when <paramref name="url"/> is neither an <c>https</c> URI nor an <c>http</c> URI on a
        /// loopback host.
        /// </exception>
        /// <remarks>
        /// The policy is <see cref="OutboundDiscoveryHttp.IsSecureOrLoopback(Uri)"/>, the same one every
        /// well-known request is measured against, so a <c>file:</c>, <c>ms-settings:</c>, or <c>vscode:</c>
        /// URL an attacker-controlled authorization server returned cannot reach <c>ShellExecute</c>. The
        /// message names only the scheme; the URL itself may carry a <c>user_code</c> and is not repeated.
        /// </remarks>
        internal static void EnsurePresentableUrl(Uri url)
        {
            ArgumentNullException.ThrowIfNull(url);

            if (!OutboundDiscoveryHttp.IsSecureOrLoopback(url))
            {
                throw new OutboundDiscoveryException($"Consent URL must use https (or http on loopback): {(url.IsAbsoluteUri ? url.Scheme : "relative URL")}");
            }
        }

        #endregion

    }

}
