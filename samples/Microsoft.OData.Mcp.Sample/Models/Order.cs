// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.OData.Mcp.Sample.Models
{
    /// <summary>
    /// Represents an order in the sample OData service.
    /// </summary>
    public class Order
    {
        /// <summary>
        /// Gets or sets the order ID.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the order number.
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string OrderNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the order date.
        /// </summary>
        public DateTimeOffset OrderDate { get; set; }

        /// <summary>
        /// Gets or sets the ship date.
        /// </summary>
        public DateTimeOffset? ShipDate { get; set; }

        /// <summary>
        /// Gets or sets the order status.
        /// </summary>
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Gets or sets the total amount.
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Gets or sets the shipping address.
        /// </summary>
        [MaxLength(500)]
        public string? ShippingAddress { get; set; }

        /// <summary>
        /// Gets or sets the customer ID.
        /// </summary>
        public int CustomerId { get; set; }

        /// <summary>
        /// Gets or sets the customer who placed this order.
        /// </summary>
        public Customer? Customer { get; set; }

        /// <summary>
        /// Gets or sets the order items.
        /// </summary>
        public ICollection<OrderItem> OrderItems { get; set; } = [];
    }
}
