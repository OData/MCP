// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// Endpoint data source with a fixed list of endpoints.
    /// </summary>
    public sealed class StaticEndpointDataSource : EndpointDataSource
    {

        #region Properties

        /// <inheritdoc />
        public override IReadOnlyList<Endpoint> Endpoints { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticEndpointDataSource"/> class.
        /// </summary>
        /// <param name="endpoints">The endpoints to expose.</param>
        public StaticEndpointDataSource(params Endpoint[] endpoints)
        {
            Endpoints = endpoints;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public override IChangeToken GetChangeToken()
        {
            return new CancellationChangeToken(CancellationToken.None);
        }

        #endregion

    }

}
