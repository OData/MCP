// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using CloudNimble.McpEssentials.Core.Tasks.Elicitations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Mcp.Sample.Data;
using Microsoft.OData.Mcp.Sample.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Sample.Controllers
{
    /// <summary>
    /// OData controller for Order entities.
    /// </summary>
    public class OrdersController : ODataController
    {
        internal readonly InMemoryDataStore _dataStore;

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
        /// Ships an order. Same method for <c>POST /odata/Orders({key})/Ship</c> and MCP
        /// <c>action_order_ship</c>.
        /// </summary>
        /// <param name="key">The order ID.</param>
        /// <param name="parameters">The action parameters, including <c>trackingNumber</c>.</param>
        /// <param name="call">The MCP <c>tools/call</c> when this invoke came from a tool; otherwise <see langword="null"/>.</param>
        /// <returns>True if successful.</returns>
        /// <remarks>
        /// Direct OData: the client POSTs <c>{"trackingNumber":"1Z..."}</c>.
        /// MCP: <paramref name="call"/> is injected and <c>RequireForm("ship")</c> collects
        /// the tracking number. <paramref name="call"/> is not an EDM parameter.
        /// </remarks>
        [HttpPost]
        [McpElicitForm("ship")]
        public IActionResult Ship(
            [FromODataUri] int key,
            [FromBody] ODataActionParameters parameters,
            [FromServices] RequestContext<CallToolRequestParams>? call = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var order = _dataStore.GetOrder(key);
            if (order is null)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Processing)
            {
                return BadRequest("Can only ship orders in Processing status");
            }

            string trackingNumber;
            if (call is not null)
            {
                var form = call.RequireForm("ship");
                if (!form.IsAccepted)
                {
                    return BadRequest("Shipping cancelled.");
                }

                trackingNumber = form.Content!["trackingNumber"].GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(trackingNumber))
                {
                    return BadRequest("Tracking number is required");
                }
            }
            else if (!TryReadTrackingNumber(parameters, out trackingNumber))
            {
                return BadRequest("Tracking number is required");
            }

            order.Status = OrderStatus.Shipped;
            order.ShipDate = DateTimeOffset.UtcNow;
            if (_dataStore.UpdateOrder(order))
            {
                return Ok(true);
            }

            return StatusCode(500, "Shipping failed");
        }

        /// <summary>
        /// Reads <c>trackingNumber</c> from OData action parameters.
        /// </summary>
        /// <param name="parameters">The action body.</param>
        /// <param name="trackingNumber">The tracking number when present.</param>
        /// <returns>
        /// <see langword="true"/> when a non-blank tracking number was supplied.
        /// </returns>
        internal static bool TryReadTrackingNumber(ODataActionParameters? parameters, out string trackingNumber)
        {
            trackingNumber = string.Empty;
            if (parameters is null
                || !parameters.TryGetValue("trackingNumber", out var value)
                || value is not string text
                || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            trackingNumber = text;
            return true;
        }
    }
}