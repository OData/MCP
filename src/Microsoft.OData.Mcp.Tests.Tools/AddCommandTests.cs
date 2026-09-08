// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.OData.Mcp.Tools.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// Tests for add-wizard internals. Each test owns a <see cref="StringReader"/> on
    /// <see cref="AddCommand.Input"/> and must not call <see cref="Console.SetIn(TextReader)"/>.
    /// </summary>
    /// <remarks>
    /// The discovery tests drive the wizard against a real OAuth protected OData service and a real
    /// authorization server through the <see cref="AddCommand.ConfigureServices"/> seam, so the probe, the
    /// challenge parse, and the well-known requests are the production ones.
    /// </remarks>
    [TestClass]
    public class AddCommandTests
    {

        #region Public Methods

        /// <summary>
        /// An API key is written into the generated command together with its header, and the operator is
        /// warned that it lands in configuration the host process can read.
        /// </summary>
        [TestMethod]
        public void BuildMcpCommand_ApiKey_EmitsKeyAndHeader_WithWarning()
        {
            var (command, output) = CommandWithInput(string.Empty);
            var settings = new AddCommandAuthSettings
            {
                ApiKey = "the-key",
                ApiKeyHeader = "X-Api-Key"
            };

            var generated = command.BuildMcpCommand("svc", "https://example.com/odata", settings, "user", verbose: false);

            generated.Should().Contain("--api-key \"the-key\"").And.Contain("--api-key-header \"X-Api-Key\"");
            generated.Should().NotContain("--env").And.NotContain("--auth-token");
            output.ToString().Should().Contain(AddCommand.SecretInConfigWarning);
        }

        /// <summary>
        /// Client credentials never put a secret on the command line or in an <c>--env</c> pair: the wizard
        /// prints the environment instruction instead.
        /// </summary>
        [TestMethod]
        public void BuildMcpCommand_ClientCredentials_NoEnv_PrintsInstruction()
        {
            var (command, output) = CommandWithInput(string.Empty);
            var settings = new AddCommandAuthSettings
            {
                ClientId = "daemon",
                Grant = OutboundGrantKind.ClientCredentials
            };

            var generated = command.BuildMcpCommand("svc", "https://example.com/odata", settings, "user", verbose: false);

            generated.Should().Contain("--client-id \"daemon\"").And.Contain("--grant client_credentials");
            generated.Should().NotContain("--env").And.NotContain("--client-secret").And.NotContain("--auth-token");
            output.ToString().Should().Contain(AddCommand.ClientCredentialsInstruction);
        }

        /// <summary>
        /// Leaving the token cache at its default keeps <c>--token-cache</c> off the generated command.
        /// </summary>
        [TestMethod]
        public void BuildMcpCommand_DefaultTokenCache_OmitsFlag()
        {
            var (command, _) = CommandWithInput(string.Empty);
            var withDefault = new AddCommandAuthSettings
            {
                ClientId = "cli",
                Grant = OutboundGrantKind.DeviceCode
            };
            var withOverride = new AddCommandAuthSettings
            {
                ClientId = "cli",
                Grant = OutboundGrantKind.DeviceCode,
                TokenCachePath = "/var/lib/odata-mcp"
            };

            command.BuildMcpCommand("svc", "https://example.com/odata", withDefault, "user", verbose: false)
                .Should().NotContain("--token-cache");
            command.BuildMcpCommand("svc", "https://example.com/odata", withOverride, "user", verbose: false)
                .Should().Contain("--token-cache \"/var/lib/odata-mcp\"");
        }

        /// <summary>
        /// The device code path emits every flag <c>start</c> needs to sign in and nothing that carries a
        /// credential.
        /// </summary>
        [TestMethod]
        public void BuildMcpCommand_DeviceCode_EmitsClientIdScopesGrantAuthServer_NoToken()
        {
            var (command, _) = CommandWithInput(string.Empty);
            var settings = new AddCommandAuthSettings
            {
                AuthServer = "https://login.example/oauth/v2.0",
                ClientId = "the-app",
                Grant = OutboundGrantKind.DeviceCode,
                Scopes = "read offline_access"
            };

            var generated = command.BuildMcpCommand("svc", "https://example.com/odata", settings, "user", verbose: true);

            generated.Should().Contain("claude mcp add svc")
                .And.Contain("--scope user")
                .And.Contain("start \"https://example.com/odata\"")
                .And.Contain("--auth-server \"https://login.example/oauth/v2.0\"")
                .And.Contain("--client-id \"the-app\"")
                .And.Contain("--scopes \"read offline_access\"")
                .And.Contain("--grant device_code")
                .And.Contain("--verbose");
            generated.Should().NotContain("--auth-token").And.NotContain("--env").And.NotContain("--api-key");
        }

        /// <summary>
        /// The escape hatch writes the pasted token and nothing else: no client id, no grant, no scopes.
        /// </summary>
        [TestMethod]
        public void BuildMcpCommand_PasteToken_EmitsAuthTokenOnly()
        {
            var (command, _) = CommandWithInput(string.Empty);
            var settings = new AddCommandAuthSettings
            {
                AuthToken = "secret-token"
            };

            var generated = command.BuildMcpCommand("svc", "https://example.com/odata", settings, "project", verbose: false);

            generated.Should().Contain("--auth-token \"secret-token\"");
            generated.Should().NotContain("--env")
                .And.NotContain("--client-id")
                .And.NotContain("--grant")
                .And.NotContain("--scopes");
        }

        /// <summary>
        /// Host-only URLs fall back to the first host label.
        /// </summary>
        [TestMethod]
        public void DeriveNameFromUrl_HostOnly_UsesHost()
        {
            new AddCommand().DeriveNameFromUrl("https://contoso.example.com/").Should().Be("contoso");
        }

        /// <summary>
        /// A service whose challenge is a bare <c>Bearer</c> — the ordinary enterprise deployment — cannot be
        /// discovered from, so the wizard says so and asks for the issuer, then discovers against that override
        /// and puts it on the generated command.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_BareBearerChallenge_PromptsForIssuerAndUsesIt()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.NotFound
            });
            using var resource = new SecuredStaticResourceServer(
                authorizationServer,
                SecuredStaticResourceServer.DefaultCsdl,
                requireAuthentication: true,
                bareBearerChallenge: true);
            var (command, output) = CommandWithInput($"{authorizationServer.Issuer}\nbare-client\n\n\n");
            command.ConfigureServices = Route(authorizationServer, resource);
            var settings = new AddCommandAuthSettings();

            await command.DiscoverAsync(resource.ServiceRoot.ToString(), settings, CancellationToken.None);

            output.ToString().Should().Contain("This service did not advertise its authorization server.");
            settings.AuthServer.Should().Be(authorizationServer.Issuer.ToString());
            settings.ClientId.Should().Be("bare-client");
            settings.Grant.Should().Be(OutboundGrantKind.DeviceCode);
            command.BuildMcpCommand("svc", resource.ServiceRoot.ToString(), settings, "user", verbose: false)
                .Should().Contain($"--auth-server \"{authorizationServer.Issuer}\"");
        }

        /// <summary>
        /// A service that advertises RFC 9728 protected resource metadata needs no issuer prompt at all: the
        /// wizard finds the authorization server, sees no registration endpoint, and asks only for the client
        /// id.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_ProtectedResourceMetadata_FindsIssuerAndPromptsForClientId()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var (command, output) = CommandWithInput("my-client\n\n");
            command.ConfigureServices = Route(authorizationServer, resource);
            var settings = new AddCommandAuthSettings();

            await command.DiscoverAsync(resource.ServiceRoot.ToString(), settings, CancellationToken.None);

            output.ToString().Should().NotContain("Authorization server (issuer) URL");
            output.ToString().Should().Contain($"Client (application) id registered with {authorizationServer.Issuer}");
            settings.AuthServer.Should().BeNull();
            settings.ClientId.Should().Be("my-client");
            settings.Grant.Should().Be(OutboundGrantKind.DeviceCode);
            settings.Scopes.Should().BeNull();
            authorizationServer.HitCount("register").Should().Be(0);
        }

        /// <summary>
        /// An anonymous service is reported as needing nothing, and the wizard asks no credential questions.
        /// </summary>
        [TestMethod]
        public async Task DiscoverAsync_UnauthenticatedService_ReportsNoAuthentication()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl, requireAuthentication: false);
            var (command, output) = CommandWithInput(string.Empty);
            command.ConfigureServices = Route(authorizationServer, resource);
            var settings = new AddCommandAuthSettings();

            await command.DiscoverAsync(resource.ServiceRoot.ToString(), settings, CancellationToken.None);

            output.ToString().Should().Contain("No authentication required");
            settings.ClientId.Should().BeNull();
            settings.Grant.Should().BeNull();
        }

        /// <summary>
        /// DisplayResult writes the command.
        /// </summary>
        [TestMethod]
        public void DisplayResult_WritesCommand()
        {
            var (command, output) = CommandWithInput(string.Empty);
            command.DisplayResult("claude mcp add demo");

            output.ToString().Should().Contain("claude mcp add demo");
        }

        /// <summary>
        /// The device code path writes the client id, the scopes, and the grant, and never a token.
        /// </summary>
        [TestMethod]
        public async Task OnExecuteAsync_DeviceCode_EmitsClientIdAndGrant_NoToken()
        {
            var (command, output) = CommandWithInput($"{LiveOData.Northwind}\nnorthwind\n2\nthe-app\nread offline_access\n\n1\nN\nN\n");
            var exit = await command.OnExecuteAsync();

            exit.Should().Be(0);
            output.ToString().Should().Contain("--client-id \"the-app\"")
                .And.Contain("--scopes \"read offline_access\"")
                .And.Contain("--grant device_code");
            output.ToString().Should().NotContain("--auth-token").And.NotContain("--env");
        }

        /// <summary>
        /// End of input during a required prompt fails the wizard.
        /// </summary>
        [TestMethod]
        public async Task OnExecuteAsync_Eof_ReturnsOne()
        {
            var (command, _) = CommandWithInput(string.Empty);
            var exit = await command.OnExecuteAsync();

            exit.Should().Be(1);
        }

        /// <summary>
        /// A complete no-auth wizard produces a Claude MCP command.
        /// </summary>
        [TestMethod]
        public async Task OnExecuteAsync_NoAuth_ReturnsZero()
        {
            var (command, output) = CommandWithInput($"{LiveOData.Northwind}\n\n8\n1\nN\nN\n");
            var exit = await command.OnExecuteAsync();

            exit.Should().Be(0);
            output.ToString().Should().Contain("claude mcp add northwind");
            output.ToString().Should().NotContain("--auth-token").And.NotContain("--grant");
        }

        /// <summary>
        /// A pasted bearer token is the only path that writes <c>--auth-token</c>, and the operator is told so
        /// before typing it.
        /// </summary>
        [TestMethod]
        public async Task OnExecuteAsync_PasteToken_IncludesAuthTokenOnlyWhenChosen()
        {
            var (command, output) = CommandWithInput($"{LiveOData.Northwind}\nnorthwind\n7\nsecret-token\n1\nN\nN\n");
            var exit = await command.OnExecuteAsync();

            exit.Should().Be(0);
            output.ToString().Should().Contain("stored only if you insist; prefer 1–4")
                .And.Contain("--auth-token \"secret-token\"");
            output.ToString().Should().NotContain("--env");
        }

        /// <summary>
        /// Confirm uses the default on empty input and parses yes/no.
        /// </summary>
        [TestMethod]
        public void PromptConfirm_DefaultYesAndNo()
        {
            CommandWithInput("\n").Command.PromptConfirm("go?", true).Should().BeTrue();
            CommandWithInput("\n").Command.PromptConfirm("go?", false).Should().BeFalse();
            CommandWithInput("yes\n").Command.PromptConfirm("go?", false).Should().BeTrue();
            CommandWithInput("n\n").Command.PromptConfirm("go?", true).Should().BeFalse();
        }

        /// <summary>
        /// Every menu index maps to the documented choice.
        /// </summary>
        [TestMethod]
        public void PromptForAuthenticationMode_Menu_MapsEveryChoice()
        {
            CommandWithInput("1\n").Command.PromptForAuthenticationMode().Should().Be(AddAuthChoice.Discover);
            CommandWithInput("2\n").Command.PromptForAuthenticationMode().Should().Be(AddAuthChoice.DeviceCode);
            CommandWithInput("3\n").Command.PromptForAuthenticationMode().Should().Be(AddAuthChoice.AuthorizationCode);
            CommandWithInput("4\n").Command.PromptForAuthenticationMode().Should().Be(AddAuthChoice.ClientCredentials);
            CommandWithInput("5\n").Command.PromptForAuthenticationMode().Should().Be(AddAuthChoice.ApiKey);
            CommandWithInput("6\n").Command.PromptForAuthenticationMode().Should().Be(AddAuthChoice.Basic);
            CommandWithInput("7\n").Command.PromptForAuthenticationMode().Should().Be(AddAuthChoice.PasteToken);
            CommandWithInput("8\n").Command.PromptForAuthenticationMode().Should().Be(AddAuthChoice.None);
        }

        /// <summary>
        /// An empty name uses the URL-derived default.
        /// </summary>
        [TestMethod]
        public void PromptForName_Empty_UsesDefault()
        {
            var (command, _) = CommandWithInput("\n");

            command.PromptForName(LiveOData.Northwind).Should().Be("northwind");
        }

        /// <summary>
        /// Invalid names retry until a legal identifier is entered.
        /// </summary>
        [TestMethod]
        public void PromptForName_RejectsInvalidThenAccepts()
        {
            var (command, _) = CommandWithInput("NOPE\nmy-service\n");

            command.PromptForName("https://contoso.example.com/odata").Should().Be("my-service");
        }

        /// <summary>
        /// Scope prompt returns the selected option.
        /// </summary>
        [TestMethod]
        public void PromptForScope_SelectsProject()
        {
            var (command, _) = CommandWithInput("2\n");

            command.PromptForScope().Should().Be("project");
        }

        /// <summary>
        /// An empty token cache answer leaves the operating system store in place.
        /// </summary>
        [TestMethod]
        public void PromptForTokenCache_Empty_LeavesDefault()
        {
            var (command, _) = CommandWithInput("\n");
            var settings = new AddCommandAuthSettings();

            command.PromptForTokenCache(settings);

            settings.TokenCachePath.Should().BeNull();
        }

        /// <summary>
        /// Invalid URLs are rejected until a valid HTTP URL is entered.
        /// </summary>
        [TestMethod]
        public void PromptForUrl_RejectsEmptyInvalidAndNonHttp()
        {
            var (command, _) = CommandWithInput("\nnot-a-url\nftp://example.com\nhttps://services.odata.org/V4/Northwind/Northwind.svc\n");

            command.PromptForUrl().Should().Be("https://services.odata.org/V4/Northwind/Northwind.svc");
        }

        /// <summary>
        /// Verbose logging prompt defaults to false.
        /// </summary>
        [TestMethod]
        public void PromptForVerboseLogging_DefaultFalse()
        {
            CommandWithInput("\n").Command.PromptForVerboseLogging().Should().BeFalse();
        }

        /// <summary>
        /// Injected line input is used for passwords instead of <see cref="Console.ReadKey(bool)"/>.
        /// </summary>
        [TestMethod]
        public void PromptPassword_InjectedReader_ReadsLine()
        {
            var (command, _) = CommandWithInput("line-secret\n");

            command.UsesLinePasswordInput().Should().BeTrue();
            command.PromptPassword("token").Should().Be("line-secret");
        }

        /// <summary>
        /// Masked password input handles characters, backspace, and enter.
        /// </summary>
        [TestMethod]
        public void PromptPassword_KeyLoop_HandlesBackspaceAndEnter()
        {
            var keys = new Queue<ConsoleKeyInfo>(
            [
                new ConsoleKeyInfo('s', ConsoleKey.S, false, false, false),
                new ConsoleKeyInfo('e', ConsoleKey.E, false, false, false),
                new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false),
                new ConsoleKeyInfo('', ConsoleKey.A, false, false, true),
                new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false),
                new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)
            ]);
            var (command, _) = CommandWithInput(string.Empty);

            command.PromptPassword("token", keys.Dequeue).Should().Be("sx");
        }

        /// <summary>
        /// PromptSelect rejects an empty option list.
        /// </summary>
        [TestMethod]
        public void PromptSelect_EmptyOptions_Throws()
        {
            var act = () => CommandWithInput(string.Empty).Command.PromptSelect("pick", []);

            act.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// Invalid selections retry until a valid index is entered.
        /// </summary>
        [TestMethod]
        public void PromptSelect_InvalidThenValid()
        {
            var (command, _) = CommandWithInput("0\nabc\n2\n");

            command.PromptSelect("pick", ["one", "two"]).Should().Be("two");
        }

        /// <summary>
        /// A connection test against the real protected service walks the whole outbound path and reports the
        /// catalog it found.
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TestConnection_ProtectedService_ReportsEntitySets()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var (command, output) = CommandWithInput(string.Empty);
            command.ConfigureServices = services =>
            {
                services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => resource.Server.CreateHandler());
                services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => authorizationServer.Handler);
            };
            var settings = new AddCommandAuthSettings
            {
                AuthToken = authorizationServer.IssueAccessToken("read")
            };

            await command.TestConnection(resource.ServiceRoot.ToString(), settings);

            output.ToString().Should().Contain("Connection successful").And.Contain("Found 1 entity sets.");
        }

        /// <summary>
        /// Connection tests against a closed port warn instead of throwing.
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TestConnection_Unreachable_DoesNotThrow()
        {
            var (command, output) = CommandWithInput(string.Empty);

            await command.TestConnection("http://127.0.0.1:1", new AddCommandAuthSettings { AuthToken = "token" });

            output.ToString().Should().Contain("Could not connect");
        }

        /// <summary>
        /// An injected <see cref="StringReader"/> is line input even when testhost does not set <c>IsInputRedirected</c>.
        /// </summary>
        [TestMethod]
        public void UsesLinePasswordInput_InjectedStringReader_IsTrue()
        {
            var (command, _) = CommandWithInput("secret\n");

            command.UsesLinePasswordInput().Should().BeTrue();
            command.PromptPassword("token").Should().Be("secret");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a wizard that reads scripted lines from a private reader.
        /// </summary>
        /// <param name="input">Stdin text.</param>
        /// <returns>
        /// The command and captured stdout.
        /// </returns>
        internal static (AddCommand Command, StringWriter Output) CommandWithInput(string input)
        {
            var output = new StringWriter();
            var command = new AddCommand
            {
                Error = output,
                Input = new StringReader(input),
                Output = output
            };

            return (command, output);
        }

        /// <summary>
        /// Points the wizard's handler-free <c>"OAuth"</c> client at both in-process fixtures, routing by path.
        /// </summary>
        /// <param name="authorizationServer">The authorization server serving the well-known documents.</param>
        /// <param name="resource">The protected OData service.</param>
        /// <returns>
        /// The registration hook to assign to <see cref="AddCommand.ConfigureServices"/>.
        /// </returns>
        internal static Action<IServiceCollection> Route(LocalAuthorizationServer authorizationServer, SecuredStaticResourceServer resource)
        {
            return services => services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new OriginRoutingHandler("/odata", resource.Server.CreateHandler(), authorizationServer.Handler));
        }

        #endregion

    }

}
