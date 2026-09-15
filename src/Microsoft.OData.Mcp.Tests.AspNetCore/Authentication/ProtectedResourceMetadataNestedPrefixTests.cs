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
    /// Nested route bases such as <c>api/v1</c> are valid Endpoint Routing prefixes and get their own document.
    /// </summary>
    [TestClass]
    public class ProtectedResourceMetadataNestedPrefixTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// A <c>401</c> under the nested base points at the nested document.
        /// </summary>
        [TestMethod]
        public async Task Challenge_NestedPrefix_PointsAtItsOwnDocument()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/api/v1/_probe/none");

            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/api/v1\"");
        }

        /// <summary>
        /// A sibling path that only shares a parent segment is not covered.
        /// </summary>
        [TestMethod]
        public async Task Challenge_ParentSegmentOnly_IsUntouched()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/api/_probe/none");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().BeEmpty();
        }

        /// <summary>
        /// The nested base is published at the path-suffixed well-known URL.
        /// </summary>
        [TestMethod]
        public async Task Document_NestedPrefix_IsPublished()
        {
            var (response, body) = await GetDocumentAsync($"{ProtectedResourceConstants.WellKnownPath}/api/v1");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.GetProperty("resource").GetString().Should().Be("http://localhost/api/v1");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureProtectedResource(ProtectedResourceMetadataOptions options)
        {
            base.ConfigureProtectedResource(options);

            options.Prefixes.Add("api/v1");
        }

        #endregion

    }

}
