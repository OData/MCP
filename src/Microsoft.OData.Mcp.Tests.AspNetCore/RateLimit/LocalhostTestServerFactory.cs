// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit
{

    /// <summary>
    /// Named HttpClient factory for <see cref="Microsoft.OData.Mcp.Core.Execution.RemoteODataExecutor"/> against TestServer.
    /// </summary>
    public sealed class LocalhostTestServerFactory : IHttpClientFactory
    {

        #region Fields

        internal readonly Uri _baseAddress;

        internal readonly HttpMessageHandler _handler;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalhostTestServerFactory"/> class.
        /// </summary>
        /// <param name="handler">The TestServer handler.</param>
        /// <param name="baseAddress">The OData service root, including the route prefix.</param>
        public LocalhostTestServerFactory(HttpMessageHandler handler, Uri baseAddress)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(baseAddress);

            _handler = handler;
            _baseAddress = baseAddress;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = _baseAddress
            };
        }

        #endregion

    }

}
