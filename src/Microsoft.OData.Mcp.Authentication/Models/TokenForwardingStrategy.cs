// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Defines the strategies for forwarding tokens to downstream services.
    /// </summary>
    public enum TokenForwardingStrategy
    {

        /// <summary>
        /// Forward the original token without modification.
        /// </summary>
        PassThrough,

        /// <summary>
        /// Exchange the token for a new one using OAuth2 token exchange.
        /// </summary>
        Exchange,

        /// <summary>
        /// Use OAuth2 on-behalf-of flow to obtain a token for the downstream service.
        /// </summary>
        OnBehalfOf

    }

}
