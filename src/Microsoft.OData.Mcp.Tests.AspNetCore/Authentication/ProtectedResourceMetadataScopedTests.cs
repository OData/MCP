// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Net.Http.Headers;
using Microsoft.OData.Mcp.AspNetCore.Authentication;
using Microsoft.OData.Mcp.AspNetCore.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Authentication
{

    /// <summary>
    /// An API that publishes scopes: the document advertises them and the annotated challenge asks for them.
    /// </summary>
    [TestClass]
    public class ProtectedResourceMetadataScopedTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// The annotated challenge carries the published scopes so a client knows what to ask for.
        /// </summary>
        [TestMethod]
        public async Task Challenge_WithScopes_CarriesScope()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/Customers");

            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\", scope=\"Data.Read Data.Write\"");
        }

        /// <summary>
        /// The published document advertises the same scopes, plus the display metadata a consent screen shows.
        /// </summary>
        [TestMethod]
        public async Task Document_WithScopes_AdvertisesThem()
        {
            var (_, body) = await GetDocumentAsync(ProtectedResourceConstants.WellKnownPath);

            body.GetProperty("scopes_supported").EnumerateArray().Select(entry => entry.GetString()).Should()
                .BeEquivalentTo(["Data.Read", "Data.Write"]);
            body.GetProperty("resource_name").GetString().Should().Be("Contoso API");
            body.GetProperty("resource_documentation").GetString().Should().Be("https://docs.example.com/");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureProtectedResource(ProtectedResourceMetadataOptions options)
        {
            base.ConfigureProtectedResource(options);

            options.ResourceDocumentation = new Uri("https://docs.example.com/");
            options.ResourceName = "Contoso API";
            options.ScopesSupported.Add("Data.Read");
            options.ScopesSupported.Add("Data.Write");
        }

        #endregion

    }

}
