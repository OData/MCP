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

        public string City { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string ContactName { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public string Email { get; set; } = string.Empty;

        public List<Order> Orders { get; set; } = new();

        public string Phone { get; set; } = string.Empty;

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

        public List<OrderItem> OrderItems { get; set; } = new();

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

        public List<OrderItem> OrderItems { get; set; } = new();

        public decimal Price { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        #endregion

    }
}