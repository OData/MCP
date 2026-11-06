// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO.Pipelines;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Live <see cref="McpServer"/> whose outgoing requests are answered by an interceptor.
    /// </summary>
    /// <remarks>
    /// <see cref="McpServer.WithOutgoingRequestInterceptor"/> skips client capability checks, so
    /// tests can exercise <c>ElicitAsync</c> without a JSON-RPC client handshake.
    /// </remarks>
    internal sealed class InterceptedFormSession : IAsyncDisposable
    {

        #region Fields

        private readonly McpServer _root;

        private readonly StreamServerTransport _transport;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the interceptor facade used as <c>RequestContext.Server</c>.
        /// </summary>
        public McpServer Server { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="InterceptedFormSession"/> class.
        /// </summary>
        /// <param name="transport">The in-process pipe transport.</param>
        /// <param name="root">The root server that owns the transport.</param>
        /// <param name="server">The interceptor facade.</param>
        internal InterceptedFormSession(StreamServerTransport transport, McpServer root, McpServer server)
        {
            _transport = transport;
            _root = root;
            Server = server;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a stream server whose outgoing JSON-RPC is redirected to <paramref name="interceptor"/>.
        /// </summary>
        /// <param name="interceptor">
        /// Receives the method, serialized params, and cancellation token. Return serialized
        /// result JSON, or <see langword="null"/> to cancel.
        /// </param>
        /// <param name="clientCapabilities">
        /// Optional known client capabilities, used when the request has no form <c>_meta</c>.
        /// </param>
        /// <returns>The disposable session.</returns>
        public static InterceptedFormSession Create(
            Func<string, JsonNode?, CancellationToken, ValueTask<JsonNode?>> interceptor,
            ClientCapabilities? clientCapabilities = null)
        {
            ArgumentNullException.ThrowIfNull(interceptor);

            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var transport = new StreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream(),
                "odata-mcp-form-elicit-test",
                NullLoggerFactory.Instance);
            var root = McpServer.Create(
                transport,
                new McpServerOptions
                {
                    ServerInfo = new Implementation
                    {
                        Name = "test",
                        Version = "1"
                    },
                    KnownClientCapabilities = clientCapabilities
                },
                NullLoggerFactory.Instance);
#pragma warning disable MCPEXP002
            var facade = root.WithOutgoingRequestInterceptor(interceptor);
#pragma warning restore MCPEXP002
            return new InterceptedFormSession(transport, root, facade);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await Server.DisposeAsync().ConfigureAwait(false);
            if (!ReferenceEquals(Server, _root))
            {
                await _root.DisposeAsync().ConfigureAwait(false);
            }

            await _transport.DisposeAsync().ConfigureAwait(false);
        }

        #endregion

    }

}
