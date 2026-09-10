// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Resolves the space-delimited OAuth <c>scope</c> a token request should ask for, per step 8 of the
    /// discovery algorithm and matching the SDK 2.2 <c>ScopeSelectorDelegate</c> order exactly.
    /// </summary>
    /// <example>
    /// <code>
    /// var resolver = new ScopeResolver(logger);
    /// var scope = resolver.Resolve(discovery, options);
    ///
    /// var device = await deviceCodeGrant.StartAsync(discovery.AuthorizationServer, clientId, scope, discovery.Resource, cancellationToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// Per Key Decision 9 of <c>specs/v3/AUTHENTICATION.md</c>, the base scope order is: the
    /// <c>WWW-Authenticate</c> challenge <c>scope</c>, then RFC 9728 protected resource metadata
    /// <c>scopes_supported</c>, then the operator's <c>--scopes</c>, then no scope at all.
    /// <c>offline_access</c> is appended whenever the authorization server or protected resource metadata
    /// advertises it, or the authorization server advertises the <c>refresh_token</c> grant, so an interactive
    /// grant can refresh instead of re-prompting. Finally, <see cref="OutboundOAuthOptions.ScopeSelector"/> — the
    /// SDK's <see cref="SdkAuth.ScopeSelectorDelegate"/> reused as-is — runs last and may remove
    /// <c>offline_access</c>, which this type logs a warning about rather than silently accepting.
    /// </remarks>
    public sealed class ScopeResolver
    {

        #region Fields

        /// <summary>
        /// The logger this instance records the <c>offline_access</c> removal warning to.
        /// </summary>
        private readonly ILogger<ScopeResolver> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeResolver"/> class.
        /// </summary>
        /// <param name="logger">The logger this instance records the <c>offline_access</c> removal warning to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
        public ScopeResolver(ILogger<ScopeResolver> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Resolves the scope a token request should ask for, per step 8 of the discovery algorithm.
        /// </summary>
        /// <param name="discovery">The discovery result whose challenge scope, protected resource metadata, and authorization server are consulted.</param>
        /// <param name="options">The operator's outbound OAuth settings, carrying the <c>--scopes</c> fallback and an optional scope selector.</param>
        /// <returns>
        /// The resolved space-delimited scope, or <see langword="null"/> when nothing resolves to a non-empty
        /// scope.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// var scope = resolver.Resolve(discovery, options); // "openid offline_access", or null
        /// </code>
        /// </example>
        /// <remarks>
        /// <see cref="OutboundOAuthOptions.ScopeSelector"/> receives the individual scope tokens as an
        /// <see cref="IReadOnlyCollection{T}"/> — exactly the SDK 2.2 <see cref="SdkAuth.ScopeSelectorDelegate"/>
        /// signature — and its returned scopes are re-joined with single spaces.
        /// </remarks>
        public string? Resolve(OAuthDiscoveryResult discovery, OutboundOAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(discovery);
            ArgumentNullException.ThrowIfNull(options);

            var scope = discovery.ChallengeScope;

            if (string.IsNullOrWhiteSpace(scope))
            {
                scope = discovery.ProtectedResource?.ScopesSupported is { Count: > 0 } prmScopes
                    ? JoinScopes(prmScopes)
                    : null;
            }

            if (string.IsNullOrWhiteSpace(scope))
            {
                scope = options.Scopes.Count > 0 ? JoinScopes(options.Scopes) : null;
            }

            if (AdvertisesOfflineAccess(discovery))
            {
                scope = AppendScope(scope, ODataMcpAuthConstants.OfflineAccessScope);
            }

            if (options.ScopeSelector is not null)
            {
                var hadOfflineAccess = ContainsScope(scope, ODataMcpAuthConstants.OfflineAccessScope);
                IReadOnlyCollection<string>? tokens = string.IsNullOrWhiteSpace(scope)
                    ? null
                    : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                scope = JoinScopes(options.ScopeSelector(tokens));

                if (hadOfflineAccess && !ContainsScope(scope, ODataMcpAuthConstants.OfflineAccessScope))
                {
                    _logger.LogWarning("Scope selector removed offline_access; the next start may require interactive sign-in.");
                }
            }

            return string.IsNullOrWhiteSpace(scope) ? null : scope;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Determines whether <c>offline_access</c> should be appended to the base scope, per step 8 of the
        /// discovery algorithm.
        /// </summary>
        /// <param name="discovery">The discovery result whose authorization server and protected resource metadata are consulted.</param>
        /// <returns>
        /// <see langword="true"/> when the authorization server advertises the <c>offline_access</c> scope or
        /// the <c>refresh_token</c> grant, or the protected resource metadata advertises the
        /// <c>offline_access</c> scope.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> is <see langword="null"/>.</exception>
        internal static bool AdvertisesOfflineAccess(OAuthDiscoveryResult discovery)
        {
            ArgumentNullException.ThrowIfNull(discovery);

            return discovery.AuthorizationServer.AdvertisesScope(ODataMcpAuthConstants.OfflineAccessScope)
                || discovery.AuthorizationServer.AdvertisesGrant(ODataMcpAuthConstants.GrantTypeRefreshToken)
                || (discovery.ProtectedResource?.ScopesSupported?.Contains(ODataMcpAuthConstants.OfflineAccessScope) ?? false);
        }

        /// <summary>
        /// Appends a scope to a space-delimited scope string, without duplicating it.
        /// </summary>
        /// <param name="scope">The base scope, or <see langword="null"/>.</param>
        /// <param name="name">The scope to append.</param>
        /// <returns>
        /// <paramref name="scope"/> unchanged when it already contains <paramref name="name"/>; otherwise
        /// <paramref name="name"/> alone when <paramref name="scope"/> is <see langword="null"/> or white space;
        /// otherwise <paramref name="scope"/> and <paramref name="name"/> joined by a single space.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty, or white space.</exception>
        internal static string AppendScope(string? scope, string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (string.IsNullOrWhiteSpace(scope))
            {
                return name;
            }

            return ContainsScope(scope, name) ? scope : $"{scope} {name}";
        }

        /// <summary>
        /// Determines whether a space-delimited scope string contains a scope, matching whole tokens only.
        /// </summary>
        /// <param name="scope">The space-delimited scope string, or <see langword="null"/>.</param>
        /// <param name="name">The scope to look for.</param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="scope"/> contains a token equal to <paramref name="name"/>
        /// under <see cref="StringComparison.Ordinal"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty, or white space.</exception>
        internal static bool ContainsScope(string? scope, string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (string.IsNullOrWhiteSpace(scope))
            {
                return false;
            }

            return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.Ordinal);
        }

        /// <summary>
        /// Joins scope tokens into a single space-delimited scope string.
        /// </summary>
        /// <param name="scopes">The scope tokens to join, or <see langword="null"/>.</param>
        /// <returns>
        /// <see langword="null"/> when <paramref name="scopes"/> is <see langword="null"/> or contains no
        /// non-white-space entry; otherwise every non-white-space entry, in order, joined by single spaces.
        /// </returns>
        internal static string? JoinScopes(IEnumerable<string>? scopes)
        {
            if (scopes is null)
            {
                return null;
            }

            var joined = string.Join(' ', scopes.Where(static scope => !string.IsNullOrWhiteSpace(scope)));

            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }

        #endregion

    }

}
