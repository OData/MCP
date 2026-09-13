// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Execution;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Executor that always throws a configured exception, so a caller can prove what the call-tool handler does
    /// with a tool invocation that fails rather than returning an error result.
    /// </summary>
    /// <remarks>
    /// The exception travels out of <see cref="ODataToolRuntime.InvokeAsync(string, System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{string, System.Text.Json.JsonElement}}, CancellationToken)"/>
    /// exactly as thrown, which is what a mid-session outbound sign-in failure looks like to Core.
    /// </remarks>
    internal sealed class ThrowingODataExecutor : IODataExecutor
    {

        #region Properties

        /// <summary>
        /// Gets or sets the exception every execution throws.
        /// </summary>
        /// <value>
        /// Defaults to an <see cref="InvalidOperationException"/> naming this executor.
        /// </value>
        public Exception Exception { get; set; } = new InvalidOperationException("The executor was configured to throw.");

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken)
        {
            throw Exception;
        }

        #endregion

    }

}
