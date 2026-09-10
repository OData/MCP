// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Runs the RFC 6749 section 4.4 client credentials grant: a confidential client presents its own
    /// identifier and secret and receives an access token, with no end user and no elicitation anywhere in the
    /// path.
    /// </summary>
    /// <example>
    /// <code>
    /// var response = await grant.AcquireAsync(metadata, "daemon", clientSecret, "read", resource, cancellationToken);
    /// var token = response.ToTokenContainer(
    ///     previousRefreshToken: null,
    ///     clientId: "daemon",
    ///     clientSecret: clientSecret,
    ///     authorizationServer: metadata.Issuer!,
    ///     tokenEndpointAuthMethod: ClientCredentialsGrant.ResolveAuthMethod(metadata));
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>specs/v3/AUTHENTICATION.md</c> "Grants → Client credentials" the client authentication method is
    /// <c>client_secret_post</c> whenever the authorization server advertises it — or advertises nothing at all,
    /// which RFC 6749 leaves open — and <c>client_secret_basic</c> otherwise; v1 is secret-only, with no client
    /// certificate. Per <c>AUTH-12</c> the secret is never logged: it travels only in the form or the
    /// <c>Basic</c> header <see cref="TokenEndpointClient"/> builds, and the Information line here carries only
    /// the redacted token shape.
    /// <para>
    /// A client credentials response carries no refresh token — there is no end user session to refresh — so a
    /// caller whose access token has expired re-runs this grant rather than looking for one.
    /// </para>
    /// </remarks>
    public sealed class ClientCredentialsGrant
    {

        #region Fields

        /// <summary>
        /// The logger this instance records the redacted shape of each issued token to.
        /// </summary>
        private readonly ILogger<ClientCredentialsGrant> _logger;

        /// <summary>
        /// The client the token POST goes through.
        /// </summary>
        private readonly TokenEndpointClient _tokenEndpointClient;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientCredentialsGrant"/> class.
        /// </summary>
        /// <param name="tokenEndpointClient">The client the token POST goes through.</param>
        /// <param name="logger">The logger this instance records the redacted shape of each issued token to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokenEndpointClient"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        public ClientCredentialsGrant(TokenEndpointClient tokenEndpointClient, ILogger<ClientCredentialsGrant> logger)
        {
            ArgumentNullException.ThrowIfNull(tokenEndpointClient);
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
            _tokenEndpointClient = tokenEndpointClient;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Obtains an access token for the client itself.
        /// </summary>
        /// <param name="metadata">The authorization server metadata whose <c>token_endpoint</c> is posted to.</param>
        /// <param name="clientId">The confidential client identifier.</param>
        /// <param name="clientSecret">The client secret; never logged.</param>
        /// <param name="scope">The space-delimited scope to request, or <see langword="null"/> to request none.</param>
        /// <param name="resource">The RFC 8707 resource indicator, sent exactly as it was published or configured, or <see langword="null"/> when the request sends none.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>
        /// The successful token response, which normally carries no refresh token.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> or <paramref name="clientSecret"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="metadata"/> carries no token endpoint.</exception>
        /// <exception cref="OAuthTokenException">
        /// Thrown when the authorization server rejects the credentials — <c>invalid_client</c> — or when the
        /// request fails at the transport.
        /// </exception>
        /// <example>
        /// <code>
        /// try
        /// {
        ///     var response = await grant.AcquireAsync(metadata, clientId, clientSecret, scope, resource, cancellationToken);
        /// }
        /// catch (OAuthTokenException ex) when (ex.Error == "invalid_client")
        /// {
        ///     // ODATA_MCP_CLIENT_SECRET is wrong or the client is not registered.
        /// }
        /// </code>
        /// </example>
        public async Task<TokenEndpointResponse> AcquireAsync(
            AuthorizationServerMetadata metadata,
            string clientId,
            string clientSecret,
            string? scope,
            Uri? resource,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

            if (metadata.TokenEndpoint is null)
            {
                throw new InvalidOperationException("The authorization server metadata carries no token endpoint.");
            }

            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ODataMcpAuthConstants.GrantTypeParameter] = ODataMcpAuthConstants.GrantTypeClientCredentials
            };

            if (!string.IsNullOrWhiteSpace(scope))
            {
                form[ODataMcpAuthConstants.ScopeParameter] = scope;
            }

            if (resource is not null)
            {
                form[ODataMcpAuthConstants.ResourceParameter] = resource.OriginalString;
            }

            var response = await _tokenEndpointClient
                .PostAsync(metadata.TokenEndpoint, form, clientId, clientSecret, ResolveAuthMethod(metadata), cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Client credentials token issued by {Endpoint} for {ClientId}", metadata.TokenEndpoint, clientId);

            return response;
        }

        /// <summary>
        /// Selects the token endpoint authentication method this grant uses against an authorization server.
        /// </summary>
        /// <param name="metadata">The authorization server metadata whose <c>token_endpoint_auth_methods_supported</c> is consulted.</param>
        /// <returns>
        /// <see cref="TokenEndpointClient.AuthMethodClientSecretPost"/> when the server advertises it or
        /// advertises nothing at all; otherwise <see cref="TokenEndpointClient.AuthMethodClientSecretBasic"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Public because the caller has to persist the same method on the
        /// <c>TokenContainer</c> it stores, so a later silent re-acquisition authenticates exactly the way the
        /// first one did.
        /// </remarks>
        public static string ResolveAuthMethod(AuthorizationServerMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return metadata.TokenEndpointAuthMethodsSupported is not { } methods
                || methods.Contains(TokenEndpointClient.AuthMethodClientSecretPost)
                    ? TokenEndpointClient.AuthMethodClientSecretPost
                    : TokenEndpointClient.AuthMethodClientSecretBasic;
        }

        #endregion

    }

}
