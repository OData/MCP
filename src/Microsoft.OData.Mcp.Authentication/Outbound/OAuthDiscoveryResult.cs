// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Everything the outbound OAuth discovery algorithm learned about a protected OData service: the
    /// <c>WWW-Authenticate</c> challenge it started from, the RFC 9728 protected resource metadata it found (if
    /// any), the authorization server it selected, and the grants, scope, and RFC 8707 resource a grant needs.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = await discovery.DiscoverAsync(serviceRoot, wwwAuthenticate, 401, null, null, cancellationToken);
    ///
    /// Console.WriteLine(result.AuthorizationServer.TokenEndpoint);
    /// Console.WriteLine(result.Resource);
    /// if (result.AdvertisedGrants.Contains(ODataMcpAuthConstants.DeviceCodeGrantType))
    /// {
    ///     // Start a device code grant against result.AuthorizationServer.
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// Produced exclusively by <see cref="OAuthDiscovery.DiscoverAsync(Uri, IReadOnlyList{string}, int, Uri, Uri, System.Threading.CancellationToken)"/>,
    /// which never returns an instance without an authorization server: discovery fails first rather than
    /// handing a caller a half-populated result. Per <c>AUTH-13</c> the challenge <c>client_id</c> is reachable
    /// only as <see cref="OAuthChallenge.ResourceClientId"/> through <see cref="Challenge"/>, so nothing on this
    /// type can be mistaken for the client id this process authenticates with.
    /// </remarks>
    public sealed class OAuthDiscoveryResult
    {

        #region Properties

        /// <summary>
        /// Gets the grant types <see cref="AuthorizationServer"/> advertises, or an empty list when it
        /// advertises none.
        /// </summary>
        /// <remarks>
        /// Never <see langword="null"/>, so grant selection can enumerate it without a null check.
        /// </remarks>
        public IReadOnlyList<string> AdvertisedGrants { get; }

        /// <summary>
        /// Gets the authorization server metadata document discovery selected.
        /// </summary>
        /// <remarks>
        /// Guaranteed to carry an absolute <c>token_endpoint</c>, because
        /// <see cref="AuthorizationServerMetadataClient"/> rejects a document without one.
        /// </remarks>
        public AuthorizationServerMetadata AuthorizationServer { get; }

        /// <summary>
        /// Gets the candidate base URI that <see cref="AuthorizationServer"/> was discovered from.
        /// </summary>
        /// <remarks>
        /// This is the operator's <c>--auth-server</c> value, a protected resource metadata
        /// <c>authorization_servers</c> entry, or a remainder stripped from the challenge
        /// <c>authorization_uri</c>; it is not necessarily the issuer the document advertises.
        /// </remarks>
        public Uri AuthorizationServerBase { get; }

        /// <summary>
        /// Gets the <c>Bearer</c> challenge discovery started from, or <see langword="null"/> when the response
        /// carried no <c>Bearer</c> challenge.
        /// </summary>
        public OAuthChallenge? Challenge { get; }

        /// <summary>
        /// Gets the space-delimited <c>scope</c> the challenge asked for, or <see langword="null"/> when it
        /// asked for none.
        /// </summary>
        /// <remarks>
        /// This is the highest priority input to scope resolution per step 8 of the discovery algorithm.
        /// </remarks>
        public string? ChallengeScope { get; }

        /// <summary>
        /// Gets the RFC 9728 protected resource metadata document, or <see langword="null"/> when every
        /// candidate was missing, unauthorized, or unparseable.
        /// </summary>
        /// <remarks>
        /// A <see langword="null"/> value is normal and non-fatal per <c>AUTH-14</c>; it means discovery
        /// continued from the challenge and the operator's overrides.
        /// </remarks>
        public SdkAuth.ProtectedResourceMetadata? ProtectedResource { get; }

        /// <summary>
        /// Gets the RFC 8707 resource indicator a token request should ask for, or <see langword="null"/> when
        /// no token request should carry one.
        /// </summary>
        /// <remarks>
        /// Resolved per step 8b of the discovery algorithm: the protected resource metadata <c>resource</c> when
        /// it is absolute, else the operator's <c>--resource</c>, else <see langword="null"/>. There is
        /// deliberately no origin fallback: RFC 8707 <c>resource</c> is only meaningful when the resource itself
        /// published it or the operator named it, and an invented audience is rejected by strict authorization
        /// servers that would otherwise have honored <c>scope</c> alone.
        /// </remarks>
        public Uri? Resource { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthDiscoveryResult"/> class.
        /// </summary>
        /// <param name="challenge">The <c>Bearer</c> challenge discovery started from, or <see langword="null"/>.</param>
        /// <param name="protectedResource">The RFC 9728 protected resource metadata document, or <see langword="null"/>.</param>
        /// <param name="authorizationServer">The authorization server metadata document discovery selected.</param>
        /// <param name="authorizationServerBase">The candidate base URI <paramref name="authorizationServer"/> was discovered from.</param>
        /// <param name="advertisedGrants">The grant types <paramref name="authorizationServer"/> advertises.</param>
        /// <param name="challengeScope">The <c>scope</c> the challenge asked for, or <see langword="null"/>.</param>
        /// <param name="resource">The RFC 8707 resource indicator a token request should ask for, or <see langword="null"/> when it should carry none.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="authorizationServer"/>, <paramref name="authorizationServerBase"/>, or
        /// <paramref name="advertisedGrants"/> is <see langword="null"/>.
        /// </exception>
        public OAuthDiscoveryResult(
            OAuthChallenge? challenge,
            SdkAuth.ProtectedResourceMetadata? protectedResource,
            AuthorizationServerMetadata authorizationServer,
            Uri authorizationServerBase,
            IReadOnlyList<string> advertisedGrants,
            string? challengeScope,
            Uri? resource)
        {
            ArgumentNullException.ThrowIfNull(authorizationServer);
            ArgumentNullException.ThrowIfNull(authorizationServerBase);
            ArgumentNullException.ThrowIfNull(advertisedGrants);

            AdvertisedGrants = advertisedGrants;
            AuthorizationServer = authorizationServer;
            AuthorizationServerBase = authorizationServerBase;
            Challenge = challenge;
            ChallengeScope = challengeScope;
            ProtectedResource = protectedResource;
            Resource = resource;
        }

        #endregion

    }

}
