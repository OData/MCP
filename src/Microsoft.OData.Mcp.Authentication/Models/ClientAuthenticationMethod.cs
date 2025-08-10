// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Defines the client authentication methods supported by OAuth2.
    /// </summary>
    public enum ClientAuthenticationMethod
    {

        /// <summary>
        /// No client authentication (public client).
        /// </summary>
        None,

        /// <summary>
        /// Client secret sent in the Authorization header using HTTP Basic authentication.
        /// </summary>
        ClientSecret,

        /// <summary>
        /// Client secret sent in the request body as a form parameter.
        /// </summary>
        ClientSecretPost,

        /// <summary>
        /// Client authentication using TLS client certificates.
        /// </summary>
        TlsClientAuth,

        /// <summary>
        /// Client authentication using self-signed TLS client certificates.
        /// </summary>
        SelfSignedTlsClientAuth,

        /// <summary>
        /// Client authentication using JWT signed with the client's internal key.
        /// </summary>
        PrivateKeyJwt,

        /// <summary>
        /// Client authentication using JWT signed with the client secret.
        /// </summary>
        ClientSecretJwt

    }

}
