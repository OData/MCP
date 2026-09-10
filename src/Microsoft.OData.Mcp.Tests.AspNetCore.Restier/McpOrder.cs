// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Restier order entity. Exposed automatically from <see cref="McpCustomerContext.Orders"/>.
    /// </summary>
    public sealed class McpOrder
    {

        #region Properties

        /// <summary>
        /// Gets or sets the amount.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Gets or sets the related customer.
        /// </summary>
        public McpCustomer? Customer { get; set; }

        /// <summary>
        /// Gets or sets the customer key.
        /// </summary>
        public int CustomerId { get; set; }

        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        public int Id { get; set; }

        #endregion

    }

}
