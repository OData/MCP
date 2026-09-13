// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Locks the CLI <c>--grant</c> wire-name parsing and formatting behavior of <see cref="OutboundGrantKindParser"/>.
    /// </summary>
    [TestClass]
    public class OutboundGrantKindParserTests
    {

        #region Public Methods

        /// <summary>
        /// Every defined <see cref="OutboundGrantKind"/> value round-trips through <see cref="OutboundGrantKindParser.ToWireName(OutboundGrantKind)"/>.
        /// </summary>
        [TestMethod]
        public void ToWireName_AuthorizationCode_ReturnsWireName()
        {
            OutboundGrantKindParser.ToWireName(OutboundGrantKind.AuthorizationCode).Should().Be("authorization_code");
        }

        /// <summary>
        /// The client credentials grant kind formats to its wire name.
        /// </summary>
        [TestMethod]
        public void ToWireName_ClientCredentials_ReturnsWireName()
        {
            OutboundGrantKindParser.ToWireName(OutboundGrantKind.ClientCredentials).Should().Be("client_credentials");
        }

        /// <summary>
        /// The device code grant kind formats to its wire name.
        /// </summary>
        [TestMethod]
        public void ToWireName_DeviceCode_ReturnsWireName()
        {
            OutboundGrantKindParser.ToWireName(OutboundGrantKind.DeviceCode).Should().Be("device_code");
        }

        /// <summary>
        /// The identity assertion grant kind formats to its wire name.
        /// </summary>
        [TestMethod]
        public void ToWireName_IdentityAssertion_ReturnsWireName()
        {
            OutboundGrantKindParser.ToWireName(OutboundGrantKind.IdentityAssertion).Should().Be("identity_assertion");
        }

        /// <summary>
        /// The refresh token grant kind, though not CLI-selectable, still formats to its wire name.
        /// </summary>
        [TestMethod]
        public void ToWireName_RefreshToken_ReturnsWireName()
        {
            OutboundGrantKindParser.ToWireName(OutboundGrantKind.RefreshToken).Should().Be("refresh_token");
        }

        /// <summary>
        /// Every wire name <see cref="OutboundGrantKindParser.ToWireName(OutboundGrantKind)"/> produces parses back
        /// to the same kind, except <see cref="OutboundGrantKind.RefreshToken"/>, which is not CLI-selectable.
        /// </summary>
        [TestMethod]
        public void ToWireName_ThenTryParse_RoundTripsCliSelectableKinds()
        {
            foreach (var kind in new[] { OutboundGrantKind.DeviceCode, OutboundGrantKind.AuthorizationCode, OutboundGrantKind.ClientCredentials, OutboundGrantKind.IdentityAssertion })
            {
                var wireName = OutboundGrantKindParser.ToWireName(kind);

                OutboundGrantKindParser.TryParse(wireName, out var parsed).Should().BeTrue();
                parsed.Should().Be(kind);
            }
        }

        /// <summary>
        /// A value outside the defined <see cref="OutboundGrantKind"/> range throws.
        /// </summary>
        [TestMethod]
        public void ToWireName_UndefinedValue_ThrowsArgumentOutOfRangeException()
        {
            var act = () => OutboundGrantKindParser.ToWireName((OutboundGrantKind)999);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        /// <summary>
        /// <c>authorization_code</c> parses regardless of case or surrounding whitespace.
        /// </summary>
        [TestMethod]
        public void TryParse_AuthorizationCodeMixedCaseWithWhitespace_ReturnsTrue()
        {
            OutboundGrantKindParser.TryParse("  Authorization_Code  ", out var kind).Should().BeTrue();
            kind.Should().Be(OutboundGrantKind.AuthorizationCode);
        }

        /// <summary>
        /// <c>client_credentials</c> parses regardless of case or surrounding whitespace.
        /// </summary>
        [TestMethod]
        public void TryParse_ClientCredentialsMixedCaseWithWhitespace_ReturnsTrue()
        {
            OutboundGrantKindParser.TryParse("CLIENT_CREDENTIALS", out var kind).Should().BeTrue();
            kind.Should().Be(OutboundGrantKind.ClientCredentials);
        }

        /// <summary>
        /// <c>device_code</c> parses regardless of case or surrounding whitespace.
        /// </summary>
        [TestMethod]
        public void TryParse_DeviceCodeMixedCaseWithWhitespace_ReturnsTrue()
        {
            OutboundGrantKindParser.TryParse(" Device_Code ", out var kind).Should().BeTrue();
            kind.Should().Be(OutboundGrantKind.DeviceCode);
        }

        /// <summary>
        /// An empty or all-white-space value fails to parse.
        /// </summary>
        [TestMethod]
        public void TryParse_EmptyOrWhiteSpaceValue_ReturnsFalse()
        {
            OutboundGrantKindParser.TryParse(string.Empty, out _).Should().BeFalse();
            OutboundGrantKindParser.TryParse("   ", out _).Should().BeFalse();
        }

        /// <summary>
        /// <c>identity_assertion</c> parses regardless of case or surrounding whitespace.
        /// </summary>
        [TestMethod]
        public void TryParse_IdentityAssertionMixedCaseWithWhitespace_ReturnsTrue()
        {
            OutboundGrantKindParser.TryParse("Identity_Assertion", out var kind).Should().BeTrue();
            kind.Should().Be(OutboundGrantKind.IdentityAssertion);
        }

        /// <summary>
        /// A <see langword="null"/> value fails to parse.
        /// </summary>
        [TestMethod]
        public void TryParse_NullValue_ReturnsFalse()
        {
            OutboundGrantKindParser.TryParse(null, out _).Should().BeFalse();
        }

        /// <summary>
        /// <c>refresh_token</c> is not a CLI-selectable grant kind and fails to parse.
        /// </summary>
        [TestMethod]
        public void TryParse_RefreshToken_ReturnsFalse()
        {
            OutboundGrantKindParser.TryParse("refresh_token", out _).Should().BeFalse();
        }

        /// <summary>
        /// A value that is not any known wire name fails to parse.
        /// </summary>
        [TestMethod]
        public void TryParse_UnknownValue_ReturnsFalse()
        {
            OutboundGrantKindParser.TryParse("not_a_grant", out _).Should().BeFalse();
        }

        #endregion

    }

}
