// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Core.Diagnostics;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tools.Hosting;

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// <c>odata-mcp try &lt;url&gt;</c>: probe a service before committing to it. Fetches <c>$metadata</c>, reads one row
    /// from the first entity set, says which of the two is secured, checks the row against the model, and prints a
    /// verdict. With auth flags it probes again through the authenticating client; without them, on a 401 it runs
    /// OAuth discovery and prints what it found.
    /// </summary>
    /// <example>
    /// <code>
    /// odata-mcp try https://services.odata.org/V4/Northwind/Northwind.svc
    /// odata-mcp try https://api.example.com/odata --client-id {app-id}
    /// </code>
    /// </example>
    [Command(Name = "try", Description = "Probe an OData service: metadata, one row, security, and whether the results match the model.")]
    public class TryCommand
    {

        #region Fields

        /// <summary>
        /// Exit code when the service needs sign-in that no flag provided. Distinct from a hard failure so scripts can branch.
        /// </summary>
        public const int ExitSignInRequired = 2;

        #endregion

        #region Properties

        [Option("--api-key", Description = "Raw key")]
        public string? ApiKey { get; set; }

        [Option("--api-key-header", Description = "Header name. Fail if key set without header")]
        public string? ApiKeyHeader { get; set; }

        [Option("--auth-server", Description = "AS issuer override")]
        public string? AuthServer { get; set; }

        [Option("--auth-timeout <SECONDS>", Description = "Seconds. Default 300")]
        public int? AuthTimeout { get; set; }

        [Option("-t|--auth-token", Description = "Authentication token for the OData service")]
        public string? AuthToken { get; set; }

        [Option("--basic-password", Description = "Basic auth password")]
        public string? BasicPassword { get; set; }

        [Option("--basic-user", Description = "Basic auth user name")]
        public string? BasicUser { get; set; }

        [Option("--client-id", Description = "Public/confidential client id")]
        public string? ClientId { get; set; }

        [Option("--client-metadata-document", Description = "CIMD URI")]
        public string? ClientMetadataDocument { get; set; }

        [Option("--client-secret", Description = "Client secret. Also read from ODATA_MCP_CLIENT_SECRET")]
        public string? ClientSecret { get; set; }

        [Option("--grant", Description = "device_code | authorization_code | client_credentials | identity_assertion")]
        public string? Grant { get; set; }

        [Option("--idp-client-id", Description = "Enterprise IdP client id")]
        public string? IdpClientId { get; set; }

        [Option("--idp-client-secret", Description = "Enterprise IdP client secret")]
        public string? IdpClientSecret { get; set; }

        [Option("--idp-id-token-file", Description = "Path to a UTF-8 OIDC id token")]
        public string? IdpIdTokenFile { get; set; }

        [Option("--idp-scope", Description = "Scope requested from the enterprise IdP")]
        public string? IdpScope { get; set; }

        [Option("--idp-token-endpoint", Description = "Enterprise IdP token endpoint")]
        public string? IdpTokenEndpoint { get; set; }

        [Option("--idp-url", Description = "Enterprise IdP base URL")]
        public string? IdpUrl { get; set; }

        /// <summary>
        /// Where the report is written. Defaults to standard output.
        /// </summary>
        public TextWriter Output { get; set; } = Console.Out;

        [Option("--redirect-uri", Description = "Loopback override")]
        public string? RedirectUri { get; set; }

        [Option("--resource", Description = "OAuth resource/audience override")]
        public string? Resource { get; set; }

        [Option("--scopes", Description = "Fallback scope list (space-separated)")]
        public string? Scopes { get; set; }

        [Option("--token-cache", Description = "Optional directory for Latchkey File/Dpapi backends")]
        public string? TokenCache { get; set; }

        [Argument(0, "OData service root URL (a pasted $metadata URL is accepted)")]
        [Required]
        public string? Url { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Runs the probe and prints the report.
        /// </summary>
        /// <returns>
        /// 0 when the service is ready, <see cref="ExitSignInRequired"/> when it needs sign-in no flag provided, 1 otherwise.
        /// </returns>
        public async Task<int> OnExecuteAsync()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                Console.Error.WriteLine("Error: OData service URL is required.");
                return 1;
            }

            OutboundOAuthOptions options;
            Uri root;
            try
            {
                options = BuildOptions();
                root = ToolsMcpHost.NormalizeServiceRoot(Url);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }

            using var host = ToolsMcpHost.BuildHost(root, options, includeStdioMcp: false, verbose: false, lifetime: null, configureServices: null, out _);
            var factory = host.Services.GetRequiredService<IHttpClientFactory>();
            var parser = host.Services.GetRequiredService<CsdlParser>();

            var anonymous = await new ServiceProbe(factory.CreateClient(), parser).ProbeAsync(root, CancellationToken.None).ConfigureAwait(false);
            await Output.WriteAsync(Render(anonymous, "anonymously")).ConfigureAwait(false);

            if (anonymous.Verdict is not (ServiceVerdict.DataSecured or ServiceVerdict.MetadataSecured))
            {
                return anonymous.Verdict == ServiceVerdict.Ready ? 0 : 1;
            }

            if (HasAnyCredential(options))
            {
                var authenticated = await new ServiceProbe(factory.CreateClient(ODataMcpAuthConstants.ODataHttpClientName), parser).ProbeAsync(root, CancellationToken.None).ConfigureAwait(false);
                await Output.WriteAsync(Render(authenticated, "with the credentials you passed")).ConfigureAwait(false);

                return authenticated.Verdict == ServiceVerdict.Ready ? 0 : 1;
            }

            await Output.WriteAsync(await DiscoverAsync(host, anonymous, options, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

            return ExitSignInRequired;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Binds the outbound OAuth flags exactly as <c>start</c> does.
        /// </summary>
        /// <returns>
        /// The options.
        /// </returns>
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

        /// <summary>
        /// The first <c>WWW-Authenticate</c> challenge, formatted for the security line.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>
        /// The challenge in parentheses, or a note that there was none.
        /// </returns>
        internal static string Challenge(ServiceProbeResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            var first = result.Challenges.FirstOrDefault();

            return string.IsNullOrWhiteSpace(first) ? " (no WWW-Authenticate header)" : $" ({first})";
        }

        /// <summary>
        /// Runs OAuth discovery from the challenge the anonymous probe collected and describes what it found, without
        /// starting a grant.
        /// </summary>
        /// <param name="host">The host that owns the discovery services.</param>
        /// <param name="result">The anonymous probe result.</param>
        /// <param name="options">The options, for overrides.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The security section of the report.
        /// </returns>
        internal static async Task<string> DiscoverAsync(Microsoft.Extensions.Hosting.IHost host, ServiceProbeResult result, OutboundOAuthOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(options);

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("Sign-in required. Discovering how this service wants to be authenticated…");
            try
            {
                var discovery = host.Services.GetRequiredService<OAuthDiscovery>();
                var found = await discovery.DiscoverAsync(result.ServiceRoot, result.Challenges, result.SecuredStatusCode ?? 401, options.AuthServer, options.Resource, cancellationToken).ConfigureAwait(false);
                report.AppendLine($"  Protected resource metadata : {(found.ProtectedResource is null ? "not published (RFC 9728 document not found)" : "found")}");
                report.AppendLine($"  Authorization server        : {found.AuthorizationServer.Issuer}");
                report.AppendLine($"  Grants advertised           : {(found.AdvertisedGrants.Count == 0 ? "none listed" : string.Join(", ", found.AdvertisedGrants))}");
                if (!string.IsNullOrWhiteSpace(found.ChallengeScope))
                {
                    report.AppendLine($"  Scope from challenge        : {found.ChallengeScope}");
                }

                if (found.Resource is not null)
                {
                    report.AppendLine($"  Resource                    : {found.Resource}");
                }

                report.AppendLine();
                report.AppendLine($"Next: odata-mcp try {result.ServiceRoot} --client-id <your-app-id>   (add --grant, --scopes, or --auth-server if discovery needs help)");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                report.AppendLine($"  Discovery failed: {ex.Message}");
                report.AppendLine();
                report.AppendLine($"Next: pass credentials explicitly, e.g. odata-mcp try {result.ServiceRoot} --auth-token <token>, or --auth-server <issuer> --client-id <app-id> when the service publishes no OAuth metadata.");
            }

            return report.ToString();
        }

        /// <summary>
        /// Whether any flag supplied a way to authenticate: a token, key, basic credentials, or an OAuth client.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>
        /// <c>true</c> when the authenticating client would do something the anonymous one did not.
        /// </returns>
        internal static bool HasAnyCredential(OutboundOAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return options.HasExplicitCredentials || !string.IsNullOrWhiteSpace(options.ClientId) || options.Grant is not null;
        }

        /// <summary>
        /// Renders one probe result as the four numbered lines plus a verdict.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="how">How the probe was made, for the heading: <c>anonymously</c> or <c>with the credentials you passed</c>.</param>
        /// <returns>
        /// The report text.
        /// </returns>
        internal static string Render(ServiceProbeResult result, string how)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentException.ThrowIfNullOrWhiteSpace(how);

            var report = new StringBuilder();
            report.AppendLine($"Probing {result.ServiceRoot} {how}");
            report.AppendLine($"  [1/4] Metadata  {Label(result.Metadata)}  {result.Metadata.Detail}");
            report.AppendLine($"  [2/4] Data      {Label(result.Data)}  {result.Data.Detail}");
            report.AppendLine($"  [3/4] Security  {SecurityLine(result)}");
            report.AppendLine($"  [4/4] Results   {Label(result.Results)}  {result.Results.Detail}");
            report.AppendLine($"Verdict: {VerdictLine(result)}");

            return report.ToString();
        }

        /// <summary>
        /// A fixed-width label for a step outcome.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <returns>
        /// <c>ok</c>, <c>401</c>, <c>403</c>, <c>404</c>, <c>bad</c>, <c>fail</c>, or <c>skip</c>, padded to four characters.
        /// </returns>
        internal static string Label(ProbeStep step)
        {
            ArgumentNullException.ThrowIfNull(step);

            var label = step.Outcome switch
            {
                ProbeOutcome.Passed => "ok",
                ProbeOutcome.Unauthorized => "401",
                ProbeOutcome.Forbidden => "403",
                ProbeOutcome.NotFound => "404",
                ProbeOutcome.Unreadable => "bad",
                ProbeOutcome.Failed => "fail",
                _ => "skip"
            };

            return label.PadRight(4);
        }

        /// <summary>
        /// The security line: which of metadata and data refused an anonymous caller, and with what challenge.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>
        /// One line.
        /// </returns>
        internal static string SecurityLine(ServiceProbeResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.Metadata.IsSecured())
            {
                return $"{Label(result.Metadata)}  metadata itself is secured{Challenge(result)}";
            }

            if (result.Data.IsSecured())
            {
                return $"{Label(result.Data)}  metadata is public, data is secured{Challenge(result)}";
            }

            if (result.Metadata.Outcome == ProbeOutcome.Passed && result.Data.Outcome == ProbeOutcome.Passed)
            {
                return "none  metadata and data both answered";
            }

            return "skip  could not be determined";
        }

        /// <summary>
        /// The verdict line with the next command to run.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>
        /// One line.
        /// </returns>
        internal static string VerdictLine(ServiceProbeResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            return result.Verdict switch
            {
                ServiceVerdict.Ready => $"ready. Next: odata-mcp start {result.ServiceRoot}",
                ServiceVerdict.DataSecured => "data requires sign-in.",
                ServiceVerdict.MetadataSecured => "metadata requires sign-in.",
                ServiceVerdict.Unreachable => "unreachable. Check the service root; the tool appends $metadata itself.",
                _ => "unreadable. The service answered, but not with OData the catalog can use."
            };
        }

        #endregion

    }

}
