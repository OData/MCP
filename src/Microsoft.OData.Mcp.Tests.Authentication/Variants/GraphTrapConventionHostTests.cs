// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Variants
{

    /// <summary>
    /// The same secured host whose protected resource metadata document answers <c>401</c> rather than the
    /// RFC 9728 body — the shape Microsoft Graph returns, and the one a naive client loops on.
    /// </summary>
    /// <remarks>
    /// Discovery has to fall back to the <c>authorization_uri</c> the challenge carries and get on with the
    /// grant. The counter is the whole point: exactly one GET at the trap, because the <c>"OAuth"</c> named
    /// client carries no outbound handler and therefore cannot answer its own <c>401</c> by starting discovery
    /// again.
    /// </remarks>
    [TestClass]
    public class GraphTrapConventionHostTests : OutboundVariantHost
    {

        #region Public Methods

        /// <summary>
        /// A protected resource metadata document that challenges instead of answering costs one GET, and the
        /// tools still work.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task UnauthorizedProtectedResourceMetadata_CostsOneGetAndToolsStillWork()
        {
            await RunRepresentativeToolsAsync();

            AuthorizationServer!.HitCount("prm").Should().Be(1);
            AuthorizationServer.HitCount("devicecode").Should().Be(1);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override LocalAuthorizationServerOptions CreateAuthorizationServerOptions()
        {
            return new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.Unauthorized
            };
        }

        #endregion

    }

}
