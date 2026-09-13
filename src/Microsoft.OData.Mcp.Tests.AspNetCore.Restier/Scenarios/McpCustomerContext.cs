// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios
{

    /// <summary>
    /// In-memory DbContext for Restier customer tests.
    /// </summary>
    public sealed class McpCustomerContext : DbContext
    {

        #region Properties

        /// <summary>
        /// Gets the customers. Restier publishes this DbSet as the Customers entity set.
        /// </summary>
        public DbSet<McpCustomer> Customers { get; set; } = null!;

        /// <summary>
        /// Gets the orders. Restier publishes this DbSet as the Orders entity set.
        /// </summary>
        public DbSet<McpOrder> Orders { get; set; } = null!;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpCustomerContext"/> class.
        /// </summary>
        /// <param name="options">EF Core options.</param>
        public McpCustomerContext(DbContextOptions<McpCustomerContext> options)
            : base(options)
        {
        }

        #endregion

    }

}
