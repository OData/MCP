// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net.Http;
using System.Text;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Security
{

    /// <summary>
    /// JSON bodies for MCP HTTP tests.
    /// </summary>
    internal static class McpJsonContent
    {

        #region Public Methods

        /// <summary>
        /// Builds an empty JSON object with the MCP content type.
        /// </summary>
        /// <returns>
        /// The content.
        /// </returns>
        public static StringContent EmptyObject()
        {
            return new StringContent("{}", Encoding.UTF8, "application/json");
        }

        #endregion

    }

}
