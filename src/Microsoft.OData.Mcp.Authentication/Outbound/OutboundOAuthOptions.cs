// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Operator-supplied and discovered outbound OAuth settings for a single remote OData service — the
    /// options CLI hop 2 (<c>ODataOutboundAuthHandler</c>, <c>OutboundOAuthClient</c>, and the grant classes)
    /// runs on.
    /// </summary>
    /// <example>
    /// <code>
    /// var options = new OutboundOAuthOptions
    /// {
    ///     ClientId = "my-cli-client",
    ///     Scopes = ["offline_access"],
    ///     Grant = OutboundGrantKind.DeviceCode
    /// };
    ///
    /// options = OutboundOAuthOptions.FromEnvironment(options);
    /// options.Validate();
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>specs/v3/AUTHENTICATION.md</c> this type is named <c>OutboundOAuthOptions</c>, deliberately not
    /// <c>ClientOAuthOptions</c> — that SDK type is hop 1's MCP-transport options and must not be reused here.
    /// It carries no certificate properties in v1.
    /// </remarks>
    public sealed class OutboundOAuthOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets the raw API key value for the API key escape hatch, or <see langword="null"/> when unset.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the header name <see cref="ApiKey"/> is sent under, or <see langword="null"/> when unset.
        /// </summary>
        /// <remarks>
        /// Required whenever <see cref="ApiKey"/> is set; see <see cref="Validate"/>.
        /// </remarks>
        public string? ApiKeyHeader { get; set; }

        /// <summary>
        /// Gets or sets the operator's <c>--auth-server</c> override, or <see langword="null"/> to discover the
        /// authorization server from the challenge and protected resource metadata.
        /// </summary>
        public Uri? AuthServer { get; set; }

        /// <summary>
        /// Gets or sets how long an interactive grant may run before it is abandoned. Defaults to 300 seconds.
        /// </summary>
        public TimeSpan AuthTimeout { get; set; } = TimeSpan.FromSeconds(300);

        /// <summary>
        /// Gets or sets the escape-hatch bearer token that skips OAuth entirely, or <see langword="null"/> when unset.
        /// </summary>
        public string? AuthToken { get; set; }

        /// <summary>
        /// Gets or sets the RFC 7617 Basic authentication password, or <see langword="null"/> when unset.
        /// </summary>
        /// <remarks>
        /// Required whenever <see cref="BasicUser"/> is set; see <see cref="Validate"/>.
        /// </remarks>
        public string? BasicPassword { get; set; }

        /// <summary>
        /// Gets or sets the RFC 7617 Basic authentication user name, or <see langword="null"/> when unset.
        /// </summary>
        public string? BasicUser { get; set; }

        /// <summary>
        /// Gets or sets the OAuth client id this process authenticates as, or <see langword="null"/> when dynamic
        /// client registration will supply one.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client identity metadata document (CIMD) URI, or <see langword="null"/> when unset.
        /// </summary>
        public Uri? ClientMetadataDocumentUri { get; set; }

        /// <summary>
        /// Gets or sets the OAuth client secret for a confidential client, or <see langword="null"/> when unset.
        /// </summary>
        /// <remarks>
        /// <see cref="FromEnvironment(OutboundOAuthOptions)"/> binds this from
        /// <see cref="ODataMcpAuthConstants.ClientSecretEnvironmentVariable"/> when it is unset. Never logged or
        /// printed.
        /// </remarks>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the delegate invoked to present a consent request to the operator or an MCP client, or
        /// <see langword="null"/> when interactive sign-in should throw <see cref="OAuthConsentRequiredException"/>
        /// instead.
        /// </summary>
        /// <remarks>
        /// Deliberately a delegate, not a new DI interface, per <c>AUTH-7</c>. The auth handler reads this
        /// property on every 401 rather than capturing it in a constructor, because <c>IHttpClientFactory</c>
        /// may cache the handler across a presenter change.
        /// </remarks>
        public Func<OutboundConsentRequest, CancellationToken, Task>? ConsentPresenter { get; set; }

        /// <summary>
        /// Gets or sets the operator's <c>--grant</c> selection, or <see langword="null"/> to let
        /// <c>GrantSelector</c> pick from advertised <c>grant_types_supported</c>.
        /// </summary>
        public OutboundGrantKind? Grant { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="ODataMcpAuthConstants.IdTokenEnvironmentVariable"/> was present when
        /// <see cref="FromEnvironment(OutboundOAuthOptions)"/> last ran.
        /// </summary>
        /// <remarks>
        /// Set by <see cref="FromEnvironment(OutboundOAuthOptions)"/>, never by an operator flag, so
        /// <see cref="Validate"/> can treat the environment variable as a valid identity assertion id-token
        /// source without reading <see cref="Environment.GetEnvironmentVariable(string)"/> itself.
        /// </remarks>
        public bool HasEnvironmentIdToken { get; set; }

        /// <summary>
        /// Gets whether any of <see cref="AuthToken"/>, <see cref="ApiKey"/>, or <see cref="BasicUser"/> is set,
        /// meaning this process authenticates without running an OAuth grant at all.
        /// </summary>
        public bool HasExplicitCredentials =>
            !string.IsNullOrWhiteSpace(AuthToken) || !string.IsNullOrWhiteSpace(ApiKey) || !string.IsNullOrWhiteSpace(BasicUser);

        /// <summary>
        /// Gets or sets the enterprise IdP client id used to obtain the id token the identity assertion grant
        /// exchanges, or <see langword="null"/> when unset.
        /// </summary>
        public string? IdpClientId { get; set; }

        /// <summary>
        /// Gets or sets the enterprise IdP client secret, or <see langword="null"/> when unset.
        /// </summary>
        public string? IdpClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the path to a UTF-8 OIDC id token file for the identity assertion grant, or
        /// <see langword="null"/> when unset.
        /// </summary>
        /// <remarks>
        /// <see cref="ODataMcpAuthConstants.IdTokenEnvironmentVariable"/> is the other id-token source; see
        /// <see cref="HasEnvironmentIdToken"/> and <see cref="FromEnvironment(OutboundOAuthOptions)"/>. The token
        /// is never passed on argv.
        /// </remarks>
        public string? IdpIdTokenFile { get; set; }

        /// <summary>
        /// Gets or sets the scope requested from the enterprise IdP, or <see langword="null"/> when unset.
        /// </summary>
        public string? IdpScope { get; set; }

        /// <summary>
        /// Gets or sets the enterprise IdP token endpoint, or <see langword="null"/> when unset.
        /// </summary>
        /// <remarks>
        /// Either this or <see cref="IdpUrl"/> satisfies the identity assertion endpoint requirement in
        /// <see cref="Validate"/>.
        /// </remarks>
        public Uri? IdpTokenEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the enterprise IdP base URL, or <see langword="null"/> when unset.
        /// </summary>
        /// <remarks>
        /// Either this or <see cref="IdpTokenEndpoint"/> satisfies the identity assertion endpoint requirement in
        /// <see cref="Validate"/>.
        /// </remarks>
        public Uri? IdpUrl { get; set; }

        /// <summary>
        /// Gets or sets the loopback redirect URI override for the authorization code grant, or
        /// <see langword="null"/> to use an ephemeral loopback port.
        /// </summary>
        public Uri? RedirectUri { get; set; }

        /// <summary>
        /// Gets or sets the RFC 8707 resource indicator / audience override, or <see langword="null"/> to derive
        /// it from protected resource metadata or the service root's origin.
        /// </summary>
        public Uri? Resource { get; set; }

        /// <summary>
        /// Gets or sets the delegate that filters or appends scopes after <c>offline_access</c> is appended, or
        /// <see langword="null"/> when unset.
        /// </summary>
        /// <remarks>
        /// Runs last in scope resolution and may remove <c>offline_access</c>. Reused as-is from the SDK per
        /// <c>specs/v3/AUTHENTICATION.md</c>'s SDK reuse table.
        /// </remarks>
        public SdkAuth.ScopeSelectorDelegate? ScopeSelector { get; set; }

        /// <summary>
        /// Gets or sets the fallback scope list requested when the authorization server advertises none.
        /// </summary>
        public List<string> Scopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the directory the Latchkey File and Dpapi backends store cached tokens in, or
        /// <see langword="null"/> for the per-user default (see <c>LatchkeyTokenCache.DefaultTokenCacheDirectory</c>).
        /// </summary>
        public string? TokenCachePath { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Binds settings this instance does not already carry from their environment variable equivalents.
        /// </summary>
        /// <param name="options">The options instance to bind into and return.</param>
        /// <returns>
        /// <paramref name="options"/>, mutated in place: <see cref="ClientSecret"/> is set from
        /// <see cref="ODataMcpAuthConstants.ClientSecretEnvironmentVariable"/> when it was unset, and
        /// <see cref="HasEnvironmentIdToken"/> is set to <see langword="true"/> when
        /// <see cref="ODataMcpAuthConstants.IdTokenEnvironmentVariable"/> is present and
        /// <see cref="IdpIdTokenFile"/> was unset.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// var options = OutboundOAuthOptions.FromEnvironment(new OutboundOAuthOptions());
        /// </code>
        /// </example>
        /// <remarks>
        /// Never logs or prints the values it reads. An explicit <see cref="ClientSecret"/> is left untouched
        /// even when the environment variable is also set, so a flag always wins over the environment.
        /// </remarks>
        public static OutboundOAuthOptions FromEnvironment(OutboundOAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                var clientSecret = Environment.GetEnvironmentVariable(ODataMcpAuthConstants.ClientSecretEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(clientSecret))
                {
                    options.ClientSecret = clientSecret;
                }
            }

            if (string.IsNullOrWhiteSpace(options.IdpIdTokenFile) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable)))
            {
                options.HasEnvironmentIdToken = true;
            }

            return options;
        }

        /// <summary>
        /// Validates that this instance's settings are internally consistent before an outbound grant is attempted.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="ApiKey"/> is set without <see cref="ApiKeyHeader"/>; when <see cref="BasicUser"/>
        /// is set without <see cref="BasicPassword"/>; when <see cref="AuthTimeout"/> is not greater than zero;
        /// when <see cref="Grant"/> is set to an undefined <see cref="OutboundGrantKind"/> value; or when
        /// <see cref="Grant"/> is <see cref="OutboundGrantKind.IdentityAssertion"/> and neither
        /// <see cref="IdpUrl"/> nor <see cref="IdpTokenEndpoint"/> is set, neither <see cref="IdpIdTokenFile"/>
        /// nor <see cref="HasEnvironmentIdToken"/> supplies an id token, or either <see cref="IdpClientId"/> or
        /// <see cref="ClientId"/> is unset.
        /// </exception>
        /// <example>
        /// <code>
        /// var options = new OutboundOAuthOptions { ApiKey = "secret" };
        ///
        /// options.Validate(); // throws: --api-key requires --api-key-header.
        /// </code>
        /// </example>
        /// <remarks>
        /// Every thrown message names the CLI flag that is missing or invalid, so a caller can surface it
        /// directly to the operator without translation.
        /// </remarks>
        public void Validate()
        {
            if (!string.IsNullOrWhiteSpace(ApiKey) && string.IsNullOrWhiteSpace(ApiKeyHeader))
            {
                throw new ArgumentException("--api-key requires --api-key-header.");
            }

            if (!string.IsNullOrWhiteSpace(BasicUser) && string.IsNullOrWhiteSpace(BasicPassword))
            {
                throw new ArgumentException("--basic-user requires --basic-password.");
            }

            if (AuthTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentException("--auth-timeout must be greater than zero.");
            }

            if (Grant is not { } grant)
            {
                return;
            }

            if (!Enum.IsDefined(grant))
            {
                throw new ArgumentException("--grant value is not a defined grant kind.");
            }

            if (grant != OutboundGrantKind.IdentityAssertion)
            {
                return;
            }

            if (IdpUrl is null && IdpTokenEndpoint is null)
            {
                throw new ArgumentException("--grant identity_assertion requires --idp-url or --idp-token-endpoint.");
            }

            if (string.IsNullOrWhiteSpace(IdpIdTokenFile) && !HasEnvironmentIdToken)
            {
                throw new ArgumentException("--grant identity_assertion requires --idp-id-token-file or the ODATA_MCP_ID_TOKEN environment variable.");
            }

            if (string.IsNullOrWhiteSpace(IdpClientId) || string.IsNullOrWhiteSpace(ClientId))
            {
                throw new ArgumentException("--grant identity_assertion requires --idp-client-id and --client-id.");
            }
        }

        #endregion

    }

}
