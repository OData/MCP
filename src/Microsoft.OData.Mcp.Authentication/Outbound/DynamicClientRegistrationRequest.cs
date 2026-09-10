// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The RFC 7591 section 2 client metadata document <see cref="DynamicClientRegistrar"/> POSTs to an
    /// authorization server's <c>registration_endpoint</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// var request = new DynamicClientRegistrationRequest
    /// {
    ///     ClientName = "odata-mcp",
    ///     GrantTypes = [ODataMcpAuthConstants.GrantTypeAuthorizationCode],
    ///     RedirectUris = ["http://127.0.0.1:53219/callback/"],
    ///     ResponseTypes = [ODataMcpAuthConstants.ResponseTypeCode],
    ///     TokenEndpointAuthMethod = TokenEndpointClient.AuthMethodNone
    /// };
    /// </code>
    /// </example>
    /// <remarks>
    /// This exists because the SDK's own registration request type is not public — only its
    /// <see cref="SdkAuth.DynamicClientRegistrationResponse"/> counterpart is — and outbound OAuth serializes
    /// exclusively through <see cref="OutboundOAuthJsonContext"/> (AOT: JSON source-gen; no reflection).
    /// Properties left <see langword="null"/> are omitted from the wire document, so an authorization server
    /// that rejects an empty <c>redirect_uris</c> never sees one.
    /// </remarks>
    public sealed class DynamicClientRegistrationRequest
    {

        #region Properties

        /// <summary>
        /// Gets or sets the RFC 7591 <c>application_type</c>, such as <c>native</c>, or <see langword="null"/>
        /// to let the authorization server default it.
        /// </summary>
        [JsonPropertyName("application_type")]
        public string? ApplicationType { get; set; }

        /// <summary>
        /// Gets or sets the human-readable client name shown on a consent screen.
        /// </summary>
        [JsonPropertyName("client_name")]
        public string? ClientName { get; set; }

        /// <summary>
        /// Gets or sets the informational home page of the client, or <see langword="null"/> when there is none.
        /// </summary>
        [JsonPropertyName("client_uri")]
        public string? ClientUri { get; set; }

        /// <summary>
        /// Gets or sets the grant types this client intends to use.
        /// </summary>
        [JsonPropertyName("grant_types")]
        public IList<string>? GrantTypes { get; set; }

        /// <summary>
        /// Gets or sets the redirect URIs the authorization server may redirect to, or <see langword="null"/>
        /// for a client that never uses a redirect-based grant.
        /// </summary>
        [JsonPropertyName("redirect_uris")]
        public IList<string>? RedirectUris { get; set; }

        /// <summary>
        /// Gets or sets the response types this client's authorization requests use.
        /// </summary>
        [JsonPropertyName("response_types")]
        public IList<string>? ResponseTypes { get; set; }

        /// <summary>
        /// Gets or sets the token endpoint authentication method this client will use, normally
        /// <see cref="TokenEndpointClient.AuthMethodNone"/> for a public CLI.
        /// </summary>
        [JsonPropertyName("token_endpoint_auth_method")]
        public string? TokenEndpointAuthMethod { get; set; }

        #endregion

    }

}
