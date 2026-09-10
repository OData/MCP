// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Controls how <see cref="LocalAuthorizationServer"/> answers RFC 9728 protected resource metadata requests.
    /// </summary>
    /// <remarks>
    /// Discovery clients must cope with all three shapes: a well-formed document, an authorization server that
    /// challenges the metadata request itself (the Microsoft Graph behavior), and a resource that publishes nothing.
    /// </remarks>
    public enum ProtectedResourceMetadataMode
    {

        /// <summary>
        /// Returns <c>200 OK</c> with a well-formed RFC 9728 document.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// Returns <c>401 Unauthorized</c> with a <c>WWW-Authenticate</c> challenge carrying <c>authorization_uri</c> and <c>client_id</c>.
        /// </summary>
        Unauthorized = 1,

        /// <summary>
        /// Returns <c>404 Not Found</c> with no body.
        /// </summary>
        NotFound = 2

    }

}
