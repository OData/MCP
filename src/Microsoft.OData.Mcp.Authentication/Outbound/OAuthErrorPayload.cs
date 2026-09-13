// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The RFC 6749 section 5.2 error response returned from a token, device authorization, or dynamic client
    /// registration endpoint.
    /// </summary>
    /// <example>
    /// <code>
    /// var payload = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.OAuthErrorPayload);
    /// if (payload?.Error == ODataMcpAuthConstants.ErrorAuthorizationPending)
    /// {
    ///     // Keep polling.
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// Deserialized exclusively through <see cref="OutboundOAuthJsonContext"/> (AOT: JSON source-gen; no
    /// reflection). RFC 8628 device code polling reuses this same error shape for
    /// <c>authorization_pending</c>, <c>slow_down</c>, <c>expired_token</c>, and <c>access_denied</c>.
    /// </remarks>
    public sealed class OAuthErrorPayload
    {

        #region Properties

        /// <summary>
        /// Gets or sets the RFC 6749 error code, such as <c>invalid_grant</c>.
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human readable error description, or <see langword="null"/> when the server did
        /// not supply one.
        /// </summary>
        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }

        /// <summary>
        /// Gets or sets a URI identifying a human readable web page describing the error, or
        /// <see langword="null"/> when the server did not supply one.
        /// </summary>
        [JsonPropertyName("error_uri")]
        public string? ErrorUri { get; set; }

        #endregion

    }

}
