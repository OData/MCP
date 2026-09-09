// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tools.Hosting;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Runs a real <see cref="ToolsMcpHost"/> — the process <c>odata-mcp start</c> builds — against an
    /// in-process OAuth protected OData service and an in-process <see cref="LocalAuthorizationServer"/>, and
    /// exposes the resulting session so a linked tool-test suite can drive it exactly as it drives the
    /// in-process one.
    /// </summary>
    /// <example>
    /// <code>
    /// using var fixture = new OutboundToolsHostFixture(authorizationServer, testServer, new Uri("http://localhost/odata/"));
    ///
    /// await fixture.CreateSessionAsync(hostCatalogOptions, CancellationToken.None);
    ///
    /// var result = await fixture.Session.Runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
    ///
    /// fixture.Capture.Last!.RelativePath.Should().Be("Customers");
    /// </code>
    /// </example>
    /// <remarks>
    /// Nothing here is mocked. The <c>"OData"</c> named client's primary handler is the secured host's own
    /// <see cref="TestServer"/> handler and the <c>"OAuth"</c> client's is the authorization server's, so every
    /// discovery document, device code poll, token POST, and data call runs through a real ASP.NET Core
    /// pipeline behind a real <see cref="HttpClient"/> — and through the production
    /// <see cref="ODataOutboundAuthHandler"/> that attaches and refreshes the bearer token.
    /// <para>
    /// The token cache is the host's own <see cref="LatchkeyTokenCache"/> registration, pointed at a throwaway
    /// directory this fixture owns and deletes. That is what makes a restart meaningful: calling
    /// <see cref="CreateSessionAsync(ODataMcpCatalogOptions, CancellationToken)"/> a second time builds a second
    /// host against the same persisted credential, which is exactly what a second <c>odata-mcp start</c> does.
    /// </para>
    /// </remarks>
    public sealed class OutboundToolsHostFixture : IDisposable
    {

        #region Fields

        /// <summary>
        /// The capture wrapper the most recent session was built over.
        /// </summary>
        internal CapturingODataExecutor? _capture;

        /// <summary>
        /// The session the most recent <see cref="CreateSessionAsync(ODataMcpCatalogOptions, CancellationToken)"/> built.
        /// </summary>
        internal ODataMcpSession? _session;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the authorization server the outbound OAuth client discovers, polls, and exchanges against.
        /// </summary>
        public LocalAuthorizationServer AuthorizationServer { get; }

        /// <summary>
        /// Gets the executor wrapper that records every request the outbound path sent.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when read before <see cref="CreateSessionAsync(ODataMcpCatalogOptions, CancellationToken)"/> has run.</exception>
        public CapturingODataExecutor Capture
        {
            get
            {
                if (_capture is null)
                {
                    throw new InvalidOperationException($"Call {nameof(CreateSessionAsync)} before reading {nameof(Capture)}.");
                }

                return _capture;
            }
        }

        /// <summary>
        /// Gets the Tools host most recently built, or <see langword="null"/> when none has been.
        /// </summary>
        public ToolsMcpHost? Host { get; internal set; }

        /// <summary>
        /// Gets or sets the primary handler the <c>"OAuth"</c> named client dispatches through, or
        /// <see langword="null"/> to dispatch straight into <see cref="AuthorizationServer"/>.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="null"/>.
        /// </value>
        /// <remarks>
        /// Both in-process servers share the <c>http://localhost</c> origin, so a fixture that needs the
        /// discovery GETs split between them — RFC 9728 protected resource metadata served by the resource
        /// host, RFC 8414 authorization server metadata and the token endpoint served by the authorization
        /// server — sets a routing handler here. Every other fixture leaves it alone.
        /// </remarks>
        public HttpMessageHandler? OAuthHandler { get; set; }

        /// <summary>
        /// Gets or sets the outbound authentication settings the next
        /// <see cref="CreateSessionAsync(ODataMcpCatalogOptions, CancellationToken)"/> builds its host from.
        /// </summary>
        /// <value>
        /// Defaults to the seeded public <c>"cli"</c> client, the <c>read</c> and <c>write</c> scopes, this
        /// fixture's throwaway token cache directory, and <see cref="Presenter"/> standing in for a human.
        /// </value>
        /// <remarks>
        /// A fixture variant replaces or mutates this before the session is created; it is never read again
        /// afterwards, because <see cref="ToolsMcpHost"/> captures it as a singleton.
        /// </remarks>
        public OutboundOAuthOptions Options { get; set; }

        /// <summary>
        /// Gets the presenter that approves whatever interactive grant the outbound client starts.
        /// </summary>
        public AutoApproveConsentPresenter Presenter { get; }

        /// <summary>
        /// Gets the secured OData host the <c>"OData"</c> named client dispatches into.
        /// </summary>
        public TestServer ResourceServer { get; }

        /// <summary>
        /// Gets the absolute OData service root, with a trailing slash.
        /// </summary>
        public Uri ServiceRoot { get; }

        /// <summary>
        /// Gets the outbound session the linked tool tests invoke against.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when read before <see cref="CreateSessionAsync(ODataMcpCatalogOptions, CancellationToken)"/> has run.</exception>
        public ODataMcpSession Session
        {
            get
            {
                if (_session is null)
                {
                    throw new InvalidOperationException($"Call {nameof(CreateSessionAsync)} before reading {nameof(Session)}.");
                }

                return _session;
            }
        }

        /// <summary>
        /// Gets the throwaway directory the token cache persists into, deleted by <see cref="Dispose"/>.
        /// </summary>
        public string TokenCachePath { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundToolsHostFixture"/> class.
        /// </summary>
        /// <param name="authorizationServer">The authorization server the outbound OAuth client runs against.</param>
        /// <param name="resourceServer">The secured OData host the <c>"OData"</c> named client dispatches into.</param>
        /// <param name="serviceRoot">The absolute OData service root.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="authorizationServer"/>, <paramref name="resourceServer"/>, or
        /// <paramref name="serviceRoot"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceRoot"/> is not absolute.</exception>
        /// <remarks>
        /// No host is built here: a fixture variant gets a chance to change <see cref="Options"/> first, and a
        /// restart test builds two hosts from one fixture.
        /// </remarks>
        public OutboundToolsHostFixture(LocalAuthorizationServer authorizationServer, TestServer resourceServer, Uri serviceRoot)
        {
            ArgumentNullException.ThrowIfNull(authorizationServer);
            ArgumentNullException.ThrowIfNull(resourceServer);
            ArgumentNullException.ThrowIfNull(serviceRoot);

            if (!serviceRoot.IsAbsoluteUri)
            {
                throw new ArgumentException("The OData service root must be an absolute URI.", nameof(serviceRoot));
            }

            AuthorizationServer = authorizationServer;
            Presenter = new AutoApproveConsentPresenter(authorizationServer);
            ResourceServer = resourceServer;
            ServiceRoot = new Uri(serviceRoot.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
            TokenCachePath = Path.Combine(Path.GetTempPath(), $"odata-mcp-outbound-{Guid.NewGuid():N}");

            Directory.CreateDirectory(TokenCachePath);

            Options = new OutboundOAuthOptions
            {
                ClientId = "cli",
                ConsentPresenter = Presenter.PresentAsync,
                Scopes = ["read", "write"],
                TokenCachePath = TokenCachePath
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates an <see cref="HttpClient"/> against the secured host carrying a bearer token the
        /// authorization server minted, so the direct OData and MCP calls a tool test makes still succeed.
        /// </summary>
        /// <returns>
        /// The client; the caller disposes it.
        /// </returns>
        /// <remarks>
        /// The token is minted rather than taken from the cache: a test that asserts on hit counters must not
        /// have its own sanity calls change them, and the resource trusts anything the authorization server's
        /// key signed.
        /// <para>
        /// It is stamped by a <see cref="BearerStampingHandler"/> rather than set on
        /// <see cref="HttpClient.DefaultRequestHeaders"/>, because a linked test that assigns its own
        /// placeholder <c>Authorization</c> header would otherwise replace the only credential the secured host
        /// accepts and draw a <c>401</c> from the middleware instead of reaching the controller it is asserting
        /// on.
        /// </para>
        /// </remarks>
        public HttpClient CreateClient()
        {
            return new HttpClient(new BearerStampingHandler(AuthorizationServer.IssueAccessToken("read write"))
            {
                InnerHandler = ResourceServer.CreateHandler()
            })
            {
                BaseAddress = ResourceServer.BaseAddress
            };
        }

        /// <summary>
        /// Builds a Tools host against the secured service and turns its <c>$metadata</c> into a session whose
        /// catalog obeys the embedding host's own catalog options.
        /// </summary>
        /// <param name="catalogOptions">The catalog options the secured host was configured with.</param>
        /// <param name="cancellationToken">The token that cancels the host build, the sign-in, and the metadata fetch.</param>
        /// <returns>
        /// A task that completes once <see cref="Session"/> and <see cref="Capture"/> are ready.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalogOptions"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the host returned no <c>$metadata</c> document.</exception>
        /// <example>
        /// <code>
        /// await fixture.CreateSessionAsync(
        ///     TestServer.Services.GetRequiredService&lt;IOptions&lt;ODataMcpHostOptions&gt;&gt;().Value.Catalog,
        ///     CancellationToken.None);
        /// </code>
        /// </example>
        /// <remarks>
        /// The catalog is rebuilt here rather than reused from <see cref="ToolsMcpHost.Catalog"/> because the
        /// CLI has no way to know what a subclass configured — a linked test that sets
        /// <see cref="ODataMcpCatalogOptions.MaxNamedTools"/> on the embedding host expects the outbound
        /// catalog to honor it. The runtime underneath is still the host's own
        /// <see cref="RemoteODataExecutor"/> on the authenticating <c>"OData"</c> client, so every tool call
        /// goes out over the outbound path.
        /// <para>
        /// Calling this a second time disposes the previous host first, which is what makes a restart test a
        /// restart: the second host finds the first host's token in the same on-disk cache.
        /// </para>
        /// <para>
        /// The console logger the CLI installs is cleared: a linked suite runs the whole startup handshake once
        /// per test class, and hundreds of copies of it on stderr would bury the assertion that actually failed.
        /// Everything the suite asserts on is a hit counter or a captured request, not a log line.
        /// </para>
        /// </remarks>
        public async Task CreateSessionAsync(ODataMcpCatalogOptions catalogOptions, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(catalogOptions);

            Host?.Dispose();
            Host = null;
            _capture = null;
            _session = null;

            var host = await ToolsMcpHost.CreateAsync(
                ServiceRoot.AbsoluteUri,
                Options,
                includeStdioMcp: false,
                verbose: false,
                lifetime: null,
                cancellationToken,
                services =>
                {
                    services.AddLogging(logging => logging.ClearProviders());
                    services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName)
                        .ConfigurePrimaryHttpMessageHandler(() => ResourceServer.CreateHandler());
                    services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName)
                        .ConfigurePrimaryHttpMessageHandler(() => OAuthHandler ?? AuthorizationServer.Handler);
                }).ConfigureAwait(false);

            Host = host;

            if (string.IsNullOrWhiteSpace(host.Session.MetadataXml))
            {
                throw new InvalidOperationException("The Tools host returned no $metadata document to build an outbound catalog from.");
            }

            var model = host.Host.Services.GetRequiredService<CsdlParser>().ParseFromString(host.Session.MetadataXml);
            var catalog = new ODataMcpCatalog(model, CopyCatalogOptions(catalogOptions, RouteName(ServiceRoot)));

            _capture = new CapturingODataExecutor(new RemoteODataExecutor(host.Host.Services.GetRequiredService<IHttpClientFactory>()));
            _session = new ODataMcpSession(catalog, new ODataToolRuntime(catalog, _capture), host.Session.MetadataXml);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Host?.Dispose();
            Host = null;

            if (Directory.Exists(TokenCachePath))
            {
                try
                {
                    Directory.Delete(TokenCachePath, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // A cache file another handle still holds is a throwaway under the OS temp path either way.
                }
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Copies catalog options and stamps the route name the outbound catalog advertises its resources under.
        /// </summary>
        /// <param name="source">The embedding host's catalog options.</param>
        /// <param name="routeName">The route name to stamp.</param>
        /// <returns>
        /// A copy that shares no mutable state with <paramref name="source"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="routeName"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <remarks>
        /// Mirrors what the ASP.NET Core session factory does per route, so a linked test that asserts on an
        /// <c>odata://odata/…</c> resource URI sees the same value it sees in process.
        /// </remarks>
        internal static ODataMcpCatalogOptions CopyCatalogOptions(ODataMcpCatalogOptions source, string routeName)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentException.ThrowIfNullOrWhiteSpace(routeName);

            return new ODataMcpCatalogOptions
            {
                EnumJsonFormat = source.EnumJsonFormat,
                ExcludeEntitySets = [.. source.ExcludeEntitySets],
                IncludeCreate = source.IncludeCreate,
                IncludeDelete = source.IncludeDelete,
                IncludeEntitySets = [.. source.IncludeEntitySets],
                IncludeUpdate = source.IncludeUpdate,
                InstructionsPreface = source.InstructionsPreface,
                IsDynamicModel = source.IsDynamicModel,
                MaxCompletionValues = source.MaxCompletionValues,
                MaxExpandLength = source.MaxExpandLength,
                MaxFilterLength = source.MaxFilterLength,
                MaxNamedTools = source.MaxNamedTools,
                MaxRequestBodyBytes = source.MaxRequestBodyBytes,
                MaxResources = source.MaxResources,
                MaxResponseBytes = source.MaxResponseBytes,
                MaxSelectLength = source.MaxSelectLength,
                RouteName = routeName
            };
        }

        /// <summary>
        /// Derives the route name a service root's catalog advertises resources under.
        /// </summary>
        /// <param name="serviceRoot">The absolute OData service root.</param>
        /// <returns>
        /// The last non-empty path segment, or <c>"odata"</c> when the root has none.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRoot"/> is <see langword="null"/>.</exception>
        internal static string RouteName(Uri serviceRoot)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);

            var segments = serviceRoot.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            return segments.Length == 0 ? "odata" : segments[^1];
        }

        #endregion

    }

}
