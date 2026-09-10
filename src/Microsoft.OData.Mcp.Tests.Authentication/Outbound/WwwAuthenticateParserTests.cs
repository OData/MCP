// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Locks the RFC 9110 / RFC 6750 <c>WWW-Authenticate</c> challenge parsing behavior.
    /// </summary>
    [TestClass]
    public class WwwAuthenticateParserTests
    {

        #region Fields

        /// <summary>
        /// The literal Microsoft Graph <c>$metadata</c> 401 challenge from the specification's "Graph trap" worked example.
        /// </summary>
        internal const string GraphChallenge = "Bearer realm=\"\", authorization_uri=\"https://login.microsoftonline.com/common/oauth2/authorize\", client_id=\"00000003-0000-0000-c000-000000000000\"";

        /// <summary>
        /// An RFC 9728 protected-resource-metadata challenge.
        /// </summary>
        internal const string ResourceMetadataChallenge = "Bearer resource_metadata=\"https://localhost/.well-known/oauth-protected-resource/odata\", scope=\"read\"";

        #endregion

        #region Public Methods

        /// <summary>
        /// A <c>Basic</c> challenge keeps its scheme and realm and populates none of the OAuth-specific properties.
        /// </summary>
        [TestMethod]
        public void Parse_BasicChallenge_ReturnsSchemeWithoutOAuthFields()
        {
            var challenge = WwwAuthenticateParser.Parse("Basic realm=\"x\"");

            challenge.Scheme.Should().Be(ODataMcpAuthConstants.BasicScheme);
            challenge.Realm.Should().Be("x");
            challenge.AuthorizationUri.Should().BeNull();
            challenge.Error.Should().BeNull();
            challenge.ErrorDescription.Should().BeNull();
            challenge.ResourceClientId.Should().BeNull();
            challenge.ResourceMetadata.Should().BeNull();
            challenge.Scope.Should().BeNull();
        }

        /// <summary>
        /// A challenge whose scheme is missing entirely is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_EmptyScheme_ThrowsFormatException()
        {
            Action act = () => WwwAuthenticateParser.Parse("=oops");

            act.Should().Throw<FormatException>();
        }

        /// <summary>
        /// An empty realm is preserved as an empty string rather than collapsed to <see langword="null"/>.
        /// </summary>
        [TestMethod]
        public void Parse_EmptyQuotedValue_ReturnsEmptyString()
        {
            var challenge = WwwAuthenticateParser.Parse(GraphChallenge);

            challenge.Realm.Should().NotBeNull();
            challenge.Realm.Should().BeEmpty();
        }

        /// <summary>
        /// An <c>error</c> / <c>error_description</c> challenge surfaces both values.
        /// </summary>
        [TestMethod]
        public void Parse_ErrorParameters_ReturnsErrorAndDescription()
        {
            var challenge = WwwAuthenticateParser.Parse("Bearer error=\"invalid_token\", error_description=\"expired\"");

            challenge.Scheme.Should().Be(ODataMcpAuthConstants.BearerScheme);
            challenge.Error.Should().Be(ODataMcpAuthConstants.ErrorInvalidToken);
            challenge.ErrorDescription.Should().Be("expired");
            challenge.Parameters[ODataMcpAuthConstants.ErrorParameter].Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// Escaped quotes inside a quoted-string are unescaped in the parsed value.
        /// </summary>
        [TestMethod]
        public void Parse_EscapedQuotesInValue_UnescapesValue()
        {
            var challenge = WwwAuthenticateParser.Parse("Bearer realm=\"say \\\"hi\\\"\"");

            challenge.Realm.Should().Be("say \"hi\"");
        }

        /// <summary>
        /// The Microsoft Graph challenge yields the authorization URI and stores the resource app id as <c>ResourceClientId</c>.
        /// </summary>
        [TestMethod]
        public void Parse_GraphChallenge_ReturnsBearerWithResourceClientId()
        {
            var challenge = WwwAuthenticateParser.Parse(GraphChallenge);

            challenge.Scheme.Should().Be(ODataMcpAuthConstants.BearerScheme);
            challenge.Realm.Should().BeEmpty();
            challenge.AuthorizationUri.Should().Be(new Uri("https://login.microsoftonline.com/common/oauth2/authorize"));
            challenge.ResourceClientId.Should().Be(ODataMcpAuthConstants.MicrosoftGraphResourceAppId);
            challenge.ResourceMetadata.Should().BeNull();
            challenge.Scope.Should().BeNull();
        }

        /// <summary>
        /// Parameter lookups on <c>Parameters</c> ignore case and every parsed parameter is present.
        /// </summary>
        [TestMethod]
        public void Parse_GraphChallenge_ExposesEveryParameterCaseInsensitively()
        {
            var challenge = WwwAuthenticateParser.Parse(GraphChallenge);

            challenge.Parameters.Count.Should().Be(3);
            challenge.Parameters.ContainsKey("REALM").Should().BeTrue();
            challenge.Parameters.ContainsKey("Authorization_Uri").Should().BeTrue();
            challenge.Parameters[ODataMcpAuthConstants.ClientIdParameter].Should().Be(ODataMcpAuthConstants.MicrosoftGraphResourceAppId);
        }

        /// <summary>
        /// Parameter names are matched case-insensitively when the typed properties are populated.
        /// </summary>
        [TestMethod]
        public void Parse_MixedCaseParameterNames_PopulatesTypedProperties()
        {
            var challenge = WwwAuthenticateParser.Parse("Bearer ReAlM=\"api\", SCOPE=\"read write\"");

            challenge.Realm.Should().Be("api");
            challenge.Scope.Should().Be("read write");
        }

        /// <summary>
        /// Two auth-params that are not comma separated are malformed.
        /// </summary>
        [TestMethod]
        public void Parse_MissingCommaBetweenParameters_ThrowsFormatException()
        {
            Action act = () => WwwAuthenticateParser.Parse("Bearer realm=\"a\" scope=\"s\"");

            act.Should().Throw<FormatException>();
        }

        /// <summary>
        /// A <see langword="null"/> header value is rejected before any parsing happens.
        /// </summary>
        [TestMethod]
        public void Parse_NullHeaderValue_ThrowsArgumentNullException()
        {
            Action act = () => WwwAuthenticateParser.Parse(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// A parameter without a value is malformed.
        /// </summary>
        [TestMethod]
        public void Parse_ParameterWithNoValue_ThrowsFormatException()
        {
            Action act = () => WwwAuthenticateParser.Parse("Bearer scope=\"s\", realm=");

            act.Should().Throw<FormatException>();
        }

        /// <summary>
        /// A relative <c>resource_metadata</c> value stays in <c>Parameters</c> but does not produce a typed URI.
        /// </summary>
        [TestMethod]
        public void Parse_RelativeResourceMetadata_LeavesTypedPropertyNull()
        {
            var challenge = WwwAuthenticateParser.Parse("Bearer resource_metadata=\"/.well-known/oauth-protected-resource\"");

            challenge.ResourceMetadata.Should().BeNull();
            challenge.Parameters[ODataMcpAuthConstants.ResourceMetadataParameter].Should().Be("/.well-known/oauth-protected-resource");
        }

        /// <summary>
        /// An RFC 9728 challenge yields the protected resource metadata URI and the requested scope.
        /// </summary>
        [TestMethod]
        public void Parse_ResourceMetadataChallenge_ReturnsMetadataUriAndScope()
        {
            var challenge = WwwAuthenticateParser.Parse(ResourceMetadataChallenge);

            challenge.Scheme.Should().Be(ODataMcpAuthConstants.BearerScheme);
            challenge.ResourceMetadata.Should().Be(new Uri("https://localhost/.well-known/oauth-protected-resource/odata"));
            challenge.Scope.Should().Be("read");
            challenge.AuthorizationUri.Should().BeNull();
            challenge.ResourceClientId.Should().BeNull();
        }

        /// <summary>
        /// A header value carrying two challenges returns the first one from <c>Parse</c>.
        /// </summary>
        [TestMethod]
        public void Parse_TwoChallengesInOneValue_ReturnsTheFirst()
        {
            var challenge = WwwAuthenticateParser.Parse("Bearer realm=\"a\", scope=\"s\", Basic realm=\"b\"");

            challenge.Scheme.Should().Be(ODataMcpAuthConstants.BearerScheme);
            challenge.Realm.Should().Be("a");
            challenge.Scope.Should().Be("s");
        }

        /// <summary>
        /// Unquoted token values are supported alongside quoted-strings.
        /// </summary>
        [TestMethod]
        public void Parse_UnquotedTokenValues_ReturnsValues()
        {
            var challenge = WwwAuthenticateParser.Parse("Bearer realm=api, scope=read");

            challenge.Realm.Should().Be("api");
            challenge.Scope.Should().Be("read");
            challenge.Parameters.Count.Should().Be(2);
        }

        /// <summary>
        /// A quoted-string that is never closed is malformed.
        /// </summary>
        [TestMethod]
        public void Parse_UnterminatedQuotedString_ThrowsFormatException()
        {
            Action act = () => WwwAuthenticateParser.Parse("Bearer realm=\"unterminated");

            act.Should().Throw<FormatException>();
        }

        /// <summary>
        /// A whitespace-only header value is rejected before any parsing happens.
        /// </summary>
        [TestMethod]
        public void Parse_WhitespaceHeaderValue_ThrowsArgumentException()
        {
            Action act = () => WwwAuthenticateParser.Parse("   ");

            act.Should().ThrowExactly<ArgumentException>();
        }

        /// <summary>
        /// A blank entry in a header value sequence contributes no challenge.
        /// </summary>
        [TestMethod]
        public void ParseAll_BlankHeaderValues_ReturnsEmptyList()
        {
            var challenges = WwwAuthenticateParser.ParseAll(new List<string> { "  ", string.Empty });

            challenges.Should().BeEmpty();
        }

        /// <summary>
        /// A comma inside a quoted-string does not start a new challenge.
        /// </summary>
        [TestMethod]
        public void ParseAll_CommaInsideQuotedValue_ReturnsSingleChallenge()
        {
            var challenges = WwwAuthenticateParser.ParseAll("Bearer scope=\"read, write\", realm=\"api\"");

            challenges.Count.Should().Be(1);
            challenges[0].Scope.Should().Be("read, write");
            challenges[0].Realm.Should().Be("api");
        }

        /// <summary>
        /// Separate header values each contribute their own challenges.
        /// </summary>
        [TestMethod]
        public void ParseAll_MultipleHeaderValues_ReturnsChallengeFromEachValue()
        {
            var challenges = WwwAuthenticateParser.ParseAll(new List<string> { "Basic realm=\"b\"", ResourceMetadataChallenge });

            challenges.Count.Should().Be(2);
            challenges[0].Scheme.Should().Be(ODataMcpAuthConstants.BasicScheme);
            challenges[1].Scheme.Should().Be(ODataMcpAuthConstants.BearerScheme);
            challenges[1].Scope.Should().Be("read");
        }

        /// <summary>
        /// A <see langword="null"/> header value sequence is rejected before any parsing happens.
        /// </summary>
        [TestMethod]
        public void ParseAll_NullValues_ThrowsArgumentNullException()
        {
            Action act = () => WwwAuthenticateParser.ParseAll((IEnumerable<string>)null!);

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// Two challenges in a single header value are split on the scheme that follows a comma.
        /// </summary>
        [TestMethod]
        public void ParseAll_TwoChallengesInOneValue_ReturnsBoth()
        {
            var challenges = WwwAuthenticateParser.ParseAll("Bearer realm=\"a\", scope=\"s\", Basic realm=\"b\"");

            challenges.Count.Should().Be(2);
            challenges[0].Scheme.Should().Be(ODataMcpAuthConstants.BearerScheme);
            challenges[0].Realm.Should().Be("a");
            challenges[0].Scope.Should().Be("s");
            challenges[0].Parameters.Count.Should().Be(2);
            challenges[1].Scheme.Should().Be(ODataMcpAuthConstants.BasicScheme);
            challenges[1].Realm.Should().Be("b");
            challenges[1].Parameters.Count.Should().Be(1);
        }

        /// <summary>
        /// The challenge type exposes the resource app id only as <c>ResourceClientId</c>, never as a <c>ClientId</c>.
        /// </summary>
        [TestMethod]
        public void ResourceClientId_IsTheOnlyClientIdShapedMember()
        {
            var names = typeof(OAuthChallenge).GetMembers().Select(member => member.Name).ToList();

            names.Should().Contain(nameof(OAuthChallenge.ResourceClientId));
            names.Should().NotContain("ClientId");
        }

        /// <summary>
        /// The first <c>Bearer</c> challenge wins, whatever its position in the list.
        /// </summary>
        [TestMethod]
        public void SelectBearer_MixedChallenges_ReturnsFirstBearer()
        {
            var challenges = WwwAuthenticateParser.ParseAll("Bearer realm=\"a\", scope=\"s\", Basic realm=\"b\"");

            var bearer = WwwAuthenticateParser.SelectBearer(challenges);

            bearer.Should().NotBeNull();
            bearer!.Scheme.Should().Be(ODataMcpAuthConstants.BearerScheme);
            bearer.Realm.Should().Be("a");
        }

        /// <summary>
        /// A lowercase scheme still matches the <c>Bearer</c> scheme.
        /// </summary>
        [TestMethod]
        public void SelectBearer_LowercaseScheme_ReturnsChallenge()
        {
            var challenges = WwwAuthenticateParser.ParseAll("bearer realm=\"a\"");

            var bearer = WwwAuthenticateParser.SelectBearer(challenges);

            bearer.Should().NotBeNull();
            bearer!.Realm.Should().Be("a");
        }

        /// <summary>
        /// A list without a <c>Bearer</c> challenge selects nothing.
        /// </summary>
        [TestMethod]
        public void SelectBearer_NoBearerChallenge_ReturnsNull()
        {
            var challenges = WwwAuthenticateParser.ParseAll("Basic realm=\"x\"");

            WwwAuthenticateParser.SelectBearer(challenges).Should().BeNull();
        }

        /// <summary>
        /// A <see langword="null"/> challenge list is rejected before any selection happens.
        /// </summary>
        [TestMethod]
        public void SelectBearer_NullChallenges_ThrowsArgumentNullException()
        {
            Action act = () => WwwAuthenticateParser.SelectBearer(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        #endregion

    }

}
