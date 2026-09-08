// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Locks the outbound OAuth grant selection behavior of <see cref="GrantSelector"/> against the discovery
    /// algorithm's step 6.
    /// </summary>
    [TestClass]
    public class GrantSelectorTests
    {

        #region Public Methods

        /// <summary>
        /// Every defined <see cref="OutboundGrantKind"/> reports its implementation status against the fixed
        /// table this build ships: with the identity assertion grant landed, all five are implemented.
        /// </summary>
        [TestMethod]
        public void IsImplemented_Table()
        {
            GrantSelector.IsImplemented(OutboundGrantKind.AuthorizationCode).Should().BeTrue();
            GrantSelector.IsImplemented(OutboundGrantKind.ClientCredentials).Should().BeTrue();
            GrantSelector.IsImplemented(OutboundGrantKind.DeviceCode).Should().BeTrue();
            GrantSelector.IsImplemented(OutboundGrantKind.IdentityAssertion).Should().BeTrue();
            GrantSelector.IsImplemented(OutboundGrantKind.RefreshToken).Should().BeTrue();
        }

        /// <summary>
        /// Per RFC 8414, an authorization server that publishes no <c>grant_types_supported</c> at all is
        /// treated as advertising exactly <c>authorization_code</c> and <c>implicit</c>.
        /// </summary>
        [TestMethod]
        public void IsAdvertised_EmptyGrantTypes_DefaultsToAuthorizationCode()
        {
            var discovery = CreateDiscovery([]);

            GrantSelector.IsAdvertised(discovery, OutboundGrantKind.AuthorizationCode).Should().BeTrue();
            GrantSelector.IsAdvertised(discovery, OutboundGrantKind.DeviceCode).Should().BeFalse();
            GrantSelector.IsAdvertised(discovery, OutboundGrantKind.ClientCredentials).Should().BeFalse();
        }

        /// <summary>
        /// The RFC 8628 short form <c>device_code</c> is recognized the same way the URN spelling is.
        /// </summary>
        [TestMethod]
        public void Select_DefaultInteractive_DeviceCodeWhenAdvertisedAsShortForm()
        {
            var discovery = CreateDiscovery([ODataMcpAuthConstants.GrantTypeDeviceCode, ODataMcpAuthConstants.GrantTypeAuthorizationCode]);

            var kind = GrantSelector.Select(discovery, new OutboundOAuthOptions(), interactive: true);

            kind.Should().Be(OutboundGrantKind.DeviceCode);
        }

        /// <summary>
        /// An interactive session with no <c>--grant</c> override selects the RFC 8628 device code grant when the
        /// authorization server advertises the URN spelling.
        /// </summary>
        [TestMethod]
        public void Select_DefaultInteractive_DeviceCodeWhenAdvertisedAsUrn()
        {
            var discovery = CreateDiscovery([ODataMcpAuthConstants.DeviceCodeGrantType, ODataMcpAuthConstants.GrantTypeAuthorizationCode]);

            var kind = GrantSelector.Select(discovery, new OutboundOAuthOptions(), interactive: true);

            kind.Should().Be(OutboundGrantKind.DeviceCode);
        }

        /// <summary>
        /// An operator-configured <c>--client-id</c> that also advertises the device code grant still selects
        /// device code when it is passed explicitly through <c>--grant</c>.
        /// </summary>
        [TestMethod]
        public void Select_ExplicitDeviceCode_ReturnsDeviceCode()
        {
            var discovery = CreateDiscovery([ODataMcpAuthConstants.DeviceCodeGrantType]);
            var options = new OutboundOAuthOptions
            {
                Grant = OutboundGrantKind.DeviceCode
            };

            var kind = GrantSelector.Select(discovery, options, interactive: true);

            kind.Should().Be(OutboundGrantKind.DeviceCode);
        }

        /// <summary>
        /// An operator-supplied <c>--grant</c> value the authorization server does not advertise fails first,
        /// naming the issuer, rather than being silently substituted.
        /// </summary>
        [TestMethod]
        public void Select_ExplicitGrantNotAdvertised_Throws()
        {
            var discovery = CreateDiscovery([ODataMcpAuthConstants.DeviceCodeGrantType]);
            var options = new OutboundOAuthOptions
            {
                Grant = OutboundGrantKind.ClientCredentials
            };

            var act = () => GrantSelector.Select(discovery, options, interactive: false);

            act.Should().Throw<OutboundDiscoveryException>().WithMessage("Grant 'client_credentials' is not advertised by https://as.example/.");
        }

        /// <summary>
        /// An explicit <see cref="OutboundGrantKind.RefreshToken"/> can never be selected by an operator, even
        /// though the CLI parser already rejects it; the guard here is defense in depth.
        /// </summary>
        [TestMethod]
        public void Select_ExplicitRefreshToken_ThrowsArgumentException()
        {
            var discovery = CreateDiscovery([ODataMcpAuthConstants.GrantTypeRefreshToken]);
            var options = new OutboundOAuthOptions
            {
                Grant = OutboundGrantKind.RefreshToken
            };

            var act = () => GrantSelector.Select(discovery, options, interactive: false);

            act.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// Enterprise IdP settings plus a readable id token source select the identity assertion grant once the
        /// authorization server advertises the RFC 7523 JWT bearer grant type — in preference to the
        /// interactive device code grant the same server also advertises.
        /// </summary>
        [TestMethod]
        public void Select_IdentityAssertionConfigured_SelectsIdentityAssertion()
        {
            var discovery = CreateDiscovery([ODataMcpAuthConstants.JwtBearerGrantType, ODataMcpAuthConstants.DeviceCodeGrantType]);
            var options = new OutboundOAuthOptions
            {
                IdpUrl = new Uri("https://idp.example/"),
                IdpIdTokenFile = "id-token.jwt"
            };

            GrantSelector.Select(discovery, options, interactive: true).Should().Be(OutboundGrantKind.IdentityAssertion);
        }

        /// <summary>
        /// Enterprise IdP settings the authorization server cannot honor fail first, naming the missing grant
        /// type, rather than silently falling through to an interactive grant the operator did not ask for.
        /// </summary>
        [TestMethod]
        public void Select_IdentityAssertionConfiguredButNotAdvertised_Throws()
        {
            var discovery = CreateDiscovery([ODataMcpAuthConstants.DeviceCodeGrantType]);
            var options = new OutboundOAuthOptions
            {
                IdpUrl = new Uri("https://idp.example/"),
                IdpIdTokenFile = "id-token.jwt"
            };

            var act = () => GrantSelector.Select(discovery, options, interactive: true);

            act.Should().Throw<OutboundDiscoveryException>()
                .WithMessage("Identity assertion is configured but https://as.example/ does not advertise urn:ietf:params:oauth:grant-type:jwt-bearer; pass --grant or fix --idp-url.");
        }

        /// <summary>
        /// An interactive session whose authorization server advertises only <c>authorization_code</c> falls
        /// back to it rather than failing.
        /// </summary>
        [TestMethod]
        public void Select_InteractiveWithoutDeviceCode_FallsBackToAuthorizationCode()
        {
            var discovery = CreateDiscovery([ODataMcpAuthConstants.GrantTypeAuthorizationCode]);

            var grant = GrantSelector.Select(discovery, new OutboundOAuthOptions(), interactive: true);

            grant.Should().Be(OutboundGrantKind.AuthorizationCode);
        }

        /// <summary>
        /// A non-interactive session with nothing usable — no client credentials capability, no device code, no
        /// authorization code fallback attempted — fails first with the operator-facing remediation message.
        /// </summary>
        [TestMethod]
        public void Select_NonInteractiveNothingUsable_Throws()
        {
            var discovery = CreateDiscovery([]);

            var act = () => GrantSelector.Select(discovery, new OutboundOAuthOptions(), interactive: false);

            act.Should().Throw<OutboundDiscoveryException>().WithMessage("Authorization server advertises no grant this client can use; pass --grant or --auth-server.");
        }

        /// <summary>
        /// A non-interactive session with an advertised client credentials grant and a configured client secret
        /// selects client credentials.
        /// </summary>
        [TestMethod]
        public void Select_NonInteractiveWithSecretAndClientCredentials_SelectsClientCredentials()
        {
            var discovery = CreateDiscovery([ODataMcpAuthConstants.GrantTypeClientCredentials]);
            var options = new OutboundOAuthOptions
            {
                ClientSecret = "s3cr3t"
            };

            var grant = GrantSelector.Select(discovery, options, interactive: false);

            grant.Should().Be(OutboundGrantKind.ClientCredentials);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a minimal <see cref="OAuthDiscoveryResult"/> whose authorization server advertises exactly
        /// <paramref name="grantTypesSupported"/>, for grant selection tests that never touch scope or resource
        /// resolution.
        /// </summary>
        /// <param name="grantTypesSupported">The grant types the authorization server advertises.</param>
        /// <returns>
        /// The discovery result.
        /// </returns>
        internal static OAuthDiscoveryResult CreateDiscovery(IReadOnlyList<string> grantTypesSupported)
        {
            var authorizationServer = new AuthorizationServerMetadata
            {
                GrantTypesSupported = [.. grantTypesSupported],
                Issuer = "https://as.example/",
                TokenEndpoint = new Uri("https://as.example/token")
            };

            return new OAuthDiscoveryResult(
                challenge: null,
                protectedResource: null,
                authorizationServer: authorizationServer,
                authorizationServerBase: new Uri("https://as.example/"),
                advertisedGrants: grantTypesSupported,
                challengeScope: null,
                resource: new Uri("https://resource.example/"));
        }

        #endregion

    }

}
