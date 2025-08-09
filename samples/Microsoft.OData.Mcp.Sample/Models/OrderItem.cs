using System.ComponentModel.DataAnnotations;

namespace Microsoft.OData.Mcp.Sample.Models
{
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
}
