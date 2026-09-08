// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tools.Hosting;

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// Fetches live metadata and prints the declared entity set count.
    /// </summary>
    [Command(Name = "test", Description = "Fetch $metadata from an OData service and print the entity set count.")]
    public class TestCommand
    {

        #region Properties

        /// <summary>
        /// Gets or sets the raw API key value for the API key escape hatch.
        /// </summary>
        [Option("--api-key", Description = "Raw key")]
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the header name <see cref="ApiKey"/> is sent under.
        /// </summary>
        [Option("--api-key-header", Description = "Header name. Fail if key set without header")]
        public string? ApiKeyHeader { get; set; }

        /// <summary>
        /// Gets or sets the operator's authorization server override.
        /// </summary>
        [Option("--auth-server", Description = "AS issuer override")]
        public string? AuthServer { get; set; }

        /// <summary>
        /// Gets or sets how long, in seconds, an interactive grant may run before it is abandoned.
        /// </summary>
        [Option("--auth-timeout <SECONDS>", Description = "Seconds. Default 300")]
        public int? AuthTimeout { get; set; }

        /// <summary>
        /// Gets or sets the authentication token.
        /// </summary>
        [Option("-t|--auth-token", Description = "Authentication token for the OData service")]
        public string? AuthToken { get; set; }

        /// <summary>
        /// Gets or sets the RFC 7617 Basic authentication password.
        /// </summary>
        [Option("--basic-password", Description = "Basic auth password")]
        public string? BasicPassword { get; set; }

        /// <summary>
        /// Gets or sets the RFC 7617 Basic authentication user name.
        /// </summary>
        [Option("--basic-user", Description = "Basic auth user name")]
        public string? BasicUser { get; set; }

        /// <summary>
        /// Gets or sets the OAuth client id this process authenticates as.
        /// </summary>
        [Option("--client-id", Description = "Public/confidential client id")]
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client identity metadata document (CIMD) URI.
        /// </summary>
        [Option("--client-metadata-document", Description = "CIMD URI")]
        public string? ClientMetadataDocument { get; set; }

        /// <summary>
        /// Gets or sets the OAuth client secret for a confidential client.
        /// </summary>
        [Option("--client-secret", Description = "Client secret. Also read from ODATA_MCP_CLIENT_SECRET")]
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the operator's <c>--grant</c> selection.
        /// </summary>
        [Option("--grant", Description = "device_code | authorization_code | client_credentials | identity_assertion")]
        public string? Grant { get; set; }

        /// <summary>
        /// Gets or sets the enterprise IdP client id used to obtain the id token the identity assertion grant exchanges.
        /// </summary>
        [Option("--idp-client-id", Description = "Enterprise IdP client id")]
        public string? IdpClientId { get; set; }

        /// <summary>
        /// Gets or sets the enterprise IdP client secret.
        /// </summary>
        [Option("--idp-client-secret", Description = "Enterprise IdP client secret")]
        public string? IdpClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the path to a UTF-8 OIDC id token file for the identity assertion grant.
        /// </summary>
        [Option("--idp-id-token-file", Description = "Path to a UTF-8 OIDC id token")]
        public string? IdpIdTokenFile { get; set; }

        /// <summary>
        /// Gets or sets the scope requested from the enterprise IdP.
        /// </summary>
        [Option("--idp-scope", Description = "Scope requested from the enterprise IdP")]
        public string? IdpScope { get; set; }

        /// <summary>
        /// Gets or sets the enterprise IdP token endpoint.
        /// </summary>
        [Option("--idp-token-endpoint", Description = "Enterprise IdP token endpoint")]
        public string? IdpTokenEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the enterprise IdP base URL.
        /// </summary>
        [Option("--idp-url", Description = "Enterprise IdP base URL")]
        public string? IdpUrl { get; set; }

        /// <summary>
        /// Gets or sets the loopback redirect URI override for the authorization code grant.
        /// </summary>
        [Option("--redirect-uri", Description = "Loopback override")]
        public string? RedirectUri { get; set; }

        /// <summary>
        /// Gets or sets the RFC 8707 resource indicator / audience override.
        /// </summary>
        [Option("--resource", Description = "OAuth resource/audience override")]
        public string? Resource { get; set; }

        /// <summary>
        /// Gets or sets the fallback scope list requested when the authorization server advertises none.
        /// </summary>
        [Option("--scopes", Description = "Fallback scope list (space-separated)")]
        public string? Scopes { get; set; }

        /// <summary>
        /// Gets or sets the directory the Latchkey File and Dpapi backends store cached tokens in.
        /// </summary>
        [Option("--token-cache", Description = "Optional directory for Latchkey File/Dpapi backends")]
        public string? TokenCache { get; set; }

        /// <summary>
        /// Gets or sets the OData service URL.
        /// </summary>
        [Argument(0, "OData service URL")]
        [Required]
        public string? Url { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes the test command.
        /// </summary>
        /// <returns>
        /// Exit code 0 when metadata parses.
        /// </returns>
        public async Task<int> OnExecuteAsync()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                Console.Error.WriteLine("Error: OData service URL is required.");

                return 1;
            }

            try
            {
                using var host = await ToolsMcpHost
                    .CreateAsync(Url, BuildOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None)
                    .ConfigureAwait(false);
                var count = host.Catalog.CompleteEntitySetNames(string.Empty).Count;
                Console.Error.WriteLine($"Entity sets: {count}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");

                return 1;
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Binds this command's flags into a validated <see cref="OutboundOAuthOptions"/>.
        /// </summary>
        /// <returns>
        /// The bound and validated options.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when a URI-shaped flag is not an absolute URI, <see cref="Grant"/> is not a recognized wire
        /// name, or the resulting combination of flags fails <see cref="OutboundOAuthOptions.Validate"/>.
        /// </exception>
        internal OutboundOAuthOptions BuildOptions()
        {
            return OutboundOptionsBinder.Bind(
                AuthToken,
                ClientId,
                ClientSecret,
                Scopes,
                AuthServer,
                Resource,
                Grant,
                RedirectUri,
                TokenCache,
                AuthTimeout,
                ApiKey,
                ApiKeyHeader,
                BasicUser,
                BasicPassword,
                ClientMetadataDocument,
                IdpUrl,
                IdpTokenEndpoint,
                IdpClientId,
                IdpClientSecret,
                IdpScope,
                IdpIdTokenFile);
        }

        #endregion

    }

}
