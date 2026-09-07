// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tools.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// Tests for add-wizard internals. Each test owns a <see cref="StringReader"/> on
    /// <see cref="AddCommand.Input"/> and must not call <see cref="Console.SetIn(TextReader)"/>.
    /// </summary>
    [TestClass]
    public class AddCommandTests
    {

        #region Public Methods

        /// <summary>
        /// A complete no-auth wizard produces a Claude MCP command.
        /// </summary>
        [TestMethod]
        public async Task OnExecuteAsync_NoAuth_ReturnsZero()
        {
            var (command, output) = CommandWithInput($"{LiveOData.Northwind}\n\nN\n1\nN\nN\n");
            var exit = await command.OnExecuteAsync();

            exit.Should().Be(0);
            output.ToString().Should().Contain("claude mcp add northwind");
        }

        /// <summary>
        /// Bearer authentication is written into the generated command.
        /// </summary>
        [TestMethod]
        public async Task OnExecuteAsync_BearerToken_IncludesAuth()
        {
            var (command, output) = CommandWithInput($"{LiveOData.Northwind}\nnorthwind\nY\n1\nsecret-token\n1\nN\nN\n");
            var exit = await command.OnExecuteAsync();

            exit.Should().Be(0);
            output.ToString().Should().Contain("--auth-token \"secret-token\"");
        }

        /// <summary>
        /// Basic auth encodes username and password.
        /// </summary>
        [TestMethod]
        public async Task PromptForAuthentication_BasicAuth_EncodesCredentials()
        {
            var (command, _) = CommandWithInput("Y\n3\nalice\nsecret\n");
            var result = await command.PromptForAuthentication();

            result.needsAuth.Should().BeTrue();
            result.authType.Should().Be("Basic Auth (Username/Password)");
            result.authToken.Should().Be(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("alice:secret")));
        }

        /// <summary>
        /// API key authentication returns the typed key.
        /// </summary>
        [TestMethod]
        public async Task PromptForAuthentication_ApiKey_ReturnsKey()
        {
            var (command, _) = CommandWithInput("Y\n2\napi-key-value\n");
            var result = await command.PromptForAuthentication();

            result.needsAuth.Should().BeTrue();
            result.authType.Should().Be("API Key");
            result.authToken.Should().Be("api-key-value");
        }

        /// <summary>
        /// Empty bearer tokens retry the authentication prompt.
        /// </summary>
        [TestMethod]
        public async Task PromptForAuthentication_EmptyBearer_Retries()
        {
            var (command, _) = CommandWithInput("Y\n1\n\nY\n1\ntoken\n");
            var result = await command.PromptForAuthentication();

            result.authToken.Should().Be("token");
        }

        /// <summary>
        /// Empty API keys retry the authentication prompt.
        /// </summary>
        [TestMethod]
        public async Task PromptForAuthentication_EmptyApiKey_Retries()
        {
            var (command, _) = CommandWithInput("Y\n2\n\nY\n2\nkey\n");
            var result = await command.PromptForAuthentication();

            result.authToken.Should().Be("key");
        }

        /// <summary>
        /// Empty basic-auth usernames retry.
        /// </summary>
        [TestMethod]
        public async Task PromptForAuthentication_EmptyUsername_Retries()
        {
            var (command, _) = CommandWithInput("Y\n3\n\nY\n3\nbob\npass\n");
            var result = await command.PromptForAuthentication();

            result.authToken.Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// Empty basic-auth passwords retry.
        /// </summary>
        [TestMethod]
        public async Task PromptForAuthentication_EmptyPassword_Retries()
        {
            var (command, _) = CommandWithInput("Y\n3\nbob\n\nY\n3\nbob\npass\n");
            var result = await command.PromptForAuthentication();

            result.needsAuth.Should().BeTrue();
        }

        /// <summary>
        /// Declining authentication returns no token.
        /// </summary>
        [TestMethod]
        public async Task PromptForAuthentication_No_ReturnsNullToken()
        {
            var (command, _) = CommandWithInput("N\n");
            var result = await command.PromptForAuthentication();

            result.needsAuth.Should().BeFalse();
            result.authToken.Should().BeNull();
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
        /// Invalid names retry until a legal identifier is entered.
        /// </summary>
        [TestMethod]
        public void PromptForName_RejectsInvalidThenAccepts()
        {
            var (command, _) = CommandWithInput("NOPE\nmy-service\n");

            command.PromptForName("https://contoso.example.com/odata").Should().Be("my-service");
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
        /// Scope prompt returns the selected option.
        /// </summary>
        [TestMethod]
        public void PromptForScope_SelectsProject()
        {
            var (command, _) = CommandWithInput("2\n");

            command.PromptForScope().Should().Be("project");
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
        /// Verbose logging prompt defaults to false.
        /// </summary>
        [TestMethod]
        public void PromptForVerboseLogging_DefaultFalse()
        {
            CommandWithInput("\n").Command.PromptForVerboseLogging().Should().BeFalse();
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
                new ConsoleKeyInfo('\u0001', ConsoleKey.A, false, false, true),
                new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false),
                new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)
            ]);
            var (command, _) = CommandWithInput(string.Empty);

            command.PromptPassword("token", keys.Dequeue).Should().Be("sx");
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
        /// Connection tests against a closed port warn instead of throwing.
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task TestConnection_Unreachable_DoesNotThrow()
        {
            var (command, output) = CommandWithInput(string.Empty);

            await command.TestConnection("http://127.0.0.1:1", null);
            output.ToString().Should().Contain("Could not connect");
        }

        /// <summary>
        /// A metadata URL that is not found reports the status.
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task TestConnection_MissingMetadata_DoesNotThrow()
        {
            var (command, output) = CommandWithInput(string.Empty);

            await command.TestConnection("https://services.odata.org/V4/does-not-exist", null);
            output.ToString().Should().MatchRegex("Connection returned|Could not connect");
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

        /// <summary>
        /// Host-only URLs fall back to the first host label.
        /// </summary>
        [TestMethod]
        public void DeriveNameFromUrl_HostOnly_UsesHost()
        {
            new AddCommand().DeriveNameFromUrl("https://contoso.example.com/").Should().Be("contoso");
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
        /// Wizard with live connection test still succeeds.
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task OnExecuteAsync_WithConnectionTest_ReturnsZero()
        {
            var (command, _) = CommandWithInput($"{LiveOData.Northwind}\n\nN\n1\nN\nY\n");
            var exit = await command.OnExecuteAsync();

            exit.Should().Be(0);
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

        #endregion

    }

}
