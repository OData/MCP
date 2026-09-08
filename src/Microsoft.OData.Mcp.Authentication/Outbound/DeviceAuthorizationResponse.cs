// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The RFC 8628 section 3.2 device authorization response returned from a device authorization endpoint.
    /// </summary>
    /// <example>
    /// <code>
    /// var response = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.DeviceAuthorizationResponse);
    /// var pollInterval = response!.Interval ?? 5;
    /// </code>
    /// </example>
    /// <remarks>
    /// Deserialized exclusively through <see cref="OutboundOAuthJsonContext"/> (AOT: JSON source-gen; no
    /// reflection). <see cref="Interval"/> is <see langword="null"/> when the server omits it; RFC 8628
    /// callers treat an absent interval as five seconds, but this type does not bake that default in so the
    /// distinction between "absent" and "explicitly five" survives for callers that care.
    /// </remarks>
    public sealed class DeviceAuthorizationResponse
    {

        #region Properties

        /// <summary>
        /// Gets or sets the device verification code the client polls the token endpoint with.
        /// </summary>
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the lifetime, in seconds, of <see cref="DeviceCode"/> and <see cref="UserCode"/>.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of seconds the client must wait between token endpoint polls, or
        /// <see langword="null"/> when the server did not advertise one.
        /// </summary>
        /// <remarks>
        /// A caller should treat a <see langword="null"/> value as five seconds per RFC 8628 section 3.2.
        /// </remarks>
        [JsonPropertyName("interval")]
        public int? Interval { get; set; }

        /// <summary>
        /// Gets or sets the end-user verification code.
        /// </summary>
        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the end-user verification URI the client displays or opens.
        /// </summary>
        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a verification URI that already carries <see cref="UserCode"/>, or <see langword="null"/>
        /// when the server did not advertise one.
        /// </summary>
        [JsonPropertyName("verification_uri_complete")]
        public string? VerificationUriComplete { get; set; }

        #endregion

    }

}
