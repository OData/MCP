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
    /// OData controller for Customer entities.
    /// </summary>
    public class CustomersController : ODataController
    {
        internal readonly InMemoryDataStore _dataStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomersController"/> class.
        /// </summary>
        /// <param name="dataStore">The in-memory data store.</param>
        public CustomersController(InMemoryDataStore dataStore)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        /// <summary>
        /// Gets all customers.
        /// </summary>
        /// <returns>The customers collection.</returns>
        [EnableQuery(PageSize = 100)]
        public IQueryable<Customer> Get()
        {
            return _dataStore.Customers;
        }

        /// <summary>
        /// Gets a single customer by ID.
        /// </summary>
        /// <param name="key">The customer ID.</param>
        /// <returns>The customer.</returns>
        [EnableQuery]
        public SingleResult<Customer> Get([FromODataUri] int key)
        {
            var result = _dataStore.Customers.Where(c => c.Id == key);
            return SingleResult.Create(result);
        }

        /// <summary>
        /// Creates a new customer.
        /// </summary>
        /// <param name="customer">The customer to create.</param>
        /// <returns>The created customer.</returns>
        public IActionResult Post([FromBody] Customer customer)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var created = _dataStore.AddCustomer(customer);
            return Created(created);
        }

        /// <summary>
        /// Updates an existing customer.
        /// </summary>
        /// <param name="key">The customer ID.</param>
        /// <param name="customer">The updated customer.</param>
        /// <returns>The updated customer.</returns>
        public IActionResult Put([FromODataUri] int key, [FromBody] Customer customer)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (key != customer.Id)
            {
                return BadRequest("Key mismatch");
            }

            var existing = _dataStore.GetCustomer(key);
            if (existing == null)
            {
                return NotFound();
            }

            if (_dataStore.UpdateCustomer(customer))
            {
                return Updated(customer);
            }

            return StatusCode(500, "Update failed");
        }

        /// <summary>
        /// Partially updates an existing customer.
        /// </summary>
        /// <param name="key">The customer ID.</param>
        /// <param name="patch">The delta patch.</param>
        /// <returns>The updated customer.</returns>
        public IActionResult Patch([FromODataUri] int key, [FromBody] Delta<Customer> patch)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customer = _dataStore.GetCustomer(key);
            if (customer == null)
            {
                return NotFound();
            }

            patch.Patch(customer);

            if (_dataStore.UpdateCustomer(customer))
            {
                return Updated(customer);
            }

            return StatusCode(500, "Update failed");
        }

        /// <summary>
        /// Deletes a customer.
        /// </summary>
        /// <param name="key">The customer ID.</param>
        /// <returns>No content if successful.</returns>
        public IActionResult Delete([FromODataUri] int key)
        {
            var customer = _dataStore.GetCustomer(key);
            if (customer == null)
            {
                return NotFound();
            }

            // Check if customer has orders
            var hasOrders = _dataStore.Orders.Any(o => o.CustomerId == key);
            if (hasOrders)
            {
                return BadRequest("Cannot delete customer with existing orders");
            }

            if (_dataStore.DeleteCustomer(key))
            {
                return NoContent();
            }

            return StatusCode(500, "Delete failed");
        }

        /// <summary>
        /// Gets orders for a specific customer.
        /// </summary>
        /// <param name="key">The customer ID.</param>
        /// <returns>The customer's orders.</returns>
        [EnableQuery]
        public IQueryable<Order> GetOrders([FromODataUri] int key)
        {
            return _dataStore.Orders.Where(o => o.CustomerId == key);
        }

        /// <summary>
        /// Updates a customer's credit limit.
        /// </summary>
        /// <param name="key">The customer ID.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <returns>True if successful.</returns>
        [HttpPost]
        public IActionResult UpdateCreditLimit([FromODataUri] int key, [FromBody] ODataActionParameters parameters)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customer = _dataStore.GetCustomer(key);
            if (customer == null)
            {
                return NotFound();
            }

            if (parameters.TryGetValue("newLimit", out var limitValue) && limitValue is decimal newLimit)
            {
                if (newLimit < 0)
                {
                    return BadRequest("Credit limit cannot be negative");
                }

                customer.CreditLimit = newLimit;
                if (_dataStore.UpdateCustomer(customer))
                {
                    return Ok(true);
                }
            }

            return BadRequest("Invalid parameters");
        }

        /// <summary>
        /// Deactivates a customer.
        /// </summary>
        /// <param name="key">The customer ID.</param>
        /// <returns>True if successful.</returns>
        [HttpPost]
        public IActionResult Deactivate([FromODataUri] int key)
        {
            var customer = _dataStore.GetCustomer(key);
            if (customer == null)
            {
                return NotFound();
            }

            customer.IsActive = false;
            if (_dataStore.UpdateCustomer(customer))
            {
                return Ok(true);
            }

            return StatusCode(500, "Deactivation failed");
        }
    }
}