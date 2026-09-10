// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Core.Execution
{

    /// <summary>
    /// Executes one OData HTTP request. Query option keys must not include '$';
    /// implementations add the prefix on the wire.
    /// </summary>
    public interface IODataExecutor
    {

        /// <summary>
        /// Executes the request.
        /// </summary>
        /// <param name="request">The OData request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The HTTP result.
        /// </returns>
        Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken);

    }

}
