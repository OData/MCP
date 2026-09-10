// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tools.Hosting;

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// Interactive wizard command that generates the <c>claude mcp add</c> registration line for a remote OData
    /// service, including the outbound OAuth flags <c>odata-mcp start</c> needs to sign in.
    /// </summary>
    /// <remarks>
    /// This command provides an interactive experience for users to configure their OData
    /// service connection and generates the appropriate <c>claude mcp add</c> command for Claude Code.
    /// Tests must assign <see cref="Input"/> and <see cref="Output"/> instead of calling
    /// <see cref="Console.SetIn(TextReader)"/>. Windows testhost wraps <c>SetIn</c> in
    /// <c>SyncTextReader</c> and leaves <see cref="Console.IsInputRedirected"/> false, so
    /// <see cref="Console.ReadKey(bool)"/> blocks the Visual Studio Test Explorer.
    /// <para>
    /// Per <c>specs/v3/AUTHENTICATION.md</c> "CLI UX → <c>odata-mcp add</c>" the generated command never
    /// carries an access or refresh token, and never carries <c>--env</c>: <c>claude mcp add --env</c> takes
    /// <c>KEY=VALUE</c>, so a bare name is either invalid or empty. A client secret is an instruction, not a
    /// value. An API key, a Basic password, and a pasted bearer token do reach the configuration file, and each
    /// one is warned about at the moment it is collected.
    /// </para>
    /// </remarks>
    [Command(Name = "add", Description = "Interactive wizard to generate Claude Code MCP registration command")]
    public class AddCommand
    {

        #region Fields

        /// <summary>
        /// The instruction printed whenever the client credentials grant is chosen, verbatim from
        /// <c>specs/v3/AUTHENTICATION.md</c>.
        /// </summary>
        internal const string ClientCredentialsInstruction = "Set ODATA_MCP_CLIENT_SECRET in the environment that launches the MCP host; start reads it.";

        /// <summary>
        /// The warning printed whenever a credential is about to be written into MCP configuration, verbatim
        /// from <c>specs/v3/AUTHENTICATION.md</c>.
        /// </summary>
        internal const string SecretInConfigWarning = "This secret will live in MCP config the host process can read. Prefer OAuth (1–4).";

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the last-chance service registration hook the probe and the connection test apply,
        /// mirroring <see cref="ToolsMcpHost.CreateAsync"/>. Production leaves it <see langword="null"/>.
        /// </summary>
        internal Action<IServiceCollection>? ConfigureServices { get; set; }

        /// <summary>
        /// Gets or sets the error writer. Defaults to <see cref="Console.Error"/>.
        /// </summary>
        internal TextWriter Error { get; set; } = Console.Error;

        /// <summary>
        /// Gets or sets the input reader. Defaults to <see cref="Console.In"/>.
        /// </summary>
        internal TextReader Input { get; set; } = Console.In;

        /// <summary>
        /// Gets or sets the output writer. Defaults to <see cref="Console.Out"/>.
        /// </summary>
        internal TextWriter Output { get; set; } = Console.Out;

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes the interactive wizard for generating MCP registration commands.
        /// </summary>
        /// <returns>
        /// Exit code: <c>0</c> for success, <c>1</c> when the wizard could not finish.
        /// </returns>
        public async Task<int> OnExecuteAsync()
        {
            Output.WriteLine("🚀 OData MCP Setup Wizard for Claude Code");
            Output.WriteLine("==========================================\n");
            Output.WriteLine("Let's set up your OData connection!\n");

            try
            {
                var url = PromptForUrl();
                var name = PromptForName(url);
                var choice = PromptForAuthenticationMode();
                var settings = await CollectAuthSettingsAsync(url, choice, CancellationToken.None).ConfigureAwait(false);
                var scope = PromptForScope();
                var verbose = PromptForVerboseLogging();

                if (PromptConfirm("Would you like to test the connection first?", true))
                {
                    await TestConnection(url, settings).ConfigureAwait(false);
                }

                DisplayResult(BuildMcpCommand(name, url, settings, scope, verbose));

                return 0;
            }
            catch (Exception ex)
            {
                WriteColoredLine(ConsoleColor.Red, $"\n❌ Error: {ex.Message}");

                return 1;
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the MCP registration command from the wizard's answers, printing the instruction or warning
        /// the chosen credential requires.
        /// </summary>
        /// <param name="name">The name for the MCP server.</param>
        /// <param name="url">The OData service URL.</param>
        /// <param name="settings">The authentication settings the wizard collected.</param>
        /// <param name="scope">The scope for the MCP server (<c>user</c> or <c>project</c>).</param>
        /// <param name="verbose">Whether to enable verbose logging.</param>
        /// <returns>
        /// The formatted MCP command string.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/>, <paramref name="url"/>, or <paramref name="scope"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <example>
        /// <code>
        /// // claude mcp add graph --scope user -- dotnet odata-mcp -- start "https://graph.microsoft.com/v1.0"
        /// //   --client-id "{app}" --scopes "https://graph.microsoft.com/.default" --grant device_code
        /// </code>
        /// </example>
        /// <remarks>
        /// <c>--token-cache</c> is emitted only when the operator chose a directory other than the default
        /// operating system store, and <c>--auth-token</c> only for
        /// <see cref="AddAuthChoice.PasteToken"/>. There is no <c>--client-secret</c> and no <c>--env</c> on
        /// any path.
        /// </remarks>
        internal string BuildMcpCommand(string name, string url, AddCommandAuthSettings settings, string scope, bool verbose)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);

            var builder = new StringBuilder($"claude mcp add {name}");

            builder.Append($" --scope {scope}");
            builder.Append($" -- dotnet odata-mcp -- start \"{url}\"");

            if (!string.IsNullOrWhiteSpace(settings.AuthServer))
            {
                builder.Append($" --auth-server \"{settings.AuthServer}\"");
            }

            if (!string.IsNullOrWhiteSpace(settings.ClientId))
            {
                builder.Append($" --client-id \"{settings.ClientId}\"");
            }

            if (!string.IsNullOrWhiteSpace(settings.Scopes))
            {
                builder.Append($" --scopes \"{settings.Scopes}\"");
            }

            if (settings.Grant is { } grant)
            {
                builder.Append($" --grant {OutboundGrantKindParser.ToWireName(grant)}");
            }

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                builder.Append($" --api-key \"{settings.ApiKey}\" --api-key-header \"{settings.ApiKeyHeader}\"");
            }

            if (!string.IsNullOrWhiteSpace(settings.BasicUser))
            {
                builder.Append($" --basic-user \"{settings.BasicUser}\" --basic-password \"{settings.BasicPassword}\"");
            }

            if (!string.IsNullOrWhiteSpace(settings.TokenCachePath))
            {
                builder.Append($" --token-cache \"{settings.TokenCachePath}\"");
            }

            if (!string.IsNullOrWhiteSpace(settings.AuthToken))
            {
                builder.Append($" --auth-token \"{settings.AuthToken}\"");
            }

            if (verbose)
            {
                builder.Append(" --verbose");
            }

            if (settings.Grant is OutboundGrantKind.ClientCredentials)
            {
                Output.WriteLine(ClientCredentialsInstruction);
            }

            if (!string.IsNullOrWhiteSpace(settings.ApiKey) || !string.IsNullOrWhiteSpace(settings.BasicUser))
            {
                Output.WriteLine(SecretInConfigWarning);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Runs the prompts the chosen authentication path needs and returns everything the generated command
        /// and the connection test require.
        /// </summary>
        /// <param name="url">The OData service URL, probed when the operator chose discovery.</param>
        /// <param name="choice">The path the operator chose.</param>
        /// <param name="cancellationToken">The token that cancels the discovery probe.</param>
        /// <returns>
        /// The collected settings.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <remarks>
        /// The token cache directory is only offered on the OAuth paths, because it is the store access and
        /// refresh tokens live in and the other three paths never obtain one.
        /// </remarks>
        internal async Task<AddCommandAuthSettings> CollectAuthSettingsAsync(string url, AddAuthChoice choice, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            var settings = new AddCommandAuthSettings();

            switch (choice)
            {
                case AddAuthChoice.None:
                    return settings;

                case AddAuthChoice.Discover:
                    await DiscoverAsync(url, settings, cancellationToken).ConfigureAwait(false);
                    break;

                case AddAuthChoice.DeviceCode:
                case AddAuthChoice.AuthorizationCode:
                case AddAuthChoice.ClientCredentials:
                    settings.Grant = choice is AddAuthChoice.DeviceCode
                        ? OutboundGrantKind.DeviceCode
                        : choice is AddAuthChoice.AuthorizationCode ? OutboundGrantKind.AuthorizationCode : OutboundGrantKind.ClientCredentials;
                    PromptForOAuthClient(settings);
                    break;

                case AddAuthChoice.ApiKey:
                    Output.WriteLine(SecretInConfigWarning);
                    settings.ApiKeyHeader = PromptInput("Header name the API key is sent in", "X-Api-Key");
                    settings.ApiKey = PromptPassword("Enter your API key");

                    return settings;

                case AddAuthChoice.Basic:
                    Output.WriteLine(SecretInConfigWarning);
                    settings.BasicUser = PromptInput("Username");
                    settings.BasicPassword = PromptPassword("Password");

                    return settings;

                case AddAuthChoice.PasteToken:
                    Output.WriteLine("stored only if you insist; prefer 1–4");
                    settings.AuthToken = PromptPassword("Enter your bearer token");

                    return settings;

                default:
                    throw new ArgumentOutOfRangeException(nameof(choice), choice, "The authentication choice is not a defined AddAuthChoice value.");
            }

            if (settings.Grant is not null)
            {
                PromptForTokenCache(settings);
            }

            return settings;
        }

        /// <summary>
        /// Builds a service provider carrying the handler-free <c>"OAuth"</c> named client discovery and the
        /// <c>$metadata</c> probe run through.
        /// </summary>
        /// <returns>
        /// The provider, which the caller disposes.
        /// </returns>
        /// <remarks>
        /// Deliberately not a <see cref="ToolsMcpHost"/>: the probe has to see the service's raw <c>401</c> and
        /// its <c>WWW-Authenticate</c> header, which is exactly what the outbound authentication handler exists
        /// to swallow. <see cref="ConfigureServices"/> runs last so a test can point both the probe and
        /// discovery at in-process servers.
        /// </remarks>
        internal ServiceProvider CreateDiscoveryServices()
        {
            var services = new ServiceCollection();

            services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName);
            services.AddLogging();

            ConfigureServices?.Invoke(services);

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Derives a default name from the OData service URL.
        /// </summary>
        /// <param name="url">The OData service URL.</param>
        /// <returns>
        /// A suggested name for the connection.
        /// </returns>
        internal string DeriveNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var segments = uri.Segments
                    .Where(s => !string.IsNullOrWhiteSpace(s) && s != "/")
                    .Select(s => s.TrimEnd('/'))
                    .ToList();

                if (segments.Any(s => s.Equals("Northwind", StringComparison.OrdinalIgnoreCase)))
                {
                    return "northwind";
                }

                if (segments.Any(s => s.Contains("TripPin", StringComparison.OrdinalIgnoreCase)))
                {
                    return "trippin";
                }

                if (segments.Count > 0)
                {
                    var lastSegment = segments.Last()
                        .Replace(".svc", "")
                        .Replace("Service", "")
                        .Replace("OData", "")
                        .ToLowerInvariant();

                    if (!string.IsNullOrWhiteSpace(lastSegment))
                    {
                        return lastSegment;
                    }
                }

                return uri.Host.Split('.').First().ToLowerInvariant();
            }
            catch
            {
                return "odata-service";
            }
        }

        /// <summary>
        /// Probes the service's <c>$metadata</c>, reads whatever challenge comes back, runs OAuth discovery, and
        /// fills in the client identifier, grant, and scopes the operator will need.
        /// </summary>
        /// <param name="url">The OData service URL.</param>
        /// <param name="settings">The settings this probe fills in.</param>
        /// <param name="cancellationToken">The token that cancels the probe and the well-known requests.</param>
        /// <returns>
        /// A task that completes once the operator has answered every question the probe raised.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The common enterprise case is a bare <c>Bearer</c> challenge from which nothing can be discovered.
        /// That is not a failure: the wizard says so plainly and asks for the issuer URL, then re-runs discovery
        /// with that override, which is the whole reason the override exists.
        /// </remarks>
        internal async Task DiscoverAsync(string url, AddCommandAuthSettings settings, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ArgumentNullException.ThrowIfNull(settings);

            var root = ToolsMcpHost.NormalizeServiceRoot(url);

            using var provider = CreateDiscoveryServices();

            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var client = httpClientFactory.CreateClient(ODataMcpAuthConstants.OAuthHttpClientName);

            using var response = await client.GetAsync(new Uri(root, "$metadata"), cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                Output.WriteLine("No authentication required");

                return;
            }

            if (response.StatusCode is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden)
            {
                Output.WriteLine($"The service answered {(int)response.StatusCode}; discovery needs a 401 or 403 challenge to read.");

                return;
            }

            IReadOnlyList<string> challenges = [.. response.Headers.WwwAuthenticate.Select(value => value.ToString())];
            var discovery = new OAuthDiscovery(
                new ProtectedResourceMetadataClient(httpClientFactory, loggerFactory.CreateLogger<ProtectedResourceMetadataClient>()),
                new AuthorizationServerMetadataClient(httpClientFactory, loggerFactory.CreateLogger<AuthorizationServerMetadataClient>()),
                loggerFactory.CreateLogger<OAuthDiscovery>());

            OAuthDiscoveryResult result;
            try
            {
                result = await discovery
                    .DiscoverAsync(root, challenges, (int)response.StatusCode, authorizationServerOverride: null, resourceOverride: null, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OutboundDiscoveryException)
            {
                Output.WriteLine("This service did not advertise its authorization server.");

                var issuer = PromptInput("Authorization server (issuer) URL");

                settings.AuthServer = issuer;
                result = await discovery
                    .DiscoverAsync(root, challenges, (int)response.StatusCode, new Uri(issuer, UriKind.Absolute), resourceOverride: null, cancellationToken)
                    .ConfigureAwait(false);
            }

            var issuerName = string.IsNullOrWhiteSpace(result.AuthorizationServer.Issuer)
                ? result.AuthorizationServerBase.AbsoluteUri
                : result.AuthorizationServer.Issuer;

            if (result.AuthorizationServer.RegistrationEndpoint is null)
            {
                settings.ClientId = PromptInput($"Client (application) id registered with {issuerName}");
            }
            else
            {
                Output.WriteLine($"{issuerName} supports dynamic client registration; start will register the client automatically.");
            }

            settings.Grant = PromptForRecommendedGrant(result);

            var challengeScope = result.ChallengeScope;
            var advertisedScopes = result.ProtectedResource?.ScopesSupported;
            if (string.IsNullOrWhiteSpace(challengeScope) && (advertisedScopes is null || advertisedScopes.Count == 0))
            {
                var scopes = PromptInput("Scopes (space-separated, Enter to skip)");

                settings.Scopes = string.IsNullOrWhiteSpace(scopes) ? null : scopes;
            }
        }

        /// <summary>
        /// Displays the generated command and instructions to the user.
        /// </summary>
        /// <param name="command">The generated MCP command.</param>
        internal void DisplayResult(string command)
        {
            Output.WriteLine("\n✨ Perfect! Here's your command:\n");
            WriteColoredLine(ConsoleColor.Green, command);

            Output.WriteLine("\nNext steps:");
            Output.WriteLine("1. Open Claude Code terminal (not chat)");
            Output.WriteLine("2. Type or paste this command exactly as shown");
            Output.WriteLine("3. Press Enter");
            Output.WriteLine("4. The server will start automatically");
            Output.WriteLine("5. Return to Claude chat and query your OData service!");
            Output.WriteLine("\nExample query: 'Show me all products from the OData service'");
            Output.WriteLine("\nNote: Make sure you have the .NET 10 SDK installed and the tool has been installed globally.");
        }

        /// <summary>
        /// Prompts for a yes/no confirmation.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="defaultValue">The default value if user just presses Enter.</param>
        /// <returns>
        /// <see langword="true"/> if the user confirms; otherwise <see langword="false"/>.
        /// </returns>
        internal bool PromptConfirm(string message, bool defaultValue)
        {
            var defaultText = defaultValue ? "Y/n" : "y/N";
            Output.Write($"{message} ({defaultText}): ");
            var response = ReadLineRequired().Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(response))
            {
                return defaultValue;
            }

            return response is "y" or "yes";
        }

        /// <summary>
        /// Prompts the operator for how the connection should authenticate.
        /// </summary>
        /// <returns>
        /// The chosen path.
        /// </returns>
        /// <remarks>
        /// The menu order is the order <see cref="AddAuthChoice"/> documents, minus
        /// <see cref="AddAuthChoice.None"/>, which is offered last because an operator who already knows the
        /// service is open does not need to read six OAuth options first.
        /// </remarks>
        internal AddAuthChoice PromptForAuthenticationMode()
        {
            var options = new (string Label, AddAuthChoice Choice)[]
            {
                ("Discover (recommended)", AddAuthChoice.Discover),
                ("Device code", AddAuthChoice.DeviceCode),
                ("Authorization code (loopback)", AddAuthChoice.AuthorizationCode),
                ("Client credentials", AddAuthChoice.ClientCredentials),
                ("API key", AddAuthChoice.ApiKey),
                ("Basic", AddAuthChoice.Basic),
                ("Paste token (escape hatch)", AddAuthChoice.PasteToken),
                ("None", AddAuthChoice.None)
            };
            var label = PromptSelect("How should this connection authenticate?", [.. options.Select(option => option.Label)]);

            return options.First(option => string.Equals(option.Label, label, StringComparison.Ordinal)).Choice;
        }

        /// <summary>
        /// Prompts the user for a connection name.
        /// </summary>
        /// <param name="url">The OData service URL to derive a default from.</param>
        /// <returns>
        /// The chosen connection name.
        /// </returns>
        internal string PromptForName(string url)
        {
            var defaultName = DeriveNameFromUrl(url);
            var name = PromptInput("What would you like to name this connection?", defaultName);

            if (!Regex.IsMatch(name, @"^[a-z0-9-]+$"))
            {
                Output.WriteLine("Name must be lowercase alphanumeric with hyphens only (e.g., 'my-service')");

                return PromptForName(url);
            }

            return name;
        }

        /// <summary>
        /// Prompts for the client identifier an operator-selected OAuth grant runs as, when they have one.
        /// </summary>
        /// <param name="settings">The settings the answer is written to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Empty is a legitimate answer on this path: an authorization server that offers RFC 7591 dynamic
        /// client registration needs no pre-registered identifier, and <c>start</c> will register one.
        /// </remarks>
        internal void PromptForOAuthClient(AddCommandAuthSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var clientId = PromptInput("Client (application) id (Enter to register dynamically)");
            settings.ClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId;

            var scopes = PromptInput("Scopes (space-separated, Enter to skip)");
            settings.Scopes = string.IsNullOrWhiteSpace(scopes) ? null : scopes;

            if (settings.Grant is OutboundGrantKind.ClientCredentials)
            {
                Output.WriteLine(ClientCredentialsInstruction);
            }
        }

        /// <summary>
        /// Offers the grant discovery recommends and lets the operator override it.
        /// </summary>
        /// <param name="discovery">The discovery result whose advertised grants drive the recommendation.</param>
        /// <returns>
        /// The grant the generated command pins.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="discovery"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The recommendation is <see cref="GrantSelector.Select(OAuthDiscoveryResult, OutboundOAuthOptions, bool)"/>
        /// run interactively, which is exactly what <c>start</c> will do; an authorization server that
        /// advertises nothing that selection can use falls back to the authorization code grant, which RFC 8414
        /// makes the default for a server that publishes no <c>grant_types_supported</c> at all.
        /// </remarks>
        internal OutboundGrantKind PromptForRecommendedGrant(OAuthDiscoveryResult discovery)
        {
            ArgumentNullException.ThrowIfNull(discovery);

            OutboundGrantKind recommended;
            try
            {
                recommended = GrantSelector.Select(discovery, new OutboundOAuthOptions(), interactive: true);
            }
            catch (Exception exception) when (exception is OutboundDiscoveryException or NotSupportedException)
            {
                recommended = OutboundGrantKind.AuthorizationCode;
            }

            var wireName = OutboundGrantKindParser.ToWireName(recommended);

            if (PromptConfirm($"Use the {wireName} grant?", true))
            {
                return recommended;
            }

            var options = new (string Label, OutboundGrantKind Grant)[]
            {
                (OutboundGrantKindParser.ToWireName(OutboundGrantKind.DeviceCode), OutboundGrantKind.DeviceCode),
                (OutboundGrantKindParser.ToWireName(OutboundGrantKind.AuthorizationCode), OutboundGrantKind.AuthorizationCode),
                (OutboundGrantKindParser.ToWireName(OutboundGrantKind.ClientCredentials), OutboundGrantKind.ClientCredentials)
            };
            var chosen = PromptSelect("Which grant should the generated command pin?", [.. options.Select(option => option.Label)]);
            var grant = options.First(option => string.Equals(option.Label, chosen, StringComparison.Ordinal)).Grant;

            if (grant is OutboundGrantKind.ClientCredentials)
            {
                Output.WriteLine(ClientCredentialsInstruction);
            }

            return grant;
        }

        /// <summary>
        /// Prompts the user for the MCP server scope.
        /// </summary>
        /// <returns>
        /// The selected scope (<c>user</c> or <c>project</c>).
        /// </returns>
        internal string PromptForScope()
        {
            Output.WriteLine("Where should this MCP server be available?");
            Output.WriteLine("  • user - Available globally for all Claude Code sessions");
            Output.WriteLine("  • project - Only available in the current project");
            Output.WriteLine();

            return PromptSelect("Select scope", ["user", "project"]);
        }

        /// <summary>
        /// Offers a non-default token cache directory, leaving <c>--token-cache</c> off the generated command
        /// when the operator keeps the operating system credential store.
        /// </summary>
        /// <param name="settings">The settings the answer is written to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is <see langword="null"/>.</exception>
        internal void PromptForTokenCache(AddCommandAuthSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var path = PromptInput("Token cache directory (Enter for default)");

            settings.TokenCachePath = string.IsNullOrWhiteSpace(path) ? null : path;
        }

        /// <summary>
        /// Prompts the user for the OData service URL.
        /// </summary>
        /// <returns>
        /// The validated OData service URL.
        /// </returns>
        internal string PromptForUrl()
        {
            Output.WriteLine("Examples:");
            Output.WriteLine("  • https://services.odata.org/V4/Northwind/Northwind.svc");
            Output.WriteLine("  • https://services.odata.org/V4/TripPinServiceRW");
            Output.WriteLine();

            var url = PromptInput("What's your OData service URL?");

            if (string.IsNullOrWhiteSpace(url))
            {
                Output.WriteLine("URL cannot be empty");

                return PromptForUrl();
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Output.WriteLine("Please enter a valid URL");

                return PromptForUrl();
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                Output.WriteLine("URL must be HTTP or HTTPS");

                return PromptForUrl();
            }

            return url;
        }

        /// <summary>
        /// Prompts the user for verbose logging preference.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if verbose logging should be enabled.
        /// </returns>
        internal bool PromptForVerboseLogging()
        {
            return PromptConfirm("Would you like to enable verbose logging?", false);
        }

        /// <summary>
        /// Prompts for a string input with an optional default value.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="defaultValue">Optional default value.</param>
        /// <returns>
        /// The user's input or default value.
        /// </returns>
        internal string PromptInput(string message, string? defaultValue = null)
        {
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                Output.Write($"{message} (default: {defaultValue}): ");
            }
            else
            {
                Output.Write($"{message}: ");
            }

            var input = ReadLineRequired().Trim();

            if (string.IsNullOrWhiteSpace(input) && !string.IsNullOrWhiteSpace(defaultValue))
            {
                return defaultValue;
            }

            return input;
        }

        /// <summary>
        /// Prompts for a password. Uses line input when stdin is redirected, when
        /// <see cref="Input"/> is not the process console, or when an explicit key
        /// reader is omitted in tests. Otherwise masks keystrokes.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="readKey">Optional key reader used by tests to drive the masked path.</param>
        /// <returns>
        /// The entered password.
        /// </returns>
        /// <remarks>
        /// Never call <see cref="Console.ReadKey(bool)"/> unless <paramref name="readKey"/> is
        /// supplied or the process console is the live TTY. Visual Studio testhost does not set
        /// <see cref="Console.IsInputRedirected"/> after <c>SetIn</c>, and <c>ReadKey</c> ignores
        /// a redirected <see cref="StringReader"/>.
        /// </remarks>
        internal string PromptPassword(string message, Func<ConsoleKeyInfo>? readKey = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            Output.Write($"{message}: ");
            if (readKey is null && UsesLinePasswordInput())
            {
                return Input.ReadLine() ?? string.Empty;
            }

            readKey ??= static () => Console.ReadKey(intercept: true);
            var password = new StringBuilder();

            while (true)
            {
                var key = readKey();

                if (key.Key == ConsoleKey.Enter)
                {
                    Output.WriteLine();
                    break;
                }

                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Length--;
                    Output.Write("\b \b");
                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                    Output.Write('*');
                }
            }

            return password.ToString();
        }

        /// <summary>
        /// Prompts for a selection from a list of options.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="options">The list of options.</param>
        /// <returns>
        /// The selected option.
        /// </returns>
        internal string PromptSelect(string message, string[] options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            ArgumentNullException.ThrowIfNull(options);

            if (options.Length == 0)
            {
                throw new ArgumentException("At least one option is required.", nameof(options));
            }

            Output.WriteLine(message);
            for (var i = 0; i < options.Length; i++)
            {
                Output.WriteLine($"  {i + 1}. {options[i]}");
            }

            while (true)
            {
                Output.Write("Select (1-" + options.Length + "): ");
                var input = ReadLineRequired();

                if (int.TryParse(input.Trim(), out var choice) && choice >= 1 && choice <= options.Length)
                {
                    return options[choice - 1];
                }

                Output.WriteLine("Invalid selection. Please try again.");
            }
        }

        /// <summary>
        /// Reads a line from <see cref="Input"/> or throws when the stream ends.
        /// </summary>
        /// <returns>
        /// The line, which may be empty.
        /// </returns>
        internal string ReadLineRequired()
        {
            var line = Input.ReadLine();
            if (line is null)
            {
                throw new EndOfStreamException("Input ended before the wizard finished.");
            }

            return line;
        }

        /// <summary>
        /// Tests the connection by building the same host <c>odata-mcp start</c> builds, with the credentials
        /// the wizard collected.
        /// </summary>
        /// <param name="url">The OData service URL.</param>
        /// <param name="settings">The authentication settings the wizard collected.</param>
        /// <returns>
        /// A task that completes once the outcome has been written.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// This is the real thing, not a probe: an interactive grant prints its sign-in URL and user code to
        /// stderr through <see cref="StdioConsentPresenter"/> and waits for a human, which is what an operator
        /// running a wizard expects a connection test to do. Every failure is reported rather than thrown,
        /// because a service that cannot be reached from the wizard may still work once the host is launched
        /// with the generated command.
        /// </remarks>
        internal async Task TestConnection(string url, AddCommandAuthSettings settings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ArgumentNullException.ThrowIfNull(settings);

            Output.Write("\n⏳ Testing connection...");

            try
            {
                using var host = await ToolsMcpHost
                    .CreateAsync(url, settings.ToOutboundOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None, ConfigureServices)
                    .ConfigureAwait(false);

                Output.WriteLine("\r✅ Connection successful!     ");
                Output.WriteLine($"   Found {host.Catalog.CompleteEntitySetNames(string.Empty).Count} entity sets.");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"\r⚠️  Could not connect: {ex.Message}");
                Output.WriteLine("   The service might still work when properly configured.");
            }
        }

        /// <summary>
        /// Returns whether a password should be read as a line instead of masked keystrokes.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when stdin is redirected, <see cref="Input"/> is not the process
        /// console, or <see cref="Input"/> is a <see cref="StringReader"/>.
        /// </returns>
        internal bool UsesLinePasswordInput()
        {
            return Console.IsInputRedirected
                || !ReferenceEquals(Input, Console.In)
                || Input is StringReader;
        }

        /// <summary>
        /// Writes a colored line when <see cref="Output"/> is the process console.
        /// </summary>
        /// <param name="color">The console color.</param>
        /// <param name="text">The text to write.</param>
        internal void WriteColoredLine(ConsoleColor color, string text)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            if (!ReferenceEquals(Output, Console.Out))
            {
                Output.WriteLine(text);

                return;
            }

            Console.ForegroundColor = color;
            Output.WriteLine(text);
            Console.ResetColor();
        }

        #endregion

    }

}
