// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Restier.Variants
{

    /// <summary>
    /// The same secured Restier host started twice against one persisted token cache, which is what an operator
    /// does every time they restart <c>odata-mcp start</c>.
    /// </summary>
    /// <remarks>
    /// The second host is built from the same fixture, so it reads the credential the first one wrote to disk.
    /// A restart that costs a token-endpoint POST would mean the cache is not being consulted; a restart that
    /// costs a second device authorization would mean a human is being asked to sign in again for nothing.
    /// </remarks>
    [TestClass]
    public class RestartRestierHostTests : OutboundRestierVariantHost
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestartRestierHostTests"/> class.
        /// </summary>
        public RestartRestierHostTests()
            : base("RestartRestierHost")
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// A second Tools host over the same on-disk cache signs in with no authorization server traffic at all
        /// and serves the same tools.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task SecondHost_ReusesCachedTokenAndSpendsNoGrant()
        {
            await RunRepresentativeToolsAsync(30);

            var tokenHits = AuthorizationServer!.HitCount("token");

            await Outbound!.CreateSessionAsync(HostCatalogOptions(), CancellationToken.None);
            await RunRepresentativeToolsAsync(31);

            AuthorizationServer.HitCount("token").Should().Be(tokenHits);
            AuthorizationServer.HitCount("devicecode").Should().Be(1);
            Outbound.Presenter.Presentations.Should().Be(1);
        }

        #endregion

    }

}
