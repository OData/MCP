// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// Minimal <see cref="IEndpointRouteBuilder"/> that exposes a caller-supplied data-source list.
    /// </summary>
    public sealed class RecordingEndpointRouteBuilder : IEndpointRouteBuilder
    {

        #region Properties

        /// <inheritdoc />
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

        /// <inheritdoc />
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public IApplicationBuilder CreateApplicationBuilder()
        {
            throw new NotSupportedException();
        }

        #endregion

    }

}
