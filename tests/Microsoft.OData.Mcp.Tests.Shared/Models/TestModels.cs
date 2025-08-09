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
        /// Gets a large model with many entities for performance testing.
        /// </summary>
        public static IEdmModel GetLargeModel()
        {
            var builder = new ODataConventionModelBuilder();
            
            // Add standard entities
            builder.EntitySet<Customer>("Customers");
            builder.EntitySet<Order>("Orders");
            builder.EntitySet<OrderItem>("OrderItems");
            builder.EntitySet<Product>("Products");
            builder.EntitySet<Employee>("Employees");
            builder.EntitySet<VipCustomer>("VipCustomers");
            
            // Add many dummy entity sets for performance testing
            for (int i = 0; i < 50; i++)
            {
                var entityType = builder.EntityType<Customer>();
                entityType.Name = $"Entity{i}";
                entityType.Namespace = "TestNamespace";
            }
            
            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets a minimal model with a single entity for basic tests.
        /// </summary>
        public static IEdmModel GetMinimalModel()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");
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
        /// Gets a simple model with basic Customer/Order entities.
        /// </summary>
        public static IEdmModel GetSimpleModel()
        {
            var builder = new ODataConventionModelBuilder();
            
            builder.EntitySet<Customer>("Customers");
            builder.EntitySet<Order>("Orders");
            builder.EntitySet<Product>("Products");
            builder.EntitySet<OrderItem>("OrderItems");
            
            // Relationships are configured automatically by convention in OData
            
            return builder.GetEdmModel();
        }

        #endregion

    }
}
