// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.AspNetCore.Constants;

namespace Microsoft.OData.Mcp.Tests.Authentication.Hosting
{

    /// <summary>
    /// Splits the CLI's <c>"OAuth"</c> discovery traffic between the two in-process servers that share the
    /// <c>http://localhost</c> origin: RFC 9728 protected resource metadata is answered by the OData host,
    /// everything else by the authorization server.
    /// </summary>
    /// <example>
    /// <code>
    /// fixture.OAuthHandler = new WellKnownRoutingHandler(TestServer.CreateHandler(), AuthorizationServer.Handler);
    /// </code>
    /// </example>
    /// <remarks>
    /// In a real deployment the two live on different hosts and an <see cref="HttpClient"/> routes between them
    /// by DNS. In process they do not, so the split has to be made on the one thing that distinguishes them:
    /// the well-known path. That is not a convenience — it is the whole assertion. A protected resource
    /// metadata document that came back through this handler provably came from
    /// <c>AddProtectedResourceMetadata</c> and not from the authorization server fixture, which is why the
    /// fixture's own document is turned off in the tests that use this.
    /// </remarks>
    public sealed class WellKnownRoutingHandler : HttpMessageHandler
    {

        #region Fields

        /// <summary>
        /// The authorization server every request that is not protected resource metadata is sent to.
        /// </summary>
        internal readonly HttpMessageInvoker _authorizationServer;

        /// <summary>
        /// The OData resource host protected resource metadata requests are sent to.
        /// </summary>
        internal readonly HttpMessageInvoker _resource;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="WellKnownRoutingHandler"/> class.
        /// </summary>
        /// <param name="resource">The OData resource host protected resource metadata requests are sent to.</param>
        /// <param name="authorizationServer">The authorization server every other request is sent to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="resource"/> or <paramref name="authorizationServer"/> is <see langword="null"/>.</exception>
        public WellKnownRoutingHandler(HttpMessageHandler resource, HttpMessageHandler authorizationServer)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(authorizationServer);

            _authorizationServer = new HttpMessageInvoker(authorizationServer, disposeHandler: false);
            _resource = new HttpMessageInvoker(resource, disposeHandler: false);
        }

        #endregion

        #region Protected Methods

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var invoker = path.StartsWith(ProtectedResourceConstants.WellKnownPath, StringComparison.OrdinalIgnoreCase)
                ? _resource
                : _authorizationServer;

            return invoker.SendAsync(request, cancellationToken);
        }

        #endregion

    }

}
