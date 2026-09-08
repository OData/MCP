// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Locks the construction and validation behavior of <see cref="OutboundConsentRequest"/> and the
    /// pass-through properties of <see cref="OAuthConsentRequiredException"/>.
    /// </summary>
    [TestClass]
    public class OutboundConsentRequestTests
    {

        #region Public Methods

        /// <summary>
        /// Omitting <c>elicitationId</c> generates a <c>D</c>-format GUID.
        /// </summary>
        [TestMethod]
        public void Constructor_NoElicitationId_GeneratesDFormatGuid()
        {
            var request = new OutboundConsentRequest(
                OutboundGrantKind.DeviceCode,
                new Uri("https://login.example.com/device"),
                "Sign in to continue.",
                userCode: "ABCD-EFGH",
                verificationUri: new Uri("https://example.com/devicelogin"));

            request.ElicitationId.Should().NotBeNullOrWhiteSpace();
            Guid.TryParseExact(request.ElicitationId, "D", out _).Should().BeTrue();
        }

        /// <summary>
        /// A consent URL whose scheme is neither <c>https</c> nor loopback <c>http</c> is refused outright.
        /// The URL is server-controlled and ends up at <c>ShellExecute</c> and at an MCP elicitation, so
        /// <c>file:</c> — like <c>ms-settings:</c> or <c>vscode:</c> — never gets that far.
        /// </summary>
        [TestMethod]
        public void Constructor_NonPresentableUrlScheme_ThrowsOutboundDiscoveryException()
        {
            var act = () => new OutboundConsentRequest(
                OutboundGrantKind.DeviceCode,
                new Uri("file:///x"),
                "Sign in to continue.",
                userCode: null,
                verificationUri: null);

            act.Should().Throw<OutboundDiscoveryException>()
                .WithMessage("Consent URL must use https (or http on loopback): file");
        }

        /// <summary>
        /// A non-loopback <c>http</c> verification URI is refused even when the primary consent URL is a
        /// perfectly good <c>https</c> one, because both are shown to a human.
        /// </summary>
        [TestMethod]
        public void Constructor_NonPresentableVerificationUri_ThrowsOutboundDiscoveryException()
        {
            var act = () => new OutboundConsentRequest(
                OutboundGrantKind.DeviceCode,
                new Uri("https://login.example.com/device"),
                "Sign in to continue.",
                userCode: "ABCD-EFGH",
                verificationUri: new Uri("http://device.example.com/verify"));

            act.Should().Throw<OutboundDiscoveryException>()
                .WithMessage("Consent URL must use https (or http on loopback): http");
        }

        /// <summary>
        /// A relative consent URL is rejected.
        /// </summary>
        [TestMethod]
        public void Constructor_RelativeUrl_ThrowsArgumentException()
        {
            var act = () => new OutboundConsentRequest(
                OutboundGrantKind.DeviceCode,
                new Uri("/relative", UriKind.Relative),
                "Sign in to continue.",
                userCode: null,
                verificationUri: null);

            act.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// A supplied <c>elicitationId</c> is kept rather than replaced.
        /// </summary>
        [TestMethod]
        public void Constructor_SuppliedElicitationId_IsKept()
        {
            var request = new OutboundConsentRequest(
                OutboundGrantKind.AuthorizationCode,
                new Uri("https://login.example.com/authorize"),
                "Sign in to continue.",
                userCode: null,
                verificationUri: null,
                elicitationId: "my-elicitation-id");

            request.ElicitationId.Should().Be("my-elicitation-id");
        }

        /// <summary>
        /// The constructor stores every argument on its matching property.
        /// </summary>
        [TestMethod]
        public void Constructor_ValidArguments_PopulatesProperties()
        {
            var url = new Uri("https://login.example.com/device");
            var verificationUri = new Uri("https://example.com/devicelogin");

            var request = new OutboundConsentRequest(
                OutboundGrantKind.DeviceCode,
                url,
                "Sign in to continue.",
                userCode: "ABCD-EFGH",
                verificationUri: verificationUri);

            request.Kind.Should().Be(OutboundGrantKind.DeviceCode);
            request.Url.Should().BeSameAs(url);
            request.Message.Should().Be("Sign in to continue.");
            request.UserCode.Should().Be("ABCD-EFGH");
            request.VerificationUri.Should().BeSameAs(verificationUri);
        }

        /// <summary>
        /// A white-space message is rejected.
        /// </summary>
        [TestMethod]
        public void Constructor_WhiteSpaceMessage_ThrowsArgumentException()
        {
            var act = () => new OutboundConsentRequest(
                OutboundGrantKind.DeviceCode,
                new Uri("https://login.example.com/device"),
                "   ",
                userCode: null,
                verificationUri: null);

            act.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// The exception's message names the wrapped request's message.
        /// </summary>
        [TestMethod]
        public void OAuthConsentRequiredException_Message_ContainsRequestMessage()
        {
            var request = new OutboundConsentRequest(
                OutboundGrantKind.DeviceCode,
                new Uri("https://login.example.com/device"),
                "Sign in to continue.",
                userCode: null,
                verificationUri: null);

            var exception = new OAuthConsentRequiredException(request);

            exception.Message.Should().Contain("Sign in to continue.");
        }

        /// <summary>
        /// The two-argument constructor wraps the inner exception.
        /// </summary>
        [TestMethod]
        public void OAuthConsentRequiredException_WithInnerException_SetsInnerException()
        {
            var request = new OutboundConsentRequest(
                OutboundGrantKind.DeviceCode,
                new Uri("https://login.example.com/device"),
                "Sign in to continue.",
                userCode: null,
                verificationUri: null);
            var inner = new InvalidOperationException("refresh failed");

            var exception = new OAuthConsentRequiredException(request, inner);

            exception.InnerException.Should().BeSameAs(inner);
        }

        /// <summary>
        /// The exception exposes the wrapped request and pass-through properties for every request field.
        /// </summary>
        [TestMethod]
        public void OAuthConsentRequiredException_WrapsRequest_ExposesPassThroughProperties()
        {
            var request = new OutboundConsentRequest(
                OutboundGrantKind.DeviceCode,
                new Uri("https://login.example.com/device"),
                "Sign in to continue.",
                userCode: "ABCD-EFGH",
                verificationUri: new Uri("https://example.com/devicelogin"));

            var exception = new OAuthConsentRequiredException(request);

            exception.Request.Should().BeSameAs(request);
            exception.ElicitationId.Should().Be(request.ElicitationId);
            exception.Kind.Should().Be(request.Kind);
            exception.Url.Should().BeSameAs(request.Url);
            exception.UserCode.Should().Be(request.UserCode);
            exception.VerificationUri.Should().BeSameAs(request.VerificationUri);
        }

        #endregion

    }

}
