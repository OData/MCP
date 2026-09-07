// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// Interactive wizard command to generate Claude Code MCP registration commands.
    /// </summary>
    /// <remarks>
    /// This command provides an interactive experience for users to configure their OData
    /// service connection and generates the appropriate /mcp add command for Claude Code.
    /// Tests must assign <see cref="Input"/> and <see cref="Output"/> instead of calling
    /// <see cref="Console.SetIn(TextReader)"/>. Windows testhost wraps <c>SetIn</c> in
    /// <c>SyncTextReader</c> and leaves <see cref="Console.IsInputRedirected"/> false, so
    /// <see cref="Console.ReadKey(bool)"/> blocks the Visual Studio Test Explorer.
    /// </remarks>
    [Command(Name = "add", Description = "Interactive wizard to generate Claude Code MCP registration command")]
    public class AddCommand
    {

        #region Properties

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
        /// <returns>Exit code (0 for success).</returns>
        public async Task<int> OnExecuteAsync()
        {
            Output.WriteLine("🚀 OData MCP Setup Wizard for Claude Code");
            Output.WriteLine("==========================================\n");
            Output.WriteLine("Let's set up your OData connection!\n");

            try
            {
                var url = PromptForUrl();
                var name = PromptForName(url);
                var (_, authToken, _) = await PromptForAuthentication();
                var scope = PromptForScope();
                var verbose = PromptForVerboseLogging();
                var shouldTest = PromptConfirm("Would you like to test the connection first?", true);
                if (shouldTest)
                {
                    await TestConnection(url, authToken);
                }

                var command = BuildMcpCommand(name, url, authToken, scope, verbose);
                DisplayResult(command);

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
        /// Builds the MCP command string based on user inputs.
        /// </summary>
        /// <param name="name">The name for the MCP server.</param>
        /// <param name="url">The OData service URL.</param>
        /// <param name="authToken">Optional authentication token.</param>
        /// <param name="scope">The scope for the MCP server (user or project).</param>
        /// <param name="verbose">Whether to enable verbose logging.</param>
        /// <returns>The formatted MCP command string.</returns>
        internal string BuildMcpCommand(string name, string url, string? authToken, string scope, bool verbose)
        {
            var sb = new StringBuilder($"claude mcp add {name}");

            sb.Append($" --scope {scope}");

            if (!string.IsNullOrWhiteSpace(authToken))
            {
                sb.Append($" --env ODATA_AUTH_TOKEN={authToken}");
            }

            sb.Append($" -- dotnet odata-mcp -- start \"{url}\"");

            if (!string.IsNullOrWhiteSpace(authToken))
            {
                sb.Append($" --auth-token \"{authToken}\"");
            }

            if (verbose)
            {
                sb.Append(" --verbose");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Derives a default name from the OData service URL.
        /// </summary>
        /// <param name="url">The OData service URL.</param>
        /// <returns>A suggested name for the connection.</returns>
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
        /// <returns>True if user confirms, false otherwise.</returns>
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
        /// Prompts the user for authentication details.
        /// </summary>
        /// <returns>A tuple containing authentication information.</returns>
        internal async Task<(bool needsAuth, string? authToken, string? authType)> PromptForAuthentication()
        {
            var needsAuth = PromptConfirm("Does your service require authentication?", false);

            if (!needsAuth)
            {
                return (false, null, null);
            }

            var authType = PromptSelect("What type of authentication?",
                ["Bearer Token", "API Key", "Basic Auth (Username/Password)"]);

            string? authToken = null;

            switch (authType)
            {
                case "Bearer Token":
                    authToken = PromptPassword("Enter your bearer token");
                    if (string.IsNullOrWhiteSpace(authToken))
                    {
                        Output.WriteLine("Token cannot be empty");
                        return await PromptForAuthentication();
                    }
                    break;

                case "API Key":
                    authToken = PromptPassword("Enter your API key");
                    if (string.IsNullOrWhiteSpace(authToken))
                    {
                        Output.WriteLine("API key cannot be empty");
                        return await PromptForAuthentication();
                    }
                    break;

                case "Basic Auth (Username/Password)":
                    var username = PromptInput("Username");
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        Output.WriteLine("Username cannot be empty");
                        return await PromptForAuthentication();
                    }
                    var password = PromptPassword("Password");
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        Output.WriteLine("Password cannot be empty");
                        return await PromptForAuthentication();
                    }
                    authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                    break;
            }

            return (true, authToken, authType);
        }

        /// <summary>
        /// Prompts the user for a connection name.
        /// </summary>
        /// <param name="url">The OData service URL to derive a default from.</param>
        /// <returns>The chosen connection name.</returns>
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
        /// Prompts the user for the MCP server scope.
        /// </summary>
        /// <returns>The selected scope (user or project).</returns>
        internal string PromptForScope()
        {
            Output.WriteLine("Where should this MCP server be available?");
            Output.WriteLine("  • user - Available globally for all Claude Code sessions");
            Output.WriteLine("  • project - Only available in the current project");
            Output.WriteLine();

            return PromptSelect("Select scope", ["user", "project"]);
        }

        /// <summary>
        /// Prompts the user for the OData service URL.
        /// </summary>
        /// <returns>The validated OData service URL.</returns>
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
        /// <returns>True if verbose logging should be enabled.</returns>
        internal bool PromptForVerboseLogging()
        {
            return PromptConfirm("Would you like to enable verbose logging?", false);
        }

        /// <summary>
        /// Prompts for a string input with an optional default value.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="defaultValue">Optional default value.</param>
        /// <returns>The user's input or default value.</returns>
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
        /// <returns>The entered password.</returns>
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
        /// <returns>The selected option.</returns>
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
        /// Tests the connection to the OData service.
        /// </summary>
        /// <param name="url">The OData service URL.</param>
        /// <param name="authToken">Optional authentication token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async Task TestConnection(string url, string? authToken)
        {
            Output.Write("\n⏳ Testing connection...");

            try
            {
                using var client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };

                if (!string.IsNullOrWhiteSpace(authToken))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                }

                var metadataUrl = url.TrimEnd('/') + "/$metadata";
                var response = await client.GetAsync(metadataUrl);

                if (response.IsSuccessStatusCode)
                {
                    Output.WriteLine("\r✅ Connection successful!     ");

                    var content = await response.Content.ReadAsStringAsync();
                    var entityCount = Regex.Matches(content, "<EntityType").Count;
                    var entitySetCount = Regex.Matches(content, "<EntitySet").Count;

                    if (entityCount > 0 || entitySetCount > 0)
                    {
                        Output.WriteLine($"   Found {entitySetCount} entity sets and {entityCount} entity types.");
                    }
                }
                else
                {
                    Output.WriteLine($"\r⚠️  Connection returned {response.StatusCode}. This might still work with proper authentication.");
                }
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
        /// <c>true</c> when stdin is redirected, <see cref="Input"/> is not the process
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
