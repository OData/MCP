// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.OData.Mcp.Authentication.Outbound;

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// Maps the raw <c>StartCommand</c> / <c>TestCommand</c> CLI flag values onto an <see cref="OutboundOAuthOptions"/>
    /// instance, so both commands share one implementation of that mapping.
    /// </summary>
    /// <remarks>
    /// Every URI-shaped flag is parsed with <see cref="Uri.TryCreate(string?, UriKind, out Uri)"/> against
    /// <see cref="UriKind.Absolute"/>; a value that fails to parse throws an <see cref="ArgumentException"/> that
    /// names the offending flag. <c>--scopes</c> is split on white space. <c>--grant</c> is parsed with
    /// <see cref="OutboundGrantKindParser.TryParse(string?, out OutboundGrantKind)"/>. The resulting options are
    /// then passed through <see cref="OutboundOAuthOptions.FromEnvironment(OutboundOAuthOptions)"/> and
    /// <see cref="OutboundOAuthOptions.Validate"/> before being returned, so an invalid or incomplete combination
    /// of flags fails before either command makes a network call.
    /// </remarks>
    internal static class OutboundOptionsBinder
    {

        #region Internal Methods

        /// <summary>
        /// Builds and validates an <see cref="OutboundOAuthOptions"/> from raw CLI flag values.
        /// </summary>
        /// <param name="authToken">The <c>-t|--auth-token</c> escape-hatch bearer token.</param>
        /// <param name="clientId">The <c>--client-id</c> value.</param>
        /// <param name="clientSecret">The <c>--client-secret</c> value.</param>
        /// <param name="scopes">The <c>--scopes</c> space-separated scope list.</param>
        /// <param name="authServer">The <c>--auth-server</c> absolute URI.</param>
        /// <param name="resource">The <c>--resource</c> absolute URI.</param>
        /// <param name="grant">The <c>--grant</c> wire name.</param>
        /// <param name="redirectUri">The <c>--redirect-uri</c> absolute URI.</param>
        /// <param name="tokenCache">The <c>--token-cache</c> directory.</param>
        /// <param name="authTimeoutSeconds">The <c>--auth-timeout</c> value, in seconds.</param>
        /// <param name="apiKey">The <c>--api-key</c> value.</param>
        /// <param name="apiKeyHeader">The <c>--api-key-header</c> value.</param>
        /// <param name="basicUser">The <c>--basic-user</c> value.</param>
        /// <param name="basicPassword">The <c>--basic-password</c> value.</param>
        /// <param name="clientMetadataDocument">The <c>--client-metadata-document</c> absolute URI.</param>
        /// <param name="idpUrl">The <c>--idp-url</c> absolute URI.</param>
        /// <param name="idpTokenEndpoint">The <c>--idp-token-endpoint</c> absolute URI.</param>
        /// <param name="idpClientId">The <c>--idp-client-id</c> value.</param>
        /// <param name="idpClientSecret">The <c>--idp-client-secret</c> value.</param>
        /// <param name="idpScope">The <c>--idp-scope</c> value.</param>
        /// <param name="idpIdTokenFile">The <c>--idp-id-token-file</c> path.</param>
        /// <returns>
        /// A fully bound and validated <see cref="OutboundOAuthOptions"/> instance.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when a URI-shaped flag is not an absolute URI, when <paramref name="grant"/> is set but is not a
        /// recognized <c>--grant</c> wire name, or when <see cref="OutboundOAuthOptions.Validate"/> rejects the
        /// resulting combination of flags.
        /// </exception>
        /// <example>
        /// <code>
        /// var options = OutboundOptionsBinder.Bind(
        ///     authToken: null, clientId: "cli", clientSecret: null, scopes: "read write", authServer: null,
        ///     resource: null, grant: "device_code", redirectUri: null, tokenCache: null, authTimeoutSeconds: null,
        ///     apiKey: null, apiKeyHeader: null, basicUser: null, basicPassword: null, clientMetadataDocument: null,
        ///     idpUrl: null, idpTokenEndpoint: null, idpClientId: null, idpClientSecret: null, idpScope: null,
        ///     idpIdTokenFile: null);
        /// </code>
        /// </example>
        /// <remarks>
        /// Never logs or echoes <paramref name="authToken"/>, <paramref name="clientSecret"/>,
        /// <paramref name="apiKey"/>, <paramref name="basicPassword"/>, or <paramref name="idpClientSecret"/>.
        /// </remarks>
        internal static OutboundOAuthOptions Bind(
            string? authToken,
            string? clientId,
            string? clientSecret,
            string? scopes,
            string? authServer,
            string? resource,
            string? grant,
            string? redirectUri,
            string? tokenCache,
            int? authTimeoutSeconds,
            string? apiKey,
            string? apiKeyHeader,
            string? basicUser,
            string? basicPassword,
            string? clientMetadataDocument,
            string? idpUrl,
            string? idpTokenEndpoint,
            string? idpClientId,
            string? idpClientSecret,
            string? idpScope,
            string? idpIdTokenFile)
        {
            var options = new OutboundOAuthOptions
            {
                ApiKey = apiKey,
                ApiKeyHeader = apiKeyHeader,
                AuthServer = ParseAbsoluteUri(authServer, "--auth-server"),
                AuthToken = authToken,
                BasicPassword = basicPassword,
                BasicUser = basicUser,
                ClientId = clientId,
                ClientMetadataDocumentUri = ParseAbsoluteUri(clientMetadataDocument, "--client-metadata-document"),
                ClientSecret = clientSecret,
                IdpClientId = idpClientId,
                IdpClientSecret = idpClientSecret,
                IdpIdTokenFile = idpIdTokenFile,
                IdpScope = idpScope,
                IdpTokenEndpoint = ParseAbsoluteUri(idpTokenEndpoint, "--idp-token-endpoint"),
                IdpUrl = ParseAbsoluteUri(idpUrl, "--idp-url"),
                RedirectUri = ParseAbsoluteUri(redirectUri, "--redirect-uri"),
                Resource = ParseAbsoluteUri(resource, "--resource"),
                Scopes = ParseScopes(scopes),
                TokenCachePath = tokenCache
            };

            if (authTimeoutSeconds is { } seconds)
            {
                options.AuthTimeout = TimeSpan.FromSeconds(seconds);
            }

            if (!string.IsNullOrWhiteSpace(grant))
            {
                if (!OutboundGrantKindParser.TryParse(grant, out var parsedGrant))
                {
                    throw new ArgumentException("--grant must be one of device_code, authorization_code, client_credentials, identity_assertion.");
                }

                options.Grant = parsedGrant;
            }

            options = OutboundOAuthOptions.FromEnvironment(options);
            options.Validate();

            return options;
        }

        /// <summary>
        /// Parses a CLI flag value as an absolute <see cref="Uri"/>.
        /// </summary>
        /// <param name="value">The raw flag value, or <see langword="null"/> when the flag was not supplied.</param>
        /// <param name="flagName">The flag's long name, used in the thrown message.</param>
        /// <returns>
        /// The parsed <see cref="Uri"/>, or <see langword="null"/> when <paramref name="value"/> is
        /// <see langword="null"/> or white space.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not an absolute URI.</exception>
        /// <example>
        /// <code>
        /// OutboundOptionsBinder.ParseAbsoluteUri("https://as.example.com", "--auth-server");
        /// </code>
        /// </example>
        /// <remarks>
        /// Shared by every URI-shaped flag so each reports a consistent, flag-named failure message.
        /// </remarks>
        internal static Uri? ParseAbsoluteUri(string? value, string flagName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException($"{flagName} must be an absolute URI.");
            }

            return uri;
        }

        /// <summary>
        /// Splits a <c>--scopes</c> flag value into its individual scope names.
        /// </summary>
        /// <param name="scopes">The raw, space-separated flag value.</param>
        /// <returns>
        /// The trimmed, non-empty scope names, in the order supplied; an empty list when <paramref name="scopes"/>
        /// is <see langword="null"/> or white space.
        /// </returns>
        /// <example>
        /// <code>
        /// OutboundOptionsBinder.ParseScopes("read  write "); // ["read", "write"]
        /// </code>
        /// </example>
        /// <remarks>
        /// Splits on any white space, not just single spaces, so tabs and repeated spaces are tolerated.
        /// </remarks>
        internal static List<string> ParseScopes(string? scopes)
        {
            return string.IsNullOrWhiteSpace(scopes)
                ? []
                : [.. scopes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }

        #endregion

    }

}
