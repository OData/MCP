// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Execution;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Executor that must not be called for model-only tools.
    /// </summary>
    internal sealed class UnusedODataExecutor : IODataExecutor
    {

        #region Public Methods

        /// <inheritdoc />
        public Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Model-only tools must not call the executor.");
        }

        #endregion

    }

}
