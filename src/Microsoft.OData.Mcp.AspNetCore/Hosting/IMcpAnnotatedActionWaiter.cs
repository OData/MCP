// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Host completion source for an annotated action after URL elicitation is accepted.
    /// </summary>
    /// <remarks>
    /// Register with <c>WaitForAnnotatedAction&lt;TWaiter&gt;</c>. Without a waiter, the task
    /// completes with the Started payload after elicitation (the URL was shown; the grant still
    /// finishes only in the browser). Decline, timeout, and <c>tasks/cancel</c> do not call
    /// <see cref="WaitAsync"/>.
    /// </remarks>
    public interface IMcpAnnotatedActionWaiter
    {

        /// <summary>
        /// Waits for the host callback that finishes the annotated action.
        /// </summary>
        /// <param name="context">Action name, binding key, one-shot result JSON, and task id.</param>
        /// <param name="cancellationToken">Timeout, <c>tasks/cancel</c>, and process shutdown.</param>
        /// <returns>
        /// The terminal tool result, or <see langword="null"/> to keep the Started payload.
        /// </returns>
        Task<CallToolResult?> WaitAsync(McpAnnotatedActionContext context, CancellationToken cancellationToken);

    }

}
