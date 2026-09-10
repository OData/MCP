// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Variants
{

    /// <summary>
    /// The same secured host reached by a daemon: a confidential client pinned to the client credentials grant,
    /// with nobody at a keyboard to approve anything.
    /// </summary>
    /// <remarks>
    /// The presentation count staying at zero is the assertion that matters. A daemon that turns a <c>401</c>
    /// into a device code prompt writes a verification URL to a console nobody is watching and then blocks
    /// forever, which is exactly the failure the pinned grant exists to prevent.
    /// </remarks>
    [TestClass]
    public class ClientCredentialsConventionHostTests : OutboundVariantHost
    {

        #region Public Methods

        /// <summary>
        /// A daemon signs in with its own secret, never prompts, and serves every tool.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task DaemonClient_SignsInWithoutPrompting()
        {
            await RunRepresentativeToolsAsync();

            AuthorizationServer!.HitCount($"token:{ODataMcpAuthConstants.GrantTypeClientCredentials}").Should().Be(1);
            AuthorizationServer.HitCount("devicecode").Should().Be(0);
            AuthorizationServer.HitCount("authorize").Should().Be(0);
            Outbound!.Presenter.Presentations.Should().Be(0);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override OutboundOAuthOptions CreateOutboundOptions()
        {
            var options = base.CreateOutboundOptions();
            options.ClientId = "daemon";
            options.ClientSecret = "daemon-secret";
            options.Grant = OutboundGrantKind.ClientCredentials;

            return options;
        }

        #endregion

    }

}
