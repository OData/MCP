using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.OData.Mcp.Tests.Integration
{
    /// <summary>
    /// Test client for communicating with MCP server via STDIO protocol.
    /// </summary>
    public class McpProtocolTestClient : IDisposable
    {
        private readonly Process _serverProcess;
        private readonly StreamWriter _stdin;
        private readonly StreamReader _stdout;
        private readonly ILogger<McpProtocolTestClient> _logger;
        private int _nextRequestId = 1;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new MCP protocol test client.
        /// </summary>
        /// <param name="serverPath">Path to the MCP server executable or DLL.</param>
        /// <param name="logger">Logger instance.</param>
        public McpProtocolTestClient(string serverPath, ILogger<McpProtocolTestClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("Starting MCP server process: {ServerPath}", serverPath);

            // Determine if we're running a DLL or EXE
            string fileName;
            string arguments;
            
            if (serverPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                // Run via dotnet command for DLL
                fileName = "dotnet";
                // Pass "start" command with a test URL - no port means STDIO mode
                arguments = $"\"{serverPath}\" start http://testserver/odata/$metadata";
            }
            else
            {
                // Run as executable
                fileName = serverPath;
                // Pass "start" command with a test URL - no port means STDIO mode
                arguments = "start http://testserver/odata/$metadata";
            }

            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(serverPath)
                }
            };

            _serverProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogWarning("MCP Server Error: {Error}", e.Data);
                }
            };

            if (!_serverProcess.Start())
            {
                throw new InvalidOperationException($"Failed to start MCP server process: {serverPath}");
            }

            _serverProcess.BeginErrorReadLine();

            _stdin = _serverProcess.StandardInput;
            _stdout = _serverProcess.StandardOutput;

            _logger.LogInformation("MCP server process started with PID: {ProcessId}", _serverProcess.Id);
        }

        /// <summary>
        /// Initializes the MCP connection and performs handshake.
        /// </summary>
        public async Task<McpInitializeResponse> InitializeAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initializing MCP connection...");

            var initializeRequest = new McpRequest
            {
                JsonRpc = "2.0",
                Id = _nextRequestId++,
                Method = "initialize",
                Params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { }
                    },
                    clientInfo = new
                    {
                        name = "Microsoft.OData.Mcp.Tests",
                        version = "1.0.0"
                    }
                }
            };

            var response = await SendRequestAsync<McpInitializeResponse>(initializeRequest, cancellationToken);
            
            _logger.LogInformation("MCP connection initialized successfully. Server: {ServerName} v{ServerVersion}", 
                response.Result?.ServerInfo?.Name, response.Result?.ServerInfo?.Version);

            return response;
        }

        /// <summary>
        /// Lists available tools from the MCP server.
        /// </summary>
        public async Task<McpListToolsResponse> ListToolsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Requesting available tools...");

            var request = new McpRequest
            {
                JsonRpc = "2.0",
                Id = _nextRequestId++,
                Method = "tools/list",
                Params = new { }
            };

            var response = await SendRequestAsync<McpListToolsResponse>(request, cancellationToken);
            
            _logger.LogInformation("Retrieved {ToolCount} tools from server", response.Result?.Tools?.Length ?? 0);

            return response;
        }

        /// <summary>
        /// Calls a specific tool with the given parameters.
        /// </summary>
        public async Task<McpCallToolResponse> CallToolAsync(string toolName, object parameters, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Calling tool: {ToolName}", toolName);

            var request = new McpRequest
            {
                JsonRpc = "2.0",
                Id = _nextRequestId++,
                Method = "tools/call",
                Params = new
                {
                    name = toolName,
                    arguments = parameters ?? new { }
                }
            };

            var response = await SendRequestAsync<McpCallToolResponse>(request, cancellationToken);
            
            _logger.LogInformation("Tool call completed: {ToolName}", toolName);

            return response;
        }

        /// <summary>
        /// Sends a ping request to test server responsiveness.
        /// </summary>
        public async Task<McpPingResponse> PingAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Sending ping to server...");

            var request = new McpRequest
            {
                JsonRpc = "2.0",
                Id = _nextRequestId++,
                Method = "ping",
                Params = new { }
            };

            var response = await SendRequestAsync<McpPingResponse>(request, cancellationToken);
            
            _logger.LogInformation("Ping successful");

            return response;
        }

        /// <summary>
        /// Sends a raw JSON-RPC request and returns the raw response.
        /// </summary>
        public async Task<string> SendRawRequestAsync(string jsonRequest, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Sending raw request: {Request}", jsonRequest);

            await _stdin.WriteLineAsync(jsonRequest);
            await _stdin.FlushAsync();

            var response = await _stdout.ReadLineAsync();
            
            _logger.LogDebug("Received raw response: {Response}", response);

            return response ?? throw new InvalidOperationException("Received null response from MCP server");
        }

        /// <summary>
        /// Sends a request and deserializes the response.
        /// </summary>
        private async Task<T> SendRequestAsync<T>(McpRequest request, CancellationToken cancellationToken) where T : class
        {
            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            _logger.LogDebug("Sending request: {Request}", requestJson);

            await _stdin.WriteLineAsync(requestJson);
            await _stdin.FlushAsync();

            var responseJson = await _stdout.ReadLineAsync();
            if (string.IsNullOrEmpty(responseJson))
            {
                throw new InvalidOperationException("Received empty response from MCP server");
            }

            _logger.LogDebug("Received response: {Response}", responseJson);

            var response = JsonSerializer.Deserialize<T>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (response == null)
            {
                throw new InvalidOperationException($"Failed to deserialize response as {typeof(T).Name}");
            }

            return response;
        }

        /// <summary>
        /// Checks if the server process is still running.
        /// </summary>
        public bool IsServerRunning => !_serverProcess.HasExited;

        /// <summary>
        /// Gets the server process ID.
        /// </summary>
        public int ServerProcessId => _serverProcess.Id;

        /// <summary>
        /// Disposes the client and terminates the server process.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogInformation("Disposing MCP protocol test client...");

            try
            {
                _stdin?.Close();
                _stdout?.Close();

                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill();
                    _serverProcess.WaitForExit(5000);
                }

                _serverProcess?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MCP protocol test client");
            }

            _disposed = true;
            _logger.LogInformation("MCP protocol test client disposed");
        }
    }

    #region MCP Protocol Message Types

    /// <summary>
    /// Base MCP request structure.
    /// </summary>
    public class McpRequest
    {
        public string JsonRpc { get; set; } = "2.0";
        public int Id { get; set; }
        public string Method { get; set; } = string.Empty;
        public object? Params { get; set; }
    }

    /// <summary>
    /// Base MCP response structure.
    /// </summary>
    public class McpResponse<T>
    {
        public string JsonRpc { get; set; } = "2.0";
        public int Id { get; set; }
        public T? Result { get; set; }
        public McpError? Error { get; set; }
    }

    /// <summary>
    /// MCP error structure.
    /// </summary>
    public class McpError
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    /// <summary>
    /// Initialize response structure.
    /// </summary>
    public class McpInitializeResponse : McpResponse<McpInitializeResult> { }

    public class McpInitializeResult
    {
        public string ProtocolVersion { get; set; } = string.Empty;
        public McpServerCapabilities? Capabilities { get; set; }
        public McpServerInfo? ServerInfo { get; set; }
    }

    public class McpServerCapabilities
    {
        public McpToolsCapability? Tools { get; set; }
    }

    public class McpToolsCapability
    {
        public bool ListChanged { get; set; }
    }

    public class McpServerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }

    /// <summary>
    /// List tools response structure.
    /// </summary>
    public class McpListToolsResponse : McpResponse<McpListToolsResult> { }

    public class McpListToolsResult
    {
        public McpTool[]? Tools { get; set; }
    }

    public class McpTool
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public object? InputSchema { get; set; }
    }

    /// <summary>
    /// Call tool response structure.
    /// </summary>
    public class McpCallToolResponse : McpResponse<McpCallToolResult> { }

    public class McpCallToolResult
    {
        public McpContent[]? Content { get; set; }
        public bool IsError { get; set; }
    }

    public class McpContent
    {
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Ping response structure.
    /// </summary>
    public class McpPingResponse : McpResponse<object> { }

    #endregion
}