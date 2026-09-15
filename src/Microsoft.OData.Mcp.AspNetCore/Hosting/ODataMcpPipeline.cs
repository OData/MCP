// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Host-lifetime state for in-process OData execution. <c>AddODataMcp</c> registers this disabled;
    /// <c>UseODataMcp</c> is the on switch.
    /// </summary>
    public sealed class ODataMcpPipeline
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether <c>UseODataMcp</c> has turned the host on.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the captured application <see cref="RequestDelegate"/>, set by
        /// <see cref="ODataMcpPipelineStartupFilter"/> at host start. Capture always runs;
        /// execution still requires <see cref="IsEnabled"/>.
        /// </summary>
        public RequestDelegate? Pipeline { get; set; }

        #endregion

    }

}
