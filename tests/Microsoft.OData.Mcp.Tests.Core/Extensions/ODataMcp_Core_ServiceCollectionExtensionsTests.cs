using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Server;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.OData.Mcp.Core.Tools.Generators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Extensions
{

    /// <summary>
    /// Tests for the <see cref="ODataMcp_Core_ServiceCollectionExtensions"/> class in Core.
    /// </summary>
    [TestClass]
    public class ODataMcp_Core_ServiceCollectionExtensionsTests
    {

        /// <summary>
        /// Tests that AddODataMcpServerCore registers required services.
        /// </summary>
        [TestMethod]
        public void AddODataMcpServerCore_WithConfiguration_ShouldRegisterRequiredServices()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["McpServer:ODataService:BaseUrl"] = "https://test.com",
                    ["McpServer:ODataService:RequestTimeout"] = "00:02:00"
                })
                .Build();

            // Act
            services.AddODataMcpCore(configuration);

            // Assert
            services.Should().NotBeNull();

            // Build service provider to verify registrations
            var serviceProvider = services.BuildServiceProvider();

            // Verify core services are registered
            serviceProvider.GetService<ICsdlMetadataParser>().Should().NotBeNull();
            serviceProvider.GetService<IMcpToolFactory>().Should().NotBeNull();
            serviceProvider.GetService<QueryToolGenerator>().Should().NotBeNull();
            serviceProvider.GetService<CrudToolGenerator>().Should().NotBeNull();
            serviceProvider.GetService<NavigationToolGenerator>().Should().NotBeNull();
            serviceProvider.GetService<ODataMcpTools>().Should().NotBeNull();
            serviceProvider.GetService<DynamicODataMcpTools>().Should().NotBeNull();
        }

        /// <summary>
        /// Tests that AddODataMcpServerCore with action configures services correctly.
        /// </summary>
        [TestMethod]
        public void AddODataMcpServerCore_WithAction_ShouldConfigureServices()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddODataMcpCore(options =>
            {
                options.ODataService.BaseUrl = "https://configured.com";
                options.ODataService.RequestTimeout = TimeSpan.FromMinutes(5);
            });

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<IOptions<McpServerConfiguration>>();

            options.Value.ODataService.BaseUrl.Should().Be("https://configured.com");
            options.Value.ODataService.RequestTimeout.Should().Be(TimeSpan.FromMinutes(5));
        }

    }

}
