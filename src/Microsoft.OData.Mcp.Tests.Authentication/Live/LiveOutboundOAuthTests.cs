// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Live
{

    /// <summary>
    /// Opt-in probes that point the real outbound OAuth path at a real OData service, so an operator can prove
    /// against their own tenant what the in-process fixtures can only model.
    /// </summary>
    /// <example>
    /// <code>
    /// $env:ODATA_MCP_LIVE_OAUTH_URL = "https://graph.microsoft.com/v1.0"
    /// $env:ODATA_MCP_CLIENT_ID = "00000000-0000-0000-0000-000000000000"
    /// $env:ODATA_MCP_SCOPES = "https://graph.microsoft.com/.default"
    ///
    /// dotnet test src/Microsoft.OData.Mcp.Tests.Authentication -c Debug --filter FullyQualifiedName~LiveOutboundOAuthTests
    /// </code>
    /// </example>
    /// <remarks>
    /// Every test here reports <see cref="Assert.Inconclusive(string)"/> rather than failing when its
    /// environment variables are unset, so an ordinary run — and continuous integration — is unaffected. The
    /// sign-in test needs a human: the presenter prints the verification URL and the user code and returns,
    /// exactly as <c>odata-mcp start</c> does, and the grant waits out
    /// <see cref="OutboundOAuthOptions.AuthTimeout"/>. Per <c>AUTH-12</c> nothing here prints a token: a
    /// verification URL and a user code are the only two things a human needs, and they are the only two things
    /// written.
    /// </remarks>
    [TestClass]
    public class LiveOutboundOAuthTests
    {

        #region Fields

        /// <summary>
        /// The environment variable naming the authorization server issuer to force, when discovery cannot find
        /// one.
        /// </summary>
        internal const string AuthServerVariable = "ODATA_MCP_AUTH_SERVER";

        /// <summary>
        /// The environment variable naming the pre-registered client identifier the grant runs as.
        /// </summary>
        internal const string ClientIdVariable = "ODATA_MCP_CLIENT_ID";

        /// <summary>
        /// The environment variable naming the grant to pin, in its wire spelling.
        /// </summary>
        internal const string GrantVariable = "ODATA_MCP_GRANT";

        /// <summary>
        /// The environment variable naming the space-delimited fallback scope list.
        /// </summary>
        internal const string ScopesVariable = "ODATA_MCP_SCOPES";

        /// <summary>
        /// The environment variable naming the OData service root to probe.
        /// </summary>
        internal const string ServiceUrlVariable = "ODATA_MCP_LIVE_OAUTH_URL";

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the MSTest context every diagnostic line is written to.
        /// </summary>
        public TestContext TestContext { get; set; } = null!;

        #endregion

        #region Public Methods

        /// <summary>
        /// Reports what a real service actually answers an unauthenticated <c>$metadata</c> request with, so an
        /// operator can see the status, the challenge, and whether RFC 9728 metadata exists before deciding
        /// which flags they need.
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task LiveOutboundOAuth_Probe_ReportsChallenge()
        {
            var serviceUrl = Environment.GetEnvironmentVariable(ServiceUrlVariable);

            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                Assert.Inconclusive($"Set {ServiceUrlVariable} to run this probe.");
            }

            var root = new Uri(serviceUrl.TrimEnd('/') + "/", UriKind.Absolute);

            using var client = new HttpClient();

            try
            {
                using var response = await client.GetAsync(new Uri(root, "$metadata"), CancellationToken.None);

                Write($"{root}$metadata answered {(int)response.StatusCode} {response.StatusCode}");

                IReadOnlyList<string> challenges = [.. response.Headers.WwwAuthenticate.Select(value => value.ToString())];

                Write(challenges.Count == 0
                    ? "WWW-Authenticate: (none)"
                    : $"WWW-Authenticate: {string.Join(" | ", challenges)}");

                var challenge = WwwAuthenticateParser.SelectBearer(WwwAuthenticateParser.ParseAll(challenges));

                foreach (var candidate in OAuthDiscovery.PrmCandidates(root, challenge))
                {
                    using var metadata = await client.GetAsync(candidate, CancellationToken.None);

                    Write($"Protected resource metadata at {candidate}: {(int)metadata.StatusCode} {metadata.StatusCode}");
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
            {
                Write($"The probe could not reach {root}: {exception.Message}");
            }
        }

        /// <summary>
        /// Signs in against a real service and proves the catalog it produced is usable: the query tool is
        /// advertised and listing the entity sets succeeds.
        /// </summary>
        [TestMethod]
        [Timeout(360000)]
        public async Task LiveOutboundOAuth_SignsInAndServesCatalog()
        {
            var serviceUrl = Environment.GetEnvironmentVariable(ServiceUrlVariable);
            var clientId = Environment.GetEnvironmentVariable(ClientIdVariable);

            if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(clientId))
            {
                Assert.Inconclusive($"Set {ServiceUrlVariable} and {ClientIdVariable} to run this probe.");
            }

            var options = new OutboundOAuthOptions
            {
                AuthTimeout = TimeSpan.FromSeconds(300),
                ClientId = clientId,
                ConsentPresenter = PresentAsync
            };

            if (Environment.GetEnvironmentVariable(AuthServerVariable) is { } authServer && !string.IsNullOrWhiteSpace(authServer))
            {
                options.AuthServer = new Uri(authServer, UriKind.Absolute);
            }

            if (Environment.GetEnvironmentVariable(GrantVariable) is { } grant && !string.IsNullOrWhiteSpace(grant))
            {
                options.Grant = OutboundGrantKindParser.TryParse(grant, out var parsed)
                    ? parsed
                    : throw new InvalidOperationException($"{GrantVariable} is not a grant this build accepts: {grant}");
            }

            if (Environment.GetEnvironmentVariable(ScopesVariable) is { } scopes && !string.IsNullOrWhiteSpace(scopes))
            {
                options.Scopes = [.. scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            }

            using var host = await ToolsMcpHost.CreateAsync(
                serviceUrl,
                options,
                includeStdioMcp: false,
                verbose: false,
                lifetime: null,
                CancellationToken.None);

            host.Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");

            var result = await host.Session.Runtime.InvokeAsync("odata_list_entity_sets", new Dictionary<string, JsonElement>(), CancellationToken.None);

            result.IsError.Should().BeFalse("odata_list_entity_sets answered: {0}", result.Text);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Prints the sign-in URL and user code and returns, leaving a human to complete the grant.
        /// </summary>
        /// <param name="request">The consent request the outbound OAuth client produced.</param>
        /// <param name="cancellationToken">The token that cancels the presentation.</param>
        /// <returns>
        /// A completed task.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Per <c>AUTH-12</c> this prints the verification URL and the user code and nothing else: no device
        /// code, no authorization code, no token.
        /// </remarks>
        internal Task PresentAsync(OutboundConsentRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Write($"Sign in at {request.Url}");

            if (!string.IsNullOrWhiteSpace(request.UserCode))
            {
                Write($"Code: {request.UserCode}");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Writes a diagnostic line to both the MSTest output and stderr, so it is visible whether the run is
        /// hosted by a test explorer or a terminal.
        /// </summary>
        /// <param name="message">The line to write.</param>
        internal void Write(string message)
        {
            TestContext?.WriteLine(message);
            Console.Error.WriteLine(message);
        }

        #endregion

    }

}
