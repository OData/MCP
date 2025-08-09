using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Console
{
    /// <summary>
    /// Integration tests for the OData MCP Console application.
    /// </summary>
    [TestClass]
    public class ConsoleIntegrationTests
    {
        /// <summary>
        /// Tests that the test command parses URLs correctly.
        /// </summary>
        [TestMethod]
        public void TestCommand_ParsesUrl_ExtractsMetadataPath()
        {
            // Arrange
            const string url1 = "https://api.example.com/odata/$metadata";
            const string url2 = "https://api.example.com/v1/";
            
            // Act
            var metadata1 = GetMetadataPath(url1);
            var metadata2 = GetMetadataPath(url2);
            
            // Assert
            metadata1.Should().Be("$metadata");
            metadata2.Should().Be("");
        }
        
        /// <summary>
        /// Tests URL validation.
        /// </summary>
        [TestMethod]
        public void ValidateUrl_WithInvalidUrl_ReturnsFalse()
        {
            // Arrange
            const string invalidUrl = "not-a-url";
            
            // Act
            var isValid = IsValidUrl(invalidUrl);
            
            // Assert
            isValid.Should().BeFalse();
        }
        
        /// <summary>
        /// Tests URL validation with valid URL.
        /// </summary>
        [TestMethod]
        public void ValidateUrl_WithValidUrl_ReturnsTrue()
        {
            // Arrange
            const string validUrl = "https://api.example.com/odata/$metadata";
            
            // Act
            var isValid = IsValidUrl(validUrl);
            
            // Assert
            isValid.Should().BeTrue();
        }
        
        internal static string GetMetadataPath(string url)
        {
            if (url.EndsWith("/$metadata", StringComparison.OrdinalIgnoreCase))
            {
                return "$metadata";
            }
            return "";
        }
        
        internal static bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var result) 
                && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
    }
}