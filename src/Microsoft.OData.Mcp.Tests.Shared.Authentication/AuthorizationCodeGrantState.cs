// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Tracks a single RFC 6749 authorization code grant between the authorize redirect and the token exchange.
    /// </summary>
    /// <remarks>
    /// The code is consumed by the first successful token exchange, so a replayed code fails with <c>invalid_grant</c>.
    /// </remarks>
    public sealed class AuthorizationCodeGrantState
    {

        #region Properties

        /// <summary>
        /// Gets or sets the client identifier that requested the code.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the opaque authorization code handed back on the redirect.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the RFC 7636 <c>S256</c> code challenge the token exchange must satisfy.
        /// </summary>
        public string CodeChallenge { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the redirect URI the code was issued for.
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the RFC 8707 <c>resource</c> indicator the authorize request carried, if any.
        /// </summary>
        public string Resource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the space-delimited scope the client requested.
        /// </summary>
        public string Scope { get; set; } = string.Empty;

        #endregion

    }

}
