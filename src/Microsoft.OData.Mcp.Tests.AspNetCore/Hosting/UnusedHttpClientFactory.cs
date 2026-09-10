// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// Factory that is never used by URI-only in-process executor tests.
    /// </summary>
    internal sealed class UnusedHttpClientFactory : IHttpClientFactory
    {

        #region Public Methods

        /// <inheritdoc />
        public HttpClient CreateClient(string name)
        {
            throw new InvalidOperationException("HTTP is not required for URI tests.");
        }

        #endregion

    }

}
