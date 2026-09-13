// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="TokenEndpointResponse.ToTokenContainer(string?, string?, string?, string, string?)"/>,
    /// the single place the RFC 6749 wire shape becomes the SDK cache and runtime shape.
    /// </summary>
    [TestClass]
    public class TokenEndpointResponseTests
    {

        #region Public Methods

        /// <summary>
        /// Every wire field lands on the container, an absent <c>token_type</c> defaults to <c>Bearer</c>, an
        /// omitted <c>refresh_token</c> keeps the previous one, and <c>ObtainedAt</c> is stamped at map time.
        /// </summary>
        [TestMethod]
        public void ToTokenContainer_MapsFields_AndDefaultsBearer()
        {
            var before = DateTimeOffset.UtcNow;
            var response = new TokenEndpointResponse
            {
                AccessToken = "access-abc",
                ExpiresIn = 1234,
                Scope = "read offline_access",
                TokenType = "   "
            };

            var container = response.ToTokenContainer("refresh-previous", "cli", "cli-secret", "http://localhost/oauth/v2.0", "client_secret_post");

            container.AccessToken.Should().Be("access-abc");
            container.AuthorizationServer.Should().Be("http://localhost/oauth/v2.0");
            container.ClientId.Should().Be("cli");
            container.ClientSecret.Should().Be("cli-secret");
            container.ExpiresIn.Should().Be(1234);
            container.RefreshToken.Should().Be("refresh-previous");
            container.Scope.Should().Be("read offline_access");
            container.TokenEndpointAuthMethod.Should().Be("client_secret_post");
            container.TokenType.Should().Be("Bearer");
            container.ObtainedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// A response carrying no <c>access_token</c> is a protocol violation the mapper refuses rather than
        /// producing a container whose access token is empty.
        /// </summary>
        [TestMethod]
        public void ToTokenContainer_MissingAccessToken_Throws()
        {
            var response = new TokenEndpointResponse
            {
                TokenType = "Bearer",
                ExpiresIn = 3600
            };

            Action act = () => response.ToTokenContainer(null, "cli", null, "http://localhost/oauth/v2.0", "none");

            act.Should().Throw<InvalidOperationException>();
        }

        /// <summary>
        /// A response that does carry a <c>refresh_token</c> replaces the previous one instead of keeping it.
        /// </summary>
        [TestMethod]
        public void ToTokenContainer_ResponseRefreshToken_ReplacesPrevious()
        {
            var response = new TokenEndpointResponse
            {
                AccessToken = "access-abc",
                RefreshToken = "refresh-new",
                TokenType = "Bearer"
            };

            var container = response.ToTokenContainer("refresh-previous", "cli", null, "http://localhost/oauth/v2.0", "none");

            container.RefreshToken.Should().Be("refresh-new");
        }

        /// <summary>
        /// The authorization server the token came from is required, because a cached container that cannot
        /// name its issuer cannot be refreshed on a cold start.
        /// </summary>
        [TestMethod]
        public void ToTokenContainer_WhitespaceAuthorizationServer_Throws()
        {
            var response = new TokenEndpointResponse
            {
                AccessToken = "access-abc",
                TokenType = "Bearer"
            };

            Action act = () => response.ToTokenContainer(null, "cli", null, "   ", "none");

            act.Should().Throw<ArgumentException>();
        }

        #endregion

    }

}
