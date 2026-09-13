// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Execution;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Records the last OData request and returns a configured result.
    /// </summary>
    internal sealed class RecordingODataExecutor : IODataExecutor
    {

        #region Properties

        /// <summary>
        /// Gets the last request.
        /// </summary>
        public ODataExecuteRequest? Last { get; internal set; }

        /// <summary>
        /// Gets or sets the result to return.
        /// </summary>
        public ODataExecuteResult Result { get; set; } = new()
        {
            Body = """{"value":[]}""",
            IsSuccess = true,
            MediaType = "application/json",
            StatusCode = 200
        };

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken)
        {
            Last = request;

            return Task.FromResult(Result);
        }

        #endregion

    }

}
