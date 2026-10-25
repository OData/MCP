// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Context passed to <see cref="IMcpAnnotatedActionWaiter.WaitAsync"/> after elicitation is accepted.
    /// </summary>
    public sealed class McpAnnotatedActionContext
    {

        #region Properties

        /// <summary>
        /// Gets or sets the EDM action short name.
        /// </summary>
        public string ActionName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the bound entity key as supplied to the tool, or <c>null</c> when unbound.
        /// </summary>
        public string? BindingKey { get; set; }

        /// <summary>
        /// Gets or sets the one-shot Started JSON from the inner OData POST.
        /// </summary>
        public string StartedJson { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MCP task id, or empty when the related-task metadata is absent.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        #endregion

    }

}
