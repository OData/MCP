using Microsoft.OData.Mcp.Tests.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Tests.Shared.Data
{

    /// <summary>
    /// Provides test data for various entities.
    /// </summary>
    public static class TestDataProviders
    {

        #region Public Methods

        /// <summary>
        /// Gets a list of test customers.
        /// </summary>
        public static List<Customer> GetCustomers()
        {
            return new List<Customer>
            {
                new Customer
                {
                    CustomerId = 1,
                    CompanyName = "Contoso Ltd",
                    ContactName = "John Smith",
                    Email = "john.smith@contoso.com",
                    Phone = "555-0100",
                    City = "Seattle",
                    Country = "USA"
                },
                new Customer
                {
                    CustomerId = 2,
                    CompanyName = "Fabrikam Inc",
                    ContactName = "Jane Doe",
                    Email = "jane.doe@fabrikam.com",
                    Phone = "555-0101",
                    City = "New York",
                    Country = "USA"
                },
                new Customer
                {
                    CustomerId = 3,
                    CompanyName = "Adventure Works",
                    ContactName = "Bob Johnson",
                    Email = "bob@adventure-works.com",
                    Phone = "555-0102",
                    City = "London",
                    Country = "UK"
                },
                new Customer
                {
                    CustomerId = 4,
                    CompanyName = "北京科技", // Beijing Technology in Chinese
                    ContactName = "李明", // Li Ming
                    Email = "liming@beijingtech.cn",
                    Phone = "86-10-12345678",
                    City = "北京",
                    Country = "China"
                },
                new Customer
                {
                    CustomerId = 5,
                    CompanyName = "مؤسسة التقنية", // Technology Foundation in Arabic
                    ContactName = "أحمد محمد", // Ahmed Mohammed
                    Email = "ahmed@techfound.ae",
                    Phone = "971-4-1234567",
                    City = "دبي", // Dubai
                    Country = "UAE"
                }
            };
        }

        /// <summary>
        /// Gets a list of test edge case entities.
        /// </summary>
        public static List<客戶> GetEdgeCaseEntities()
        {
            return new List<客戶>
            {
                new 客戶
                {
                    Id = 1,
                    Name = "Normal Name",
                    名前 = "田中太郎", // Tanaka Taro in Japanese
                    العربية = "محمد", // Mohammed in Arabic
                    EmojiProperty = "Party! 🎉",
                    VeryLongPropertyNameThatExceedsNormalLimitsAndTestsHowTheSystemHandlesExtremelyLongIdentifiers = 
                        "This is a value for a very long property name"
                },
                new 客戶
                {
                    Id = 2,
                    Name = "Special Chars !@#$%^&*()",
                    名前 = "山田花子",
                    العربية = "فاطمة",
                    EmojiProperty = "🎊🎈🎁",
                    VeryLongPropertyNameThatExceedsNormalLimitsAndTestsHowTheSystemHandlesExtremelyLongIdentifiers = 
                        "Another value with special chars: < > & \" ' / \\ | ` ~"
                }
            };
        }

        /// <summary>
        /// Gets a list of test employees with hierarchical relationships.
        /// </summary>
        public static List<Employee> GetEmployees()
        {
            var ceo = new Employee
            {
                PersonId = 1,
                EmployeeNumber = 1001,
                FirstName = "Alice",
                LastName = "CEO",
                Email = "alice@company.com",
                BirthDate = new DateTime(1970, 1, 1),
                HireDate = new DateTime(2010, 1, 1),
                Department = "Executive",
                Title = "Chief Executive Officer",
                Salary = 500000,
                Address = new Address
                {
                    Street = "1 Executive Way",
                    City = "Seattle",
                    State = "WA",
                    PostalCode = "98101",
                    Country = "USA"
                }
            };

            var manager1 = new Employee
            {
                PersonId = 2,
                EmployeeNumber = 1002,
                FirstName = "Bob",
                LastName = "Manager",
                Email = "bob@company.com",
                BirthDate = new DateTime(1975, 5, 15),
                HireDate = new DateTime(2012, 3, 1),
                Department = "Engineering",
                Title = "Engineering Manager",
                Salary = 150000,
                Manager = ceo,
                ManagerId = 1
            };

            var manager2 = new Employee
            {
                PersonId = 3,
                EmployeeNumber = 1003,
                FirstName = "Carol",
                LastName = "Manager",
                Email = "carol@company.com",
                BirthDate = new DateTime(1980, 8, 20),
                HireDate = new DateTime(2013, 6, 1),
                Department = "Sales",
                Title = "Sales Manager",
                Salary = 140000,
                Manager = ceo,
                ManagerId = 1
            };

            ceo.Reports = new List<Employee> { manager1, manager2 };

            return new List<Employee> { ceo, manager1, manager2 };
        }

        /// <summary>
        /// Gets a large dataset for performance testing.
        /// </summary>
        public static List<Customer> GetLargeDataset(int count = 1000)
        {
            var customers = new List<Customer>();
            var cities = new[] { "Seattle", "New York", "London", "Tokyo", "Sydney" };
            var countries = new[] { "USA", "UK", "Japan", "Australia", "Canada" };
            
            for (int i = 1; i <= count; i++)
            {
                customers.Add(new Customer
                {
                    CustomerId = i,
                    CompanyName = $"Company {i}",
                    ContactName = $"Contact {i}",
                    Email = $"contact{i}@company{i}.com",
                    Phone = $"555-{i:D4}",
                    City = cities[i % cities.Length],
                    Country = countries[i % countries.Length]
                });
            }
            
            return customers;
        }

        /// <summary>
        /// Gets a list of test orders.
        /// </summary>
        public static List<Order> GetOrders()
        {
            return new List<Order>
            {
                new Order
                {
                    OrderId = 1,
                    CustomerId = 1,
                    OrderDate = DateTime.Now.AddDays(-30),
                    OrderAmount = 1500.00m,
                    Status = "Shipped"
                },
                new Order
                {
                    OrderId = 2,
                    CustomerId = 1,
                    OrderDate = DateTime.Now.AddDays(-15),
                    OrderAmount = 2500.00m,
                    Status = "Processing"
                },
                new Order
                {
                    OrderId = 3,
                    CustomerId = 2,
                    OrderDate = DateTime.Now.AddDays(-7),
                    OrderAmount = 750.00m,
                    Status = "Delivered"
                },
                new Order
                {
                    OrderId = 4,
                    CustomerId = 3,
                    OrderDate = DateTime.Now.AddDays(-2),
                    OrderAmount = 3200.00m,
                    Status = "Pending"
                }
            };
        }

        /// <summary>
        /// Gets a list of test products.
        /// </summary>
        public static List<Product> GetProducts()
        {
            return new List<Product>
            {
                new Product
                {
                    ProductId = 1,
                    ProductName = "Widget",
                    Description = "Standard widget",
                    Category = "Hardware",
                    Price = 19.99m,
                    InStock = true
                },
                new Product
                {
                    ProductId = 2,
                    ProductName = "Gadget",
                    Description = "Premium gadget",
                    Category = "Electronics",
                    Price = 99.99m,
                    InStock = true
                },
                new Product
                {
                    ProductId = 3,
                    ProductName = "Tool",
                    Description = "Professional tool",
                    Category = "Tools",
                    Price = 49.99m,
                    InStock = false
                },
                new Product
                {
                    ProductId = 4,
                    ProductName = "特別製品", // Special Product in Japanese
                    Description = "Unicode test product",
                    Category = "International",
                    Price = 299.99m,
                    InStock = true
                }
            };
        }

        /// <summary>
        /// Gets a list of VIP customers.
        /// </summary>
        public static List<VipCustomer> GetVipCustomers()
        {
            return new List<VipCustomer>
            {
                new VipCustomer
                {
                    CustomerId = 100,
                    CompanyName = "Premium Corp",
                    ContactName = "VIP Contact",
                    Email = "vip@premium.com",
                    Phone = "555-VIP1",
                    City = "Beverly Hills",
                    Country = "USA",
                    VipLevel = "Platinum",
                    MemberSince = DateTime.Now.AddYears(-5),
                    CreditLimit = 100000m,
                    LoyaltyPoints = 50000
                },
                new VipCustomer
                {
                    CustomerId = 101,
                    CompanyName = "Elite Industries",
                    ContactName = "Elite Contact",
                    Email = "contact@elite.com",
                    Phone = "555-VIP2",
                    City = "Monaco",
                    Country = "Monaco",
                    VipLevel = "Gold",
                    MemberSince = DateTime.Now.AddYears(-3),
                    CreditLimit = 50000m,
                    LoyaltyPoints = 25000
                }
            };
        }

        #endregion

    }
}