// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.AspNetCore.Authentication;
using Microsoft.OData.Mcp.AspNetCore.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Authentication
{

    /// <summary>
    /// <c>/</c> is the other spelling of the application root, matching a leading-slash route base.
    /// </summary>
    [TestClass]
    public class ProtectedResourceMetadataSlashPrefixTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// <c>/</c> normalizes to the root and dedupes with an empty string if both are listed.
        /// </summary>
        [TestMethod]
        public async Task Document_SlashPrefix_DescribesTheRoot()
        {
            var (response, body) = await GetDocumentAsync(ProtectedResourceConstants.WellKnownPath);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.GetProperty("resource").GetString().Should().Be("http://localhost");
        }

        /// <summary>
        /// A path-suffixed well-known URL is not published for the root.
        /// </summary>
        [TestMethod]
        public async Task Document_SlashPrefix_HasNoPathSuffix()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync($"{ProtectedResourceConstants.WellKnownPath}/odata");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureProtectedResource(ProtectedResourceMetadataOptions options)
        {
            base.ConfigureProtectedResource(options);

            options.Prefixes.Add("/");
            options.Prefixes.Add(string.Empty);
        }

        #endregion

    }

}
