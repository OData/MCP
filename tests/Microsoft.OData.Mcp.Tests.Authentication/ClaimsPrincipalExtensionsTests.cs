// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication
{
    /// <summary>
    /// Tests for ClaimsPrincipal extension methods.
    /// </summary>
    [TestClass]
    public class ClaimsPrincipalExtensionsTests
    {
        /// <summary>
        /// Tests that GetUserId returns the correct user ID from claims.
        /// </summary>
        [TestMethod]
        public void GetUserId_WithNameIdentifierClaim_ReturnsUserId()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user123"),
                new Claim(ClaimTypes.Name, "John Doe")
            };
            var identity = new ClaimsIdentity(claims, "test");
            var principal = new ClaimsPrincipal(identity);
            
            // Act
            var userId = principal.GetUserId();
            
            // Assert
            userId.Should().Be("user123");
        }
        
        /// <summary>
        /// Tests that GetUserId returns null when no user ID claim exists.
        /// </summary>
        [TestMethod]
        public void GetUserId_WithoutNameIdentifierClaim_ReturnsNull()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "John Doe")
            };
            var identity = new ClaimsIdentity(claims, "test");
            var principal = new ClaimsPrincipal(identity);
            
            // Act
            var userId = principal.GetUserId();
            
            // Assert
            userId.Should().BeNull();
        }
        
        /// <summary>
        /// Tests that GetUserName returns the correct user name from claims.
        /// </summary>
        [TestMethod]
        public void GetUserName_WithNameClaim_ReturnsUserName()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "John Doe"),
                new Claim(ClaimTypes.Email, "john@example.com")
            };
            var identity = new ClaimsIdentity(claims, "test");
            var principal = new ClaimsPrincipal(identity);
            
            // Act
            var userName = principal.GetUserName();
            
            // Assert
            userName.Should().Be("John Doe");
        }
        
        /// <summary>
        /// Tests that GetUserEmail returns the correct email from claims.
        /// </summary>
        [TestMethod]
        public void GetUserEmail_WithEmailClaim_ReturnsEmail()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, "john@example.com"),
                new Claim(ClaimTypes.Name, "John Doe")
            };
            var identity = new ClaimsIdentity(claims, "test");
            var principal = new ClaimsPrincipal(identity);
            
            // Act
            var email = principal.GetUserEmail();
            
            // Assert
            email.Should().Be("john@example.com");
        }
        
        /// <summary>
        /// Tests that GetUserRoles returns all roles from claims.
        /// </summary>
        [TestMethod]
        public void GetUserRoles_WithRoleClaims_ReturnsAllRoles()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(ClaimTypes.Role, "User"),
                new Claim(ClaimTypes.Name, "John Doe")
            };
            var identity = new ClaimsIdentity(claims, "test");
            var principal = new ClaimsPrincipal(identity);
            
            // Act
            var roles = principal.GetUserRoles();
            
            // Assert
            roles.Should().HaveCount(2);
            roles.Should().Contain("Admin");
            roles.Should().Contain("User");
        }
        
        /// <summary>
        /// Tests that GetUserRoles returns empty list when no roles exist.
        /// </summary>
        [TestMethod]
        public void GetUserRoles_WithoutRoleClaims_ReturnsEmptyList()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "John Doe")
            };
            var identity = new ClaimsIdentity(claims, "test");
            var principal = new ClaimsPrincipal(identity);
            
            // Act
            var roles = principal.GetUserRoles();
            
            // Assert
            roles.Should().BeEmpty();
        }
    }
}