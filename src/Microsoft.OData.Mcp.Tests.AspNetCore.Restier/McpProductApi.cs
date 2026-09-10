// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.Restier.EntityFrameworkCore;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Restier API over <see cref="McpProductContext"/>.
    /// </summary>
    public sealed class McpProductApi : EntityFrameworkApi<McpProductContext>
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpProductApi"/> class.
        /// </summary>
        /// <param name="serviceProvider">The Restier service provider.</param>
        public McpProductApi(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        #endregion

    }

}
