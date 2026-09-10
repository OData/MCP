// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Seeds in-memory Restier databases during API container build.
    /// </summary>
    public static class RestierTestSeed
    {

        #region Public Methods

        /// <summary>
        /// Creates the customer database and inserts Contoso and Fabrikam when empty.
        /// </summary>
        /// <param name="restierServices">The per-API Restier service collection.</param>
        public static void EnsureCustomers(IServiceCollection restierServices)
        {
            using var provider = restierServices.BuildServiceProvider();
            using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<McpCustomerContext>();
            db.Database.EnsureCreated();
            if (db.Customers.Any())
            {
                return;
            }

            db.Customers.Add(new McpCustomer
            {
                CompanyName = "Contoso",
                Id = 1
            });
            db.Customers.Add(new McpCustomer
            {
                CompanyName = "Fabrikam",
                Id = 2
            });
            db.Orders.Add(new McpOrder
            {
                Amount = 100m,
                CustomerId = 1,
                Id = 1
            });
            db.SaveChanges();
        }

        /// <summary>
        /// Creates five customers so skip/top pages are observable.
        /// </summary>
        /// <param name="restierServices">The per-API Restier service collection.</param>
        public static void EnsureFiveCustomers(IServiceCollection restierServices)
        {
            using var provider = restierServices.BuildServiceProvider();
            using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<McpCustomerContext>();
            db.Database.EnsureCreated();
            if (db.Customers.Any())
            {
                return;
            }

            db.Customers.AddRange(
                new McpCustomer
                {
                    CompanyName = "Contoso",
                    Id = 1
                },
                new McpCustomer
                {
                    CompanyName = "Fabrikam",
                    Id = 2
                },
                new McpCustomer
                {
                    CompanyName = "Northwind",
                    Id = 3
                },
                new McpCustomer
                {
                    CompanyName = "AdventureWorks",
                    Id = 4
                },
                new McpCustomer
                {
                    CompanyName = "Wide World",
                    Id = 5
                });
            db.Orders.Add(new McpOrder
            {
                Amount = 100m,
                CustomerId = 1,
                Id = 1
            });
            db.SaveChanges();
        }

        /// <summary>
        /// Creates the product database and inserts Widget when empty.
        /// </summary>
        /// <param name="restierServices">The per-API Restier service collection.</param>
        public static void EnsureProducts(IServiceCollection restierServices)
        {
            using var provider = restierServices.BuildServiceProvider();
            using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<McpProductContext>();
            db.Database.EnsureCreated();
            if (db.Products.Any())
            {
                return;
            }

            db.Products.Add(new McpProduct
            {
                Id = 1,
                Name = "Widget"
            });
            db.SaveChanges();
        }

        #endregion

    }

}
