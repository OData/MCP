// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core
{
    /// <summary>
    /// Integration tests for Core project functionality.
    /// </summary>
    [TestClass]
    public class CoreIntegrationTests
    {
        /// <summary>
        /// Tests that Core services can be registered and resolved.
        /// </summary>
        [TestMethod]
        public void Core_ServiceRegistration_ShouldWork()
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
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetService<ICsdlMetadataParser>().Should().NotBeNull();
            serviceProvider.GetService<IMcpToolFactory>().Should().NotBeNull();
        }

        /// <summary>
        /// Tests that configuration is properly bound.
        /// </summary>
        [TestMethod]
        public void Core_Configuration_ShouldBind()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["McpServer:ODataService:BaseUrl"] = "https://configured.test.com",
                    ["McpServer:ODataService:RequestTimeout"] = "00:05:00",
                    ["McpServer:ServerInfo:Name"] = "Test Server",
                    ["McpServer:ServerInfo:Version"] = "2.0.0"
                })
                .Build();

            // Act
            services.AddODataMcpCore(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<IOptions<McpServerConfiguration>>();

            // Assert
            options.Value.Should().NotBeNull();
            options.Value.ODataService.BaseUrl.Should().Be("https://configured.test.com");
            options.Value.ODataService.RequestTimeout.Should().Be(TimeSpan.FromMinutes(5));
            options.Value.ServerInfo.Name.Should().Be("Test Server");
            options.Value.ServerInfo.Version.Should().Be("2.0.0");
        }
    }
}
