// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Locks the outbound OAuth scope resolution behavior of <see cref="ScopeResolver"/> against the discovery
    /// algorithm's step 8, including the SDK 2.2 <c>ScopeSelectorDelegate</c> hand-off.
    /// </summary>
    [TestClass]
    public class ScopeResolverTests
    {

        #region Public Methods

        /// <summary>
        /// A base scope that already carries <c>offline_access</c> is left exactly as it is; the scope is never
        /// duplicated.
        /// </summary>
        [TestMethod]
        public void AppendScope_AlreadyPresent_ReturnsUnchanged()
        {
            ScopeResolver.AppendScope("read offline_access", ODataMcpAuthConstants.OfflineAccessScope).Should().Be("read offline_access");
        }

        /// <summary>
        /// Appending to a <see langword="null"/> base scope produces exactly the appended scope, with no leading
        /// space.
        /// </summary>
        [TestMethod]
        public void AppendScope_NullBase_ReturnsNameAlone()
        {
            ScopeResolver.AppendScope(null, ODataMcpAuthConstants.OfflineAccessScope).Should().Be(ODataMcpAuthConstants.OfflineAccessScope);
        }

        /// <summary>
        /// Appending a scope not already present adds it after a single space.
        /// </summary>
        [TestMethod]
        public void AppendScope_NotPresent_AppendsWithSingleSpace()
        {
            ScopeResolver.AppendScope("read", ODataMcpAuthConstants.OfflineAccessScope).Should().Be($"read {ODataMcpAuthConstants.OfflineAccessScope}");
        }

        /// <summary>
        /// A space-separated scope string is searched token by token, so <c>read</c> does not falsely match a
        /// search for <c>read_write</c>.
        /// </summary>
        [TestMethod]
        public void ContainsScope_MatchesWholeTokenOnly()
        {
            ScopeResolver.ContainsScope("read write", "read").Should().BeTrue();
            ScopeResolver.ContainsScope("read write", "read_write").Should().BeFalse();
        }

        /// <summary>
        /// A <see langword="null"/> or white-space scope string contains nothing.
        /// </summary>
        [TestMethod]
        public void ContainsScope_NullOrWhiteSpace_ReturnsFalse()
        {
            ScopeResolver.ContainsScope(null, "offline_access").Should().BeFalse();
            ScopeResolver.ContainsScope("   ", "offline_access").Should().BeFalse();
        }

        /// <summary>
        /// Joining an empty scope sequence, or one whose entries are all white space, yields <see langword="null"/>.
        /// </summary>
        [TestMethod]
        public void JoinScopes_EmptyOrWhiteSpaceOnly_ReturnsNull()
        {
            ScopeResolver.JoinScopes([]).Should().BeNull();
            ScopeResolver.JoinScopes([" ", ""]).Should().BeNull();
        }

        /// <summary>
        /// A <see langword="null"/> scope sequence joins to <see langword="null"/>.
        /// </summary>
        [TestMethod]
        public void JoinScopes_Null_ReturnsNull()
        {
            ScopeResolver.JoinScopes(null).Should().BeNull();
        }

        /// <summary>
        /// Joining scopes space-delimits them in order, skipping blank entries.
        /// </summary>
        [TestMethod]
        public void JoinScopes_Scopes_JoinsWithSpaces()
        {
            ScopeResolver.JoinScopes(["read", "", "write"]).Should().Be("read write");
        }

        /// <summary>
        /// Neither a challenge scope, protected resource metadata scopes, nor <c>--scopes</c> is present, and the
        /// authorization server advertises nothing that implies <c>offline_access</c>: the resolved scope is
        /// <see langword="null"/>. When the authorization server advertises the <c>refresh_token</c> grant
        /// instead, the resolved scope becomes exactly <c>offline_access</c>.
        /// </summary>
        [TestMethod]
        public void Resolve_Nothing_ReturnsNull_UnlessOfflineAccessAdvertised()
        {
            var resolver = new ScopeResolver(NullLogger());
            var options = new OutboundOAuthOptions();

            var withNothingAdvertised = CreateDiscovery();
            resolver.Resolve(withNothingAdvertised, options).Should().BeNull();

            var withRefreshTokenAdvertised = CreateDiscovery(grantTypesSupported: [ODataMcpAuthConstants.GrantTypeRefreshToken]);
            resolver.Resolve(withRefreshTokenAdvertised, options).Should().Be(ODataMcpAuthConstants.OfflineAccessScope);
        }

        /// <summary>
        /// When the authorization server advertises the <c>offline_access</c> scope, it is appended to the base
        /// scope exactly once, even when the base scope already carries it.
        /// </summary>
        [TestMethod]
        public void Resolve_AppendsOfflineAccess_WhenAdvertised_NoDuplicate()
        {
            var resolver = new ScopeResolver(NullLogger());
            var discovery = CreateDiscovery(scopesSupported: [ODataMcpAuthConstants.OfflineAccessScope]);

            var withoutOfflineAccess = new OutboundOAuthOptions
            {
                Scopes = ["read"]
            };
            resolver.Resolve(discovery, withoutOfflineAccess).Should().Be($"read {ODataMcpAuthConstants.OfflineAccessScope}");

            var withOfflineAccessAlready = new OutboundOAuthOptions
            {
                Scopes = ["read", ODataMcpAuthConstants.OfflineAccessScope]
            };
            resolver.Resolve(discovery, withOfflineAccessAlready).Should().Be($"read {ODataMcpAuthConstants.OfflineAccessScope}");
        }

        /// <summary>
        /// A challenge scope always wins over protected resource metadata scopes and <c>--scopes</c>, per step 8
        /// of the discovery algorithm.
        /// </summary>
        [TestMethod]
        public void Resolve_ChallengeScope_WinsOverPrmAndOptions()
        {
            var resolver = new ScopeResolver(NullLogger());
            var protectedResource = new SdkAuth.ProtectedResourceMetadata
            {
                ScopesSupported = ["prm-scope"]
            };
            var options = new OutboundOAuthOptions
            {
                Scopes = ["options-scope"]
            };
            var discovery = CreateDiscovery(challengeScope: "challenge-scope", protectedResource: protectedResource);

            var scope = resolver.Resolve(discovery, options);

            scope.Should().Be("challenge-scope");
        }

        /// <summary>
        /// A <see langword="null"/> <paramref name="discovery"/> is rejected before any resolution happens.
        /// </summary>
        [TestMethod]
        public void Resolve_NullDiscovery_Throws()
        {
            var resolver = new ScopeResolver(NullLogger());

            var act = () => resolver.Resolve(null!, new OutboundOAuthOptions());

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// Protected resource metadata <c>scopes_supported</c> is used when there is no challenge scope, and it
        /// still wins over <c>--scopes</c>.
        /// </summary>
        [TestMethod]
        public void Resolve_PrmScopes_WhenNoChallenge()
        {
            var resolver = new ScopeResolver(NullLogger());
            var protectedResource = new SdkAuth.ProtectedResourceMetadata
            {
                ScopesSupported = ["prm-scope-a", "prm-scope-b"]
            };
            var options = new OutboundOAuthOptions
            {
                Scopes = ["options-scope"]
            };
            var discovery = CreateDiscovery(protectedResource: protectedResource);

            var scope = resolver.Resolve(discovery, options);

            scope.Should().Be("prm-scope-a prm-scope-b");
        }

        /// <summary>
        /// <c>--scopes</c> is used only when neither a challenge scope nor protected resource metadata scopes
        /// are available.
        /// </summary>
        [TestMethod]
        public void Resolve_OptionsScopes_WhenNoChallengeOrPrm()
        {
            var resolver = new ScopeResolver(NullLogger());
            var options = new OutboundOAuthOptions
            {
                Scopes = ["options-scope-a", "options-scope-b"]
            };
            var discovery = CreateDiscovery();

            var scope = resolver.Resolve(discovery, options);

            scope.Should().Be("options-scope-a options-scope-b");
        }

        /// <summary>
        /// A scope selector may append a scope the base resolution never proposed, and the resolved scope
        /// carries it.
        /// </summary>
        [TestMethod]
        public void Resolve_ScopeSelectorAddingScope_Applied()
        {
            var resolver = new ScopeResolver(NullLogger());
            var options = new OutboundOAuthOptions
            {
                Scopes = ["read"],
                ScopeSelector = scope => [.. scope ?? [], "extra-scope"]
            };
            var discovery = CreateDiscovery();

            var scope = resolver.Resolve(discovery, options);

            scope.Should().Be("read extra-scope");
        }

        /// <summary>
        /// A scope selector that strips <c>offline_access</c> out of a scope that carried it before the selector
        /// ran logs a warning naming the consequence, per step 8.
        /// </summary>
        [TestMethod]
        public void Resolve_ScopeSelectorRemovingOfflineAccess_LogsWarning()
        {
            var logs = new CapturingLoggerProvider();
            var resolver = new ScopeResolver(CreateLogger(logs));
            var options = new OutboundOAuthOptions
            {
                Scopes = ["read"],
                ScopeSelector = scope => (scope ?? []).Where(value => value != ODataMcpAuthConstants.OfflineAccessScope)
            };
            var discovery = CreateDiscovery(grantTypesSupported: [ODataMcpAuthConstants.GrantTypeRefreshToken]);

            var scope = resolver.Resolve(discovery, options);

            scope.Should().Be("read");
            logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning);
            logs.AllText.Should().Contain("removed offline_access");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a minimal <see cref="OAuthDiscoveryResult"/> for scope resolution tests, defaulting to an
        /// authorization server and protected resource that advertise nothing.
        /// </summary>
        /// <param name="challengeScope">The challenge scope to carry, or <see langword="null"/> for none.</param>
        /// <param name="protectedResource">The protected resource metadata to carry, or <see langword="null"/> for none.</param>
        /// <param name="grantTypesSupported">The grant types the authorization server advertises.</param>
        /// <param name="scopesSupported">The scopes the authorization server advertises.</param>
        /// <returns>
        /// The discovery result.
        /// </returns>
        internal static OAuthDiscoveryResult CreateDiscovery(
            string? challengeScope = null,
            SdkAuth.ProtectedResourceMetadata? protectedResource = null,
            IReadOnlyList<string>? grantTypesSupported = null,
            IReadOnlyList<string>? scopesSupported = null)
        {
            var authorizationServer = new AuthorizationServerMetadata
            {
                GrantTypesSupported = grantTypesSupported is null ? null : [.. grantTypesSupported],
                Issuer = "https://as.example/",
                ScopesSupported = scopesSupported is null ? null : [.. scopesSupported],
                TokenEndpoint = new Uri("https://as.example/token")
            };

            return new OAuthDiscoveryResult(
                challenge: null,
                protectedResource: protectedResource,
                authorizationServer: authorizationServer,
                authorizationServerBase: new Uri("https://as.example/"),
                advertisedGrants: grantTypesSupported ?? [],
                challengeScope: challengeScope,
                resource: new Uri("https://resource.example/"));
        }

        /// <summary>
        /// Creates an <see cref="ILogger{TCategoryName}"/> that appends every message to <paramref name="logs"/>.
        /// </summary>
        /// <param name="logs">The provider every message is captured to.</param>
        /// <returns>
        /// The logger.
        /// </returns>
        internal static ILogger<ScopeResolver> CreateLogger(CapturingLoggerProvider logs)
        {
            return new Logger<ScopeResolver>(new LoggerFactory([logs]));
        }

        /// <summary>
        /// Creates an <see cref="ILogger{TCategoryName}"/> that discards every message, for tests that do not
        /// assert on logging.
        /// </summary>
        /// <returns>
        /// The logger.
        /// </returns>
        internal static ILogger<ScopeResolver> NullLogger()
        {
            return new Logger<ScopeResolver>(new LoggerFactory());
        }

        #endregion

    }

}
