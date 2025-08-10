using System;
using System.IO;
using System.Linq;
using System.Text;
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
    /// </remarks>
    [Command(Name = "add", Description = "Interactive wizard to generate Claude Code MCP registration command")]
    public class AddCommand
    {

        #region Public Methods

        /// <summary>
        /// Executes the interactive wizard for generating MCP registration commands.
        /// </summary>
        /// <returns>Exit code (0 for success).</returns>
        public async Task<int> OnExecuteAsync()
        {
            Console.WriteLine("🚀 OData MCP Setup Wizard for Claude Code");
            Console.WriteLine("==========================================\n");
            Console.WriteLine("Let's set up your OData connection!\n");

            try
            {
                // Question 1: URL
                var url = PromptForUrl();

                // Question 2: Name (with smart default)
                var name = PromptForName(url);

                // Question 3: Authentication
                var (needsAuth, authToken, authType) = await PromptForAuthentication();

                // Question 4: Scope
                var scope = PromptForScope();

                // Question 5: Verbose logging
                var verbose = PromptForVerboseLogging();

                // Question 6: Test connection (optional)
                var shouldTest = PromptConfirm("Would you like to test the connection first?", true);
                if (shouldTest)
                {
                    await TestConnection(url, authToken);
                }

                // Build and display the command
                var command = BuildMcpCommand(name, url, authToken, scope, verbose);
                DisplayResult(command);

                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Builds the MCP command string based on user inputs.
        /// </summary>
        /// <param name="name">The name for the MCP server.</param>
        /// <param name="url">The OData service URL.</param>
        /// <param name="authToken">Optional authentication token.</param>
        /// <param name="scope">The scope for the MCP server (user or project).</param>
        /// <param name="verbose">Whether to enable verbose logging.</param>
        /// <returns>The formatted MCP command string.</returns>
        private string BuildMcpCommand(string name, string url, string? authToken, string scope, bool verbose)
        {
            var sb = new StringBuilder($"claude mcp add {name}");

            // Add scope
            sb.Append($" --scope {scope}");

            // Add environment variable for auth if needed
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                sb.Append($" --env ODATA_AUTH_TOKEN={authToken}");
            }

            // Add the -- separator before the command           
            sb.Append($" -- dotnet odata-mcp -- start \"{url}\"");

            // Add auth token parameter if provided
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                sb.Append($" --auth-token \"{authToken}\"");
            }

            // Add verbose flag if requested
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
        private string DeriveNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var segments = uri.Segments
                    .Where(s => !string.IsNullOrWhiteSpace(s) && s != "/")
                    .Select(s => s.TrimEnd('/'))
                    .ToList();

                // Look for common patterns
                if (segments.Any(s => s.Equals("Northwind", StringComparison.OrdinalIgnoreCase)))
                    return "northwind";
                if (segments.Any(s => s.Contains("TripPin", StringComparison.OrdinalIgnoreCase)))
                    return "trippin";
                
                // Use the last meaningful segment
                if (segments.Count > 0)
                {
                    var lastSegment = segments.Last()
                        .Replace(".svc", "")
                        .Replace("Service", "")
                        .Replace("OData", "")
                        .ToLowerInvariant();
                    
                    if (!string.IsNullOrWhiteSpace(lastSegment))
                        return lastSegment;
                }

                // Fall back to host name
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
        private void DisplayResult(string command)
        {
            Console.WriteLine("\n✨ Perfect! Here's your command:\n");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(command);
            Console.ResetColor();

            Console.WriteLine("\nNext steps:");
            Console.WriteLine("1. Open Claude Code terminal (not chat)");
            Console.WriteLine("2. Type or paste this command exactly as shown");
            Console.WriteLine("3. Press Enter");
            Console.WriteLine("4. The server will start automatically");
            Console.WriteLine("5. Return to Claude chat and query your OData service!");
            Console.WriteLine("\nExample query: 'Show me all products from the OData service'");
            Console.WriteLine("\nNote: Make sure you have the .NET 10 SDK installed and the tool has been installed globally.");
        }

        /// <summary>
        /// Prompts for a yes/no confirmation.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="defaultValue">The default value if user just presses Enter.</param>
        /// <returns>True if user confirms, false otherwise.</returns>
        private bool PromptConfirm(string message, bool defaultValue)
        {
            var defaultText = defaultValue ? "Y/n" : "y/N";
            Console.Write($"{message} ({defaultText}): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            
            if (string.IsNullOrEmpty(response))
                return defaultValue;
            
            return response == "y" || response == "yes";
        }

        /// <summary>
        /// Prompts for a string input with an optional default value.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="defaultValue">Optional default value.</param>
        /// <returns>The user's input or default value.</returns>
        private string PromptInput(string message, string? defaultValue = null)
        {
            if (!string.IsNullOrEmpty(defaultValue))
                Console.Write($"{message} (default: {defaultValue}): ");
            else
                Console.Write($"{message}: ");
            
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(defaultValue))
                return defaultValue;
            
            return input ?? "";
        }

        /// <summary>
        /// Prompts for a password (masked input).
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <returns>The entered password.</returns>
        private string PromptPassword(string message)
        {
            Console.Write($"{message}: ");
            var password = new StringBuilder();
            
            while (true)
            {
                var key = Console.ReadKey(true);
                
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Length--;
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
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
        private string PromptSelect(string message, string[] options)
        {
            Console.WriteLine(message);
            for (int i = 0; i < options.Length; i++)
            {
                Console.WriteLine($"  {i + 1}. {options[i]}");
            }
            
            while (true)
            {
                Console.Write("Select (1-" + options.Length + "): ");
                var input = Console.ReadLine()?.Trim();
                
                if (int.TryParse(input, out var choice) && choice >= 1 && choice <= options.Length)
                {
                    return options[choice - 1];
                }
                
                Console.WriteLine("Invalid selection. Please try again.");
            }
        }

        /// <summary>
        /// Prompts the user for authentication details.
        /// </summary>
        /// <returns>A tuple containing authentication information.</returns>
        private async Task<(bool needsAuth, string? authToken, string? authType)> PromptForAuthentication()
        {
            var needsAuth = PromptConfirm("Does your service require authentication?", false);
            
            if (!needsAuth)
                return (false, null, null);

            var authType = PromptSelect("What type of authentication?", 
                new[] { "Bearer Token", "API Key", "Basic Auth (Username/Password)" });

            string? authToken = null;

            switch (authType)
            {
                case "Bearer Token":
                    authToken = PromptPassword("Enter your bearer token");
                    if (string.IsNullOrWhiteSpace(authToken))
                    {
                        Console.WriteLine("Token cannot be empty");
                        return await PromptForAuthentication();
                    }
                    break;
                    
                case "API Key":
                    authToken = PromptPassword("Enter your API key");
                    if (string.IsNullOrWhiteSpace(authToken))
                    {
                        Console.WriteLine("API key cannot be empty");
                        return await PromptForAuthentication();
                    }
                    break;
                    
                case "Basic Auth (Username/Password)":
                    var username = PromptInput("Username");
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        Console.WriteLine("Username cannot be empty");
                        return await PromptForAuthentication();
                    }
                    var password = PromptPassword("Password");
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        Console.WriteLine("Password cannot be empty");
                        return await PromptForAuthentication();
                    }
                    // For basic auth, we'll pass the token as-is and let the start command handle encoding
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
        private string PromptForName(string url)
        {
            var defaultName = DeriveNameFromUrl(url);
            
            var name = PromptInput("What would you like to name this connection?", defaultName);
            
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z0-9-]+$"))
            {
                Console.WriteLine("Name must be lowercase alphanumeric with hyphens only (e.g., 'my-service')");
                return PromptForName(url);
            }
            
            return name;
        }

        /// <summary>
        /// Prompts the user for the OData service URL.
        /// </summary>
        /// <returns>The validated OData service URL.</returns>
        private string PromptForUrl()
        {
            Console.WriteLine("Examples:");
            Console.WriteLine("  • https://services.odata.org/V4/Northwind/Northwind.svc");
            Console.WriteLine("  • https://services.odata.org/V4/TripPinServiceRW");
            Console.WriteLine();

            var url = PromptInput("What's your OData service URL?");
            
            if (string.IsNullOrWhiteSpace(url))
            {
                Console.WriteLine("URL cannot be empty");
                return PromptForUrl();
            }
            
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Console.WriteLine("Please enter a valid URL");
                return PromptForUrl();
            }
            
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                Console.WriteLine("URL must be HTTP or HTTPS");
                return PromptForUrl();
            }
            
            return url;
        }

        /// <summary>
        /// Prompts the user for the MCP server scope.
        /// </summary>
        /// <returns>The selected scope (user or project).</returns>
        private string PromptForScope()
        {
            Console.WriteLine("Where should this MCP server be available?");
            Console.WriteLine("  • user - Available globally for all Claude Code sessions");
            Console.WriteLine("  • project - Only available in the current project");
            Console.WriteLine();
            
            var scope = PromptSelect("Select scope", new[] { "user", "project" });
            return scope;
        }

        /// <summary>
        /// Prompts the user for verbose logging preference.
        /// </summary>
        /// <returns>True if verbose logging should be enabled.</returns>
        private bool PromptForVerboseLogging()
        {
            return PromptConfirm("Would you like to enable verbose logging?", false);
        }

        /// <summary>
        /// Tests the connection to the OData service.
        /// </summary>
        /// <param name="url">The OData service URL.</param>
        /// <param name="authToken">Optional authentication token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task TestConnection(string url, string? authToken)
        {
            Console.Write("\n⏳ Testing connection...");
            
            try
            {
                using var client = new System.Net.Http.HttpClient();
                
                // Add auth header if provided
                if (!string.IsNullOrWhiteSpace(authToken))
                {
                    client.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                }

                // Try to fetch metadata
                var metadataUrl = url.TrimEnd('/') + "/$metadata";
                var response = await client.GetAsync(metadataUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("\r✅ Connection successful!     ");
                    
                    // Try to parse and show entity count
                    var content = await response.Content.ReadAsStringAsync();
                    var entityCount = System.Text.RegularExpressions.Regex.Matches(content, "<EntityType").Count;
                    var entitySetCount = System.Text.RegularExpressions.Regex.Matches(content, "<EntitySet").Count;
                    
                    if (entityCount > 0 || entitySetCount > 0)
                    {
                        Console.WriteLine($"   Found {entitySetCount} entity sets and {entityCount} entity types.");
                    }
                }
                else
                {
                    Console.WriteLine($"\r⚠️  Connection returned {response.StatusCode}. This might still work with proper authentication.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\r⚠️  Could not connect: {ex.Message}");
                Console.WriteLine("   The service might still work when properly configured.");
            }
        }

        #endregion

    }

}
