// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Execution;

namespace Microsoft.OData.Mcp.Tests.Shared
{

    /// <summary>
    /// Forwards every OData request to a real executor while recording the wire path and query.
    /// </summary>
    public sealed class CapturingODataExecutor : IODataExecutor
    {

        #region Fields

        internal readonly IODataExecutor _inner;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the last forwarded request.
        /// </summary>
        public ODataExecuteRequest? Last { get; internal set; }

        /// <summary>
        /// Gets every forwarded request in order.
        /// </summary>
        public List<ODataExecuteRequest> Requests { get; } = [];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CapturingODataExecutor"/> class.
        /// </summary>
        /// <param name="inner">The real executor.</param>
        public CapturingODataExecutor(IODataExecutor inner)
        {
            ArgumentNullException.ThrowIfNull(inner);

            _inner = inner;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Last = request;
            Requests.Add(request);

            return _inner.ExecuteAsync(request, cancellationToken);
        }

        #endregion

    }

}
