// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.Shared.Models
{

    /// <summary>
    /// Provides various EDM models for testing different scenarios.
    /// </summary>
    public static class TestModels
    {

        #region Public Methods

        /// <summary>
        /// Gets a complex model with inheritance, navigation properties, and complex types.
        /// </summary>
        public static IEdmModel GetComplexModel()
        {
            var builder = new ODataConventionModelBuilder();
            
            // Configure entity sets
            builder.EntitySet<Employee>("Employees");
            builder.EntitySet<VipCustomer>("VipCustomers");
            builder.EntitySet<Customer>("Customers");
            builder.EntitySet<Order>("Orders");
            
            // Configure complex type
            builder.ComplexType<Address>();
            
            // Configure inheritance
            builder.EntityType<Employee>().DerivesFrom<Person>();
            builder.EntityType<VipCustomer>().DerivesFrom<Customer>();
            
            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets an edge case model with Unicode names, special characters, and very long names.
        /// </summary>
        public static IEdmModel GetEdgeCaseModel()
        {
            var builder = new ODataConventionModelBuilder();
            
            // Unicode entity name
            builder.EntitySet<客戶>("客戶集"); // Customers in Chinese
            
            // Very long entity name
            builder.EntitySet<ThisIsAnExtremelyLongEntityNameThatIsDesignedToTestHowTheSystemHandlesVeryLongIdentifiersInVariousContexts>(
                "ExtremelyLongEntities");
            
            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets a model with unbound functions and actions and no entity sets.
        /// </summary>
        public static IEdmModel GetOperationsOnlyModel()
        {
            var builder = new ODataConventionModelBuilder
            {
                Namespace = "Ops"
            };

            builder.Function("MostValuable").Returns<int>();
            builder.Function("GetStatus").Returns<string>().Parameter<string>("code");
            builder.Action("Reset");

            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets a wide model with many entity sets and no operations.
        /// </summary>
        /// <param name="entitySetCount">The number of entity sets to declare.</param>
        /// <returns>
        /// The model.
        /// </returns>
        public static IEdmModel GetWideModel(int entitySetCount = 200)
        {
            var model = new EdmModel();
            var container = new EdmEntityContainer("Wide", "Container");
            for (var i = 0; i < entitySetCount; i++)
            {
                var type = new EdmEntityType("Wide", $"Row{i:D3}");
                var key = type.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, isNullable: false);
                type.AddKeys(key);
                model.AddElement(type);
                container.AddEntitySet($"Rows{i:D3}", type);
            }

            model.AddElement(container);

            return model;
        }

        /// <summary>
        /// Gets a minimal model with a single entity for basic tests.
        /// </summary>
        public static IEdmModel GetMinimalModel()
        {
            var builder = new ODataConventionModelBuilder();
            IgnoreSecret(builder.EntitySet<Customer>("Customers").EntityType);
            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets a model designed for multi-tenant scenarios.
        /// </summary>
        public static IEdmModel GetMultiTenantModel()
        {
            var builder = new ODataConventionModelBuilder
            {
                Namespace = "Tenant"
            };

            // Each tenant might have different entity visibility
            builder.EntitySet<Customer>("Customers");
            builder.EntitySet<Order>("Orders");
            builder.EntitySet<Employee>("Employees");
            
            // Configure tenant isolation
            var customer = builder.EntityType<Customer>();
            customer.Property(c => c.CustomerId).IsRequired();
            
            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets a model for testing without authentication.
        /// </summary>
        public static IEdmModel GetNoAuthModel()
        {
            var builder = new ODataConventionModelBuilder
            {
                Namespace = "Public"
            };

            // Only public entities
            builder.EntitySet<Product>("Products");
            
            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets a model with client-driven and server-driven paging entity sets over <see cref="Customer"/>.
        /// </summary>
        /// <returns>
        /// The paging model.
        /// </returns>
        public static IEdmModel GetPagingModel()
        {
            var builder = new ODataConventionModelBuilder();
            IgnoreSecret(builder.EntitySet<Customer>("ClientCustomers").EntityType);
            IgnoreSecret(builder.EntitySet<Customer>("ServerCustomers").EntityType);

            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets Customers, Products, and an unbound function for per-route rate-limit tests.
        /// </summary>
        /// <returns>
        /// The model.
        /// </returns>
        public static IEdmModel GetRateLimitModel()
        {
            var builder = new ODataConventionModelBuilder();
            IgnoreSecret(builder.EntitySet<Customer>("Customers").EntityType);
            builder.EntitySet<Product>("Products");
            builder.Function("MostValuable").Returns<int>();

            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets a rich convention model covering keys, navigation, binary/stream, and status-code fixtures.
        /// </summary>
        /// <returns>
        /// The model.
        /// </returns>
        public static IEdmModel GetRichModel()
        {
            var builder = new ODataConventionModelBuilder();
            IgnoreSecret(builder.EntitySet<Customer>("Customers").EntityType);
            builder.EntitySet<Order>("Orders");
            builder.EntitySet<Product>("Products");
            builder.EntitySet<OrderItem>("OrderItems");
            builder.EntitySet<Widget>("Widgets");
            var details = builder.EntitySet<OrderDetail>("OrderDetails").EntityType;
            details.HasKey(item => new { item.OrderID, item.ProductID });
            builder.EntitySet<Document>("Documents");
            builder.EntityType<Flag>().HasKey(flag => flag.FlagId);
            builder.EntitySet<Flag>("Flags");
            IgnoreSecret(builder.EntitySet<Customer>("Forbidden").EntityType);
            IgnoreSecret(builder.EntitySet<Customer>("ReadOnlyItems").EntityType);
            IgnoreSecret(builder.EntitySet<Customer>("Duplicates").EntityType);
            IgnoreSecret(builder.EntitySet<Customer>("Etags").EntityType);
            IgnoreSecret(builder.EntitySet<Customer>("Payloads").EntityType);
            IgnoreSecret(builder.EntitySet<Customer>("Booms").EntityType);
            IgnoreSecret(builder.EntitySet<Customer>("Maintenance").EntityType);
            builder.Function("MostValuable").Returns<int>();
            builder.Function("GetStatus").Returns<string>().Parameter<string>("code");
            builder.Action("Reset");

            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets a simple model with basic Customer/Order entities.
        /// </summary>
        public static IEdmModel GetSimpleModel()
        {
            var builder = new ODataConventionModelBuilder();

            IgnoreSecret(builder.EntitySet<Customer>("Customers").EntityType);
            builder.EntitySet<Order>("Orders");
            builder.EntitySet<Product>("Products");
            builder.EntitySet<OrderItem>("OrderItems");

            return builder.GetEdmModel();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Omits the CLR-only <see cref="Customer.InternalSecret"/> from the EDM.
        /// </summary>
        /// <param name="customer">The customer type configuration.</param>
        internal static void IgnoreSecret(EntityTypeConfiguration<Customer> customer)
        {
            ArgumentNullException.ThrowIfNull(customer);

            customer.Ignore(item => item.InternalSecret);
        }

        #endregion

    }
}
