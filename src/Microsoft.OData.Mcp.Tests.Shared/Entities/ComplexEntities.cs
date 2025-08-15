// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Tests.Shared.Entities
{

    #region Base Types

    /// <summary>
    /// Base class for testing inheritance.
    /// </summary>
    public abstract class Person
    {

        #region Properties

        public Address? Address { get; set; }

        public DateTime BirthDate { get; set; }

        public string Email { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public int PersonId { get; set; }

        #endregion

    }

    #endregion

    #region Derived Types

    /// <summary>
    /// Employee entity derived from Person.
    /// </summary>
    public class Employee : Person
    {

        #region Properties

        public string Department { get; set; } = string.Empty;

        public int EmployeeNumber { get; set; }

        public DateTime HireDate { get; set; }

        public Employee? Manager { get; set; }

        public int? ManagerId { get; set; }

        public List<Employee> Reports { get; set; } = [];

        public decimal Salary { get; set; }

        public string Title { get; set; } = string.Empty;

        #endregion

    }

    /// <summary>
    /// VipCustomer entity derived from Customer.
    /// </summary>
    public class VipCustomer : Customer
    {

        #region Properties

        public decimal CreditLimit { get; set; }

        public int LoyaltyPoints { get; set; }

        public DateTime MemberSince { get; set; }

        public string VipLevel { get; set; } = string.Empty;

        #endregion

    }

    #endregion

    #region Complex Types

    /// <summary>
    /// Complex type for address.
    /// </summary>
    public class Address
    {

        #region Properties

        public string City { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string PostalCode { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public string Street { get; set; } = string.Empty;

        #endregion

    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Entity with Unicode name for testing edge cases.
    /// </summary>
    public class 客戶 // Customer in Chinese
    {

        #region Properties

        public string العربية { get; set; } = string.Empty; // Arabic property name

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string VeryLongPropertyNameThatExceedsNormalLimitsAndTestsHowTheSystemHandlesExtremelyLongIdentifiers { get; set; } = string.Empty;

        public string 名前 { get; set; } = string.Empty; // Japanese property name

        public string EmojiProperty { get; set; } = string.Empty; // Property for testing special characters in values

        #endregion

    }

    /// <summary>
    /// Entity with a very long name for testing.
    /// </summary>
    public class ThisIsAnExtremelyLongEntityNameThatIsDesignedToTestHowTheSystemHandlesVeryLongIdentifiersInVariousContexts
    {

        #region Properties

        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;

        #endregion

    }

    #endregion

}