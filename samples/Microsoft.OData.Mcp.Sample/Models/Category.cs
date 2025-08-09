using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.OData.Mcp.Sample.Models
{
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
        public ICollection<Product> Products { get; set; } = [];
    }
}
