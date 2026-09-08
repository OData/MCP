// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="OutboundAuthLogRedactor"/> against a real <see cref="CapturingLoggerProvider"/>, with
    /// no mocking anywhere, proving that a dummy <see cref="SdkAuth.TokenContainer"/> never reaches a log sink.
    /// </summary>
    [TestClass]
    public class OutboundAuthLogRedactorTests
    {

        #region Public Methods

        /// <summary>
        /// <see cref="OutboundAuthLogRedactor.DescribeToken(SdkAuth.TokenContainer)"/> reports the token type,
        /// expiry, refresh presence, and scope of a dummy token container, but never its access or refresh
        /// token values, even after the description is logged through a real logger.
        /// </summary>
        [TestMethod]
        public void DescribeToken_DummyContainer_NeverEmitsTokenValues()
        {
            var token = new SdkAuth.TokenContainer
            {
                TokenType = "Bearer",
                AccessToken = "SECRET-ACCESS",
                RefreshToken = "SECRET-REFRESH",
                ExpiresIn = 3600,
                Scope = "read",
                ObtainedAt = DateTimeOffset.UtcNow
            };

            var provider = new CapturingLoggerProvider();
            using (var factory = LoggerFactory.Create(builder => builder.AddProvider(provider)))
            {
                var logger = factory.CreateLogger("Test");

                logger.LogInformation("token {Token}", OutboundAuthLogRedactor.DescribeToken(token));
            }

            provider.AllText.Should().Contain("type=Bearer");
            provider.AllText.Should().Contain("expiresIn=3600");
            provider.AllText.Should().Contain("hasRefresh=true");
            provider.AllText.Should().Contain("scope=read");
            provider.AllText.Should().NotContain("SECRET-ACCESS");
            provider.AllText.Should().NotContain("SECRET-REFRESH");
        }

        /// <summary>
        /// A token container with no refresh token and no scope reports <c>hasRefresh=false</c> and
        /// <c>scope=none</c>; a container with no expiry reports <c>expiresIn=none</c>.
        /// </summary>
        [TestMethod]
        public void DescribeToken_NoRefreshNoScope_ReportsNone()
        {
            var token = new SdkAuth.TokenContainer
            {
                TokenType = "Bearer",
                AccessToken = "SECRET-ACCESS",
                ObtainedAt = DateTimeOffset.UtcNow
            };

            var description = OutboundAuthLogRedactor.DescribeToken(token);

            description.Should().Be("type=Bearer expiresIn=none hasRefresh=false scope=none");
        }

        /// <summary>
        /// An <c>Authorization</c> header written as <c>Name: Scheme value</c> or <c>name=Scheme value</c>, with
        /// the header name in either case, has its value masked while the scheme is preserved.
        /// </summary>
        [TestMethod]
        public void Redact_AuthorizationHeader_MasksValue()
        {
            OutboundAuthLogRedactor.Redact("Authorization: Bearer eyJabc").Should().Be("Authorization: Bearer ***");
            OutboundAuthLogRedactor.Redact("authorization=Basic Zm9v").Should().Be("authorization=Basic ***");
        }

        /// <summary>
        /// An empty string is not an exception case — logs can legitimately be empty — so it redacts to an
        /// empty string.
        /// </summary>
        [TestMethod]
        public void Redact_Empty_ReturnsEmpty()
        {
            OutboundAuthLogRedactor.Redact(string.Empty).Should().BeEmpty();
        }

        /// <summary>
        /// Sensitive form-encoded values are masked while unrelated names, and a sensitive name's own text
        /// appearing as someone else's value, are left untouched.
        /// </summary>
        [TestMethod]
        public void Redact_FormEncoded_MasksValues()
        {
            var redacted = OutboundAuthLogRedactor.Redact("grant_type=refresh_token&refresh_token=abc&client_id=cli&client_secret=s3");

            redacted.Should().Be("grant_type=refresh_token&refresh_token=***&client_id=cli&client_secret=***");
        }

        /// <summary>
        /// A JSON string value containing an embedded escaped quote, or ending in an escaped backslash, is
        /// masked in full — the escape sequence is never mistaken for the value's closing quote, which would
        /// otherwise leave the remainder of the value, and a stray quote character, unmasked.
        /// </summary>
        [TestMethod]
        public void Redact_JsonValueWithEscapedQuote_MasksWholeValue()
        {
            OutboundAuthLogRedactor.Redact("{\"access_token\":\"a\\\"b\",\"token_type\":\"Bearer\"}")
                .Should().Be("{\"access_token\":\"***\",\"token_type\":\"Bearer\"}");

            OutboundAuthLogRedactor.Redact("{\"access_token\":\"a\\\\\",\"token_type\":\"Bearer\"}")
                .Should().Be("{\"access_token\":\"***\",\"token_type\":\"Bearer\"}");
        }

        /// <summary>
        /// Text with no sensitive content is returned unchanged.
        /// </summary>
        [TestMethod]
        public void Redact_NoSensitiveContent_ReturnsInputUnchanged()
        {
            const string text = "Discovering authorization for http://localhost/odata/ after HTTP 401";

            OutboundAuthLogRedactor.Redact(text).Should().Be(text);
        }

        /// <summary>
        /// A <see langword="null"/> input is a programming error, not an empty log line, so it throws.
        /// </summary>
        [TestMethod]
        public void Redact_Null_Throws()
        {
            var act = () => OutboundAuthLogRedactor.Redact(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// Every sensitive JSON property value is masked regardless of name casing or whitespace around the
        /// colon, while the property name, quoting, and unrelated properties are preserved.
        /// </summary>
        [TestMethod]
        public void Redact_TokenJson_MasksSensitiveProperties()
        {
            const string json = "{\"access_token\":\"AT1\",\"refresh_token\":\"RT1\",\"client_secret\":\"CS1\","
                + "\"device_code\":\"DC1\",\"code_verifier\":\"CV1\",\"id_token\":\"IT1\","
                + "\"token_type\":\"Bearer\",\"expires_in\":3600,\"Access_Token\" : \"x\"}";

            var redacted = OutboundAuthLogRedactor.Redact(json);

            redacted.Should().Contain("\"access_token\":\"***\"");
            redacted.Should().Contain("\"refresh_token\":\"***\"");
            redacted.Should().Contain("\"client_secret\":\"***\"");
            redacted.Should().Contain("\"device_code\":\"***\"");
            redacted.Should().Contain("\"code_verifier\":\"***\"");
            redacted.Should().Contain("\"id_token\":\"***\"");
            redacted.Should().Contain("\"token_type\":\"Bearer\"");
            redacted.Should().Contain("\"expires_in\":3600");
            redacted.Should().NotContain("AT1");
            redacted.Should().NotContain("RT1");
            redacted.Should().NotContain("CS1");
            redacted.Should().NotContain("DC1");
            redacted.Should().NotContain("CV1");
            redacted.Should().NotContain("IT1");
            redacted.Should().NotContain("\"x\"");
        }

        #endregion

    }

}
