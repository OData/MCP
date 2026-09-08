// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Variants
{

    /// <summary>
    /// The same secured host reached with a token the operator pasted on the command line, which is the
    /// <c>--auth-token</c> short-circuit.
    /// </summary>
    /// <remarks>
    /// An explicit credential means the operator owns the token's lifetime, so the outbound client must not
    /// discover, must not run a grant, and must not refresh. Every authorization server counter staying at zero
    /// is what proves it: the only traffic in this run is the secured OData service itself.
    /// </remarks>
    [TestClass]
    public class ExplicitBearerConventionHostTests : OutboundVariantHost
    {

        #region Public Methods

        /// <summary>
        /// A pasted bearer token serves every tool without touching the authorization server.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task PastedToken_RunsToolsWithNoAuthorizationServerTraffic()
        {
            await RunRepresentativeToolsAsync();

            AuthorizationServer!.HitCount("prm").Should().Be(0);
            AuthorizationServer.HitCount("metadata-v2").Should().Be(0);
            AuthorizationServer.HitCount("devicecode").Should().Be(0);
            AuthorizationServer.HitCount("token").Should().Be(0);
            Outbound!.Presenter.Presentations.Should().Be(0);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override OutboundOAuthOptions CreateOutboundOptions()
        {
            var options = base.CreateOutboundOptions();
            options.AuthToken = AuthorizationServer!.IssueAccessToken("read write");

            return options;
        }

        #endregion

    }

}
