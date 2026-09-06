// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.Core.Server
{

    /// <summary>
    /// Test HTTP client factory for creating test HTTP clients.
    /// </summary>
    internal class TestHttpClientFactory : IHttpClientFactory
    {

        #region Fields

        private readonly HttpMessageHandler _handler;

        #endregion

        #region Constructors

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        #endregion

        #region Public Methods

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler);
        }

        #endregion

    }

}
