// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Execution;

namespace Microsoft.OData.Mcp.Benchmarks.Infrastructure
{

    /// <summary>
    /// Executor that answers every request with a canned success, so write-path benchmarks measure the
    /// runtime's validation and formatting without a network.
    /// </summary>
    public sealed class CannedODataExecutor : IODataExecutor
    {

        #region Public Methods

        /// <inheritdoc />
        public Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var created = request.Method == HttpMethod.Post;

            return Task.FromResult(new ODataExecuteResult
            {
                Body = created ? request.JsonBody ?? "{}" : string.Empty,
                IsSuccess = true,
                MediaType = created ? "application/json" : null,
                StatusCode = created ? 201 : 204
            });
        }

        #endregion

    }

}
