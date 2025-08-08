using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.OData.Mcp.Sample.Models
{
    /// <summary>
    /// Represents a customer in the sample OData service.
    /// </summary>
    public class Customer
    {
        /// <summary>
        /// Gets or sets the customer ID.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the customer's name.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the customer's email address.
        /// </summary>
        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the customer's phone number.
        /// </summary>
        [Phone]
        [MaxLength(20)]
        public string? Phone { get; set; }

        /// <summary>
        /// Gets or sets the customer's address.
        /// </summary>
        [MaxLength(500)]
        public string? Address { get; set; }

        /// <summary>
        /// Gets or sets the customer's city.
        /// </summary>
        [MaxLength(100)]
        public string? City { get; set; }

        /// <summary>
        /// Gets or sets the customer's country.
        /// </summary>
        [MaxLength(100)]
        public string? Country { get; set; }

        /// <summary>
        /// Gets or sets the date when the customer was created.
        /// </summary>
        public DateTimeOffset CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the customer's credit limit.
        /// </summary>
        public decimal CreditLimit { get; set; }

        /// <summary>
        /// Gets or sets whether the customer is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the orders placed by this customer.
        /// </summary>
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }

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
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    /// <summary>
    /// Represents an order item in the sample OData service.
    /// </summary>
    public class OrderItem
    {
        /// <summary>
        /// Gets or sets the order item ID.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the quantity.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Gets or sets the unit price.
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Gets or sets the discount percentage.
        /// </summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>
        /// Gets or sets the order ID.
        /// </summary>
        public int OrderId { get; set; }

        /// <summary>
        /// Gets or sets the order.
        /// </summary>
        public Order? Order { get; set; }

        /// <summary>
        /// Gets or sets the product ID.
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Gets or sets the product.
        /// </summary>
        public Product? Product { get; set; }
    }

    /// <summary>
    /// Represents a product in the sample OData service.
    /// </summary>
    public class Product
    {
        /// <summary>
        /// Gets or sets the product ID.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the product description.
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the SKU.
        /// </summary>
        [MaxLength(50)]
        public string? Sku { get; set; }

        /// <summary>
        /// Gets or sets the unit price.
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Gets or sets the units in stock.
        /// </summary>
        public int UnitsInStock { get; set; }

        /// <summary>
        /// Gets or sets the units on order.
        /// </summary>
        public int UnitsOnOrder { get; set; }

        /// <summary>
        /// Gets or sets the reorder level.
        /// </summary>
        public int ReorderLevel { get; set; }

        /// <summary>
        /// Gets or sets whether the product is discontinued.
        /// </summary>
        public bool Discontinued { get; set; }

        /// <summary>
        /// Gets or sets the release date.
        /// </summary>
        public DateTimeOffset? ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets the category ID.
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the category.
        /// </summary>
        public Category? Category { get; set; }

        /// <summary>
        /// Gets or sets the order items containing this product.
        /// </summary>
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    /// <summary>
    /// Represents a product category in the sample OData service.
    /// </summary>
    public class Category
    {
        /// <summary>
        /// Gets or sets the category ID.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the category name.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category description.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the products in this category.
        /// </summary>
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    /// <summary>
    /// Order status constants.
    /// </summary>
    public static class OrderStatus
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Shipped = "Shipped";
        public const string Delivered = "Delivered";
        public const string Cancelled = "Cancelled";
    }
}