// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// The authentication paths the <c>odata-mcp add</c> wizard offers an operator.
    /// </summary>
    /// <remarks>
    /// Per <c>AUTH-5</c> this is a top-level type. The order is the order the menu presents, and it is
    /// deliberate: the four OAuth paths come before the two credential-in-config paths, which come before the
    /// escape hatch, because the warning text the wizard prints for the last three refers to "OAuth (1–4)".
    /// </remarks>
    public enum AddAuthChoice
    {

        /// <summary>
        /// The service needs no credential at all.
        /// </summary>
        None,

        /// <summary>
        /// Probe the service's <c>$metadata</c>, read the challenge, run OAuth discovery, and recommend a grant.
        /// </summary>
        Discover,

        /// <summary>
        /// RFC 8628 device authorization grant.
        /// </summary>
        DeviceCode,

        /// <summary>
        /// RFC 6749 authorization code grant with PKCE over a loopback redirect.
        /// </summary>
        AuthorizationCode,

        /// <summary>
        /// RFC 6749 client credentials grant, for a daemon acting as itself.
        /// </summary>
        ClientCredentials,

        /// <summary>
        /// A raw API key sent in an operator-named header.
        /// </summary>
        ApiKey,

        /// <summary>
        /// HTTP Basic authentication.
        /// </summary>
        Basic,

        /// <summary>
        /// A bearer token the operator already holds, written into the generated command.
        /// </summary>
        PasteToken

    }

}
