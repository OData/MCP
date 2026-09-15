// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Net.Http.Headers;
using Microsoft.OData.Mcp.AspNetCore.Authentication;
using Microsoft.OData.Mcp.AspNetCore.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Authentication
{

    /// <summary>
    /// An explicit prefix list replaces the root default, which is how a host covers only some of its routes.
    /// </summary>
    [TestClass]
    public class ProtectedResourceMetadataExplicitPrefixTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// A <c>401</c> outside every covered prefix is never annotated.
        /// </summary>
        [TestMethod]
        public async Task Challenge_OutsidePrefix_IsUntouched()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/outside/_probe/none");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().BeEmpty();
        }

        /// <summary>
        /// A prefix is matched by whole segments, so <c>custom</c> never claims <c>customfoo</c>.
        /// </summary>
        [TestMethod]
        public async Task Challenge_SegmentLookalikePrefix_IsUntouched()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/customfoo/_probe/none");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().BeEmpty();
        }

        /// <summary>
        /// The root is not published, because the explicit list replaced the default.
        /// </summary>
        [TestMethod]
        public async Task Document_DefaultRoot_IsNotPublishedAsASuffix()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync($"{ProtectedResourceConstants.WellKnownPath}/odata");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// The configured prefix is published even though routing never advertised it, including a leading
        /// slash that Endpoint Routing also accepts.
        /// </summary>
        [TestMethod]
        public async Task Document_ExplicitPrefix_IsPublished()
        {
            var (response, body) = await GetDocumentAsync($"{ProtectedResourceConstants.WellKnownPath}/custom");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.GetProperty("resource").GetString().Should().Be("http://localhost/custom");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureProtectedResource(ProtectedResourceMetadataOptions options)
        {
            base.ConfigureProtectedResource(options);

            options.Prefixes.Add("/custom/");
        }

        #endregion

    }

}
