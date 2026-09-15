// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// Entity set whose DELETE requires an Authorization header.
    /// </summary>
    public sealed class AuthDeletesController : ODataController
    {

        #region Public Methods

        /// <summary>
        /// Deletes when the user is authenticated or Authorization is present.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// 401 or 204.
        /// </returns>
        [HttpDelete]
        public IActionResult Delete(int key)
        {
            if (User.Identity?.IsAuthenticated != true && !Request.Headers.ContainsKey("Authorization"))
            {
                return Unauthorized();
            }

            return NoContent();
        }

        /// <summary>
        /// Returns an empty set.
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

        #endregion

    }

}
