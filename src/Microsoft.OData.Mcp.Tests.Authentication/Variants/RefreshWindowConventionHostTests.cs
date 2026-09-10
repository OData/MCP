// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Variants
{

    /// <summary>
    /// The same secured host issuing twenty-second access tokens, so the suite crosses the send path's refresh
    /// window instead of spending its whole run on the token the startup handshake acquired.
    /// </summary>
    /// <remarks>
    /// Twenty seconds is inside <see cref="ODataMcpAuthConstants.TokenRefreshSkew"/>, so the very first tool
    /// call finds the cached token unattachable and refreshes it. The assertion is <c>&gt;= 1</c> rather than an
    /// exact count because the refresh is coalesced: how many of the five operations land inside one window
    /// depends on how fast the machine runs them, and pinning that number would make the test a stopwatch.
    /// </remarks>
    [TestClass]
    public class RefreshWindowConventionHostTests : OutboundVariantHost
    {

        #region Public Methods

        /// <summary>
        /// Running the representative operations against a short-lived token spends at least one
        /// <c>refresh_token</c> POST and still succeeds.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task ShortLivedToken_RefreshesOnTheSendPath()
        {
            await RunRepresentativeToolsAsync();

            AuthorizationServer!.HitCount($"token:{ODataMcpAuthConstants.GrantTypeRefreshToken}").Should().BeGreaterThanOrEqualTo(1);
            AuthorizationServer.HitCount("devicecode").Should().Be(1);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override LocalAuthorizationServerOptions CreateAuthorizationServerOptions()
        {
            return new LocalAuthorizationServerOptions
            {
                AccessTokenLifetime = TimeSpan.FromSeconds(20)
            };
        }

        #endregion

    }

}
