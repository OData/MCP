// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Selects the <see cref="OutboundGrantKind"/> outbound OAuth runs for a remote OData service, per step 6 of
    /// the discovery algorithm: an operator override when advertised and implemented, else enterprise identity
    /// assertion, else non-interactive client credentials, else the interactive default of device code falling
    /// back to authorization code.
    /// </summary>
    /// <example>
    /// <code>
    /// var kind = GrantSelector.Select(discovery, options, interactive: true);
    /// if (kind == OutboundGrantKind.DeviceCode)
    /// {
    ///     // Run the device code grant against discovery.AuthorizationServer.
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// This type never runs a grant itself; it only decides which one a caller should run. Per <c>AUTH-5</c> it
    /// is a top-level type, and per <c>specs/v3/AUTHENTICATION.md</c> it throws
    /// <see cref="NotSupportedException"/> — never silently substituting another grant — for a kind this build
    /// has not registered a grant class for yet, so a PR that lands a new grant only has to add it to
    /// <see cref="Implemented"/>.
    /// </remarks>
    public static class GrantSelector
    {

        #region Fields

        /// <summary>
        /// The grant kinds this build can actually run.
        /// </summary>
        /// <remarks>
        /// Grew one <see cref="OutboundGrantKind"/> at a time as each PR landed its grant class:
        /// <see cref="OutboundGrantKind.AuthorizationCode"/> in PR 5,
        /// <see cref="OutboundGrantKind.ClientCredentials"/> in PR 6, and
        /// <see cref="OutboundGrantKind.IdentityAssertion"/> in PR 9. Every kind is now registered, so
        /// <see cref="RequireImplemented(OutboundGrantKind)"/> no longer refuses anything — it stays as the
        /// guard a future kind would trip.
        /// </remarks>
        public static readonly IReadOnlySet<OutboundGrantKind> Implemented = new HashSet<OutboundGrantKind>
        {
            OutboundGrantKind.AuthorizationCode,
            OutboundGrantKind.ClientCredentials,
            OutboundGrantKind.DeviceCode,
            OutboundGrantKind.IdentityAssertion,
            OutboundGrantKind.RefreshToken
        };

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether a grant kind is registered in this build.
        /// </summary>
        /// <param name="kind">The grant kind to check.</param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="kind"/> is in <see cref="Implemented"/>.
        /// </returns>
        public static bool IsImplemented(OutboundGrantKind kind)
        {
            return Implemented.Contains(kind);
        }

        /// <summary>
        /// Selects the grant kind to run for a discovered authorization server, per step 6 of the discovery
        /// algorithm.
        /// </summary>
        /// <param name="discovery">The discovery result whose advertised grants and authorization server are consulted.</param>
        /// <param name="options">The operator's outbound OAuth settings.</param>
        /// <param name="interactive">Whether this process can run an interactive grant, such as device code.</param>
        /// <returns>
        /// The selected grant kind.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="OutboundOAuthOptions.Grant"/> is <see cref="OutboundGrantKind.RefreshToken"/>,
        /// which is never an operator-selectable grant.
        /// </exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when <see cref="OutboundOAuthOptions.Grant"/> is set to a grant the authorization server does
        /// not advertise, when identity assertion is configured but the authorization server does not advertise
        /// the RFC 7523 JWT bearer grant type, or when nothing usable is advertised and none of the automatic
        /// selection rules apply.
        /// </exception>
        /// <exception cref="NotSupportedException">Thrown when the grant that would otherwise be selected is not in <see cref="Implemented"/>.</exception>
        /// <example>
        /// <code>
        /// var kind = GrantSelector.Select(discovery, options, interactive: !Console.IsInputRedirected);
        /// </code>
        /// </example>
        /// <remarks>
        /// Selection order is: (1) an explicit <see cref="OutboundOAuthOptions.Grant"/> override; (2) enterprise
        /// identity assertion when an IdP endpoint and an id token source are both configured, which fails first
        /// rather than silently falling through when the authorization server does not advertise the RFC 7523
        /// JWT bearer grant type; (3) non-interactive client
        /// credentials when advertised and a client secret is configured; (4) the interactive default of device
        /// code, falling back to authorization code, when <paramref name="interactive"/> is <see langword="true"/>.
        /// </remarks>
        public static OutboundGrantKind Select(OAuthDiscoveryResult discovery, OutboundOAuthOptions options, bool interactive)
        {
            ArgumentNullException.ThrowIfNull(discovery);
            ArgumentNullException.ThrowIfNull(options);

            if (options.Grant is { } grant)
            {
                return SelectExplicit(discovery, grant);
            }

            if (IsIdentityAssertionConfigured(options))
            {
                if (!IsAdvertised(discovery, OutboundGrantKind.IdentityAssertion))
                {
                    throw new OutboundDiscoveryException($"Identity assertion is configured but {discovery.AuthorizationServer.Issuer} does not advertise {ODataMcpAuthConstants.JwtBearerGrantType}; pass --grant or fix --idp-url.");
                }

                return RequireImplemented(OutboundGrantKind.IdentityAssertion);
            }

            if (!interactive && !string.IsNullOrWhiteSpace(options.ClientSecret) && IsAdvertised(discovery, OutboundGrantKind.ClientCredentials))
            {
                return RequireImplemented(OutboundGrantKind.ClientCredentials);
            }

            if (interactive)
            {
                if (IsAdvertised(discovery, OutboundGrantKind.DeviceCode))
                {
                    return RequireImplemented(OutboundGrantKind.DeviceCode);
                }

                if (IsAdvertised(discovery, OutboundGrantKind.AuthorizationCode))
                {
                    return RequireImplemented(OutboundGrantKind.AuthorizationCode);
                }
            }

            throw new OutboundDiscoveryException("Authorization server advertises no grant this client can use; pass --grant or --auth-server.");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Determines whether a grant kind is advertised, per RFC 8414 section 2: a server that publishes no
        /// <c>grant_types_supported</c> at all is treated as advertising exactly its default,
        /// <c>authorization_code</c> and <c>implicit</c>.
        /// </summary>
        /// <param name="discovery">The discovery result whose <see cref="OAuthDiscoveryResult.AdvertisedGrants"/> is consulted.</param>
        /// <param name="kind">The grant kind to look for.</param>
        /// <returns>
        /// <see langword="true"/> when the authorization server advertises <paramref name="kind"/>, treating the
        /// RFC 8628 short form and URN spellings of device code as equivalent.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="kind"/> is not a defined <see cref="OutboundGrantKind"/> value.</exception>
        /// <remarks>
        /// Every comparison against <see cref="OAuthDiscoveryResult.AdvertisedGrants"/> is
        /// <see cref="StringComparison.Ordinal"/>, matching the rest of this package's wire-name comparisons.
        /// </remarks>
        internal static bool IsAdvertised(OAuthDiscoveryResult discovery, OutboundGrantKind kind)
        {
            ArgumentNullException.ThrowIfNull(discovery);

            IReadOnlyList<string> grants = discovery.AdvertisedGrants.Count == 0
                ? [ODataMcpAuthConstants.GrantTypeAuthorizationCode, "implicit"]
                : discovery.AdvertisedGrants;

            return kind switch
            {
                OutboundGrantKind.DeviceCode => grants.Contains(ODataMcpAuthConstants.DeviceCodeGrantType, StringComparer.Ordinal)
                    || grants.Contains(ODataMcpAuthConstants.GrantTypeDeviceCode, StringComparer.Ordinal),
                OutboundGrantKind.AuthorizationCode => grants.Contains(ODataMcpAuthConstants.GrantTypeAuthorizationCode, StringComparer.Ordinal),
                OutboundGrantKind.ClientCredentials => grants.Contains(ODataMcpAuthConstants.GrantTypeClientCredentials, StringComparer.Ordinal),
                OutboundGrantKind.IdentityAssertion => grants.Contains(ODataMcpAuthConstants.JwtBearerGrantType, StringComparer.Ordinal),
                OutboundGrantKind.RefreshToken => grants.Contains(ODataMcpAuthConstants.GrantTypeRefreshToken, StringComparer.Ordinal),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The grant kind is not a defined OutboundGrantKind value.")
            };
        }

        /// <summary>
        /// Determines whether the operator configured enough to attempt the enterprise identity assertion grant.
        /// </summary>
        /// <param name="options">The operator's outbound OAuth settings.</param>
        /// <returns>
        /// <see langword="true"/> when an IdP endpoint (<see cref="OutboundOAuthOptions.IdpUrl"/> or
        /// <see cref="OutboundOAuthOptions.IdpTokenEndpoint"/>) is set and an id token source
        /// (<see cref="OutboundOAuthOptions.IdpIdTokenFile"/> or <see cref="OutboundOAuthOptions.HasEnvironmentIdToken"/>)
        /// is available.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
        internal static bool IsIdentityAssertionConfigured(OutboundOAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return (options.IdpUrl is not null || options.IdpTokenEndpoint is not null)
                && (!string.IsNullOrWhiteSpace(options.IdpIdTokenFile) || options.HasEnvironmentIdToken);
        }

        /// <summary>
        /// Returns <paramref name="kind"/> when it is implemented, or throws when it is not.
        /// </summary>
        /// <param name="kind">The grant kind that selection is about to return.</param>
        /// <returns>
        /// <paramref name="kind"/>.
        /// </returns>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="kind"/> is not in <see cref="Implemented"/>.</exception>
        internal static OutboundGrantKind RequireImplemented(OutboundGrantKind kind)
        {
            if (!IsImplemented(kind))
            {
                throw new NotSupportedException($"Grant '{OutboundGrantKindParser.ToWireName(kind)}' is not implemented in this build.");
            }

            return kind;
        }

        /// <summary>
        /// Resolves an operator's explicit <c>--grant</c> override.
        /// </summary>
        /// <param name="discovery">The discovery result whose advertised grants are consulted.</param>
        /// <param name="grant">The operator's <c>--grant</c> value.</param>
        /// <returns>
        /// <paramref name="grant"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="grant"/> is <see cref="OutboundGrantKind.RefreshToken"/>.</exception>
        /// <exception cref="OutboundDiscoveryException">Thrown when the authorization server does not advertise <paramref name="grant"/>.</exception>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="grant"/> is not in <see cref="Implemented"/>.</exception>
        internal static OutboundGrantKind SelectExplicit(OAuthDiscoveryResult discovery, OutboundGrantKind grant)
        {
            if (grant == OutboundGrantKind.RefreshToken)
            {
                throw new ArgumentException("Grant 'refresh_token' cannot be selected explicitly; it is only ever chosen internally once a refresh token is cached.");
            }

            if (!IsAdvertised(discovery, grant))
            {
                throw new OutboundDiscoveryException($"Grant '{OutboundGrantKindParser.ToWireName(grant)}' is not advertised by {discovery.AuthorizationServer.Issuer}.");
            }

            return RequireImplemented(grant);
        }

        #endregion

    }

}
