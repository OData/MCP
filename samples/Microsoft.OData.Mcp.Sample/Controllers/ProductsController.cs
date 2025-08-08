using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Mcp.Sample.Data;
using Microsoft.OData.Mcp.Sample.Models;

namespace Microsoft.OData.Mcp.Sample.Controllers
{
    /// <summary>
    /// OData controller for Product entities.
    /// </summary>
    public class ProductsController : ODataController
    {
        private readonly InMemoryDataStore _dataStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProductsController"/> class.
        /// </summary>
        /// <param name="dataStore">The in-memory data store.</param>
        public ProductsController(InMemoryDataStore dataStore)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        /// <summary>
        /// Gets all products.
        /// </summary>
        /// <returns>The products collection.</returns>
        [EnableQuery(PageSize = 100)]
        public IQueryable<Product> Get()
        {
            return _dataStore.Products;
        }

        /// <summary>
        /// Gets a single product by ID.
        /// </summary>
        /// <param name="key">The product ID.</param>
        /// <returns>The product.</returns>
        [EnableQuery]
        public SingleResult<Product> Get([FromODataUri] int key)
        {
            var result = _dataStore.Products.Where(p => p.Id == key);
            return SingleResult.Create(result);
        }

        /// <summary>
        /// Creates a new product.
        /// </summary>
        /// <param name="product">The product to create.</param>
        /// <returns>The created product.</returns>
        public IActionResult Post([FromBody] Product product)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate category exists if specified
            if (product.CategoryId.HasValue)
            {
                var category = _dataStore.GetCategory(product.CategoryId.Value);
                if (category == null)
                {
                    return BadRequest("Invalid category ID");
                }
            }

            var created = _dataStore.AddProduct(product);
            return Created(created);
        }

        /// <summary>
        /// Updates an existing product.
        /// </summary>
        /// <param name="key">The product ID.</param>
        /// <param name="product">The updated product.</param>
        /// <returns>The updated product.</returns>
        public IActionResult Put([FromODataUri] int key, [FromBody] Product product)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (key != product.Id)
            {
                return BadRequest("Key mismatch");
            }

            var existing = _dataStore.GetProduct(key);
            if (existing == null)
            {
                return NotFound();
            }

            // Validate category exists if specified
            if (product.CategoryId.HasValue)
            {
                var category = _dataStore.GetCategory(product.CategoryId.Value);
                if (category == null)
                {
                    return BadRequest("Invalid category ID");
                }
            }

            if (_dataStore.UpdateProduct(product))
            {
                return Updated(product);
            }

            return StatusCode(500, "Update failed");
        }

        /// <summary>
        /// Partially updates an existing product.
        /// </summary>
        /// <param name="key">The product ID.</param>
        /// <param name="patch">The delta patch.</param>
        /// <returns>The updated product.</returns>
        public IActionResult Patch([FromODataUri] int key, [FromBody] Delta<Product> patch)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = _dataStore.GetProduct(key);
            if (product == null)
            {
                return NotFound();
            }

            patch.Patch(product);

            // Validate category if it was changed
            if (patch.GetChangedPropertyNames().Contains("CategoryId") && product.CategoryId.HasValue)
            {
                var category = _dataStore.GetCategory(product.CategoryId.Value);
                if (category == null)
                {
                    return BadRequest("Invalid category ID");
                }
            }

            if (_dataStore.UpdateProduct(product))
            {
                return Updated(product);
            }

            return StatusCode(500, "Update failed");
        }

        /// <summary>
        /// Deletes a product.
        /// </summary>
        /// <param name="key">The product ID.</param>
        /// <returns>No content if successful.</returns>
        public IActionResult Delete([FromODataUri] int key)
        {
            var product = _dataStore.GetProduct(key);
            if (product == null)
            {
                return NotFound();
            }

            // Check if product is used in any orders
            var hasOrders = _dataStore.OrderItems.Any(oi => oi.ProductId == key);
            if (hasOrders)
            {
                return BadRequest("Cannot delete product that has been ordered");
            }

            if (_dataStore.DeleteProduct(key))
            {
                return NoContent();
            }

            return StatusCode(500, "Delete failed");
        }

        /// <summary>
        /// Gets the category for a product.
        /// </summary>
        /// <param name="key">The product ID.</param>
        /// <returns>The category.</returns>
        [EnableQuery]
        public SingleResult<Category> GetCategory([FromODataUri] int key)
        {
            var product = _dataStore.GetProduct(key);
            if (product == null || !product.CategoryId.HasValue)
            {
                return SingleResult.Create(Enumerable.Empty<Category>().AsQueryable());
            }

            var result = _dataStore.Categories.Where(c => c.Id == product.CategoryId.Value);
            return SingleResult.Create(result);
        }

        /// <summary>
        /// Discontinues a product.
        /// </summary>
        /// <param name="key">The product ID.</param>
        /// <returns>True if successful.</returns>
        [HttpPost]
        public IActionResult Discontinue([FromODataUri] int key)
        {
            var product = _dataStore.GetProduct(key);
            if (product == null)
            {
                return NotFound();
            }

            if (product.Discontinued)
            {
                return BadRequest("Product is already discontinued");
            }

            product.Discontinued = true;
            if (_dataStore.UpdateProduct(product))
            {
                return Ok(true);
            }

            return StatusCode(500, "Discontinuation failed");
        }

        /// <summary>
        /// Applies a discount to a product.
        /// </summary>
        /// <param name="key">The product ID.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <returns>The new price after discount.</returns>
        [HttpPost]
        public IActionResult ApplyDiscount([FromODataUri] int key, [FromBody] ODataActionParameters parameters)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = _dataStore.GetProduct(key);
            if (product == null)
            {
                return NotFound();
            }

            if (parameters.TryGetValue("percentage", out var percentageValue) && percentageValue is decimal percentage)
            {
                if (percentage < 0 || percentage > 100)
                {
                    return BadRequest("Discount percentage must be between 0 and 100");
                }

                var discountedPrice = product.UnitPrice * (1 - (percentage / 100));
                product.UnitPrice = Math.Round(discountedPrice, 2);
                
                if (_dataStore.UpdateProduct(product))
                {
                    return Ok(product.UnitPrice);
                }
            }

            return BadRequest("Invalid parameters");
        }

        /// <summary>
        /// Restocks a product.
        /// </summary>
        /// <param name="key">The product ID.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <returns>True if successful.</returns>
        [HttpPost]
        public IActionResult Restock([FromODataUri] int key, [FromBody] ODataActionParameters parameters)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = _dataStore.GetProduct(key);
            if (product == null)
            {
                return NotFound();
            }

            if (parameters.TryGetValue("quantity", out var quantityValue) && quantityValue is int quantity)
            {
                if (quantity <= 0)
                {
                    return BadRequest("Quantity must be positive");
                }

                product.UnitsInStock += quantity;
                
                if (_dataStore.UpdateProduct(product))
                {
                    return Ok(true);
                }
            }

            return BadRequest("Invalid parameters");
        }
    }
}