// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Thrown when interactive sign-in is required to complete an outbound OAuth grant and
    /// <see cref="OutboundOAuthOptions.ConsentPresenter"/> is <see langword="null"/> — the mid-session path,
    /// where the caller (an MCP tool invocation) must elicit the operator instead of blocking on stderr.
    /// </summary>
    /// <example>
    /// <code>
    /// throw new OAuthConsentRequiredException(consentRequest);
    /// </code>
    /// </example>
    /// <remarks>
    /// This type lives in <c>Microsoft.OData.Mcp.Authentication.Outbound</c>, not Tools, so Authentication never
    /// references Tools. Per <c>AUTH-12</c> the wrapped <see cref="Request"/> never carries an access token, a
    /// refresh token, or a <c>device_code</c> — only what is needed to complete sign-in out of band. This type
    /// is not marked <c>[Serializable]</c>.
    /// </remarks>
    public class OAuthConsentRequiredException : Exception
    {

        #region Properties

        /// <summary>
        /// Gets <see cref="Request"/>'s <see cref="OutboundConsentRequest.ElicitationId"/>.
        /// </summary>
        public string ElicitationId => Request.ElicitationId;

        /// <summary>
        /// Gets <see cref="Request"/>'s <see cref="OutboundConsentRequest.Kind"/>.
        /// </summary>
        public OutboundGrantKind Kind => Request.Kind;

        /// <summary>
        /// Gets the consent request that must be completed before the interrupted call can succeed.
        /// </summary>
        public OutboundConsentRequest Request { get; }

        /// <summary>
        /// Gets <see cref="Request"/>'s <see cref="OutboundConsentRequest.Url"/>.
        /// </summary>
        public Uri Url => Request.Url;

        /// <summary>
        /// Gets <see cref="Request"/>'s <see cref="OutboundConsentRequest.UserCode"/>.
        /// </summary>
        public string? UserCode => Request.UserCode;

        /// <summary>
        /// Gets <see cref="Request"/>'s <see cref="OutboundConsentRequest.VerificationUri"/>.
        /// </summary>
        public Uri? VerificationUri => Request.VerificationUri;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthConsentRequiredException"/> class.
        /// </summary>
        /// <param name="request">The consent request that must be completed before the interrupted call can succeed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        public OAuthConsentRequiredException(OutboundConsentRequest request)
            : base(FormatMessage(request))
        {
            Request = request;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthConsentRequiredException"/> class with the
        /// exception that caused sign-in to become necessary, such as a failed token refresh.
        /// </summary>
        /// <param name="request">The consent request that must be completed before the interrupted call can succeed.</param>
        /// <param name="inner">The exception that caused sign-in to become necessary.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        public OAuthConsentRequiredException(OutboundConsentRequest request, Exception inner)
            : base(FormatMessage(request), inner)
        {
            Request = request;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds this exception's message and validates <paramref name="request"/>, before the base constructor
        /// runs, so the <c>: base(...)</c> call stays the single place the message is composed.
        /// </summary>
        /// <param name="request">The consent request that must be completed before the interrupted call can succeed.</param>
        /// <returns>
        /// A message of the form <c>Interactive sign-in is required: {request.Message}</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        internal static string FormatMessage(OutboundConsentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return $"Interactive sign-in is required: {request.Message}";
        }

        #endregion

    }

}
