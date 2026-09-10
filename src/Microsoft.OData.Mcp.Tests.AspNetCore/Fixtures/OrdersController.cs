// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// In-memory Orders entity set for convention navigation and expand tests.
    /// </summary>
    public sealed class OrdersController : ODataController
    {

        #region Fields

        internal readonly List<Order> _orders =
        [
            new Order
            {
                CustomerId = 1,
                OrderAmount = 10m,
                OrderId = 1,
                Status = "Open"
            }
        ];

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded orders.
        /// </summary>
        /// <returns>
        /// The order query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Order> Get()
        {
            return _orders.AsQueryable();
        }

        /// <summary>
        /// Returns a single order.
        /// </summary>
        /// <param name="key">The order key.</param>
        /// <returns>
        /// The order, or not found.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IActionResult Get(int key)
        {
            var order = _orders.FirstOrDefault(item => item.OrderId == key);

            return order is null ? NotFound() : Ok(order);
        }

        #endregion

    }

}
