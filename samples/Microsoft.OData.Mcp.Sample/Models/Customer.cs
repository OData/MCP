// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.OData.Mcp.Sample.Models
{
    /// <summary>
    /// Represents a customer in the sample OData service.
    /// </summary>
    public class Customer
    {
        /// <summary>
        /// Gets or sets the customer ID.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the customer's name.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the customer's email address.
        /// </summary>
        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the customer's phone number.
        /// </summary>
        [Phone]
        [MaxLength(20)]
        public string? Phone { get; set; }

        /// <summary>
        /// Gets or sets the customer's address.
        /// </summary>
        [MaxLength(500)]
        public string? Address { get; set; }

        /// <summary>
        /// Gets or sets the customer's city.
        /// </summary>
        [MaxLength(100)]
        public string? City { get; set; }

        /// <summary>
        /// Gets or sets the customer's country.
        /// </summary>
        [MaxLength(100)]
        public string? Country { get; set; }

        /// <summary>
        /// Gets or sets the date when the customer was created.
        /// </summary>
        public DateTimeOffset CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the customer's credit limit.
        /// </summary>
        public decimal CreditLimit { get; set; }

        /// <summary>
        /// Gets or sets whether the customer is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the orders placed by this customer.
        /// </summary>
        public ICollection<Order> Orders { get; set; } = [];
    }
}
