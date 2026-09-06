// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;

namespace Microsoft.OData.Mcp.Tests.Core.Server
{

    /// <summary>
    /// Test HTTP message handler for mocking HTTP responses.
    /// </summary>
    internal class TestHttpMessageHandler : HttpMessageHandler
    {

        #region Properties

        public int RequestCount { get; private set; }

        public string Response { get; set; }

        public bool ShouldFail { get; set; }

        #endregion

        #region Constructors

        public TestHttpMessageHandler(string response)
        {
            Response = response;
        }

        #endregion

        #region Protected Methods

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            if (ShouldFail)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Internal Server Error")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Response)
            });
        }

        #endregion

    }

}
