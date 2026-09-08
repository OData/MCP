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
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Constants;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Core.Parsing;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tools.Hosting
{

    /// <summary>
    /// The local MCP host for a remote OData service: one generic host that owns the authenticating
    /// <c>"OData"</c> client, the unauthenticated <c>"OAuth"</c> client, the token cache, the catalog built from
    /// the service's <c>$metadata</c>, and — when the process is serving stdio — the MCP server itself.
    /// </summary>
    /// <example>
    /// <code>
    /// using var lifetime = new CancellationTokenSource();
    /// using var toolsHost = await ToolsMcpHost.CreateAsync(
    ///     "https://api.example.com/odata",
    ///     new OutboundOAuthOptions { ClientId = "cli" },
    ///     includeStdioMcp: true,
    ///     verbose: false,
    ///     lifetime,
    ///     lifetime.Token);
    ///
    /// await toolsHost.Host.RunAsync(lifetime.Token);
    /// </code>
    /// </example>
    /// <remarks>
    /// There is exactly one <c>Build()</c>, and everything the process needs is registered before it. That
    /// matters because <see cref="IHttpClientFactory"/> cannot be shared between service providers: the
    /// <c>$metadata</c> GET has to run through the same authenticating client the tools later run through, or
    /// the credential the handler negotiated would be attached to one client and thrown away with the other.
    /// </remarks>
    public sealed class ToolsMcpHost : IDisposable
    {

        #region Properties

        /// <summary>
        /// Gets the catalog advertised by this host.
        /// </summary>
        public ODataMcpCatalog Catalog { get; }

        /// <summary>
        /// Gets the generic host that owns every service this host resolves, including the two named HTTP
        /// clients and, when stdio is enabled, the MCP server.
        /// </summary>
        public IHost Host { get; }

        /// <summary>
        /// Gets the MCP session.
        /// </summary>
        public ODataMcpSession Session { get; }

        /// <summary>
        /// Gets the remote service root, always with a trailing slash.
        /// </summary>
        public Uri ServiceRoot { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolsMcpHost"/> class.
        /// </summary>
        /// <param name="host">The generic host that owns every service this host resolves.</param>
        /// <param name="serviceRoot">The OData service root.</param>
        /// <param name="session">The MCP session.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="host"/>, <paramref name="serviceRoot"/>, or <paramref name="session"/> is
        /// <see langword="null"/>.
        /// </exception>
        public ToolsMcpHost(IHost host, Uri serviceRoot, ODataMcpSession session)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(serviceRoot);
            ArgumentNullException.ThrowIfNull(session);

            Catalog = session.Catalog;
            Host = host;
            ServiceRoot = serviceRoot;
            Session = session;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Builds the host, authenticates against the remote service, fetches and parses <c>$metadata</c>, and
        /// returns a host whose catalog and session are ready to serve.
        /// </summary>
        /// <param name="serviceUrl">The OData service root URL.</param>
        /// <param name="options">The operator's outbound authentication settings.</param>
        /// <param name="includeStdioMcp">Whether to register the stdio MCP server and its <c>shutdown_server</c> tool.</param>
        /// <param name="verbose">Whether to log at <see cref="LogLevel.Debug"/> instead of <see cref="LogLevel.Information"/>.</param>
        /// <param name="lifetime">The lifetime <c>shutdown_server</c> cancels; required when <paramref name="includeStdioMcp"/> is <see langword="true"/>.</param>
        /// <param name="cancellationToken">The token that cancels the metadata fetch and any sign-in it triggers.</param>
        /// <param name="configureServices">A last chance to add or replace registrations, applied immediately before the single <c>Build()</c>.</param>
        /// <returns>
        /// The host.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceUrl"/> is empty, is not absolute, or is not <c>http</c> or <c>https</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>, or <paramref name="lifetime"/> is <see langword="null"/> while <paramref name="includeStdioMcp"/> is <see langword="true"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the <c>$metadata</c> fetch has to run an OAuth grant and this host's credential store cannot round-trip a value.</exception>
        /// <exception cref="HttpRequestException">Thrown when the <c>$metadata</c> request fails after authentication.</exception>
        /// <example>
        /// <code>
        /// // A test can point both named clients at in-process servers without touching the network.
        /// using var toolsHost = await ToolsMcpHost.CreateAsync(
        ///     resource.ServiceRoot.ToString(),
        ///     options,
        ///     includeStdioMcp: false,
        ///     verbose: false,
        ///     lifetime: null,
        ///     cancellationToken,
        ///     services =&gt;
        ///     {
        ///         services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName)
        ///             .ConfigurePrimaryHttpMessageHandler(() =&gt; resource.Server.CreateHandler());
        ///     });
        /// </code>
        /// </example>
        /// <remarks>
        /// The order here is the whole point. Registrations first, <paramref name="configureServices"/> last,
        /// then one <c>Build()</c>; only afterwards does anything touch the network, because the
        /// <c>$metadata</c> GET is what drives the authentication handler through discovery, consent, and the
        /// token cache. A restart that finds a still-valid cached token spends exactly one request here: no
        /// device code, no token endpoint, no discovery.
        /// <para>
        /// The credential store is not probed here either: <c>OutboundOAuthClient</c> proves it usable on the
        /// first acquisition, so a service that answers <c>$metadata</c> without a challenge never touches the
        /// operating system keyring and never creates a token cache directory.
        /// </para>
        /// </remarks>
        public static async Task<ToolsMcpHost> CreateAsync(
            string serviceUrl,
            OutboundOAuthOptions options,
            bool includeStdioMcp,
            bool verbose,
            CancellationTokenSource? lifetime,
            CancellationToken cancellationToken,
            Action<IServiceCollection>? configureServices = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);
            ArgumentNullException.ThrowIfNull(options);

            if (includeStdioMcp && lifetime is null)
            {
                throw new ArgumentNullException(nameof(lifetime), "A stdio MCP host needs a lifetime for shutdown_server to cancel.");
            }

            var root = NormalizeServiceRoot(serviceUrl);

            options.ConsentPresenter ??= StdioConsentPresenter.PresentAsync;

            var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Logging.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
            if (!verbose)
            {
                builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
            }

            builder.Services.AddSingleton<CsdlParser>();
            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton<SdkAuth.ITokenCache>(provider => new LatchkeyTokenCache(
                root,
                options.ClientId ?? "anonymous",
                options.TokenCachePath,
                provider.GetRequiredService<ILogger<LatchkeyTokenCache>>()));
            builder.Services.AddSingleton<ProtectedResourceMetadataClient>();
            builder.Services.AddSingleton<AuthorizationServerMetadataClient>();
            builder.Services.AddSingleton<OAuthDiscovery>();
            builder.Services.AddSingleton(provider => new OutboundOAuthClient(
                root,
                provider.GetRequiredService<IHttpClientFactory>(),
                options,
                provider.GetRequiredService<SdkAuth.ITokenCache>(),
                provider.GetRequiredService<OAuthDiscovery>(),
                provider.GetRequiredService<ILoggerFactory>()));
            builder.Services.AddTransient<ODataOutboundAuthHandler>();
            builder.Services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName, client => client.BaseAddress = root)
                .AddHttpMessageHandler(provider => provider.GetRequiredService<ODataOutboundAuthHandler>());
            builder.Services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName);
            builder.Services.AddSingleton<RemoteODataExecutor>();
            builder.Services.AddSingleton<ToolsMcpSessionHolder>();

            if (includeStdioMcp)
            {
                var shutdown = new ShutdownServerTool(lifetime!);

                builder.Services.AddSingleton(shutdown);
                builder.Services.AddMcpServer()
                    .WithStdioServerTransport()
                    .WithODataCatalogHandlers(
                        provider =>
                        {
                            var session = provider.GetRequiredService<ToolsMcpSessionHolder>().Session;
                            ArgumentNullException.ThrowIfNull(session);

                            return session;
                        },
                        _ => [CreateShutdownTool()],
                        (request, requestCancellationToken) => HandleShutdownAsync(request, shutdown, requestCancellationToken),
                        (request, exception, requestCancellationToken) =>
                        {
                            var services = request.Services;
                            ArgumentNullException.ThrowIfNull(services);

                            return HandleConsentAsync(
                                request,
                                exception,
                                services.GetRequiredService<ToolsMcpSessionHolder>(),
                                services.GetRequiredService<OutboundOAuthClient>(),
                                requestCancellationToken);
                        });
            }

            configureServices?.Invoke(builder.Services);

            var host = builder.Build();

            try
            {
                var metadataXml = await FetchMetadataAsync(host, root, cancellationToken).ConfigureAwait(false);
                var model = host.Services.GetRequiredService<CsdlParser>().ParseFromString(metadataXml);
                var catalog = new ODataMcpCatalog(model, new ODataMcpCatalogOptions
                {
                    RouteName = "remote"
                });
                var session = new ODataMcpSession(
                    catalog,
                    new ODataToolRuntime(catalog, host.Services.GetRequiredService<RemoteODataExecutor>()),
                    metadataXml);

                host.Services.GetRequiredService<ToolsMcpSessionHolder>().Session = session;

                if (includeStdioMcp)
                {
                    options.ConsentPresenter = null;
                }

                return new ToolsMcpHost(host, root, session);
            }
            catch
            {
                host.Dispose();

                throw;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Host.Dispose();
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
                InputSchema = ParseElement("""{"type":"object","properties":{"reason":{"type":"string"},"delay_seconds":{"type":"integer","minimum":0,"maximum":10}},"additionalProperties":false}"""),
                Name = "shutdown_server",
                Title = "Shut down MCP server"
            };
        }

        /// <summary>
        /// Fetches the remote service's <c>$metadata</c> document through the authenticating <c>"OData"</c>
        /// client.
        /// </summary>
        /// <param name="host">The built host whose <see cref="IHttpClientFactory"/> owns the named clients.</param>
        /// <param name="serviceRoot">The OData service root the document is resolved against.</param>
        /// <param name="cancellationToken">The token that cancels the request and any sign-in it triggers.</param>
        /// <returns>
        /// The CSDL document.
        /// </returns>
        /// <remarks>
        /// <c>Accept</c> is set on the request rather than as a client default, because the same client carries
        /// JSON data calls; <see cref="CsdlParser.ParseFromString(string)"/> is XML-only.
        /// </remarks>
        internal static async Task<string> FetchMetadataAsync(IHost host, Uri serviceRoot, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(serviceRoot);

            var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient(ODataMcpAuthConstants.ODataHttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(serviceRoot, "$metadata"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ODataMcpCatalogConstants.ApplicationXml));

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Recovers a tool call that failed because the remote OData service needs a human to sign in again:
        /// it elicits the sign-in URL from the MCP client, completes the grant the outbound client already
        /// started, and replays the tool call exactly once.
        /// </summary>
        /// <param name="request">The call request that failed.</param>
        /// <param name="exception">The exception the catalog tool threw.</param>
        /// <param name="holder">The holder whose session the retry invokes against.</param>
        /// <param name="client">The outbound OAuth client whose pending interactive grant is completed.</param>
        /// <param name="cancellationToken">The token that cancels the elicitation, the grant, and the retry.</param>
        /// <returns>
        /// The retried call's result; an error result when the sign-in was declined, timed out, or failed; or
        /// <see langword="null"/> when <paramref name="exception"/> is not a consent request, in which case the
        /// original exception propagates.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="request"/>, <paramref name="exception"/>, <paramref name="holder"/>, or
        /// <paramref name="client"/> is <see langword="null"/>.
        /// </exception>
        /// <example>
        /// <code>
        /// builder.WithODataCatalogHandlers(
        ///     resolveSession,
        ///     listExtraTools,
        ///     tryHandleExtra,
        ///     (request, exception, ct) =&gt; HandleConsentAsync(request, exception, holder, client, ct));
        /// </code>
        /// </example>
        /// <remarks>
        /// Per <c>specs/v3/AUTHENTICATION.md</c> "Token attach, elicitation, and the LLM" the elicitation is
        /// <c>url</c> mode and carries nothing but the verification URL, the correlation id, and a sentence of
        /// prose: an access token, a refresh token, and a device code are all secrets the MCP client must never
        /// see, so form mode is never used for a sign-in. The grant is started before the elicitation is
        /// awaited — the device code is already pending by the time the exception reaches here — and the two
        /// run concurrently, because a device code grant only completes while something is polling for it.
        /// <para>
        /// A client with no <c>url</c> elicitation capability falls back to stderr, which is where a stdio host
        /// can still reach a human. Either way the tool is retried exactly once: a second failure is the tool's
        /// own answer, not another sign-in.
        /// </para>
        /// </remarks>
        internal static async ValueTask<CallToolResult?> HandleConsentAsync(
            RequestContext<CallToolRequestParams> request,
            Exception exception,
            ToolsMcpSessionHolder holder,
            OutboundOAuthClient client,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(exception);
            ArgumentNullException.ThrowIfNull(holder);
            ArgumentNullException.ThrowIfNull(client);

            if (exception is not OAuthConsentRequiredException consent)
            {
                return null;
            }

            var name = request.Params?.Name;
            var session = holder.Session;
            if (string.IsNullOrWhiteSpace(name) || session is null)
            {
                return null;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var complete = client.CompleteInteractiveGrantAsync(linked.Token);
            var accepted = false;

            try
            {
                if (request.Server?.ClientCapabilities?.Elicitation?.Url is not null)
                {
                    var elicit = request.Server.ElicitAsync(new ElicitRequestParams
                    {
                        ElicitationId = consent.Request.ElicitationId,
                        Message = "Open the URL to sign in. Enter the code shown on stderr if asked.",
                        Mode = "url",
                        Url = consent.Request.Url.AbsoluteUri
                    }, linked.Token);

                    accepted = (await elicit.ConfigureAwait(false)).IsAccepted;
                }
                else
                {
                    await StdioConsentPresenter.PresentAsync(consent.Request, linked.Token).ConfigureAwait(false);

                    accepted = true;
                }

                if (accepted)
                {
                    await complete.ConfigureAwait(false);
                }
            }
            catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                accepted = false;
            }

            if (!accepted)
            {
                await linked.CancelAsync().ConfigureAwait(false);
                await ObserveAsync(complete).ConfigureAwait(false);

                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = "Sign-in declined or timed out; re-run 'odata-mcp start' if needed." }],
                    IsError = true
                };
            }

            var result = await session.Runtime.InvokeAsync(name, request.Params?.Arguments, cancellationToken).ConfigureAwait(false);

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = result.Text }],
                IsError = result.IsError,
                StructuredContent = string.IsNullOrWhiteSpace(result.StructuredContent)
                    ? null
                    : ParseElement(result.StructuredContent)
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
        /// Validates a service URL and normalizes it to an absolute root with a trailing slash.
        /// </summary>
        /// <param name="serviceUrl">The OData service root URL.</param>
        /// <returns>
        /// The normalized root.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceUrl"/> is not an absolute <c>http</c> or <c>https</c> URL.</exception>
        internal static Uri NormalizeServiceRoot(string serviceUrl)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);

            if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException($"Invalid URL: {serviceUrl}", nameof(serviceUrl));
            }

            return new Uri(uri.ToString().TrimEnd('/') + "/", UriKind.Absolute);
        }

        /// <summary>
        /// Awaits a task purely to observe its outcome, swallowing whatever it faulted or cancelled with.
        /// </summary>
        /// <param name="task">The task to observe.</param>
        /// <returns>
        /// A task that completes once <paramref name="task"/> has settled.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="task"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Used on the abandoned-sign-in path: the grant this cancels is going to fault, and an unobserved
        /// faulted <see cref="Task"/> would surface later as an unrelated <c>TaskScheduler</c> event in a
        /// process whose stderr is the operator's only diagnostic channel.
        /// </remarks>
        internal static async Task ObserveAsync(Task task)
        {
            ArgumentNullException.ThrowIfNull(task);

            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // The caller already decided this sign-in is over; the failure it ends with adds nothing.
            }
        }

        /// <summary>
        /// Parses a JSON document into a detached <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="json">The JSON text to parse.</param>
        /// <returns>
        /// The document's root element, detached from the parser's pooled buffer.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="JsonException">Thrown when <paramref name="json"/> is not valid JSON.</exception>
        /// <remarks>
        /// Used instead of <c>JsonSerializer.Deserialize&lt;JsonElement&gt;</c> because that overload is
        /// annotated <c>RequiresUnreferencedCode</c> and <c>RequiresDynamicCode</c>, which makes every caller a
        /// trim and AOT warning even though the destination type needs no reflection at all.
        /// </remarks>
        internal static JsonElement ParseElement(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);

            return document.RootElement.Clone();
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
