// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The RFC 8414 authorization server metadata document, or its OpenID Connect discovery document
    /// counterpart, restricted to the endpoints, advertised grants and scopes, and PKCE / dynamic client
    /// registration capability flags outbound OAuth discovery actually reads.
    /// </summary>
    /// <example>
    /// <code>
    /// var json = await client.GetStringAsync(wellKnownUri);
    /// var metadata = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.AuthorizationServerMetadata);
    /// if (metadata is not null &amp;&amp; metadata.AdvertisesGrant(ODataMcpAuthConstants.DeviceCodeGrantType))
    /// {
    ///     // Start a device code grant.
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// Deserialized exclusively through <see cref="OutboundOAuthJsonContext"/>; this type is never read with
    /// reflection-based <c>System.Text.Json</c> serialization (AOT: JSON source-gen; no reflection). Unknown
    /// fields the discovery document may carry (signing algorithms, UI locales, and similar) are intentionally
    /// not modeled here per <c>specs/v3/AUTHENTICATION.md</c> "Authentication outbound" type map.
    /// </remarks>
    public sealed class AuthorizationServerMetadata
    {

        #region Properties

        /// <summary>
        /// Gets or sets the RFC 6749 authorization endpoint.
        /// </summary>
        [JsonPropertyName("authorization_endpoint")]
        public Uri? AuthorizationEndpoint { get; set; }

        /// <summary>
        /// Gets or sets whether the authorization response carries the RFC 9207 <c>iss</c> parameter.
        /// </summary>
        [JsonPropertyName("authorization_response_iss_parameter_supported")]
        public bool? AuthorizationResponseIssParameterSupported { get; set; }

        /// <summary>
        /// Gets or sets whether this server accepts a client id metadata document (CIMD) in place of a
        /// pre-registered client id.
        /// </summary>
        [JsonPropertyName("client_id_metadata_document_supported")]
        public bool? ClientIdMetadataDocumentSupported { get; set; }

        /// <summary>
        /// Gets or sets the RFC 7636 PKCE code challenge methods this server accepts, such as <c>S256</c>.
        /// </summary>
        [JsonPropertyName("code_challenge_methods_supported")]
        public IList<string>? CodeChallengeMethodsSupported { get; set; }

        /// <summary>
        /// Gets or sets the RFC 8628 device authorization endpoint.
        /// </summary>
        [JsonPropertyName("device_authorization_endpoint")]
        public Uri? DeviceAuthorizationEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the OAuth 2.0 / OpenID Connect grant types this server advertises.
        /// </summary>
        [JsonPropertyName("grant_types_supported")]
        public IList<string>? GrantTypesSupported { get; set; }

        /// <summary>
        /// Gets or sets the issuer identifier, exactly as the document spells it.
        /// </summary>
        /// <remarks>
        /// Deliberately a <see cref="string"/> rather than a <see cref="Uri"/>. RFC 8414 section 2 and RFC 9207
        /// both compare issuer identifiers by simple string comparison, and <see cref="Uri"/> normalizes: it
        /// turns the authority-only issuer <c>https://accounts.google.com</c> into
        /// <c>https://accounts.google.com/</c>, which would make a conforming authorization server's
        /// <c>iss</c> look like a mix-up attack. Use <see cref="IssuerUri"/> where a URI is genuinely needed.
        /// </remarks>
        [JsonPropertyName("issuer")]
        public string? Issuer { get; set; }

        /// <summary>
        /// Gets <see cref="Issuer"/> as an absolute URI, or <see langword="null"/> when it is absent or is not
        /// an absolute URI.
        /// </summary>
        /// <remarks>
        /// Never used for an issuer comparison — that is <see cref="Issuer"/>'s job — only where a collaborator
        /// requires a <see cref="Uri"/>, such as the SDK identity assertion provider.
        /// </remarks>
        [JsonIgnore]
        public Uri? IssuerUri
        {
            get
            {
                return Uri.TryCreate(Issuer, UriKind.Absolute, out var issuer) ? issuer : null;
            }
        }

        /// <summary>
        /// Gets or sets the RFC 7591 dynamic client registration endpoint, or <see langword="null"/> when this
        /// server requires a pre-registered client id.
        /// </summary>
        [JsonPropertyName("registration_endpoint")]
        public Uri? RegistrationEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the response types this server's authorization endpoint accepts.
        /// </summary>
        [JsonPropertyName("response_types_supported")]
        public IList<string>? ResponseTypesSupported { get; set; }

        /// <summary>
        /// Gets or sets the RFC 7009 token revocation endpoint.
        /// </summary>
        [JsonPropertyName("revocation_endpoint")]
        public Uri? RevocationEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the scopes this server advertises.
        /// </summary>
        [JsonPropertyName("scopes_supported")]
        public IList<string>? ScopesSupported { get; set; }

        /// <summary>
        /// Gets or sets the RFC 6749 token endpoint.
        /// </summary>
        [JsonPropertyName("token_endpoint")]
        public Uri? TokenEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the client authentication methods the token endpoint accepts.
        /// </summary>
        [JsonPropertyName("token_endpoint_auth_methods_supported")]
        public IList<string>? TokenEndpointAuthMethodsSupported { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether this server advertises a grant type, treating the RFC 8628 short form
        /// <c>device_code</c> and the <see cref="ODataMcpAuthConstants.DeviceCodeGrantType"/> URN as
        /// equivalent in either direction.
        /// </summary>
        /// <param name="grantType">The grant type to look for, such as <c>authorization_code</c> or <c>device_code</c>.</param>
        /// <returns>
        /// <see langword="true"/> when <see cref="GrantTypesSupported"/> contains an ordinal match for
        /// <paramref name="grantType"/>, or its device-code counterpart; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="grantType"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <remarks>
        /// Advertised grant lists use either spelling depending on the authorization server, so a caller that
        /// asks for either one gets the same answer regardless of which spelling was actually published.
        /// </remarks>
        public bool AdvertisesGrant(string grantType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(grantType);

            if (GrantTypesSupported is null)
            {
                return false;
            }

            foreach (var candidate in GrantTypesSupported)
            {
                if (string.Equals(candidate, grantType, StringComparison.Ordinal)
                    || IsDeviceCodeEquivalent(candidate, grantType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether this server advertises a scope.
        /// </summary>
        /// <param name="scope">The scope to look for.</param>
        /// <returns>
        /// <see langword="true"/> when <see cref="ScopesSupported"/> contains an ordinal match for
        /// <paramref name="scope"/>; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="scope"/> is <see langword="null"/>, empty, or whitespace.</exception>
        public bool AdvertisesScope(string scope)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);

            if (ScopesSupported is null)
            {
                return false;
            }

            foreach (var candidate in ScopesSupported)
            {
                if (string.Equals(candidate, scope, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Determines whether an advertised grant type and a requested grant type are the RFC 8628 short form
        /// and URN spellings of the same device code grant.
        /// </summary>
        /// <param name="advertised">One grant type from <see cref="GrantTypesSupported"/>.</param>
        /// <param name="requested">The grant type <see cref="AdvertisesGrant(string)"/> was asked about.</param>
        /// <returns>
        /// <see langword="true"/> when one value is <see cref="ODataMcpAuthConstants.GrantTypeDeviceCode"/> and
        /// the other is <see cref="ODataMcpAuthConstants.DeviceCodeGrantType"/>; otherwise <see langword="false"/>.
        /// </returns>
        internal static bool IsDeviceCodeEquivalent(string advertised, string requested)
        {
            var advertisedIsDeviceCode = string.Equals(advertised, ODataMcpAuthConstants.GrantTypeDeviceCode, StringComparison.Ordinal)
                || string.Equals(advertised, ODataMcpAuthConstants.DeviceCodeGrantType, StringComparison.Ordinal);
            var requestedIsDeviceCode = string.Equals(requested, ODataMcpAuthConstants.GrantTypeDeviceCode, StringComparison.Ordinal)
                || string.Equals(requested, ODataMcpAuthConstants.DeviceCodeGrantType, StringComparison.Ordinal);

            return advertisedIsDeviceCode && requestedIsDeviceCode;
        }

        #endregion

    }

}
