// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Paging
{

    /// <summary>
    /// Server-driven paging entity set. <c>PageSize</c> 2 produces <c>@odata.nextLink</c>.
    /// </summary>
    public sealed class ServerCustomersController : ODataController
    {

        #region Fields

        internal readonly List<Customer> _customers = PagingCustomerSeed.Create();

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded customers in pages of two.
        /// </summary>
        /// <returns>
        /// The customer query.
        /// </returns>
        [EnableQuery(PageSize = 2)]
        [HttpGet]
        public IQueryable<Customer> Get()
        {
            return _customers.AsQueryable();
        }

        #endregion

    }

}
