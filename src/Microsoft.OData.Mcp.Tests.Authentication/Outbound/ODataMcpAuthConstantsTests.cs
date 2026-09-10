// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Locks the outbound OAuth protocol identifiers.
    /// </summary>
    [TestClass]
    public class ODataMcpAuthConstantsTests
    {

        #region Public Methods

        /// <summary>
        /// <see cref="ODataMcpAuthConstants.AuthorizeSuffixes"/> lists the well-known authorize-path suffixes in strip order.
        /// </summary>
        [TestMethod]
        public void AuthorizeSuffixes_AreInStripOrder()
        {
            ODataMcpAuthConstants.AuthorizeSuffixes.Should().Equal("/oauth2/v2.0/authorize", "/oauth2/authorize", "/authorize");
        }

        /// <summary>
        /// Environment variable names match the documented daemon-secret variables.
        /// </summary>
        [TestMethod]
        public void EnvironmentVariables_MatchDocumentedNames()
        {
            ODataMcpAuthConstants.ClientSecretEnvironmentVariable.Should().Be("ODATA_MCP_CLIENT_SECRET");
            ODataMcpAuthConstants.IdTokenEnvironmentVariable.Should().Be("ODATA_MCP_ID_TOKEN");
        }

        /// <summary>
        /// Grant type identifiers match the RFC 6749 / RFC 8628 / RFC 7523 / RFC 8693 short forms and URNs.
        /// </summary>
        [TestMethod]
        public void GrantTypes_MatchRfcIdentifiers()
        {
            ODataMcpAuthConstants.DeviceCodeGrantType.Should().Be("urn:ietf:params:oauth:grant-type:device_code");
            ODataMcpAuthConstants.GrantTypeAuthorizationCode.Should().Be("authorization_code");
            ODataMcpAuthConstants.GrantTypeClientCredentials.Should().Be("client_credentials");
            ODataMcpAuthConstants.GrantTypeDeviceCode.Should().Be("device_code");
            ODataMcpAuthConstants.GrantTypeRefreshToken.Should().Be("refresh_token");
            ODataMcpAuthConstants.JwtBearerGrantType.Should().Be("urn:ietf:params:oauth:grant-type:jwt-bearer");
            ODataMcpAuthConstants.TokenExchangeGrantType.Should().Be("urn:ietf:params:oauth:grant-type:token-exchange");
        }

        /// <summary>
        /// The Microsoft Graph resource app id is the documented sentinel used to detect (not to use as a client id) the Graph trap.
        /// </summary>
        [TestMethod]
        public void MicrosoftGraphResourceAppId_IsGraphSentinel()
        {
            ODataMcpAuthConstants.MicrosoftGraphResourceAppId.Should().Be("00000003-0000-0000-c000-000000000000");
        }

        /// <summary>
        /// The two named <c>HttpClient</c>s and the Latchkey service name match the values wired into DI.
        /// </summary>
        [TestMethod]
        public void NamedClientsAndServiceName_MatchWiring()
        {
            ODataMcpAuthConstants.LatchkeyServiceName.Should().Be("com.microsoft.odata.mcp");
            ODataMcpAuthConstants.OAuthHttpClientName.Should().Be("OAuth");
            ODataMcpAuthConstants.ODataHttpClientName.Should().Be("OData");
        }

        /// <summary>
        /// OAuth wire parameter and error names match the RFC 6749 / RFC 8628 / RFC 7636 vocabulary.
        /// </summary>
        [TestMethod]
        public void ParameterAndErrorNames_MatchOAuthVocabulary()
        {
            ODataMcpAuthConstants.AccessTokenProperty.Should().Be("access_token");
            ODataMcpAuthConstants.AuthorizationUriParameter.Should().Be("authorization_uri");
            ODataMcpAuthConstants.ClientIdParameter.Should().Be("client_id");
            ODataMcpAuthConstants.ClientNameProperty.Should().Be("client_name");
            ODataMcpAuthConstants.ClientSecretParameter.Should().Be("client_secret");
            ODataMcpAuthConstants.CodeChallengeMethodParameter.Should().Be("code_challenge_method");
            ODataMcpAuthConstants.CodeChallengeMethodS256.Should().Be("S256");
            ODataMcpAuthConstants.CodeChallengeParameter.Should().Be("code_challenge");
            ODataMcpAuthConstants.CodeParameter.Should().Be("code");
            ODataMcpAuthConstants.CodeVerifierParameter.Should().Be("code_verifier");
            ODataMcpAuthConstants.DeviceCodeParameter.Should().Be("device_code");
            ODataMcpAuthConstants.DynamicClientName.Should().Be("odata-mcp");
            ODataMcpAuthConstants.ErrorAccessDenied.Should().Be("access_denied");
            ODataMcpAuthConstants.ErrorAuthorizationPending.Should().Be("authorization_pending");
            ODataMcpAuthConstants.ErrorDescriptionParameter.Should().Be("error_description");
            ODataMcpAuthConstants.ErrorExpiredToken.Should().Be("expired_token");
            ODataMcpAuthConstants.ErrorInvalidToken.Should().Be("invalid_token");
            ODataMcpAuthConstants.ErrorParameter.Should().Be("error");
            ODataMcpAuthConstants.ErrorSlowDown.Should().Be("slow_down");
            ODataMcpAuthConstants.GrantTypeParameter.Should().Be("grant_type");
            ODataMcpAuthConstants.IssParameter.Should().Be("iss");
            ODataMcpAuthConstants.OfflineAccessScope.Should().Be("offline_access");
            ODataMcpAuthConstants.RealmParameter.Should().Be("realm");
            ODataMcpAuthConstants.RedirectUriParameter.Should().Be("redirect_uri");
            ODataMcpAuthConstants.RefreshTokenProperty.Should().Be("refresh_token");
            ODataMcpAuthConstants.ResourceMetadataParameter.Should().Be("resource_metadata");
            ODataMcpAuthConstants.ResourceParameter.Should().Be("resource");
            ODataMcpAuthConstants.ResponseTypeCode.Should().Be("code");
            ODataMcpAuthConstants.ResponseTypeParameter.Should().Be("response_type");
            ODataMcpAuthConstants.ScopeParameter.Should().Be("scope");
            ODataMcpAuthConstants.StateParameter.Should().Be("state");
            ODataMcpAuthConstants.V2Segment.Should().Be("/v2.0");
        }

        /// <summary>
        /// Header, scheme, and well-known suffix names match RFC 7235 / RFC 6750 / RFC 8414 / RFC 9728.
        /// </summary>
        [TestMethod]
        public void ProtocolHeadersAndSuffixes_MatchRfcNames()
        {
            ODataMcpAuthConstants.AuthorizationHeader.Should().Be("Authorization");
            ODataMcpAuthConstants.BasicScheme.Should().Be("Basic");
            ODataMcpAuthConstants.BearerScheme.Should().Be("Bearer");
            ODataMcpAuthConstants.OAuthAuthorizationServerSuffix.Should().Be("/.well-known/oauth-authorization-server");
            ODataMcpAuthConstants.OAuthProtectedResourceSuffix.Should().Be("/.well-known/oauth-protected-resource");
            ODataMcpAuthConstants.OpenIdConfigurationSuffix.Should().Be("/.well-known/openid-configuration");
            ODataMcpAuthConstants.WwwAuthenticateHeader.Should().Be("WWW-Authenticate");
        }

        /// <summary>
        /// <see cref="ODataMcpAuthConstants.TokenRefreshSkew"/> is the documented 30-second skew.
        /// </summary>
        [TestMethod]
        public void TokenRefreshSkew_Is30Seconds()
        {
            ODataMcpAuthConstants.TokenRefreshSkew.Should().Be(TimeSpan.FromSeconds(30));
        }

        #endregion

    }

}
