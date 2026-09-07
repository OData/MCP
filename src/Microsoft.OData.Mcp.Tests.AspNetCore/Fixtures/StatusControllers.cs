// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// Entity set that always returns 500.
    /// </summary>
    public sealed class BoomsController : ODataController
    {

        #region Public Methods

        /// <summary>
        /// Returns 500 for the collection.
        /// </summary>
        /// <returns>
        /// Internal server error.
        /// </returns>
        [HttpGet]
        public IActionResult Get()
        {
            return StatusCode(500, "Boom collection.");
        }

        /// <summary>
        /// Returns 500 for a key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Internal server error.
        /// </returns>
        [HttpGet]
        public IActionResult Get(int key)
        {
            return StatusCode(500, $"Boom {key}.");
        }

        /// <summary>
        /// Returns 500 for PATCH.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Internal server error.
        /// </returns>
        [HttpPatch]
        public IActionResult Patch(int key)
        {
            return StatusCode(500, $"Boom patch {key}.");
        }

        /// <summary>
        /// Returns 500 for POST.
        /// </summary>
        /// <returns>
        /// Internal server error.
        /// </returns>
        [HttpPost]
        public IActionResult Post()
        {
            return StatusCode(500, "Boom create.");
        }

        /// <summary>
        /// Returns 500 for DELETE.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Internal server error.
        /// </returns>
        [HttpDelete]
        public IActionResult Delete(int key)
        {
            return StatusCode(500, $"Boom delete {key}.");
        }

        #endregion

    }

    /// <summary>
    /// Per-host duplicate rows shared across in-process HTTP requests.
    /// </summary>
    public sealed class DuplicatesStore
    {

        #region Properties

        /// <summary>
        /// Gets the in-memory duplicates for this test host.
        /// </summary>
        public List<Customer> Customers { get; } =
        [
            new Customer
            {
                CompanyName = "Contoso",
                CustomerId = 1
            }
        ];

        #endregion

    }

    /// <summary>
    /// Entity set that returns 409 on duplicate create.
    /// </summary>
    public sealed class DuplicatesController : ODataController
    {

        #region Fields

        internal readonly List<Customer> _customers;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicatesController"/> class.
        /// </summary>
        /// <param name="services">Request services.</param>
        public DuplicatesController(IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);

            _customers = services.GetService<DuplicatesStore>()?.Customers ??
            [
                new Customer
                {
                    CompanyName = "Contoso",
                    CustomerId = 1
                }
            ];
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded duplicates.
        /// </summary>
        /// <returns>
        /// The query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Customer> Get()
        {
            return _customers.AsQueryable();
        }

        /// <summary>
        /// Creates or conflicts.
        /// </summary>
        /// <param name="customer">The body.</param>
        /// <returns>
        /// Created or 409.
        /// </returns>
        [HttpPost]
        public IActionResult Post([FromBody] Customer customer)
        {
            if (customer is not null && _customers.Any(item => item.CompanyName == customer.CompanyName))
            {
                return Conflict("Duplicate company.");
            }

            customer ??= new Customer();
            customer.CustomerId = _customers.Max(item => item.CustomerId) + 1;
            _customers.Add(customer);

            return Created(customer);
        }

        /// <summary>
        /// Conflicts on PATCH of the seeded company.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="update">The body.</param>
        /// <returns>
        /// 409 or the update.
        /// </returns>
        [HttpPatch]
        public IActionResult Patch(int key, [FromBody] Customer update)
        {
            if (update is not null && _customers.Any(item => item.CompanyName == update.CompanyName && item.CustomerId != key))
            {
                return Conflict("Duplicate company.");
            }

            var customer = _customers.FirstOrDefault(item => item.CustomerId == key);
            if (customer is null)
            {
                return NotFound();
            }

            if (update is not null && !string.IsNullOrWhiteSpace(update.CompanyName))
            {
                customer.CompanyName = update.CompanyName;
            }

            return Updated(customer);
        }

        /// <summary>
        /// Conflicts on DELETE.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// 409.
        /// </returns>
        [HttpDelete]
        public IActionResult Delete(int key)
        {
            return Conflict($"Cannot delete {key}.");
        }

        #endregion

    }

    /// <summary>
    /// Entity set that requires If-Match (428) and rejects a bad tag (412).
    /// </summary>
    public sealed class EtagsController : ODataController
    {

        #region Fields

        internal readonly List<Customer> _customers =
        [
            new Customer
            {
                CompanyName = "Contoso",
                CustomerId = 1
            }
        ];

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded etag customers.
        /// </summary>
        /// <returns>
        /// The query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Customer> Get()
        {
            return _customers.AsQueryable();
        }

        /// <summary>
        /// Returns a customer.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// The customer, or not found.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IActionResult Get(int key)
        {
            var customer = _customers.FirstOrDefault(item => item.CustomerId == key);

            return customer is null ? NotFound() : Ok(customer);
        }

        /// <summary>
        /// Patches when If-Match is <c>"ok"</c>.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="update">The body.</param>
        /// <returns>
        /// 428, 412, 404, or the update.
        /// </returns>
        [HttpPatch]
        public IActionResult Patch(int key, [FromBody] Customer update)
        {
            if (!Request.Headers.ContainsKey("If-Match"))
            {
                return StatusCode(428, "If-Match required.");
            }

            if (Request.Headers.IfMatch.ToString() != "\"ok\"")
            {
                return StatusCode(412, "Precondition failed.");
            }

            var customer = _customers.FirstOrDefault(item => item.CustomerId == key);
            if (customer is null)
            {
                return NotFound();
            }

            if (update is not null && !string.IsNullOrWhiteSpace(update.CompanyName))
            {
                customer.CompanyName = update.CompanyName;
            }

            return Updated(customer);
        }

        /// <summary>
        /// Creates when If-Match is <c>"ok"</c>.
        /// </summary>
        /// <param name="customer">The body.</param>
        /// <returns>
        /// 428, 412, or created.
        /// </returns>
        [HttpPost]
        public IActionResult Post([FromBody] Customer customer)
        {
            if (!Request.Headers.ContainsKey("If-Match"))
            {
                return StatusCode(428, "If-Match required.");
            }

            if (Request.Headers.IfMatch.ToString() != "\"ok\"")
            {
                return StatusCode(412, "Precondition failed.");
            }

            customer ??= new Customer();
            customer.CustomerId = _customers.Max(item => item.CustomerId) + 1;
            _customers.Add(customer);

            return Created(customer);
        }

        /// <summary>
        /// Deletes when If-Match is <c>"ok"</c>.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// 428, 412, 404, or no content.
        /// </returns>
        [HttpDelete]
        public IActionResult Delete(int key)
        {
            if (!Request.Headers.ContainsKey("If-Match"))
            {
                return StatusCode(428, "If-Match required.");
            }

            if (Request.Headers.IfMatch.ToString() != "\"ok\"")
            {
                return StatusCode(412, "Precondition failed.");
            }

            var customer = _customers.FirstOrDefault(item => item.CustomerId == key);
            if (customer is null)
            {
                return NotFound();
            }

            _customers.Remove(customer);

            return NoContent();
        }

        #endregion

    }

    /// <summary>
    /// Entity set that returns 403 for every GET.
    /// </summary>
    public sealed class ForbiddenController : ODataController
    {

        #region Public Methods

        /// <summary>
        /// Returns 403 for the collection.
        /// </summary>
        /// <returns>
        /// Forbidden.
        /// </returns>
        [HttpGet]
        public IActionResult Get()
        {
            return StatusCode(403, "Forbidden set.");
        }

        /// <summary>
        /// Returns 403 for a key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Forbidden.
        /// </returns>
        [HttpGet]
        public IActionResult Get(int key)
        {
            return StatusCode(403, $"Forbidden {key}.");
        }

        /// <summary>
        /// Returns 403 for PATCH.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Forbidden.
        /// </returns>
        [HttpPatch]
        public IActionResult Patch(int key)
        {
            return StatusCode(403, $"Forbidden patch {key}.");
        }

        /// <summary>
        /// Returns 403 for POST.
        /// </summary>
        /// <returns>
        /// Forbidden.
        /// </returns>
        [HttpPost]
        public IActionResult Post()
        {
            return StatusCode(403, "Forbidden create.");
        }

        /// <summary>
        /// Returns 403 for DELETE.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Forbidden.
        /// </returns>
        [HttpDelete]
        public IActionResult Delete(int key)
        {
            return StatusCode(403, $"Forbidden delete {key}.");
        }

        #endregion

    }

    /// <summary>
    /// Entity set that returns 503 with Retry-After.
    /// </summary>
    public sealed class MaintenanceController : ODataController
    {

        #region Public Methods

        /// <summary>
        /// Returns 503 for the collection.
        /// </summary>
        /// <returns>
        /// Service unavailable.
        /// </returns>
        [HttpGet]
        public IActionResult Get()
        {
            Response.Headers["Retry-After"] = "15";

            return StatusCode(503, "Maintenance collection.");
        }

        /// <summary>
        /// Returns 503 for a key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Service unavailable.
        /// </returns>
        [HttpGet]
        public IActionResult Get(int key)
        {
            Response.Headers["Retry-After"] = "15";

            return StatusCode(503, $"Maintenance {key}.");
        }

        /// <summary>
        /// Returns 503 for PATCH.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Service unavailable.
        /// </returns>
        [HttpPatch]
        public IActionResult Patch(int key)
        {
            Response.Headers["Retry-After"] = "15";

            return StatusCode(503, $"Maintenance patch {key}.");
        }

        /// <summary>
        /// Returns 503 for POST.
        /// </summary>
        /// <returns>
        /// Service unavailable.
        /// </returns>
        [HttpPost]
        public IActionResult Post()
        {
            Response.Headers["Retry-After"] = "15";

            return StatusCode(503, "Maintenance create.");
        }

        /// <summary>
        /// Returns 503 for DELETE.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Service unavailable.
        /// </returns>
        [HttpDelete]
        public IActionResult Delete(int key)
        {
            Response.Headers["Retry-After"] = "15";

            return StatusCode(503, $"Maintenance delete {key}.");
        }

        #endregion

    }

    /// <summary>
    /// Entity set that returns 413 when the body is larger than 64 bytes.
    /// </summary>
    public sealed class PayloadsController : ODataController
    {

        #region Public Methods

        /// <summary>
        /// Returns an empty collection.
        /// </summary>
        /// <returns>
        /// Empty query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Customer> Get()
        {
            return Enumerable.Empty<Customer>().AsQueryable();
        }

        /// <summary>
        /// Rejects large bodies with 413.
        /// </summary>
        /// <returns>
        /// 413 or created.
        /// </returns>
        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (body.Length > 64)
            {
                return StatusCode(413, "Payload too large.");
            }

            return Created(new Customer
            {
                CompanyName = "Tiny",
                CustomerId = 1
            });
        }

        /// <summary>
        /// Rejects large PATCH bodies with 413.
        /// </summary>
        /// <returns>
        /// 413 or no content.
        /// </returns>
        [HttpPatch]
        public async Task<IActionResult> Patch(int key)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (body.Length > 64)
            {
                return StatusCode(413, "Payload too large.");
            }

            return Updated(new Customer
            {
                CompanyName = "Tiny",
                CustomerId = key
            });
        }

        #endregion

    }

    /// <summary>
    /// Read-only entity set that returns 405 on POST.
    /// </summary>
    public sealed class ReadOnlyItemsController : ODataController
    {

        #region Fields

        internal readonly List<Customer> _customers =
        [
            new Customer
            {
                CompanyName = "ReadOnly",
                CustomerId = 1
            }
        ];

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded items.
        /// </summary>
        /// <returns>
        /// The query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Customer> Get()
        {
            return _customers.AsQueryable();
        }

        /// <summary>
        /// Always 405.
        /// </summary>
        /// <returns>
        /// Method not allowed.
        /// </returns>
        [HttpPost]
        public IActionResult Post()
        {
            return StatusCode(405, "Read-only set.");
        }

        /// <summary>
        /// Always 405 on PATCH.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Method not allowed.
        /// </returns>
        [HttpPatch]
        public IActionResult Patch(int key)
        {
            return StatusCode(405, "Read-only set.");
        }

        /// <summary>
        /// Always 405 on DELETE.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// Method not allowed.
        /// </returns>
        [HttpDelete]
        public IActionResult Delete(int key)
        {
            return StatusCode(405, "Read-only set.");
        }

        #endregion

    }

}
