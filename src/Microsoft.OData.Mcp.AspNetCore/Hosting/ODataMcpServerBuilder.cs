// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.McpEssentials.Core.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Configures OData MCP Tasks on <c>AddODataMcp</c>. Hosts call
    /// <see cref="WithTasks()"/> and <see cref="WaitForAnnotatedAction{TWaiter}"/> here, not on
    /// <c>UseODataMcp</c> and not via SDK <c>WithTasks</c> from app code.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddODataMcp(options => { }, mcp =>
    /// {
    ///     mcp.WithTasks();
    ///     mcp.WaitForAnnotatedAction&lt;HostAuthorizeWaiter&gt;();
    /// });
    /// </code>
    /// </example>
    public sealed class ODataMcpServerBuilder
    {

        #region Fields

        /// <summary>
        /// Owner used when the request has no authenticated user. Task id is then the secret
        /// (SEP-2663 unbound case).
        /// </summary>
        internal const string UnauthenticatedTaskOwner = "odata";

        internal readonly IServiceCollection _services;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the SDK builder already used for <c>WithHttpTransport</c> and catalog handlers.
        /// Hosts may attach logging filters. They must not call SDK <c>WithTasks</c> on it.
        /// </summary>
        public IMcpServerBuilder Server { get; }

        /// <summary>
        /// Gets the after-invoke hook installed on catalog <c>tools/call</c> for URL
        /// elicitation (MRTR or task-scoped <c>ElicitAsync</c>).
        /// </summary>
        internal Func<RequestContext<CallToolRequestParams>, CallToolResult, CancellationToken, ValueTask<CallToolResult>> AfterInvoke { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpServerBuilder"/> class.
        /// </summary>
        /// <param name="server">The SDK MCP server builder.</param>
        /// <param name="services">The application service collection.</param>
        internal ODataMcpServerBuilder(IMcpServerBuilder server, IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(services);

            Server = server;
            _services = services;
            AfterInvoke = ODataMcpAnnotatedActionTasks.AfterInvokeAsync;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Registers the host waiter used after URL elicitation is accepted.
        /// </summary>
        /// <typeparam name="TWaiter">The waiter type.</typeparam>
        /// <returns>
        /// This builder.
        /// </returns>
        /// <remarks>
        /// Required for Succeeded after elicitation. Without it, the call completes with the
        /// Started payload after the URL is shown.
        /// </remarks>
        public ODataMcpServerBuilder WaitForAnnotatedAction<TWaiter>()
            where TWaiter : class, IMcpAnnotatedActionWaiter
        {
            _services.TryAddSingleton<TWaiter>();
            _services.TryAddSingleton<IMcpAnnotatedActionWaiter>(services => services.GetRequiredService<TWaiter>());

            return this;
        }

        /// <summary>
        /// Enables MCP Tasks for annotated actions only, using <see cref="InMemoryMcpTaskStorePlus"/>.
        /// Authenticated callers are isolated by user id. Unauthenticated callers share owner
        /// <see cref="UnauthenticatedTaskOwner"/> and the task id is the secret.
        /// </summary>
        /// <returns>
        /// This builder.
        /// </returns>
        public ODataMcpServerBuilder WithTasks()
        {
            return WithTasks(tasks => tasks.UseInMemory(sp =>
            {
                var user = sp.GetService<IHttpContextAccessor>()?.HttpContext?.User;
                var id = McpTaskStoreBase.GetUserId(user);
                return string.IsNullOrWhiteSpace(id) ? UnauthenticatedTaskOwner : id;
            }));
        }

        /// <summary>
        /// Enables MCP Tasks and selects a plus store provider.
        /// </summary>
        /// <param name="configure">Selects <c>UseInMemory</c> or a provider <c>Use*</c> method.</param>
        /// <returns>
        /// This builder.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="configure"/> did not call a <c>Use*</c> method.</exception>
        /// <example>
        /// <code>
        /// mcp.WithTasks(tasks =&gt; tasks.UseInMemory("local-user"));
        /// mcp.WithTasks(tasks =&gt; tasks.UseEntityFramework&lt;AppDbContext&gt;(
        ///     sp =&gt; sp.GetRequiredService&lt;IHttpContextAccessor&gt;().HttpContext?.User));
        /// </code>
        /// </example>
        public ODataMcpServerBuilder WithTasks(Action<McpTaskStoreBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var tasks = new McpTaskStoreBuilder(_services);
            configure(tasks);
            if (tasks.Store is null)
            {
                throw new InvalidOperationException($"Call {nameof(McpTaskStoreBuilder.UseInMemory)} or a provider Use* method.");
            }

            return EnableTasks(tasks.Store, registerStore: false);
        }

        /// <summary>
        /// Enables MCP Tasks for annotated actions only. CRUD, functions, and unannotated
        /// actions stay one-shot.
        /// </summary>
        /// <param name="store">The protocol task store. Register a singleton; stateless HTTP
        /// creates a fresh server per POST.</param>
        /// <returns>
        /// This builder.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Wires <c>tasks/get</c>, <c>tasks/update</c>, and <c>tasks/cancel</c> from
        /// <c>ModelContextProtocol.Extensions.Tasks</c>. Does not wrap every catalog tool:
        /// the execution-mode selector returns Optional only for annotated actions whose
        /// request advertised the Tasks extension. Stamps <c>DefaultTimeToLive</c> on
        /// <see cref="IMcpTaskStorePlus"/> so Claude Code receives <c>ttlMs</c>.
        /// </remarks>
        public ODataMcpServerBuilder WithTasks(IMcpTaskStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            return EnableTasks(store, registerStore: true);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Stamps <c>DefaultTimeToLive</c> so serialized <c>CreateTaskResult</c> includes <c>ttlMs</c>.
        /// </summary>
        /// <param name="store">The task store.</param>
        /// <param name="ttl">The default time-to-live, typically <c>MaxOperationWait</c>.</param>
        /// <remarks>
        /// Claude Code requires <c>ttlMs</c> on a task handle. SDK serialization omits the property
        /// when <c>TimeToLive</c> is null. Plus stores and the SDK in-memory store are stamped
        /// through typed properties.
        /// </remarks>
        internal static void ApplyDefaultTimeToLive(IMcpTaskStore store, TimeSpan ttl)
        {
            ArgumentNullException.ThrowIfNull(store);

            if (store is IMcpTaskStorePlus plus)
            {
                plus.DefaultTimeToLive = ttl;
                return;
            }

            if (store is InMemoryMcpTaskStore memory)
            {
                memory.DefaultTimeToLive = ttl;
            }
        }

        /// <summary>
        /// Wires SDK <c>WithTasks</c>, the annotated-action selector, and TTL from
        /// <c>MaxOperationWait</c>.
        /// </summary>
        /// <param name="store">The store the SDK filter will capture.</param>
        /// <param name="registerStore">
        /// When <see langword="true"/>, registers <paramref name="store"/> in DI. Provider
        /// <c>Use*</c> methods already registered the store.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
        internal ODataMcpServerBuilder EnableTasks(IMcpTaskStore store, bool registerStore)
        {
            ArgumentNullException.ThrowIfNull(store);

            if (registerStore)
            {
                _services.AddSingleton(store);
            }

            _services.AddOptions<ODataMcpHostOptions>().PostConfigure(options =>
            {
                ApplyDefaultTimeToLive(store, options.Catalog.MaxOperationWait);
            });

            Server.WithTasks(store, options =>
            {
                options.ExecutionModeSelector = ODataMcpAnnotatedActionTasks.SelectExecutionMode;
            });

            return this;
        }

        #endregion

    }

}
