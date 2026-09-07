// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Core.Parsing;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tools.Hosting
{

    /// <summary>
    /// Builds the local stdio MCP host for a remote OData service.
    /// </summary>
    public sealed class ToolsMcpHost
    {

        #region Properties

        /// <summary>
        /// Gets the catalog advertised by this host.
        /// </summary>
        public ODataMcpCatalog Catalog { get; }

        /// <summary>
        /// Gets the MCP session.
        /// </summary>
        public ODataMcpSession Session { get; }

        /// <summary>
        /// Gets the remote service root.
        /// </summary>
        public Uri ServiceRoot { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolsMcpHost"/> class.
        /// </summary>
        /// <param name="serviceRoot">The OData service root.</param>
        /// <param name="session">The MCP session.</param>
        public ToolsMcpHost(Uri serviceRoot, ODataMcpSession session)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);
            ArgumentNullException.ThrowIfNull(session);

            ServiceRoot = serviceRoot;
            Session = session;
            Catalog = session.Catalog;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Fetches <c>$metadata</c>, parses it, and builds a host bound to the remote service.
        /// </summary>
        /// <param name="serviceUrl">The OData service root URL.</param>
        /// <param name="authToken">Optional bearer token.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The host.
        /// </returns>
        public static async Task<ToolsMcpHost> CreateAsync(string serviceUrl, string? authToken, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);

            if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException($"Invalid URL: {serviceUrl}", nameof(serviceUrl));
            }

            var root = new Uri(uri.ToString().TrimEnd('/') + "/", UriKind.Absolute);
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            var metadata = await http.GetStringAsync(new Uri(root, "$metadata"), cancellationToken).ConfigureAwait(false);
            var model = new CsdlParser().ParseFromString(metadata);
            var catalog = new ODataMcpCatalog(model, new ODataMcpCatalogOptions
            {
                RouteName = "remote"
            });

            var services = new ServiceCollection();
            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = root;
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                if (!string.IsNullOrWhiteSpace(authToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                }
            });

            var executor = new RemoteODataExecutor(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());
            var session = new ODataMcpSession(catalog, new ODataToolRuntime(catalog, executor), metadata);

            return new ToolsMcpHost(root, session);
        }

        /// <summary>
        /// Builds a stdio MCP host that includes <c>shutdown_server</c>.
        /// </summary>
        /// <param name="lifetime">The lifetime cancelled by <c>shutdown_server</c>.</param>
        /// <param name="verbose">Whether to log verbosely to stderr.</param>
        /// <param name="authToken">Optional bearer token for the OData HttpClient.</param>
        /// <returns>
        /// The generic host.
        /// </returns>
        public IHost BuildStdioHost(CancellationTokenSource lifetime, bool verbose, string? authToken)
        {
            ArgumentNullException.ThrowIfNull(lifetime);

            var shutdown = new ShutdownServerTool(lifetime);
            var session = Session;

            return Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole(options =>
                    {
                        options.LogToStandardErrorThreshold = LogLevel.Trace;
                    });
                    logging.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
                    if (!verbose)
                    {
                        logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
                    }
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton(session);
                    services.AddSingleton(shutdown);
                    services.AddHttpClient("OData", client =>
                    {
                        client.BaseAddress = ServiceRoot;
                        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                        if (!string.IsNullOrWhiteSpace(authToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                        }
                    });

                    services.AddMcpServer()
                        .WithStdioServerTransport()
                        .WithODataCatalogHandlers(
                            sp => sp.GetRequiredService<ODataMcpSession>(),
                            _ => [CreateShutdownTool()],
                            (request, cancellationToken) => HandleShutdownAsync(request, shutdown, cancellationToken));
                })
                .Build();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Advertises the local <c>shutdown_server</c> tool.
        /// </summary>
        /// <returns>
        /// The MCP tool.
        /// </returns>
        internal static Tool CreateShutdownTool()
        {
            return new Tool
            {
                Annotations = new ToolAnnotations
                {
                    DestructiveHint = true,
                    IdempotentHint = false,
                    OpenWorldHint = false,
                    ReadOnlyHint = false
                },
                Description = "Gracefully shuts down the OData MCP server. Local Tools host only.",
                InputSchema = JsonSerializer.Deserialize<JsonElement>("""{"type":"object","properties":{"reason":{"type":"string"},"delay_seconds":{"type":"integer","minimum":0,"maximum":10}},"additionalProperties":false}"""),
                Name = "shutdown_server",
                Title = "Shut down MCP server"
            };
        }

        /// <summary>
        /// Handles <c>shutdown_server</c> when present.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="shutdown">The shutdown tool.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A result when the tool is shutdown; otherwise <c>null</c>.
        /// </returns>
        internal static async ValueTask<CallToolResult?> HandleShutdownAsync(
            RequestContext<CallToolRequestParams> request,
            ShutdownServerTool shutdown,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(shutdown);

            if (!string.Equals(request.Params?.Name, "shutdown_server", StringComparison.Ordinal))
            {
                return null;
            }

            var arguments = request.Params?.Arguments;
            var reason = ReadString(arguments, "reason");
            var delay = 2;
            if (arguments is not null && arguments.TryGetValue("delay_seconds", out var delayValue) && delayValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                var delayText = delayValue.ValueKind == JsonValueKind.Number
                    ? delayValue.GetRawText()
                    : delayValue.ValueKind == JsonValueKind.String ? delayValue.GetString() : delayValue.GetRawText();
                if (!int.TryParse(delayText, out delay))
                {
                    throw new ArgumentException("delay_seconds must be an integer between 0 and 10.", "delay_seconds");
                }
            }

            var text = await shutdown.InvokeAsync(reason, delay, cancellationToken).ConfigureAwait(false);

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = text }]
            };
        }

        /// <summary>
        /// Reads an optional string argument.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="name">The argument name.</param>
        /// <returns>
        /// The string, or <c>null</c>.
        /// </returns>
        internal static string? ReadString(IDictionary<string, JsonElement>? arguments, string name)
        {
            if (arguments is null || !arguments.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        }

        #endregion

    }

}
