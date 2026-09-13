// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios
{

    /// <summary>
    /// In-memory DbContext for Restier product tests.
    /// </summary>
    public sealed class McpProductContext : DbContext
    {

        #region Properties

        /// <summary>
        /// Gets the products.
        /// </summary>
        public DbSet<McpProduct> Products { get; set; } = null!;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpProductContext"/> class.
        /// </summary>
        /// <param name="options">EF Core options.</param>
        public McpProductContext(DbContextOptions<McpProductContext> options)
            : base(options)
        {
        }

        #endregion

    }

}
