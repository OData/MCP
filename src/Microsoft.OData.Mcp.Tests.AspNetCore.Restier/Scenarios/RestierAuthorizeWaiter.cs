// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using ModelContextProtocol.Protocol;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios
{

    /// <summary>
    /// Test waiter that completes when the elicitation test signals success.
    /// </summary>
    public sealed class RestierAuthorizeWaiter : IMcpAnnotatedActionWaiter
    {

        #region Properties

        /// <summary>
        /// Gets the completion source tests set after elicitation is accepted.
        /// </summary>
        public TaskCompletionSource<CallToolResult?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets the number of times <see cref="WaitAsync"/> was entered.
        /// </summary>
        public int WaitCalls { get; set; }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public Task<CallToolResult?> WaitAsync(McpAnnotatedActionContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            WaitCalls++;
            return Completion.Task.WaitAsync(cancellationToken);
        }

        #endregion

    }

}
