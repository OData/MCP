// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Defines the authentication types for OData services.
    /// </summary>
    public enum ODataAuthenticationType
    {

        /// <summary>
        /// No authentication required.
        /// </summary>
        None,

        /// <summary>
        /// API key authentication using a custom header.
        /// </summary>
        ApiKey,

        /// <summary>
        /// Bearer token authentication using the Authorization header.
        /// </summary>
        Bearer,

        /// <summary>
        /// Basic authentication using username and password.
        /// </summary>
        Basic,

        /// <summary>
        /// OAuth2 client credentials flow.
        /// </summary>
        OAuth2

    }
}
