// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// What one send-path token lookup produced: the token to attach, if any, and whether obtaining it cost a
    /// refresh.
    /// </summary>
    /// <remarks>
    /// The flag exists so <see cref="ODataOutboundAuthHandler"/> can tell a token it just refreshed from one it
    /// read straight out of the cache. Without it, a token refreshed inside
    /// <see cref="ODataMcpAuthConstants.TokenRefreshSkew"/> that the resource then rejects with
    /// <c>invalid_token</c> would draw a second token endpoint POST in the same send — two refreshes to answer
    /// one challenge.
    /// </remarks>
    internal sealed class OutboundTokenResult
    {

        #region Properties

        /// <summary>
        /// Gets whether the refresh branch ran while producing <see cref="Token"/>.
        /// </summary>
        internal bool Refreshed { get; }

        /// <summary>
        /// Gets the token to attach, or <see langword="null"/> when nothing is attachable.
        /// </summary>
        internal SdkAuth.TokenContainer? Token { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundTokenResult"/> class.
        /// </summary>
        /// <param name="token">The token to attach, or <see langword="null"/> when nothing is attachable.</param>
        /// <param name="refreshed">Whether the refresh branch ran while producing <paramref name="token"/>.</param>
        internal OutboundTokenResult(SdkAuth.TokenContainer? token, bool refreshed)
        {
            Refreshed = refreshed;
            Token = token;
        }

        #endregion

    }

}
