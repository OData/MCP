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
    /// An explicit empty-string prefix is the same route base Endpoint Routing and Minimal APIs accept for the
    /// application root.
    /// </summary>
    [TestClass]
    public class ProtectedResourceMetadataEmptyPrefixTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// An explicit empty string publishes the origin document, not a path-suffixed one.
        /// </summary>
        [TestMethod]
        public async Task Document_EmptyStringPrefix_DescribesTheRoot()
        {
            var (response, body) = await GetDocumentAsync(ProtectedResourceConstants.WellKnownPath);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.GetProperty("resource").GetString().Should().Be("http://localhost");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureProtectedResource(ProtectedResourceMetadataOptions options)
        {
            base.ConfigureProtectedResource(options);

            options.Prefixes.Add(string.Empty);
        }

        #endregion

    }

}
