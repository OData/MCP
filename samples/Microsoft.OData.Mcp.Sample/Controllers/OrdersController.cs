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
    /// OData controller for Order entities.
    /// </summary>
    public class OrdersController : ODataController
    {
        private readonly InMemoryDataStore _dataStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrdersController"/> class.
        /// </summary>
        /// <param name="dataStore">The in-memory data store.</param>
        public OrdersController(InMemoryDataStore dataStore)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        /// <summary>
        /// Gets all orders.
        /// </summary>
        /// <returns>The orders collection.</returns>
        [EnableQuery(PageSize = 100)]
        public IQueryable<Order> Get()
        {
            return _dataStore.Orders;
        }

        /// <summary>
        /// Gets a single order by ID.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <returns>The order.</returns>
        [EnableQuery]
        public SingleResult<Order> Get([FromODataUri] int key)
        {
            var result = _dataStore.Orders.Where(o => o.Id == key);
            return SingleResult.Create(result);
        }

        /// <summary>
        /// Creates a new order.
        /// </summary>
        /// <param name="order">The order to create.</param>
        /// <returns>The created order.</returns>
        public IActionResult Post([FromBody] Order order)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate customer exists
            var customer = _dataStore.GetCustomer(order.CustomerId);
            if (customer == null)
            {
                return BadRequest("Invalid customer ID");
            }

            var created = _dataStore.AddOrder(order);
            return Created(created);
        }

        /// <summary>
        /// Updates an existing order.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <param name="order">The updated order.</param>
        /// <returns>The updated order.</returns>
        public IActionResult Put([FromODataUri] int key, [FromBody] Order order)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (key != order.Id)
            {
                return BadRequest("Key mismatch");
            }

            var existing = _dataStore.GetOrder(key);
            if (existing == null)
            {
                return NotFound();
            }

            // Don't allow changing shipped orders
            if (existing.Status == OrderStatus.Shipped || existing.Status == OrderStatus.Delivered)
            {
                return BadRequest("Cannot modify shipped or delivered orders");
            }

            if (_dataStore.UpdateOrder(order))
            {
                return Updated(order);
            }

            return StatusCode(500, "Update failed");
        }

        /// <summary>
        /// Partially updates an existing order.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <param name="patch">The delta patch.</param>
        /// <returns>The updated order.</returns>
        public IActionResult Patch([FromODataUri] int key, [FromBody] Delta<Order> patch)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var order = _dataStore.GetOrder(key);
            if (order == null)
            {
                return NotFound();
            }

            // Don't allow changing shipped orders
            if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
            {
                return BadRequest("Cannot modify shipped or delivered orders");
            }

            patch.Patch(order);

            if (_dataStore.UpdateOrder(order))
            {
                return Updated(order);
            }

            return StatusCode(500, "Update failed");
        }

        /// <summary>
        /// Deletes an order.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <returns>No content if successful.</returns>
        public IActionResult Delete([FromODataUri] int key)
        {
            var order = _dataStore.GetOrder(key);
            if (order == null)
            {
                return NotFound();
            }

            // Only allow deletion of pending orders
            if (order.Status != OrderStatus.Pending)
            {
                return BadRequest("Can only delete orders in Pending status");
            }

            if (_dataStore.DeleteOrder(key))
            {
                return NoContent();
            }

            return StatusCode(500, "Delete failed");
        }

        /// <summary>
        /// Gets the customer for an order.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <returns>The customer.</returns>
        [EnableQuery]
        public SingleResult<Customer> GetCustomer([FromODataUri] int key)
        {
            var order = _dataStore.GetOrder(key);
            if (order == null)
            {
                return SingleResult.Create(Enumerable.Empty<Customer>().AsQueryable());
            }

            var result = _dataStore.Customers.Where(c => c.Id == order.CustomerId);
            return SingleResult.Create(result);
        }

        /// <summary>
        /// Gets order items for a specific order.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <returns>The order items.</returns>
        [EnableQuery]
        public IQueryable<OrderItem> GetOrderItems([FromODataUri] int key)
        {
            return _dataStore.OrderItems.Where(oi => oi.OrderId == key);
        }

        /// <summary>
        /// Cancels an order.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <returns>True if successful.</returns>
        [HttpPost]
        public IActionResult Cancel([FromODataUri] int key)
        {
            var order = _dataStore.GetOrder(key);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
            {
                return BadRequest("Cannot cancel shipped or delivered orders");
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                return BadRequest("Order is already cancelled");
            }

            order.Status = OrderStatus.Cancelled;
            if (_dataStore.UpdateOrder(order))
            {
                return Ok(true);
            }

            return StatusCode(500, "Cancellation failed");
        }

        /// <summary>
        /// Processes an order.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <returns>True if successful.</returns>
        [HttpPost]
        public IActionResult Process([FromODataUri] int key)
        {
            var order = _dataStore.GetOrder(key);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Pending)
            {
                return BadRequest("Can only process orders in Pending status");
            }

            order.Status = OrderStatus.Processing;
            if (_dataStore.UpdateOrder(order))
            {
                return Ok(true);
            }

            return StatusCode(500, "Processing failed");
        }

        /// <summary>
        /// Ships an order.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <returns>True if successful.</returns>
        [HttpPost]
        public IActionResult Ship([FromODataUri] int key, [FromBody] ODataActionParameters parameters)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var order = _dataStore.GetOrder(key);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Processing)
            {
                return BadRequest("Can only ship orders in Processing status");
            }

            if (parameters.TryGetValue("trackingNumber", out var trackingValue) && trackingValue is string trackingNumber)
            {
                if (string.IsNullOrWhiteSpace(trackingNumber))
                {
                    return BadRequest("Tracking number is required");
                }

                order.Status = OrderStatus.Shipped;
                order.ShipDate = DateTimeOffset.UtcNow;
                
                if (_dataStore.UpdateOrder(order))
                {
                    return Ok(true);
                }
            }

            return BadRequest("Invalid parameters");
        }
    }
}