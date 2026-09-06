// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;

namespace Microsoft.OData.Mcp.Core.Execution
{

    /// <summary>
    /// An OData HTTP request built from MCP tool arguments.
    /// </summary>
    public sealed class ODataExecuteRequest
    {

        #region Properties

        /// <summary>
        /// Gets or sets the JSON body for create/update.
        /// </summary>
        public string? JsonBody { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method.
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.Get;

        /// <summary>
        /// Gets or sets query options without the '$' prefix.
        /// </summary>
        public Dictionary<string, string> QueryOptions { get; set; } = [];

        /// <summary>
        /// Gets or sets the path relative to the service root (for example, "Products").
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        #endregion

    }

}
