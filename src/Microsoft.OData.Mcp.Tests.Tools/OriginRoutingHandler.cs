// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// Dispatches requests to one of two in-process test servers by path prefix, so a single named
    /// <see cref="HttpClient"/> can reach both a protected resource and its authorization server even though the
    /// two fixtures share the <c>http://localhost</c> origin.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName)
    ///     .ConfigurePrimaryHttpMessageHandler(() =&gt; new OriginRoutingHandler("/odata", resource.Server.CreateHandler(), authorizationServer.Handler));
    /// </code>
    /// </example>
    /// <remarks>
    /// The <c>add</c> wizard's probe deliberately runs on the handler-free <c>"OAuth"</c> client so it can see
    /// the resource's raw <c>401</c>; that same client then carries discovery's well-known requests. In
    /// production those are two different hosts, so one handler is enough. In these tests they are two
    /// <c>TestServer</c> instances, and this is what stands in for DNS.
    /// </remarks>
    internal sealed class OriginRoutingHandler : HttpMessageHandler
    {

        #region Fields

        /// <summary>
        /// The invoker every request outside <see cref="_resourcePrefix"/> is dispatched to.
        /// </summary>
        internal readonly HttpMessageInvoker _authorizationServer;

        /// <summary>
        /// The invoker every request beneath <see cref="_resourcePrefix"/> is dispatched to.
        /// </summary>
        internal readonly HttpMessageInvoker _resource;

        /// <summary>
        /// The path prefix that identifies the protected resource.
        /// </summary>
        internal readonly string _resourcePrefix;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OriginRoutingHandler"/> class.
        /// </summary>
        /// <param name="resourcePrefix">The path prefix that identifies the protected resource, for example <c>/odata</c>.</param>
        /// <param name="resource">The handler serving the protected resource.</param>
        /// <param name="authorizationServer">The handler serving everything else, including the well-known documents.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="resource"/> or <paramref name="authorizationServer"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="resourcePrefix"/> is <see langword="null"/>, empty, or whitespace.</exception>
        public OriginRoutingHandler(string resourcePrefix, HttpMessageHandler resource, HttpMessageHandler authorizationServer)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourcePrefix);
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(authorizationServer);

            _authorizationServer = new HttpMessageInvoker(authorizationServer, disposeHandler: false);
            _resource = new HttpMessageInvoker(resource, disposeHandler: false);
            _resourcePrefix = resourcePrefix;
        }

        #endregion

        #region Protected Methods

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var invoker = path.StartsWith(_resourcePrefix, StringComparison.OrdinalIgnoreCase) ? _resource : _authorizationServer;

            return invoker.SendAsync(request, cancellationToken);
        }

        #endregion

    }

}
