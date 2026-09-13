// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Catalog;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.Extensions.DependencyInjection
{

    /// <summary>
    /// Registers catalog-backed MCP 2 handlers on an <see cref="IMcpServerBuilder"/> without assembly scanning.
    /// </summary>
    public static class ODataMcp_Core_McpServerBuilderExtensions
    {

        #region Public Methods

        /// <summary>
        /// Adds list/call/resource/complete handlers that resolve an <see cref="ODataMcpSession"/> per request.
        /// </summary>
        /// <param name="builder">The MCP server builder.</param>
        /// <param name="resolveSession">Resolves the session for the current request.</param>
        /// <param name="listExtraTools">Optional extra tools (for example, <c>shutdown_server</c>).</param>
        /// <param name="tryHandleExtra">Optional extra call handler that runs before catalog tools.</param>
        /// <param name="tryHandleCallException">Optional recovery handler that sees an exception a catalog tool threw and may answer the call itself.</param>
        /// <returns>
        /// The builder.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="resolveSession"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// services.AddMcpServer()
        ///     .WithODataCatalogHandlers(provider => provider.GetRequiredService&lt;ODataMcpSession&gt;());
        /// </code>
        /// </example>
        /// <remarks>
        /// <paramref name="tryHandleCallException"/> exists so a host can recover from a failure Core has no
        /// vocabulary for — an outbound sign-in that needs a human, for instance — without Core learning
        /// anything about authentication: it is handed the exception and either answers the call or returns
        /// <see langword="null"/>, in which case the original exception propagates unchanged.
        /// </remarks>
        public static IMcpServerBuilder WithODataCatalogHandlers(
            this IMcpServerBuilder builder,
            Func<IServiceProvider, ODataMcpSession> resolveSession,
            Func<IServiceProvider, IReadOnlyList<Tool>>? listExtraTools = null,
            Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult?>>? tryHandleExtra = null,
            Func<RequestContext<CallToolRequestParams>, Exception, CancellationToken, ValueTask<CallToolResult?>>? tryHandleCallException = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(resolveSession);

            return builder
                .WithListToolsHandler((request, cancellationToken) => ODataMcpHandlers.ListToolsAsync(ODataMcpHandlers.RequireServices(request.Services), resolveSession, listExtraTools, cancellationToken))
                .WithCallToolHandler((request, cancellationToken) => ODataMcpHandlers.CallToolAsync(request, resolveSession, tryHandleExtra, tryHandleCallException, cancellationToken))
                .WithListResourcesHandler((request, cancellationToken) => ODataMcpHandlers.ListResourcesAsync(ODataMcpHandlers.RequireServices(request.Services), resolveSession, cancellationToken))
                .WithReadResourceHandler((request, cancellationToken) => ODataMcpHandlers.ReadResourceAsync(request, resolveSession, cancellationToken))
                .WithListResourceTemplatesHandler((request, cancellationToken) => ODataMcpHandlers.ListTemplatesAsync(ODataMcpHandlers.RequireServices(request.Services), resolveSession, cancellationToken))
                .WithCompleteHandler((request, cancellationToken) => ODataMcpHandlers.CompleteAsync(request, resolveSession, cancellationToken));
        }

        #endregion

    }

}
