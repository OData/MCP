// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Mcp.Sample.Data;
using Microsoft.OData.Mcp.Sample.Models;

namespace Microsoft.OData.Mcp.Sample.Controllers
{
    /// <summary>
    /// OData controller for Category entities.
    /// </summary>
    public class CategoriesController : ODataController
    {
        internal readonly InMemoryDataStore _dataStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="CategoriesController"/> class.
        /// </summary>
        /// <param name="dataStore">The in-memory data store.</param>
        public CategoriesController(InMemoryDataStore dataStore)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        /// <summary>
        /// Gets all categories.
        /// </summary>
        /// <returns>The categories collection.</returns>
        [EnableQuery]
        public IQueryable<Category> Get()
        {
            return _dataStore.Categories;
        }

        /// <summary>
        /// Gets a single category by ID.
        /// </summary>
        /// <param name="key">The category ID.</param>
        /// <returns>The category.</returns>
        [EnableQuery]
        public SingleResult<Category> Get([FromODataUri] int key)
        {
            var result = _dataStore.Categories.Where(c => c.Id == key);
            return SingleResult.Create(result);
        }

        /// <summary>
        /// Gets products in a specific category.
        /// </summary>
        /// <param name="key">The category ID.</param>
        /// <returns>The products in the category.</returns>
        [EnableQuery]
        public IQueryable<Product> GetProducts([FromODataUri] int key)
        {
            return _dataStore.Products.Where(p => p.CategoryId == key);
        }
    }
}