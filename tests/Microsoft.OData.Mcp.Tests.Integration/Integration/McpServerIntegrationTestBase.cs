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

            base.TestSetup();
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
            // Since the Console project is configured as a dotnet tool,
            // we need to run it via "dotnet run" or use the dll directly
            var solutionDir = FindSolutionDirectory();
            if (string.IsNullOrEmpty(solutionDir))
            {
                throw new InvalidOperationException("Could not find solution directory");
            }

            var consoleProjectPath = Path.Combine(solutionDir, "src", "Microsoft.OData.Mcp.Console");
            if (!Directory.Exists(consoleProjectPath))
            {
                throw new DirectoryNotFoundException($"Console project not found at: {consoleProjectPath}");
            }

            // First, try to find the built DLL
            var targetFramework = GetTargetFramework();
            var configuration = GetBuildConfiguration();
            
            var dllPath = Path.Combine(consoleProjectPath, "bin", configuration, targetFramework, "Microsoft.OData.Mcp.Console.dll");
            
            if (!File.Exists(dllPath))
            {
                Logger?.LogInformation("DLL not found at {Path}, attempting to build console app", dllPath);
                BuildConsoleApp(consoleProjectPath, configuration, targetFramework);
                
                // Check again after build
                if (!File.Exists(dllPath))
                {
                    throw new FileNotFoundException($"Console DLL not found even after build: {dllPath}");
                }
            }

            Logger?.LogInformation("Found MCP server DLL at: {Path}", dllPath);
            return dllPath;
        }

        /// <summary>
        /// Gets the target framework to use based on the current runtime.
        /// </summary>
        private string GetTargetFramework()
        {
            var version = Environment.Version;
            if (version.Major >= 10)
                return "net10.0";
            else if (version.Major >= 9)
                return "net9.0";
            else
                return "net8.0";
        }

        /// <summary>
        /// Gets the build configuration to use.
        /// </summary>
        private string GetBuildConfiguration()
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }

        /// <summary>
        /// Finds the solution directory by walking up the directory tree.
        /// </summary>
        private string? FindSolutionDirectory()
        {
            var currentDir = new DirectoryInfo(AppContext.BaseDirectory);

            while (currentDir != null)
            {
                // Check for the solution file (note the correct name)
                if (File.Exists(Path.Combine(currentDir.FullName, "Microsoft.OData.Mcp.sln")))
                {
                    return currentDir.FullName;
                }
                currentDir = currentDir.Parent;
            }

            return null;
        }

        /// <summary>
        /// Builds the console app.
        /// </summary>
        private void BuildConsoleApp(string projectPath, string configuration, string targetFramework)
        {
            Logger?.LogInformation("Building console app project: {ProjectPath} with {Configuration} {Framework}", 
                projectPath, configuration, targetFramework);

            var buildProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build --configuration {configuration} --framework {targetFramework}",
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
