// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore
{

    /// <summary>
    /// Per-host customer rows shared across in-process HTTP requests.
    /// </summary>
    public sealed class CustomerStore
    {

        #region Properties

        /// <summary>
        /// Gets the in-memory customers for this test host.
        /// </summary>
        public List<Customer> Customers { get; } = CustomersController.CreateSeed();

        #endregion

        #region Internal Methods

        /// <summary>
        /// Restores the seed rows so a shared host can serve the next test method.
        /// </summary>
        internal void Reset()
        {
            lock (Customers)
            {
                Customers.Clear();
                Customers.AddRange(CustomersController.CreateSeed());
            }
        }

        #endregion

    }

    /// <summary>
    /// In-memory Customers entity set for convention OData MCP tests.
    /// </summary>
    public sealed class CustomersController : ODataController
    {

        #region Fields

        internal readonly List<Customer> _customers;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomersController"/> class.
        /// </summary>
        /// <param name="services">Request services. A registered <see cref="CustomerStore"/> is shared across requests.</param>
        public CustomersController(IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);

            _customers = services.GetService<CustomerStore>()?.Customers ?? CreateSeed();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Deletes a customer by key.
        /// </summary>
        /// <param name="key">The customer key.</param>
        /// <returns>
        /// No content, or not found.
        /// </returns>
        [HttpDelete]
        public IActionResult Delete(int key)
        {
            lock (_customers)
            {
                var customer = _customers.FirstOrDefault(item => item.CustomerId == key);
                if (customer is null)
                {
                    return NotFound();
                }

                _customers.Remove(customer);
            }

            return NoContent();
        }

        /// <summary>
        /// Returns the seeded customers.
        /// </summary>
        /// <returns>
        /// The customer query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Customer> Get()
        {
            return _customers.AsQueryable();
        }

        /// <summary>
        /// Returns a single customer, or not found.
        /// </summary>
        /// <param name="key">The customer key.</param>
        /// <returns>
        /// The customer, or not found.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IActionResult Get(int key)
        {
            if (key == 403)
            {
                return StatusCode(403, "Forbidden customer.");
            }

            if (key == 500)
            {
                return StatusCode(500, "Upstream boom.");
            }

            if (key == 503)
            {
                Response.Headers["Retry-After"] = "30";

                return StatusCode(503, "Maintenance.");
            }

            var customer = _customers.FirstOrDefault(item => item.CustomerId == key);

            return customer is null ? NotFound() : Ok(customer);
        }

        /// <summary>
        /// Updates a customer. Requires an authenticated user or an Authorization header.
        /// </summary>
        /// <param name="key">The customer key.</param>
        /// <param name="update">The patch body.</param>
        /// <returns>
        /// The updated customer, unauthorized, or not found.
        /// </returns>
        [HttpPatch]
        public IActionResult Patch(int key, [FromBody] Customer update)
        {
            if (User.Identity?.IsAuthenticated != true && !Request.Headers.ContainsKey("Authorization"))
            {
                return Unauthorized();
            }

            Customer? customer;
            lock (_customers)
            {
                customer = _customers.FirstOrDefault(item => item.CustomerId == key);
                if (customer is null)
                {
                    return NotFound();
                }

                if (update is not null && !string.IsNullOrWhiteSpace(update.CompanyName))
                {
                    customer.CompanyName = update.CompanyName;
                }
            }

            return Updated(customer);
        }

        /// <summary>
        /// Creates a customer. Requires an authenticated user or an Authorization header, and a company name.
        /// </summary>
        /// <param name="customer">The customer body.</param>
        /// <returns>
        /// Created, unauthorized, or bad request.
        /// </returns>
        [HttpPost]
        public IActionResult Post([FromBody] Customer customer)
        {
            if (User.Identity?.IsAuthenticated != true && !Request.Headers.ContainsKey("Authorization"))
            {
                return Unauthorized();
            }

            if (customer is null || string.IsNullOrWhiteSpace(customer.CompanyName))
            {
                return BadRequest("CompanyName is required.");
            }

            lock (_customers)
            {
                if (_customers.Any(item => item.CompanyName == customer.CompanyName))
                {
                    return Conflict("CompanyName already exists.");
                }

                customer.CustomerId = _customers.Count == 0 ? 1 : _customers.Max(item => item.CustomerId) + 1;
                _customers.Add(customer);
            }

            return Created(customer);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the seeded Contoso customer.
        /// </summary>
        /// <returns>
        /// A new seed list.
        /// </returns>
        internal static List<Customer> CreateSeed()
        {
            return
            [
                new Customer
                {
                    CompanyName = "Contoso",
                    CustomerId = 1,
                    Orders =
                    [
                        new Order
                        {
                            CustomerId = 1,
                            OrderAmount = 10m,
                            OrderId = 1,
                            Status = "Open"
                        }
                    ]
                }
            ];
        }

        #endregion

    }

}
