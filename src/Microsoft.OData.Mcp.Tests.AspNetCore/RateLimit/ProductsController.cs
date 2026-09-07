// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit
{

    /// <summary>
    /// In-memory Products entity set for per-route rate-limit tests.
    /// </summary>
    public sealed class ProductsController : ODataController
    {

        #region Fields

        internal readonly List<Product> _products =
        [
            new Product
            {
                ProductId = 1,
                ProductName = "Widget"
            }
        ];

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded products.
        /// </summary>
        /// <returns>
        /// The product query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Product> Get()
        {
            return _products.AsQueryable();
        }

        /// <summary>
        /// Returns a product by key.
        /// </summary>
        /// <param name="key">The product key.</param>
        /// <returns>
        /// The product, or not found.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IActionResult Get(int key)
        {
            var product = _products.FirstOrDefault(item => item.ProductId == key);

            return product is null ? NotFound() : Ok(product);
        }

        #endregion

    }

}
