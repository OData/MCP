// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Tests.Shared.Entities
{

    /// <summary>
    /// Simple Customer entity for testing.
    /// </summary>
    public class Customer
    {

        #region Properties

        /// <summary>
        /// Gets or sets the city. Optional: the test API only requires <see cref="CompanyName"/>, and the EDM says so.
        /// </summary>
        public string? City { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the contact name. Optional on create.
        /// </summary>
        public string? ContactName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the country. Optional on create.
        /// </summary>
        public string? Country { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the store-generated key. The test models annotate it <c>Core.Computed</c>.
        /// </summary>
        public int CustomerId { get; set; }

        /// <summary>
        /// Gets or sets the email. Optional on create.
        /// </summary>
        public string? Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a CLR-only secret that OData <c>Ignore()</c> must omit from the EDM.
        /// </summary>
        public string InternalSecret { get; set; } = string.Empty;

        public List<Order> Orders { get; set; } = [];

        /// <summary>
        /// Gets or sets the phone. Optional on create.
        /// </summary>
        public string? Phone { get; set; } = string.Empty;

        #endregion

    }

    /// <summary>
    /// Order entity for testing.
    /// </summary>
    public class Order
    {

        #region Properties

        public Customer? Customer { get; set; }

        public int CustomerId { get; set; }

        public decimal OrderAmount { get; set; }

        public DateTime OrderDate { get; set; }

        public int OrderId { get; set; }

        public List<OrderItem> OrderItems { get; set; } = [];

        public string Status { get; set; } = string.Empty;

        #endregion

    }

    /// <summary>
    /// OrderItem entity for testing.
    /// </summary>
    public class OrderItem
    {

        #region Properties

        public Order? Order { get; set; }

        public int OrderId { get; set; }

        public int OrderItemId { get; set; }

        public Product? Product { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        #endregion

    }

    /// <summary>
    /// Product entity for testing.
    /// </summary>
    public class Product
    {

        #region Properties

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool InStock { get; set; }

        public List<OrderItem> OrderItems { get; set; } = [];

        public decimal Price { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        #endregion

    }
}