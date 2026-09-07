// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Tests.Shared
{

    /// <summary>
    /// Records request URIs while forwarding to the inner handler. Never stubs a response.
    /// </summary>
    public sealed class CapturingHttpHandler : DelegatingHandler
    {

        #region Properties

        /// <summary>
        /// Gets the last request URI.
        /// </summary>
        public Uri? LastUri { get; internal set; }

        /// <summary>
        /// Gets every request URI in order.
        /// </summary>
        public List<Uri> Uris { get; } = [];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CapturingHttpHandler"/> class.
        /// </summary>
        /// <param name="inner">The inner handler that performs the HTTP call.</param>
        public CapturingHttpHandler(HttpMessageHandler inner)
            : base(inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CapturingHttpHandler"/> class with sockets.
        /// </summary>
        public CapturingHttpHandler()
            : base(new SocketsHttpHandler())
        {
        }

        #endregion

        #region Protected Methods

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            LastUri = request.RequestUri;
            if (request.RequestUri is not null)
            {
                Uris.Add(request.RequestUri);
            }

            return base.SendAsync(request, cancellationToken);
        }

        #endregion

    }

}
