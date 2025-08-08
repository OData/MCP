using System;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Microsoft.OData.Mcp.Sample.Models
{
    /// <summary>
    /// Builds EDM models for the sample OData service.
    /// </summary>
    public static class SampleEdmModel
    {
        /// <summary>
        /// Gets the V1 EDM model with basic entities.
        /// </summary>
        /// <returns>The V1 EDM model.</returns>
        public static IEdmModel GetV1Model()
        {
            var builder = new ODataConventionModelBuilder();
            
            // V1 includes only Customers and Orders
            builder.EntitySet<Customer>("Customers");
            builder.EntitySet<Order>("Orders");
            
            // Configure Customer entity
            var customer = builder.EntityType<Customer>();
            customer.HasKey(c => c.Id);
            customer.Property(c => c.Name).IsRequired();
            customer.HasMany(c => c.Orders);
            
            // Configure Order entity
            var order = builder.EntityType<Order>();
            order.HasKey(o => o.Id);
            order.Property(o => o.OrderNumber).IsRequired();
            order.HasRequired(o => o.Customer);
            
            // Add some actions
            var cancelOrder = builder.EntityType<Order>()
                .Action("Cancel");
            cancelOrder.Returns<bool>();
            
            var processOrder = builder.EntityType<Order>()
                .Action("Process");
            processOrder.Returns<bool>();
            
            // Add a function
            var getTopCustomers = builder.Function("GetTopCustomers");
            getTopCustomers.Parameter<int>("count");
            getTopCustomers.ReturnsCollectionFromEntitySet<Customer>("Customers");
            
            builder.Namespace = "SampleService.V1";
            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets the V2 EDM model with extended entities.
        /// </summary>
        /// <returns>The V2 EDM model.</returns>
        public static IEdmModel GetV2Model()
        {
            var builder = new ODataConventionModelBuilder();
            
            // V2 includes Customers, Orders, Products, and Categories
            builder.EntitySet<Customer>("Customers");
            builder.EntitySet<Order>("Orders");
            builder.EntitySet<Product>("Products");
            builder.EntitySet<Category>("Categories");
            builder.EntitySet<OrderItem>("OrderItems");
            
            // Configure Customer entity
            var customer = builder.EntityType<Customer>();
            customer.HasKey(c => c.Id);
            customer.Property(c => c.Name).IsRequired();
            customer.HasMany(c => c.Orders);
            
            // Configure Order entity
            var order = builder.EntityType<Order>();
            order.HasKey(o => o.Id);
            order.Property(o => o.OrderNumber).IsRequired();
            order.HasRequired(o => o.Customer);
            order.HasMany(o => o.OrderItems);
            
            // Configure Product entity
            var product = builder.EntityType<Product>();
            product.HasKey(p => p.Id);
            product.Property(p => p.Name).IsRequired();
            product.HasOptional(p => p.Category);
            product.HasMany(p => p.OrderItems);
            
            // Configure Category entity
            var category = builder.EntityType<Category>();
            category.HasKey(c => c.Id);
            category.Property(c => c.Name).IsRequired();
            category.HasMany(c => c.Products);
            
            // Configure OrderItem entity
            var orderItem = builder.EntityType<OrderItem>();
            orderItem.HasKey(oi => oi.Id);
            orderItem.HasRequired(oi => oi.Order);
            orderItem.HasRequired(oi => oi.Product);
            
            // Add actions
            var cancelOrder = order.Action("Cancel");
            cancelOrder.Returns<bool>();
            
            var processOrder = order.Action("Process");
            processOrder.Returns<bool>();
            
            var shipOrder = order.Action("Ship");
            shipOrder.Parameter<string>("trackingNumber");
            shipOrder.Returns<bool>();
            
            var discontinueProduct = product.Action("Discontinue");
            discontinueProduct.Returns<bool>();
            
            var applyDiscount = product.Action("ApplyDiscount");
            applyDiscount.Parameter<decimal>("percentage");
            applyDiscount.Returns<decimal>();
            
            // Add functions
            var getTopCustomers = builder.Function("GetTopCustomers");
            getTopCustomers.Parameter<int>("count");
            getTopCustomers.ReturnsCollectionFromEntitySet<Customer>("Customers");
            
            var getProductsByCategory = builder.Function("GetProductsByCategory");
            getProductsByCategory.Parameter<int>("categoryId");
            getProductsByCategory.ReturnsCollectionFromEntitySet<Product>("Products");
            
            var calculateRevenue = builder.Function("CalculateRevenue");
            calculateRevenue.Parameter<DateTimeOffset>("startDate");
            calculateRevenue.Parameter<DateTimeOffset>("endDate");
            calculateRevenue.Returns<decimal>();
            
            builder.Namespace = "SampleService.V2";
            return builder.GetEdmModel();
        }

        /// <summary>
        /// Gets the main (full) EDM model with all entities.
        /// </summary>
        /// <returns>The full EDM model.</returns>
        public static IEdmModel GetMainModel()
        {
            var builder = new ODataConventionModelBuilder();
            
            // Full model includes all entities
            builder.EntitySet<Customer>("Customers");
            builder.EntitySet<Order>("Orders");
            builder.EntitySet<Product>("Products");
            builder.EntitySet<Category>("Categories");
            builder.EntitySet<OrderItem>("OrderItems");
            
            // Configure entities using conventions
            builder.EntityType<Customer>()
                .HasKey(c => c.Id);
            
            builder.EntityType<Order>()
                .HasKey(o => o.Id);
            
            builder.EntityType<Product>()
                .HasKey(p => p.Id);
            
            builder.EntityType<Category>()
                .HasKey(c => c.Id);
            
            builder.EntityType<OrderItem>()
                .HasKey(oi => oi.Id);
            
            // Add all actions and functions from V2
            var order = builder.EntityType<Order>();
            var cancelOrder = order.Action("Cancel");
            cancelOrder.Returns<bool>();
            
            var processOrder = order.Action("Process");
            processOrder.Returns<bool>();
            
            var shipOrder = order.Action("Ship");
            shipOrder.Parameter<string>("trackingNumber");
            shipOrder.Returns<bool>();
            
            var product = builder.EntityType<Product>();
            var discontinueProduct = product.Action("Discontinue");
            discontinueProduct.Returns<bool>();
            
            var applyDiscount = product.Action("ApplyDiscount");
            applyDiscount.Parameter<decimal>("percentage");
            applyDiscount.Returns<decimal>();
            
            var restockProduct = product.Action("Restock");
            restockProduct.Parameter<int>("quantity");
            restockProduct.Returns<bool>();
            
            // Customer actions
            var customer = builder.EntityType<Customer>();
            var updateCreditLimit = customer.Action("UpdateCreditLimit");
            updateCreditLimit.Parameter<decimal>("newLimit");
            updateCreditLimit.Returns<bool>();
            
            var deactivateCustomer = customer.Action("Deactivate");
            deactivateCustomer.Returns<bool>();
            
            // Functions
            var getTopCustomers = builder.Function("GetTopCustomers");
            getTopCustomers.Parameter<int>("count");
            getTopCustomers.ReturnsCollectionFromEntitySet<Customer>("Customers");
            
            var getProductsByCategory = builder.Function("GetProductsByCategory");
            getProductsByCategory.Parameter<int>("categoryId");
            getProductsByCategory.ReturnsCollectionFromEntitySet<Product>("Products");
            
            var calculateRevenue = builder.Function("CalculateRevenue");
            calculateRevenue.Parameter<DateTimeOffset>("startDate");
            calculateRevenue.Parameter<DateTimeOffset>("endDate");
            calculateRevenue.Returns<decimal>();
            
            var getInventoryStatus = builder.Function("GetInventoryStatus");
            getInventoryStatus.ReturnsCollection<InventoryStatus>();
            
            var searchProducts = builder.Function("SearchProducts");
            searchProducts.Parameter<string>("searchTerm");
            searchProducts.ReturnsCollectionFromEntitySet<Product>("Products");
            
            // Add a singleton for store information
            builder.Singleton<StoreInfo>("Store");
            
            builder.Namespace = "SampleService";
            return builder.GetEdmModel();
        }
    }
    
    /// <summary>
    /// Represents inventory status information.
    /// </summary>
    public class InventoryStatus
    {
        /// <summary>
        /// Gets or sets the product ID.
        /// </summary>
        public int ProductId { get; set; }
        
        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the current stock level.
        /// </summary>
        public int CurrentStock { get; set; }
        
        /// <summary>
        /// Gets or sets the reorder level.
        /// </summary>
        public int ReorderLevel { get; set; }
        
        /// <summary>
        /// Gets or sets whether reorder is needed.
        /// </summary>
        public bool NeedsReorder { get; set; }
    }
    
    /// <summary>
    /// Represents store information singleton.
    /// </summary>
    public class StoreInfo
    {
        /// <summary>
        /// Gets or sets the store name.
        /// </summary>
        public string Name { get; set; } = "Sample OData Store";
        
        /// <summary>
        /// Gets or sets the store version.
        /// </summary>
        public string Version { get; set; } = "1.0.0";
        
        /// <summary>
        /// Gets or sets the store description.
        /// </summary>
        public string Description { get; set; } = "A sample OData service demonstrating MCP integration";
        
        /// <summary>
        /// Gets or sets the store established date.
        /// </summary>
        public DateTimeOffset EstablishedDate { get; set; } = DateTimeOffset.UtcNow;
    }
}