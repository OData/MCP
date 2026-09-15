// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.OData;
using Microsoft.Net.Http.Headers;
using Microsoft.OData.Mcp.AspNetCore.Authentication;
using Microsoft.OData.Mcp.AspNetCore.Constants;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Authentication
{

    /// <summary>
    /// An API serving two route bases publishes one document per prefix, and the origin form describes the
    /// first one configured.
    /// </summary>
    [TestClass]
    public class ProtectedResourceMetadataMultiPrefixTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// A <c>401</c> beneath the second prefix points at the second prefix's document, not the first.
        /// </summary>
        [TestMethod]
        public async Task Challenge_SecondPrefix_PointsAtItsOwnDocument()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/reporting/_probe/none");

            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/reporting\"");
        }

        /// <summary>
        /// Each prefix gets its own path-suffixed document naming its own resource.
        /// </summary>
        [TestMethod]
        public async Task Document_EachPrefix_HasItsOwnDocument()
        {
            var (odata, odataBody) = await GetDocumentAsync($"{ProtectedResourceConstants.WellKnownPath}/odata");
            var (reporting, reportingBody) = await GetDocumentAsync($"{ProtectedResourceConstants.WellKnownPath}/reporting");

            odata.StatusCode.Should().Be(HttpStatusCode.OK);
            odataBody.GetProperty("resource").GetString().Should().Be("http://localhost/odata");
            reporting.StatusCode.Should().Be(HttpStatusCode.OK);
            reportingBody.GetProperty("resource").GetString().Should().Be("http://localhost/reporting");
        }

        /// <summary>
        /// The origin form describes the first prefix, which is the one a client that never read a
        /// <c>resource_metadata</c> hint lands on when the root is not covered.
        /// </summary>
        [TestMethod]
        public async Task Document_OriginForm_DescribesTheFirstPrefix()
        {
            var (response, body) = await GetDocumentAsync(ProtectedResourceConstants.WellKnownPath);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.GetProperty("resource").GetString().Should().Be("http://localhost/odata");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureOData(ODataOptions options)
        {
            options.EnableQueryFeatures();
            options.AddRouteComponents("odata", TestModels.GetSimpleModel());
            options.AddRouteComponents("reporting", TestModels.GetMinimalModel());
        }

        /// <inheritdoc />
        internal override void ConfigureProtectedResource(ProtectedResourceMetadataOptions options)
        {
            base.ConfigureProtectedResource(options);

            options.Prefixes.Add("odata");
            options.Prefixes.Add("reporting");
        }

        #endregion

    }

}
