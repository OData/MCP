// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OData.Mcp.AspNetCore.Authentication;
using Microsoft.OData.Mcp.AspNetCore.Constants;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Authentication
{
    /// <summary>
    /// The default <c>AddProtectedResourceMetadata</c> call: no prefixes configured, so the protected resource
    /// is the application root.
    /// </summary>
    [TestClass]
    public class ProtectedResourceMetadataTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// The bare <c>Bearer</c> the JWT bearer handler answers with gains <c>resource_metadata</c> pointing at
        /// the origin document.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_BareBearer_GainsResourceMetadata()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/Customers");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"");
        }

        /// <summary>
        /// A <c>Basic</c> challenge sharing the header with <c>Bearer</c> survives the rewrite verbatim.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_BasicAlongsideBearer_IsPreserved()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/_probe/basic");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Basic realm=\"legacy\", Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"");
        }

        /// <summary>
        /// A challenge that already names a document is left exactly as the app wrote it.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_ExistingResourceMetadata_IsUntouched()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/_probe/existing");

            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer realm=\"contoso\", resource_metadata=\"https://existing.example.com/doc\"");
        }

        /// <summary>
        /// A <c>401</c> that carried no header at all is given a whole challenge.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_NoHeader_GainsBearerChallenge()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/_probe/none");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"");
        }

        /// <summary>
        /// The application root covers every path, so a <c>401</c> that is not under an OData prefix is still
        /// annotated.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_PathOutsideOData_IsStillAnnotated()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/outside/_probe/none");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"");
        }

        /// <summary>
        /// A successful response beneath the host is never annotated.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_SuccessfulResponse_IsUntouched()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/_probe/ok");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().BeEmpty();
        }

        /// <summary>
        /// The origin form describes the application root and is cacheable JSON.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_OriginForm_DescribesTheRoot()
        {
            var (response, body) = await GetDocumentAsync(ProtectedResourceConstants.WellKnownPath);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
            RawHeader(response, HeaderNames.CacheControl).Should().Be("public, max-age=300");
            body.GetProperty("resource").GetString().Should().Be("http://localhost");
            body.GetProperty("authorization_servers").EnumerateArray().Select(entry => entry.GetString()).Should().ContainSingle()
                .Which.Should().Be(AuthorizationServer);
            body.GetProperty("bearer_methods_supported").EnumerateArray().Select(entry => entry.GetString()).Should().ContainSingle()
                .Which.Should().Be("header");
        }

        /// <summary>
        /// The document is readable without a token, which is what keeps it from being a dead end.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_OriginForm_IsAnonymouslyReadable()
        {
            var (response, _) = await GetDocumentAsync(ProtectedResourceConstants.WellKnownPath);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().BeEmpty();
        }

        /// <summary>
        /// A verb other than <c>GET</c> or <c>HEAD</c> on a document this middleware serves is refused.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_Post_IsMethodNotAllowed()
        {
            using var client = TestServer.CreateClient();
            using var content = new StringContent("{}");
            var response = await client.PostAsync(ProtectedResourceConstants.WellKnownPath, content);

            response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        }

        /// <summary>
        /// A well-known suffix naming a prefix this host does not serve belongs to somebody else.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_UnknownSuffix_FallsThrough()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync($"{ProtectedResourceConstants.WellKnownPath}/nope");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion

    }
}
