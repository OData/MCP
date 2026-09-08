// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Locks the shape of <see cref="AuthorizationServerMetadata"/>, its grant advertisement helpers, and the
    /// AOT-safe round trip of every outbound OAuth wire DTO through <see cref="OutboundOAuthJsonContext"/>.
    /// </summary>
    [TestClass]
    public class AuthorizationServerMetadataTests
    {

        #region Public Methods

        /// <summary>
        /// A whitespace grant type is rejected before <see cref="AuthorizationServerMetadata.GrantTypesSupported"/> is consulted.
        /// </summary>
        [TestMethod]
        public void AdvertisesGrant_WhitespaceGrantType_ThrowsArgumentException()
        {
            var metadata = new AuthorizationServerMetadata();

            Action act = () => metadata.AdvertisesGrant("   ");

            act.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// A grant type this server does not advertise is reported as not advertised.
        /// </summary>
        [TestMethod]
        public void AdvertisesGrant_UnadvertisedGrant_ReturnsFalse()
        {
            var metadata = new AuthorizationServerMetadata
            {
                GrantTypesSupported = [ODataMcpAuthConstants.GrantTypeAuthorizationCode]
            };

            metadata.AdvertisesGrant("implicit").Should().BeFalse();
        }

        /// <summary>
        /// The RFC 8628 URN and the short form <c>device_code</c> are treated as the same grant in both directions.
        /// </summary>
        [TestMethod]
        public void AdvertisesGrant_DeviceCodeShortFormAndUrn_AreEquivalentBothWays()
        {
            var shortFormOnly = new AuthorizationServerMetadata
            {
                GrantTypesSupported = [ODataMcpAuthConstants.GrantTypeDeviceCode]
            };
            var urnOnly = new AuthorizationServerMetadata
            {
                GrantTypesSupported = [ODataMcpAuthConstants.DeviceCodeGrantType]
            };

            shortFormOnly.AdvertisesGrant(ODataMcpAuthConstants.DeviceCodeGrantType).Should().BeTrue();
            urnOnly.AdvertisesGrant(ODataMcpAuthConstants.GrantTypeDeviceCode).Should().BeTrue();
            urnOnly.AdvertisesGrant(ODataMcpAuthConstants.DeviceCodeGrantType).Should().BeTrue();
        }

        /// <summary>
        /// A whitespace scope is rejected before <see cref="AuthorizationServerMetadata.ScopesSupported"/> is consulted.
        /// </summary>
        [TestMethod]
        public void AdvertisesScope_WhitespaceScope_ThrowsArgumentException()
        {
            var metadata = new AuthorizationServerMetadata();

            Action act = () => metadata.AdvertisesScope("   ");

            act.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// A scope this server advertises is reported as advertised; one it does not is reported as absent.
        /// </summary>
        [TestMethod]
        public void AdvertisesScope_AdvertisedAndUnadvertisedScopes_ReturnExpectedResults()
        {
            var metadata = new AuthorizationServerMetadata
            {
                ScopesSupported = ["read", "offline_access"]
            };

            metadata.AdvertisesScope("read").Should().BeTrue();
            metadata.AdvertisesScope("write").Should().BeFalse();
        }

        /// <summary>
        /// Round-tripping the real local authorization server's versioned discovery document through the
        /// source-generated context yields the fields outbound discovery reads.
        /// </summary>
        [TestMethod]
        public async Task Deserialize_LocalAuthorizationServerDocument_PopulatesDiscoveryFields()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var client = server.Server.CreateClient();

            var json = await client.GetStringAsync("/oauth/v2.0/.well-known/openid-configuration");
            var metadata = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.AuthorizationServerMetadata);

            metadata.Should().NotBeNull();
            metadata!.Issuer.Should().NotBeNull();
            metadata.Issuer!.ToString().Should().EndWith("/oauth/v2.0");
            metadata.TokenEndpoint.Should().NotBeNull();
            metadata.TokenEndpoint!.ToString().Should().EndWith("/oauth/v2.0/token");
            metadata.DeviceAuthorizationEndpoint.Should().NotBeNull();
            metadata.CodeChallengeMethodsSupported.Should().Contain(ODataMcpAuthConstants.CodeChallengeMethodS256);
            metadata.AuthorizationResponseIssParameterSupported.Should().BeTrue();
            metadata.RegistrationEndpoint.Should().BeNull();
            metadata.AdvertisesGrant("device_code").Should().BeTrue();
            metadata.AdvertisesGrant(ODataMcpAuthConstants.DeviceCodeGrantType).Should().BeTrue();
            metadata.AdvertisesGrant("implicit").Should().BeFalse();
        }

        /// <summary>
        /// A <see cref="DeviceAuthorizationResponse"/> round-trips through the source-generated context, and its
        /// optional members are omitted from the JSON when absent.
        /// </summary>
        [TestMethod]
        public void SerializeAndDeserialize_DeviceAuthorizationResponse_RoundTripsAndOmitsNullFields()
        {
            var response = new DeviceAuthorizationResponse
            {
                DeviceCode = "device-123",
                UserCode = "ABCD-EFGH",
                VerificationUri = "http://localhost/oauth/v2.0/device",
                ExpiresIn = 300
            };

            var json = JsonSerializer.Serialize(response, OutboundOAuthJsonContext.Default.DeviceAuthorizationResponse);

            json.Should().Contain("\"device_code\"");
            json.Should().Contain("\"user_code\"");
            json.Should().Contain("\"verification_uri\"");
            json.Should().Contain("\"expires_in\"");
            json.Should().NotContain("verification_uri_complete");
            json.Should().NotContain("\"interval\"");

            var roundTripped = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.DeviceAuthorizationResponse);

            roundTripped.Should().NotBeNull();
            roundTripped!.DeviceCode.Should().Be("device-123");
            roundTripped.UserCode.Should().Be("ABCD-EFGH");
            roundTripped.VerificationUri.Should().Be("http://localhost/oauth/v2.0/device");
            roundTripped.VerificationUriComplete.Should().BeNull();
            roundTripped.ExpiresIn.Should().Be(300);
            roundTripped.Interval.Should().BeNull();
        }

        /// <summary>
        /// A <see cref="TokenEndpointResponse"/> round-trips through the source-generated context, and its
        /// optional members are omitted from the JSON when absent.
        /// </summary>
        [TestMethod]
        public void SerializeAndDeserialize_TokenEndpointResponse_RoundTripsAndOmitsNullFields()
        {
            var response = new TokenEndpointResponse
            {
                AccessToken = "access-123",
                TokenType = "Bearer",
                ExpiresIn = 3600
            };

            var json = JsonSerializer.Serialize(response, OutboundOAuthJsonContext.Default.TokenEndpointResponse);

            json.Should().Contain("\"access_token\"");
            json.Should().Contain("\"token_type\"");
            json.Should().Contain("\"expires_in\"");
            json.Should().NotContain("refresh_token");
            json.Should().NotContain("\"scope\"");

            var roundTripped = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.TokenEndpointResponse);

            roundTripped.Should().NotBeNull();
            roundTripped!.AccessToken.Should().Be("access-123");
            roundTripped.TokenType.Should().Be("Bearer");
            roundTripped.ExpiresIn.Should().Be(3600);
            roundTripped.RefreshToken.Should().BeNull();
            roundTripped.Scope.Should().BeNull();
        }

        /// <summary>
        /// An <see cref="OAuthErrorPayload"/> round-trips through the source-generated context, and its
        /// optional members are omitted from the JSON when absent.
        /// </summary>
        [TestMethod]
        public void SerializeAndDeserialize_OAuthErrorPayload_RoundTripsAndOmitsNullFields()
        {
            var payload = new OAuthErrorPayload
            {
                Error = ODataMcpAuthConstants.ErrorInvalidToken
            };

            var json = JsonSerializer.Serialize(payload, OutboundOAuthJsonContext.Default.OAuthErrorPayload);

            json.Should().Contain("\"error\"");
            json.Should().NotContain("error_description");
            json.Should().NotContain("error_uri");

            var roundTripped = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.OAuthErrorPayload);

            roundTripped.Should().NotBeNull();
            roundTripped!.Error.Should().Be(ODataMcpAuthConstants.ErrorInvalidToken);
            roundTripped.ErrorDescription.Should().BeNull();
            roundTripped.ErrorUri.Should().BeNull();
        }

        /// <summary>
        /// The SDK <see cref="SdkAuth.TokenContainer"/> cache format round-trips through the source-generated
        /// context, proving the context covers reflection-free (de)serialization for every declared type.
        /// </summary>
        [TestMethod]
        public void SerializeAndDeserialize_SdkTokenContainer_RoundTrips()
        {
            var container = new SdkAuth.TokenContainer
            {
                AccessToken = "access-123",
                TokenType = "Bearer",
                ObtainedAt = DateTimeOffset.UtcNow,
                ExpiresIn = 3600
            };

            var json = JsonSerializer.Serialize(container, OutboundOAuthJsonContext.Default.TokenContainer);
            var roundTripped = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.TokenContainer);

            roundTripped.Should().NotBeNull();
            roundTripped!.AccessToken.Should().Be("access-123");
            roundTripped.TokenType.Should().Be("Bearer");
            roundTripped.ExpiresIn.Should().Be(3600);
        }

        #endregion

    }

}
