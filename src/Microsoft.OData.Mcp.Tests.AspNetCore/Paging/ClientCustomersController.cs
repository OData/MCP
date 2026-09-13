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
    /// Client-driven paging entity set. No server <c>PageSize</c>, so <c>$top</c>/<c>$skip</c> control the page.
    /// </summary>
    public sealed class ClientCustomersController : ODataController
    {

        #region Fields

        internal readonly List<Customer> _customers = PagingCustomerSeed.Create();

        #endregion

        #region Public Methods

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

        #endregion

    }

}
