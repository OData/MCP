// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Execution;

namespace Microsoft.OData.Mcp.Benchmarks.Infrastructure
{

    /// <summary>
    /// Executor for model-only tools. Throws if anything reaches HTTP, so a benchmark that accidentally
    /// measures a network call fails instead of reporting nonsense.
    /// </summary>
    public sealed class NoopODataExecutor : IODataExecutor
    {

        #region Public Methods

        /// <inheritdoc />
        public Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Model-only benchmarks must not call the executor.");
        }

        #endregion

    }

}
