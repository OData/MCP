using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Extensions
{
    /// <summary>
    /// Tests for the ServiceCollectionExtensions class in Core.
    /// </summary>
    [TestClass]
    public class ServiceCollectionExtensionsTests
    {
        /// <summary>
        /// Tests that AddMcpServer registers required services.
        /// </summary>
        [TestMethod]
        public void AddMcpServer_ShouldRegisterRequiredServices()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services.AddMcpServer();

            // Assert
            builder.Should().NotBeNull();
            
            // Build service provider to verify registrations
            var serviceProvider = services.BuildServiceProvider();
            
            // Verify core services are registered
            serviceProvider.GetService<CsdlParser>().Should().NotBeNull();
        }

        /// <summary>Am I missing something
        /// Tests that AddMcpServer returns a builder that can be chained.
        /// </summary>
        [TestMethod]
        public void AddMcpServer_ShouldReturnChainableBuilder()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services.AddMcpServer()
                .WithStdioServerTransport();

            // Assert
            result.Should().NotBeNull();
        }

        /// <summary>
        /// Tests that multiple calls to AddMcpServer don't cause issues.
        /// </summary>
        [TestMethod]
        public void AddMcpServer_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert - MSTest doesn't have DoesNotThrow, so we test by not throwing
            services.AddMcpServer();
            services.AddMcpServer();
            
            // If we get here without exception, the test passes
            Assert.IsTrue(true);
        }
    }
}
