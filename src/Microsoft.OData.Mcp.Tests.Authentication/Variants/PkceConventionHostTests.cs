// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Variants
{

    /// <summary>
    /// The same secured host signed into with the authorization code grant and PKCE, where the browser redirect
    /// lands on the CLI's real loopback listener rather than an in-process handler.
    /// </summary>
    /// <remarks>
    /// This is the only variant that opens a real socket: the loopback callback under test is a real
    /// <see cref="System.Net.HttpListener"/>, and nothing short of a genuine HTTP request to it would prove the
    /// code ever comes back. The auto-approving presenter follows the authorization endpoint's <c>302</c> the
    /// way a browser would, without waiting for the answer, because the CLI only starts listening once the
    /// presenter returns.
    /// </remarks>
    [TestClass]
    public class PkceConventionHostTests : OutboundVariantHost
    {

        #region Public Methods

        /// <summary>
        /// The authorization code grant completes over the real loopback listener and serves every tool.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task AuthorizationCodeGrant_CompletesOverLoopbackAndRunsTools()
        {
            await RunRepresentativeToolsAsync();

            AuthorizationServer!.HitCount("authorize").Should().Be(1);
            AuthorizationServer.HitCount($"token:{ODataMcpAuthConstants.GrantTypeAuthorizationCode}").Should().Be(1);
            AuthorizationServer.HitCount("devicecode").Should().Be(0);
            Outbound!.Presenter.Presentations.Should().Be(1);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override OutboundOAuthOptions CreateOutboundOptions()
        {
            var options = base.CreateOutboundOptions();
            options.Grant = OutboundGrantKind.AuthorizationCode;

            return options;
        }

        #endregion

    }

}
