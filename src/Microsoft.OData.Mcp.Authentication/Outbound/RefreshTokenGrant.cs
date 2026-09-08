// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Runs the RFC 6749 section 6 refresh token grant: exchange the refresh token on a cached
    /// <see cref="SdkAuth.TokenContainer"/> for a fresh access token, preserving rotation and the client and
    /// server identity the container already carried.
    /// </summary>
    /// <example>
    /// <code>
    /// var refreshed = await refreshTokenGrant.RefreshAsync(metadata, current, resource, cancellationToken);
    /// await tokenCache.StoreTokensAsync(refreshed, cancellationToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>specs/v3/AUTHENTICATION.md</c> "Grants → Refresh": a response that carries a new
    /// <c>refresh_token</c> replaces the old one, and a response that omits it leaves the previous one in force
    /// — that rule lives in
    /// <see cref="TokenEndpointResponse.ToTokenContainer(string?, string?, string?, string, string?)"/>, which
    /// this class calls with the container's current refresh token. The caller owns persistence; this class
    /// never touches the cache. Per <c>AUTH-12</c> the only thing logged is the redacted shape from
    /// <see cref="OutboundAuthLogRedactor.DescribeToken(SdkAuth.TokenContainer)"/>.
    /// </remarks>
    public sealed class RefreshTokenGrant
    {

        #region Fields

        /// <summary>
        /// The logger this instance records the redacted shape of each refreshed token to.
        /// </summary>
        private readonly ILogger<RefreshTokenGrant> _logger;

        /// <summary>
        /// The client the token POST goes through.
        /// </summary>
        private readonly TokenEndpointClient _tokenEndpointClient;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshTokenGrant"/> class.
        /// </summary>
        /// <param name="tokenEndpointClient">The client the token POST goes through.</param>
        /// <param name="logger">The logger this instance records the redacted shape of each refreshed token to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokenEndpointClient"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        public RefreshTokenGrant(TokenEndpointClient tokenEndpointClient, ILogger<RefreshTokenGrant> logger)
        {
            ArgumentNullException.ThrowIfNull(tokenEndpointClient);
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
            _tokenEndpointClient = tokenEndpointClient;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Exchanges the refresh token on <paramref name="current"/> for a fresh access token.
        /// </summary>
        /// <param name="metadata">The authorization server metadata whose <c>token_endpoint</c> is posted to.</param>
        /// <param name="current">The container holding the refresh token, client identity, and authorization server to reuse.</param>
        /// <param name="resource">The RFC 8707 resource indicator, sent exactly as it was published or configured, or <see langword="null"/> when the grant sends none.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// A new container carrying the fresh access token and the effective refresh token: the rotated one when
        /// the server issued it, the previous one when the server omitted it.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> or <paramref name="current"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="current"/> carries no refresh token, no client identifier, or no
        /// authorization server that <paramref name="metadata"/> can supply either; or when
        /// <paramref name="metadata"/> carries no token endpoint.
        /// </exception>
        /// <exception cref="OAuthTokenException">
        /// Thrown when the authorization server rejects the refresh token — a rotated-away token answers
        /// <c>invalid_grant</c> — or when the request fails at the transport.
        /// </exception>
        /// <example>
        /// <code>
        /// try
        /// {
        ///     var refreshed = await refreshTokenGrant.RefreshAsync(metadata, current, resource, cancellationToken);
        ///     await tokenCache.StoreTokensAsync(refreshed, cancellationToken);
        /// }
        /// catch (OAuthTokenException ex) when (ex.Error == "invalid_grant")
        /// {
        ///     // The refresh token is gone; re-run the interactive grant.
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// The token endpoint authentication method comes from
        /// <see cref="SdkAuth.TokenContainer.TokenEndpointAuthMethod"/> when the container recorded one, so a
        /// cold start refreshes exactly the way the original grant authenticated; absent that, a container with
        /// a client secret uses <see cref="TokenEndpointClient.AuthMethodClientSecretPost"/> and one without
        /// uses <see cref="TokenEndpointClient.AuthMethodNone"/>. <c>scope</c> is echoed back only when the
        /// container recorded one, because RFC 6749 section 6 treats an absent scope as "the scope originally
        /// granted".
        /// </remarks>
        public async Task<SdkAuth.TokenContainer> RefreshAsync(AuthorizationServerMetadata metadata, SdkAuth.TokenContainer current, Uri? resource, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(current);

            if (string.IsNullOrWhiteSpace(current.RefreshToken))
            {
                throw new InvalidOperationException("No refresh token is available.");
            }

            if (string.IsNullOrWhiteSpace(current.ClientId))
            {
                throw new InvalidOperationException("No client id is available for the refresh token grant.");
            }

            if (metadata.TokenEndpoint is null)
            {
                throw new InvalidOperationException("The authorization server metadata carries no token endpoint.");
            }

            var authorizationServer = string.IsNullOrWhiteSpace(current.AuthorizationServer)
                ? metadata.Issuer
                : current.AuthorizationServer;

            if (string.IsNullOrWhiteSpace(authorizationServer))
            {
                throw new InvalidOperationException("No authorization server is available for the refresh token grant.");
            }

            var authMethod = string.IsNullOrWhiteSpace(current.TokenEndpointAuthMethod)
                ? (string.IsNullOrWhiteSpace(current.ClientSecret) ? TokenEndpointClient.AuthMethodNone : TokenEndpointClient.AuthMethodClientSecretPost)
                : current.TokenEndpointAuthMethod;

            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeRefreshToken,
                [ODataMcpAuthConstants.RefreshTokenProperty] = current.RefreshToken
            };

            if (resource is not null)
            {
                form[ODataMcpAuthConstants.ResourceParameter] = resource.OriginalString;
            }

            if (!string.IsNullOrWhiteSpace(current.Scope))
            {
                form[ODataMcpAuthConstants.ScopeParameter] = current.Scope;
            }

            var response = await _tokenEndpointClient
                .PostAsync(metadata.TokenEndpoint, form, current.ClientId, current.ClientSecret, authMethod, cancellationToken)
                .ConfigureAwait(false);

            var refreshed = response.ToTokenContainer(current.RefreshToken, current.ClientId, current.ClientSecret, authorizationServer, authMethod);

            _logger.LogInformation("Token refreshed: {Token}", OutboundAuthLogRedactor.DescribeToken(refreshed));

            return refreshed;
        }

        #endregion

    }

}
