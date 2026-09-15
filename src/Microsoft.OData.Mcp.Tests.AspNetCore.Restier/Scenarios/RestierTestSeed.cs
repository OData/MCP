// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios
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
        /// Replaces every customer and order with the two-row Contoso/Fabrikam seed.
        /// </summary>
        /// <param name="db">The live Restier context.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="db"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Authenticated suites keep one Breakdance host per test class so the outbound OAuth handshake
        /// is not paid on every method. Write tests still need the same starting rows the in-process
        /// suite gets from a fresh in-memory database.
        /// </remarks>
        public static void ResetCustomers(McpCustomerContext db)
        {
            ArgumentNullException.ThrowIfNull(db);

            db.Orders.RemoveRange(db.Orders);
            db.Customers.RemoveRange(db.Customers);
            db.SaveChanges();
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
