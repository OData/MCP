// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net.Http;
using System.Text;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// JSON bodies for Restier MCP HTTP tests.
    /// </summary>
    public static class RestierJsonContent
    {

        #region Public Methods

        /// <summary>
        /// Builds JSON with the MCP content type.
        /// </summary>
        /// <param name="json">The JSON payload.</param>
        /// <returns>
        /// The content.
        /// </returns>
        public static StringContent Json(string json)
        {
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        #endregion

    }

}
