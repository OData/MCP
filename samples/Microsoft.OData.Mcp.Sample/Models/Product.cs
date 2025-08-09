using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.OData.Mcp.Sample.Models
{
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
        public ICollection<OrderItem> OrderItems { get; set; } = [];
    }
}
