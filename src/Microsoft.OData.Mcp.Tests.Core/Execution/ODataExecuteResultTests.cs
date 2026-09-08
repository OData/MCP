// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Execution
{

    /// <summary>
    /// Tests for <see cref="ODataExecuteResult"/>.
    /// </summary>
    [TestClass]
    public class ODataExecuteResultTests
    {

        #region Public Methods

        /// <summary>
        /// FromHttp with duplicate and distinct WWW-Authenticate values preserves raw quoting, order, and dedupes.
        /// </summary>
        [TestMethod]
        public void FromHttp_WwwAuthenticateHeadersPresent_CopiesRawDeduplicatedValues()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            response.Content = new StringContent(string.Empty);
            response.Headers.TryAddWithoutValidation("WWW-Authenticate", "Bearer realm=\"\", resource_metadata=\"https://x/.well-known/oauth-protected-resource\"");
            response.Headers.TryAddWithoutValidation("WWW-Authenticate", "Bearer realm=\"\", resource_metadata=\"https://x/.well-known/oauth-protected-resource\"");
            response.Headers.TryAddWithoutValidation("WWW-Authenticate", "Basic realm=\"r\"");

            var result = ODataExecuteResult.FromHttp(response, string.Empty);

            result.WwwAuthenticate.Should().HaveCount(2);
            result.WwwAuthenticate[0].Should().Be("Bearer realm=\"\", resource_metadata=\"https://x/.well-known/oauth-protected-resource\"");
            result.WwwAuthenticate[1].Should().Be("Basic realm=\"r\"");
        }

        /// <summary>
        /// FromHttp with no WWW-Authenticate header returns an empty list.
        /// </summary>
        [TestMethod]
        public void FromHttp_NoWwwAuthenticateHeader_ReturnsEmptyList()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(string.Empty);

            var result = ODataExecuteResult.FromHttp(response, string.Empty);

            result.WwwAuthenticate.Should().BeEmpty();
        }

        #endregion

    }

}
