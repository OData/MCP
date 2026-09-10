// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The OAuth 2.0 grant kinds outbound OAuth can select for a remote OData service.
    /// </summary>
    /// <remarks>
    /// Per <c>AUTH-5</c> this is a top-level type; wire DTOs and grant-kind enums are never nested inside
    /// another type. <see cref="RefreshToken"/> is not one of the four values <c>--grant</c> accepts on the
    /// command line (see <see cref="OutboundGrantKindParser"/>) — it is only ever selected internally, once the
    /// outbound auth handler already holds a cached refresh token.
    /// </remarks>
    public enum OutboundGrantKind
    {

        /// <summary>
        /// RFC 8628 device authorization grant, for input-constrained devices and interactive CLI sign-in.
        /// </summary>
        DeviceCode,

        /// <summary>
        /// RFC 6749 authorization code grant with RFC 7636 PKCE, completed through a loopback redirect.
        /// </summary>
        AuthorizationCode,

        /// <summary>
        /// RFC 6749 client credentials grant, for a confidential client acting as itself.
        /// </summary>
        ClientCredentials,

        /// <summary>
        /// SDK identity assertion grant (RFC 7523 JWT bearer), exchanging an enterprise IdP id token for an
        /// access token.
        /// </summary>
        IdentityAssertion,

        /// <summary>
        /// RFC 6749 refresh token grant, used internally to renew an access token without re-prompting.
        /// </summary>
        RefreshToken

    }

}
