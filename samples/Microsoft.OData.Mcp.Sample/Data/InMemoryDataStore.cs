using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Mcp.Sample.Models;

namespace Microsoft.OData.Mcp.Sample.Data
{
    /// <summary>
    /// Thread-safe in-memory data store for the sample OData service.
    /// </summary>
    public class InMemoryDataStore
    {
        internal readonly ConcurrentDictionary<int, Customer> _customers = new();
        internal readonly ConcurrentDictionary<int, Order> _orders = new();
        internal readonly ConcurrentDictionary<int, Product> _products = new();
        internal readonly ConcurrentDictionary<int, Category> _categories = new();
        internal readonly ConcurrentDictionary<int, OrderItem> _orderItems = new();

        internal int _customerIdCounter = 1;
        internal int _orderIdCounter = 1;
        internal int _productIdCounter = 1;
        internal int _categoryIdCounter = 1;
        internal int _orderItemIdCounter = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryDataStore"/> class.
        /// </summary>
        public InMemoryDataStore()
        {
            SeedData();
        }

        #region Properties

        /// <summary>
        /// Gets the customers collection.
        /// </summary>
        public IQueryable<Customer> Customers => _customers.Values.AsQueryable();

        /// <summary>
        /// Gets the orders collection.
        /// </summary>
        public IQueryable<Order> Orders => _orders.Values.AsQueryable();

        /// <summary>
        /// Gets the products collection.
        /// </summary>
        public IQueryable<Product> Products => _products.Values.AsQueryable();

        /// <summary>
        /// Gets the categories collection.
        /// </summary>
        public IQueryable<Category> Categories => _categories.Values.AsQueryable();

        /// <summary>
        /// Gets the order items collection.
        /// </summary>
        public IQueryable<OrderItem> OrderItems => _orderItems.Values.AsQueryable();

        #endregion

        #region CRUD Operations

        /// <summary>
        /// Adds a new customer.
        /// </summary>
        public Customer AddCustomer(Customer customer)
        {
            customer.Id = _customerIdCounter++;
            customer.CreatedDate = DateTimeOffset.UtcNow;
            _customers[customer.Id] = customer;
            return customer;
        }

        /// <summary>
        /// Updates an existing customer.
        /// </summary>
        public bool UpdateCustomer(Customer customer)
        {
            return _customers.TryUpdate(customer.Id, customer, _customers[customer.Id]);
        }

        /// <summary>
        /// Deletes a customer.
        /// </summary>
        public bool DeleteCustomer(int id)
        {
            return _customers.TryRemove(id, out _);
        }

        /// <summary>
        /// Gets a customer by ID.
        /// </summary>
        public Customer? GetCustomer(int id)
        {
            return _customers.TryGetValue(id, out var customer) ? customer : null;
        }

        /// <summary>
        /// Adds a new order.
        /// </summary>
        public Order AddOrder(Order order)
        {
            order.Id = _orderIdCounter++;
            order.OrderDate = DateTimeOffset.UtcNow;
            order.OrderNumber = $"ORD-{order.Id:D6}";
            _orders[order.Id] = order;
            return order;
        }

        /// <summary>
        /// Updates an existing order.
        /// </summary>
        public bool UpdateOrder(Order order)
        {
            return _orders.TryUpdate(order.Id, order, _orders[order.Id]);
        }

        /// <summary>
        /// Deletes an order.
        /// </summary>
        public bool DeleteOrder(int id)
        {
            return _orders.TryRemove(id, out _);
        }

        /// <summary>
        /// Gets an order by ID.
        /// </summary>
        public Order? GetOrder(int id)
        {
            return _orders.TryGetValue(id, out var order) ? order : null;
        }

        /// <summary>
        /// Adds a new product.
        /// </summary>
        public Product AddProduct(Product product)
        {
            product.Id = _productIdCounter++;
            _products[product.Id] = product;
            return product;
        }

        /// <summary>
        /// Updates an existing product.
        /// </summary>
        public bool UpdateProduct(Product product)
        {
            return _products.TryUpdate(product.Id, product, _products[product.Id]);
        }

        /// <summary>
        /// Deletes a product.
        /// </summary>
        public bool DeleteProduct(int id)
        {
            return _products.TryRemove(id, out _);
        }

        /// <summary>
        /// Gets a product by ID.
        /// </summary>
        public Product? GetProduct(int id)
        {
            return _products.TryGetValue(id, out var product) ? product : null;
        }

        /// <summary>
        /// Adds a new category.
        /// </summary>
        public Category AddCategory(Category category)
        {
            category.Id = _categoryIdCounter++;
            _categories[category.Id] = category;
            return category;
        }

        /// <summary>
        /// Gets a category by ID.
        /// </summary>
        public Category? GetCategory(int id)
        {
            return _categories.TryGetValue(id, out var category) ? category : null;
        }

        #endregion

        #region Seed Data

        internal void SeedData()
        {
            // Seed categories
            var electronics = AddCategory(new Category 
            { 
                Name = "Electronics", 
                Description = "Electronic devices and accessories" 
            });
            
            var books = AddCategory(new Category 
            { 
                Name = "Books", 
                Description = "Physical and digital books" 
            });
            
            var clothing = AddCategory(new Category 
            { 
                Name = "Clothing", 
                Description = "Apparel and fashion items" 
            });
            
            var home = AddCategory(new Category 
            { 
                Name = "Home & Garden", 
                Description = "Home improvement and garden supplies" 
            });

            // Seed products
            var laptop = AddProduct(new Product
            {
                Name = "Professional Laptop",
                Description = "High-performance laptop for professionals",
                Sku = "LAPTOP-001",
                UnitPrice = 1299.99m,
                UnitsInStock = 50,
                UnitsOnOrder = 10,
                ReorderLevel = 20,
                CategoryId = electronics.Id,
                ReleaseDate = DateTimeOffset.UtcNow.AddMonths(-6)
            });

            var smartphone = AddProduct(new Product
            {
                Name = "Flagship Smartphone",
                Description = "Latest model smartphone with advanced features",
                Sku = "PHONE-001",
                UnitPrice = 999.99m,
                UnitsInStock = 100,
                UnitsOnOrder = 0,
                ReorderLevel = 30,
                CategoryId = electronics.Id,
                ReleaseDate = DateTimeOffset.UtcNow.AddMonths(-2)
            });

            var novel = AddProduct(new Product
            {
                Name = "Bestselling Novel",
                Description = "Award-winning fiction novel",
                Sku = "BOOK-001",
                UnitPrice = 24.99m,
                UnitsInStock = 200,
                UnitsOnOrder = 50,
                ReorderLevel = 50,
                CategoryId = books.Id
            });

            var tshirt = AddProduct(new Product
            {
                Name = "Premium Cotton T-Shirt",
                Description = "100% organic cotton t-shirt",
                Sku = "TSHIRT-001",
                UnitPrice = 29.99m,
                UnitsInStock = 150,
                UnitsOnOrder = 0,
                ReorderLevel = 40,
                CategoryId = clothing.Id
            });

            var toolkit = AddProduct(new Product
            {
                Name = "Professional Tool Kit",
                Description = "Complete home improvement tool kit",
                Sku = "TOOLS-001",
                UnitPrice = 149.99m,
                UnitsInStock = 30,
                UnitsOnOrder = 10,
                ReorderLevel = 15,
                CategoryId = home.Id
            });

            // Seed customers
            var customer1 = AddCustomer(new Customer
            {
                Name = "Contoso Corporation",
                Email = "contact@contoso.com",
                Phone = "+1-555-0100",
                Address = "1 Microsoft Way",
                City = "Redmond",
                Country = "USA",
                CreditLimit = 10000m,
                IsActive = true
            });

            var customer2 = AddCustomer(new Customer
            {
                Name = "Adventure Works",
                Email = "info@adventureworks.com",
                Phone = "+1-555-0101",
                Address = "123 Adventure Lane",
                City = "Seattle",
                Country = "USA",
                CreditLimit = 25000m,
                IsActive = true
            });

            var customer3 = AddCustomer(new Customer
            {
                Name = "Northwind Traders",
                Email = "sales@northwind.com",
                Phone = "+1-555-0102",
                Address = "456 Commerce Street",
                City = "Portland",
                Country = "USA",
                CreditLimit = 15000m,
                IsActive = true
            });

            var customer4 = AddCustomer(new Customer
            {
                Name = "Wide World Importers",
                Email = "orders@wideworldimporters.com",
                Phone = "+44-20-5555-0100",
                Address = "789 Global Plaza",
                City = "London",
                Country = "UK",
                CreditLimit = 30000m,
                IsActive = true
            });

            // Seed orders
            var order1 = AddOrder(new Order
            {
                CustomerId = customer1.Id,
                Status = OrderStatus.Delivered,
                TotalAmount = 2299.98m,
                ShippingAddress = customer1.Address,
                ShipDate = DateTimeOffset.UtcNow.AddDays(-5)
            });

            // Add order items for order1
            _orderItems[_orderItemIdCounter] = new OrderItem
            {
                Id = _orderItemIdCounter++,
                OrderId = order1.Id,
                ProductId = laptop.Id,
                Quantity = 1,
                UnitPrice = laptop.UnitPrice,
                DiscountPercent = 0
            };

            _orderItems[_orderItemIdCounter] = new OrderItem
            {
                Id = _orderItemIdCounter++,
                OrderId = order1.Id,
                ProductId = smartphone.Id,
                Quantity = 1,
                UnitPrice = smartphone.UnitPrice,
                DiscountPercent = 0
            };

            var order2 = AddOrder(new Order
            {
                CustomerId = customer2.Id,
                Status = OrderStatus.Processing,
                TotalAmount = 179.97m,
                ShippingAddress = customer2.Address
            });

            // Add order items for order2
            _orderItems[_orderItemIdCounter] = new OrderItem
            {
                Id = _orderItemIdCounter++,
                OrderId = order2.Id,
                ProductId = tshirt.Id,
                Quantity = 6,
                UnitPrice = tshirt.UnitPrice,
                DiscountPercent = 0
            };

            var order3 = AddOrder(new Order
            {
                CustomerId = customer3.Id,
                Status = OrderStatus.Pending,
                TotalAmount = 174.98m,
                ShippingAddress = customer3.Address
            });

            // Add order items for order3
            _orderItems[_orderItemIdCounter] = new OrderItem
            {
                Id = _orderItemIdCounter++,
                OrderId = order3.Id,
                ProductId = novel.Id,
                Quantity = 5,
                UnitPrice = novel.UnitPrice,
                DiscountPercent = 10 // 10% discount
            };

            _orderItems[_orderItemIdCounter] = new OrderItem
            {
                Id = _orderItemIdCounter++,
                OrderId = order3.Id,
                ProductId = toolkit.Id,
                Quantity = 1,
                UnitPrice = toolkit.UnitPrice,
                DiscountPercent = 0
            };

            // Initialize ID counters after seeding
            _customerIdCounter = _customers.Keys.Max() + 1;
            _orderIdCounter = _orders.Keys.Max() + 1;
            _productIdCounter = _products.Keys.Max() + 1;
            _categoryIdCounter = _categories.Keys.Max() + 1;
            _orderItemIdCounter = _orderItems.Keys.Max() + 1;
        }

        #endregion
    }
}