// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.OData.Mcp.Authentication.Outbound;

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// Everything the <c>odata-mcp add</c> wizard collected about how the generated <c>start</c> command should
    /// authenticate.
    /// </summary>
    /// <example>
    /// <code>
    /// var settings = new AddCommandAuthSettings
    /// {
    ///     ClientId = "00000000-0000-0000-0000-000000000000",
    ///     Grant = OutboundGrantKind.DeviceCode,
    ///     Scopes = "https://graph.microsoft.com/.default"
    /// };
    ///
    /// var command = wizard.BuildMcpCommand("graph", url, settings, "user", verbose: false);
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>AUTH-5</c> this is a top-level type rather than a tuple or a nested class, because it crosses
    /// three methods — the prompts that fill it, the connection test that turns it into
    /// <see cref="OutboundOAuthOptions"/>, and the command builder that turns it into flags.
    /// <para>
    /// There is deliberately no client secret property. Per <c>specs/v3/AUTHENTICATION.md</c> the wizard never
    /// writes a client secret into MCP configuration at all: it prints an instruction to set
    /// <see cref="ODataMcpAuthConstants.ClientSecretEnvironmentVariable"/> in the environment that launches the
    /// host, which <c>start</c> reads. <see cref="ApiKey"/>, <see cref="BasicPassword"/>, and
    /// <see cref="AuthToken"/> <em>are</em> written, and each is warned about when it is.
    /// </para>
    /// </remarks>
    public sealed class AddCommandAuthSettings
    {

        #region Properties

        /// <summary>
        /// Gets or sets the raw API key, or <see langword="null"/> when the operator chose another path.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the header name <see cref="ApiKey"/> is sent in.
        /// </summary>
        public string? ApiKeyHeader { get; set; }

        /// <summary>
        /// Gets or sets the authorization server issuer the operator supplied, or <see langword="null"/> when
        /// discovery found one on its own.
        /// </summary>
        public string? AuthServer { get; set; }

        /// <summary>
        /// Gets or sets the bearer token the operator pasted, or <see langword="null"/> for every other path.
        /// </summary>
        public string? AuthToken { get; set; }

        /// <summary>
        /// Gets or sets the HTTP Basic password.
        /// </summary>
        public string? BasicPassword { get; set; }

        /// <summary>
        /// Gets or sets the HTTP Basic user name.
        /// </summary>
        public string? BasicUser { get; set; }

        /// <summary>
        /// Gets or sets the OAuth client identifier, or <see langword="null"/> when the authorization server
        /// registers one dynamically.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the grant the generated command pins, or <see langword="null"/> for a non-OAuth path.
        /// </summary>
        public OutboundGrantKind? Grant { get; set; }

        /// <summary>
        /// Gets or sets the space-delimited scope list, or <see langword="null"/> when the challenge or the
        /// protected resource metadata already supplies one.
        /// </summary>
        public string? Scopes { get; set; }

        /// <summary>
        /// Gets or sets the token cache directory the operator chose, or <see langword="null"/> to leave the
        /// default operating system credential store in place.
        /// </summary>
        public string? TokenCachePath { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Turns these settings into the outbound options the <c>start</c> command would build from the same
        /// flags, so a connection test exercises the real authentication path rather than a simplified one.
        /// </summary>
        /// <returns>
        /// The options, with <see cref="ODataMcpAuthConstants.ClientSecretEnvironmentVariable"/> and
        /// <see cref="ODataMcpAuthConstants.IdTokenEnvironmentVariable"/> already folded in.
        /// </returns>
        /// <example>
        /// <code>
        /// using var host = await ToolsMcpHost.CreateAsync(url, settings.ToOutboundOptions(), false, false, null, ct);
        /// </code>
        /// </example>
        /// <remarks>
        /// The environment is read here for the same reason <c>start</c> reads it: a client credentials test
        /// that ignored <c>ODATA_MCP_CLIENT_SECRET</c> would fail for a reason the operator has already been
        /// told how to fix.
        /// </remarks>
        public OutboundOAuthOptions ToOutboundOptions()
        {
            var options = new OutboundOAuthOptions
            {
                ApiKey = ApiKey,
                ApiKeyHeader = ApiKeyHeader,
                AuthServer = string.IsNullOrWhiteSpace(AuthServer) ? null : new Uri(AuthServer, UriKind.Absolute),
                AuthToken = AuthToken,
                BasicPassword = BasicPassword,
                BasicUser = BasicUser,
                ClientId = ClientId,
                Grant = Grant,
                Scopes = ParseScopes(Scopes),
                TokenCachePath = TokenCachePath
            };

            OutboundOAuthOptions.FromEnvironment(options);

            return options;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Splits a space-delimited scope list.
        /// </summary>
        /// <param name="scopes">The scope list, or <see langword="null"/>.</param>
        /// <returns>
        /// The individual scopes, or an empty list.
        /// </returns>
        internal static List<string> ParseScopes(string? scopes)
        {
            return string.IsNullOrWhiteSpace(scopes)
                ? []
                : [.. scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }

        #endregion

    }

}
