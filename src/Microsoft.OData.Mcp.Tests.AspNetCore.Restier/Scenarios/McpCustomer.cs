// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios
{

    /// <summary>
    /// Seeded Restier customer used by in-process OData and MCP tests.
    /// Restier exposes this type because <see cref="McpCustomerContext"/> has a <c>Customers</c> DbSet —
    /// there is no OData controller.
    /// </summary>
    public sealed class McpCustomer
    {

        #region Properties

        /// <summary>
        /// Gets or sets the company name.
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets related orders. Restier materializes this as a navigation property.
        /// </summary>
        public List<McpOrder> Orders { get; set; } = [];

        #endregion

    }

}
