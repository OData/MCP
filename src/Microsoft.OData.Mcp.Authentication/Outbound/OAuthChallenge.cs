// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// One parsed <c>WWW-Authenticate</c> challenge: the authentication scheme, the OAuth-relevant auth-params
    /// promoted to typed properties, and the complete, case-insensitive parameter set exactly as it arrived.
    /// </summary>
    /// <example>
    /// <code>
    /// var challenge = WwwAuthenticateParser.SelectBearer(WwwAuthenticateParser.ParseAll(result.WwwAuthenticate));
    /// if (challenge is not null &amp;&amp; challenge.ResourceMetadata is not null)
    /// {
    ///     // Fetch RFC 9728 protected resource metadata from challenge.ResourceMetadata.
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// Instances are produced by <see cref="WwwAuthenticateParser"/> and are immutable once created.
    /// <see cref="ResourceClientId"/> carries the challenge <c>client_id</c>, which is the <em>resource</em>
    /// application id (Microsoft Graph advertises
    /// <see cref="ODataMcpAuthConstants.MicrosoftGraphResourceAppId"/>). It is never our public client id and
    /// must never be copied onto an outbound OAuth client id; that is why no member of this type is named
    /// <c>ClientId</c>.
    /// </remarks>
    public class OAuthChallenge
    {

        #region Fields

        /// <summary>
        /// The shared, empty, case-insensitive parameter set used by challenges that carry no auth-params.
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, string> EmptyParameters =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        #endregion

        #region Properties

        /// <summary>
        /// The challenge <c>authorization_uri</c> as an absolute URI, or <see langword="null"/> when the
        /// parameter is absent or not an absolute URI.
        /// </summary>
        /// <remarks>
        /// A non-absolute value is still available verbatim through <see cref="Parameters"/>.
        /// </remarks>
        public Uri? AuthorizationUri { get; init; }

        /// <summary>
        /// The RFC 6750 <c>error</c> code, such as <c>invalid_token</c>, or <see langword="null"/> when absent.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// The RFC 6750 <c>error_description</c> human readable text, or <see langword="null"/> when absent.
        /// </summary>
        public string? ErrorDescription { get; init; }

        /// <summary>
        /// Every auth-param of the challenge, including the ones promoted to typed properties, keyed
        /// case-insensitively with values unescaped but otherwise verbatim.
        /// </summary>
        /// <remarks>
        /// Defaults to an empty dictionary so callers never have to null-check it.
        /// </remarks>
        public IReadOnlyDictionary<string, string> Parameters { get; init; } = EmptyParameters;

        /// <summary>
        /// The RFC 7235 <c>realm</c>, or <see langword="null"/> when absent. An advertised but empty realm
        /// (Microsoft Graph sends <c>realm=""</c>) is preserved as an empty string.
        /// </summary>
        public string? Realm { get; init; }

        /// <summary>
        /// The challenge <c>client_id</c>, which identifies the protected <em>resource</em> application, or
        /// <see langword="null"/> when absent.
        /// </summary>
        /// <remarks>
        /// Per AUTH-13 this value is never used as the client id of the OAuth client this process runs.
        /// </remarks>
        public string? ResourceClientId { get; init; }

        /// <summary>
        /// The RFC 9728 <c>resource_metadata</c> location as an absolute URI, or <see langword="null"/> when
        /// the parameter is absent or not an absolute URI.
        /// </summary>
        /// <remarks>
        /// A non-absolute value is still available verbatim through <see cref="Parameters"/>.
        /// </remarks>
        public Uri? ResourceMetadata { get; init; }

        /// <summary>
        /// The authentication scheme, such as <c>Bearer</c> or <c>Basic</c>, exactly as the server spelled it.
        /// </summary>
        /// <remarks>
        /// Schemes are case-insensitive; compare with <see cref="StringComparison.OrdinalIgnoreCase"/> or use
        /// <see cref="WwwAuthenticateParser.SelectBearer(System.Collections.Generic.IReadOnlyList{OAuthChallenge})"/>.
        /// </remarks>
        public string Scheme { get; init; } = string.Empty;

        /// <summary>
        /// The space-delimited <c>scope</c> the server asks for, or <see langword="null"/> when absent.
        /// </summary>
        public string? Scope { get; init; }

        #endregion

    }

}
