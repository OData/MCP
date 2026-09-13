// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The RFC 6749 section 5.1 successful token response returned from a token endpoint.
    /// </summary>
    /// <example>
    /// <code>
    /// var response = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.TokenEndpointResponse);
    /// var container = response!.ToTokenContainer(
    ///     previousRefreshToken: null,
    ///     clientId: "cli",
    ///     clientSecret: null,
    ///     authorizationServer: metadata.Issuer!,
    ///     tokenEndpointAuthMethod: TokenEndpointClient.AuthMethodNone);
    /// </code>
    /// </example>
    /// <remarks>
    /// Deserialized exclusively through <see cref="OutboundOAuthJsonContext"/> (AOT: JSON source-gen; no
    /// reflection). This is the RFC 6749 wire shape only; it is <em>not</em> the SDK <c>TokenContainer</c>
    /// cache/runtime format, which is PascalCase and additionally carries <c>ObtainedAt</c>.
    /// <see cref="ToTokenContainer(string?, string?, string?, string, string?)"/> is the single place that
    /// conversion happens.
    /// </remarks>
    public sealed class TokenEndpointResponse
    {

        #region Properties

        /// <summary>
        /// Gets or sets the issued access token.
        /// </summary>
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the lifetime, in seconds, of <see cref="AccessToken"/>, or <see langword="null"/> when
        /// the server did not advertise one.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the refresh token, or <see langword="null"/> when the server did not issue one.
        /// </summary>
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the space-delimited scope actually granted, or <see langword="null"/> when the server
        /// did not echo it back.
        /// </summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        /// <summary>
        /// Gets or sets the token type, such as <c>Bearer</c>.
        /// </summary>
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        #endregion

        #region Public Methods

        /// <summary>
        /// Maps this wire response onto the SDK <see cref="SdkAuth.TokenContainer"/> the cache and the outbound
        /// handler read, stamping <see cref="SdkAuth.TokenContainer.ObtainedAt"/> at the moment of the call.
        /// </summary>
        /// <param name="previousRefreshToken">The refresh token the caller already holds, kept when this response omits one; or <see langword="null"/> when there is none.</param>
        /// <param name="clientId">The client identifier the tokens were issued to, persisted so a cold start can refresh without re-registering.</param>
        /// <param name="clientSecret">The client secret, or <see langword="null"/> for a public client.</param>
        /// <param name="authorizationServer">The issuer the tokens came from.</param>
        /// <param name="tokenEndpointAuthMethod">The token endpoint authentication method the grant used, or <see langword="null"/> when none applies.</param>
        /// <returns>
        /// A container carrying this response's access token, lifetime, and scope, the effective refresh token,
        /// and the client and server identity the caller supplied.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="authorizationServer"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="AccessToken"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <example>
        /// <code>
        /// var refreshed = response.ToTokenContainer(
        ///     previousRefreshToken: current.RefreshToken,
        ///     clientId: current.ClientId,
        ///     clientSecret: current.ClientSecret,
        ///     authorizationServer: current.AuthorizationServer!,
        ///     tokenEndpointAuthMethod: current.TokenEndpointAuthMethod);
        /// </code>
        /// </example>
        /// <remarks>
        /// RFC 6749 section 6 makes <c>refresh_token</c> optional on a refresh response, so an omitted value
        /// means "keep the one you have", never "you no longer have one"; that rule lives here so no grant has
        /// to remember it. An absent <c>token_type</c> becomes
        /// <see cref="ODataMcpAuthConstants.BearerScheme"/>, because the SDK container requires one and every
        /// grant this package implements is a bearer grant.
        /// </remarks>
        public SdkAuth.TokenContainer ToTokenContainer(string? previousRefreshToken, string? clientId, string? clientSecret, string authorizationServer, string? tokenEndpointAuthMethod)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(authorizationServer);

            if (string.IsNullOrWhiteSpace(AccessToken))
            {
                throw new InvalidOperationException("The token endpoint response carries no access_token and cannot become a TokenContainer.");
            }

            return new SdkAuth.TokenContainer
            {
                AccessToken = AccessToken,
                AuthorizationServer = authorizationServer,
                ClientId = clientId,
                ClientSecret = clientSecret,
                ExpiresIn = ExpiresIn,
                ObtainedAt = DateTimeOffset.UtcNow,
                RefreshToken = string.IsNullOrWhiteSpace(RefreshToken) ? previousRefreshToken : RefreshToken,
                Scope = Scope,
                TokenEndpointAuthMethod = tokenEndpointAuthMethod,
                TokenType = string.IsNullOrWhiteSpace(TokenType) ? ODataMcpAuthConstants.BearerScheme : TokenType
            };
        }

        #endregion

    }

}
