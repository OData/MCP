// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Parses and formats the CLI <c>--grant</c> wire names for <see cref="OutboundGrantKind"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="OutboundGrantKind.RefreshToken"/> is intentionally excluded from
    /// <see cref="TryParse(string, out OutboundGrantKind)"/>: it is never a value an operator passes on the
    /// command line, only a kind the outbound auth handler selects internally once a refresh token is cached.
    /// <see cref="ToWireName(OutboundGrantKind)"/> still formats it, because log lines and cache diagnostics
    /// need a name for every defined value.
    /// </remarks>
    public static class OutboundGrantKindParser
    {

        #region Public Methods

        /// <summary>
        /// Formats a grant kind as its RFC wire name.
        /// </summary>
        /// <param name="kind">The grant kind to format.</param>
        /// <returns>
        /// <c>device_code</c>, <c>authorization_code</c>, <c>client_credentials</c>, <c>identity_assertion</c>,
        /// or <c>refresh_token</c>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="kind"/> is not a defined <see cref="OutboundGrantKind"/> value.</exception>
        /// <example>
        /// <code>
        /// OutboundGrantKindParser.ToWireName(OutboundGrantKind.DeviceCode); // "device_code"
        /// </code>
        /// </example>
        public static string ToWireName(OutboundGrantKind kind)
        {
            return kind switch
            {
                OutboundGrantKind.DeviceCode => ODataMcpAuthConstants.GrantTypeDeviceCode,
                OutboundGrantKind.AuthorizationCode => ODataMcpAuthConstants.GrantTypeAuthorizationCode,
                OutboundGrantKind.ClientCredentials => ODataMcpAuthConstants.GrantTypeClientCredentials,
                OutboundGrantKind.IdentityAssertion => "identity_assertion",
                OutboundGrantKind.RefreshToken => ODataMcpAuthConstants.GrantTypeRefreshToken,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The grant kind is not a defined OutboundGrantKind value.")
            };
        }

        /// <summary>
        /// Parses a CLI <c>--grant</c> wire name into an <see cref="OutboundGrantKind"/>.
        /// </summary>
        /// <param name="value">The wire name to parse; matched case-insensitively after trimming surrounding white space.</param>
        /// <param name="kind">The parsed grant kind when parsing succeeds; otherwise the default enum value.</param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="value"/> is <c>device_code</c>, <c>authorization_code</c>,
        /// <c>client_credentials</c>, or <c>identity_assertion</c>; otherwise <see langword="false"/>.
        /// </returns>
        /// <example>
        /// <code>
        /// OutboundGrantKindParser.TryParse("  Device_Code  ", out var kind); // true, OutboundGrantKind.DeviceCode
        /// OutboundGrantKindParser.TryParse("refresh_token", out _);         // false: not CLI-selectable
        /// </code>
        /// </example>
        /// <remarks>
        /// <c>null</c>, empty, and white-space-only values return <see langword="false"/> rather than throwing,
        /// so a caller can use this directly against an optional CLI option value.
        /// </remarks>
        public static bool TryParse(string? value, out OutboundGrantKind kind)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                switch (value.Trim().ToLowerInvariant())
                {
                    case ODataMcpAuthConstants.GrantTypeDeviceCode:
                        kind = OutboundGrantKind.DeviceCode;
                        return true;

                    case ODataMcpAuthConstants.GrantTypeAuthorizationCode:
                        kind = OutboundGrantKind.AuthorizationCode;
                        return true;

                    case ODataMcpAuthConstants.GrantTypeClientCredentials:
                        kind = OutboundGrantKind.ClientCredentials;
                        return true;

                    case "identity_assertion":
                        kind = OutboundGrantKind.IdentityAssertion;
                        return true;
                }
            }

            kind = default;
            return false;
        }

        #endregion

    }

}
