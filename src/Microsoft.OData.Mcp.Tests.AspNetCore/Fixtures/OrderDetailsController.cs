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
    /// Composite-keyed OrderDetails entity set.
    /// </summary>
    public sealed class OrderDetailsController : ODataController
    {

        #region Fields

        internal readonly List<OrderDetail> _details =
        [
            new OrderDetail
            {
                OrderID = 10248,
                ProductID = 11,
                Quantity = 12
            }
        ];

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded details.
        /// </summary>
        /// <returns>
        /// The detail query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<OrderDetail> Get()
        {
            return _details.AsQueryable();
        }

        /// <summary>
        /// Returns a detail by composite key.
        /// </summary>
        /// <param name="keyOrderID">The order key part.</param>
        /// <param name="keyProductID">The product key part.</param>
        /// <returns>
        /// The detail, or not found.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IActionResult Get(int keyOrderID, int keyProductID)
        {
            var detail = _details.FirstOrDefault(item => item.OrderID == keyOrderID && item.ProductID == keyProductID);

            return detail is null ? NotFound() : Ok(detail);
        }

        #endregion

    }

}
