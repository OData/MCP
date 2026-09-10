// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net.Http;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Execution
{

    /// <summary>
    /// HttpClient factory that uses the TestServer handler without a preset base address.
    /// </summary>
    internal sealed class TestServerHandlerFactory : IHttpClientFactory
    {

        #region Fields

        internal readonly HttpMessageHandler _handler;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TestServerHandlerFactory"/> class.
        /// </summary>
        /// <param name="handler">The TestServer handler.</param>
        public TestServerHandlerFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false);
        }

        #endregion

    }

}
