// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
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
    /// <see cref="WithTasks"/> and <see cref="WaitForAnnotatedAction{TWaiter}"/> here, not on
    /// <c>UseODataMcp</c> and not via SDK <c>WithTasks</c> from app code.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddODataMcp(options => { }, mcp =>
    /// {
    ///     mcp.WithTasks(new InMemoryMcpTaskStore());
    ///     mcp.WaitForAnnotatedAction&lt;HostAuthorizeWaiter&gt;();
    /// });
    /// </code>
    /// </example>
    public sealed class ODataMcpServerBuilder
    {

        #region Fields

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
        /// the execution-mode selector returns Optional only for annotated actions.
        /// </remarks>
        public ODataMcpServerBuilder WithTasks(IMcpTaskStore store)
        {
            ArgumentNullException.ThrowIfNull(store);

            _services.AddSingleton(store);
            _services.AddOptions<ODataMcpHostOptions>().PostConfigure(options =>
            {
                if (store is InMemoryMcpTaskStore memory)
                {
                    memory.DefaultTimeToLive = options.Catalog.MaxOperationWait;
                }
            });

            Server.WithTasks(store, options =>
            {
                options.ExecutionModeSelector = context =>
                    ODataMcpAnnotatedActionTasks.IsTaskedCall(context)
                        ? McpTaskExecutionMode.Optional
                        : McpTaskExecutionMode.Synchronous;
            });

            return this;
        }

        #endregion

    }

}
