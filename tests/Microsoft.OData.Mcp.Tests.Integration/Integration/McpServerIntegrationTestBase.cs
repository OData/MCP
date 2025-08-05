using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.Assemblies;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Integration
{
    /// <summary>
    /// Base class for MCP server integration tests that communicate via the actual MCP protocol.
    /// </summary>
    public abstract class McpServerIntegrationTestBase : BreakdanceTestBase
    {
        protected McpProtocolTestClient? McpClient { get; private set; }
        protected ILogger<McpProtocolTestClient>? Logger { get; private set; }
        private string? _serverExecutablePath;

        /// <summary>
        /// Sets up the MCP server test environment.
        /// </summary>
        public override void TestSetup()
        {
            base.TestSetup();

            // Create logger for the test client  
            var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            Logger = loggerFactory.CreateLogger<McpProtocolTestClient>();

            // Locate the server executable
            _serverExecutablePath = FindServerExecutable();
            
            if (string.IsNullOrEmpty(_serverExecutablePath) || !File.Exists(_serverExecutablePath))
            {
                Assert.Fail($"Could not find MCP server executable at: {_serverExecutablePath}");
            }

            // Create and start the MCP client
            McpClient = new McpProtocolTestClient(_serverExecutablePath!, Logger);
        }

        /// <summary>
        /// Tears down the test environment.
        /// </summary>
        public override void TestTearDown()
        {
            McpClient?.Dispose();
            McpClient = null;
            Logger = null;
            base.TestTearDown();
        }

        /// <summary>
        /// Initializes the MCP connection for tests.
        /// </summary>
        protected async Task<McpInitializeResponse> InitializeMcpConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (McpClient == null)
                throw new InvalidOperationException("MCP client not initialized. Call TestSetup first.");

            return await McpClient.InitializeAsync(cancellationToken);
        }

        /// <summary>
        /// Recreates the MCP client with a new server instance.
        /// </summary>
        protected void RecreateClient(string serverExecutablePath)
        {
            McpClient?.Dispose();
            McpClient = new McpProtocolTestClient(serverExecutablePath, Logger!);
        }

        /// <summary>
        /// Finds the server executable path based on the current test configuration.
        /// </summary>
        protected string FindServerExecutable()
        {
            // Look for the console app executable in various locations
            var possiblePaths = new[]
            {
                // Release configuration
                @"..\..\..\..\samples\Microsoft.OData.Mcp.Console\bin\Release\net10.0\Microsoft.OData.Mcp.Console.exe",
                @"..\..\..\..\samples\Microsoft.OData.Mcp.Console\bin\Release\net9.0\Microsoft.OData.Mcp.Console.exe", 
                @"..\..\..\..\samples\Microsoft.OData.Mcp.Console\bin\Release\net8.0\Microsoft.OData.Mcp.Console.exe",
                
                // Debug configuration
                @"..\..\..\..\samples\Microsoft.OData.Mcp.Console\bin\Debug\net10.0\Microsoft.OData.Mcp.Console.exe",
                @"..\..\..\..\samples\Microsoft.OData.Mcp.Console\bin\Debug\net9.0\Microsoft.OData.Mcp.Console.exe",
                @"..\..\..\..\samples\Microsoft.OData.Mcp.Console\bin\Debug\net8.0\Microsoft.OData.Mcp.Console.exe",
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
                if (File.Exists(fullPath))
                {
                    Logger?.LogInformation("Found MCP server executable at: {Path}", fullPath);
                    return fullPath;
                }
            }

            // If not found, try building the console app
            var solutionDir = FindSolutionDirectory();
            if (!string.IsNullOrEmpty(solutionDir))
            {
                var consoleProjectPath = Path.Combine(solutionDir, "samples", "Microsoft.OData.Mcp.Console");
                if (Directory.Exists(consoleProjectPath))
                {
                    Logger?.LogInformation("Attempting to build console app at: {Path}", consoleProjectPath);
                    return BuildConsoleApp(consoleProjectPath);
                }
            }

            throw new FileNotFoundException("Could not locate MCP server executable. Please ensure the console app is built.");
        }

        /// <summary>
        /// Finds the solution directory by walking up the directory tree.
        /// </summary>
        private string? FindSolutionDirectory()
        {
            var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
            
            while (currentDir != null)
            {
                if (File.Exists(Path.Combine(currentDir.FullName, "Microsoft.OData.McpServer.sln")))
                {
                    return currentDir.FullName;
                }
                currentDir = currentDir.Parent;
            }

            return null;
        }

        /// <summary>
        /// Builds the console app and returns the executable path.
        /// </summary>
        private string BuildConsoleApp(string projectPath)
        {
            Logger?.LogInformation("Building console app project: {ProjectPath}", projectPath);

            var buildProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build --configuration Release --framework net10.0",
                    WorkingDirectory = projectPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            buildProcess.Start();
            var output = buildProcess.StandardOutput.ReadToEnd();
            var error = buildProcess.StandardError.ReadToEnd();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                Logger?.LogError("Build failed with exit code {ExitCode}. Output: {Output}. Error: {Error}", 
                    buildProcess.ExitCode, output, error);
                throw new InvalidOperationException($"Failed to build console app. Output: {output}. Error: {error}");
            }

            Logger?.LogInformation("Console app built successfully. Output: {Output}", output);

            var executablePath = Path.Combine(projectPath, "bin", "Release", "net10.0", "Microsoft.OData.Mcp.Console.exe");
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException($"Built executable not found at expected path: {executablePath}");
            }

            return executablePath;
        }

        /// <summary>
        /// Creates a cancellation token with a reasonable timeout for integration tests.
        /// </summary>
        protected CancellationToken CreateTestCancellationToken(TimeSpan? timeout = null)
        {
            var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(2));
            return cts.Token;
        }

        /// <summary>
        /// Waits for the server to be ready by attempting to ping it.
        /// </summary>
        protected async Task WaitForServerReadyAsync(CancellationToken cancellationToken = default)
        {
            if (McpClient == null)
                throw new InvalidOperationException("MCP client not initialized");

            const int maxAttempts = 10;
            var attempt = 0;

            while (attempt < maxAttempts)
            {
                try
                {
                    await McpClient.PingAsync(cancellationToken);
                    Logger?.LogInformation("Server is ready after {Attempts} attempts", attempt + 1);
                    return;
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt >= maxAttempts)
                    {
                        Logger?.LogError(ex, "Server failed to become ready after {MaxAttempts} attempts", maxAttempts);
                        throw new TimeoutException($"Server failed to become ready after {maxAttempts} attempts", ex);
                    }

                    Logger?.LogDebug("Server not ready yet (attempt {Attempt}/{MaxAttempts}), waiting...", attempt, maxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
        }
    }
}