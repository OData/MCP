// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Paging
{

    /// <summary>
    /// Five ordered customers used by convention paging tests.
    /// </summary>
    public static class PagingCustomerSeed
    {

        #region Properties

        /// <summary>
        /// Gets company names in key order.
        /// </summary>
        public static IReadOnlyList<string> CompanyNames { get; } =
        [
            "Contoso",
            "Fabrikam",
            "Northwind",
            "AdventureWorks",
            "Wide World"
        ];

        #endregion

        #region Public Methods

        /// <summary>
        /// Builds a mutable list of seeded customers.
        /// </summary>
        /// <returns>
        /// Five customers with <c>CustomerId</c> 1 through 5.
        /// </returns>
        public static List<Customer> Create()
        {
            var customers = new List<Customer>(CompanyNames.Count);
            for (var index = 0; index < CompanyNames.Count; index++)
            {
                customers.Add(new Customer
                {
                    CompanyName = CompanyNames[index],
                    CustomerId = index + 1
                });
            }

            return customers;
        }

        #endregion

    }

}
