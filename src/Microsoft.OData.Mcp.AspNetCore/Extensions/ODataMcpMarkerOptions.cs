// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.Extensions.DependencyInjection
{

    /// <summary>
    /// Marker options to indicate OData MCP is enabled.
    /// </summary>
    internal class ODataMcpMarkerOptions
    {

        /// <summary>
        /// Gets or sets a value indicating whether OData MCP is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

    }

}
