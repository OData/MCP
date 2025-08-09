using FluentAssertions;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Configuration
{
    /// <summary>
    /// Comprehensive tests for the McpServerConfiguration class.
    /// </summary>
    [TestClass]
    public class McpServerConfigurationTests
    {

        #region Constructor Tests

        /// <summary>
        /// Tests that default constructor initializes all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Default_InitializesAllProperties()
        {
            var config = new McpServerConfiguration();

            config.DeploymentMode.Should().Be(McpDeploymentMode.Sidecar);
            config.ServerInfo.Should().NotBeNull();
            config.ODataService.Should().NotBeNull();
            config.Authentication.Should().NotBeNull();
            config.ToolGeneration.Should().NotBeNull();
            config.Network.Should().NotBeNull();
            config.Caching.Should().NotBeNull();
            config.Monitoring.Should().NotBeNull();
            config.Security.Should().NotBeNull();
            config.FeatureFlags.Should().NotBeNull();
            config.CustomProperties.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region ForSidecar Tests

        /// <summary>
        /// Tests that ForSidecar creates properly configured instance.
        /// </summary>
        [TestMethod]
        public void ForSidecar_ValidUrl_CreatesProperConfiguration()
        {
            const string testUrl = "https://api.example.com/odata";

            var config = McpServerConfiguration.ForSidecar(testUrl);

            config.DeploymentMode.Should().Be(McpDeploymentMode.Sidecar);
            config.ServerInfo.Name.Should().Be("OData MCP Sidecar");
            config.ServerInfo.Description.Should().Be("Sidecar MCP server for OData service");
            config.ServerInfo.Version.Should().Be("1.0.0");
            
            config.ODataService.BaseUrl.Should().Be(testUrl);
            config.ODataService.MetadataPath.Should().Be("/$metadata");
            config.ODataService.AutoDiscoverMetadata.Should().BeTrue();
            config.ODataService.RefreshInterval.Should().Be(TimeSpan.FromMinutes(30));
            
            config.Network.Port.Should().Be(8080);
            config.Network.Host.Should().Be("localhost");
            config.Network.BasePath.Should().Be("/mcp");
            config.Network.EnableHttps.Should().BeFalse();
            
            config.Authentication.Enabled.Should().BeFalse(); // Authentication disabled by default for simplicity
            config.Caching.Enabled.Should().BeTrue();
            config.Caching.MetadataTtl.Should().Be(TimeSpan.FromHours(1));
            config.Caching.ToolsTtl.Should().Be(TimeSpan.FromHours(2));
        }

        /// <summary>
        /// Tests that ForSidecar throws ArgumentException for null URL.
        /// </summary>
        [TestMethod]
        public void ForSidecar_NullUrl_ThrowsArgumentException()
        {
            var act = () => McpServerConfiguration.ForSidecar(null!);

            act.Should().Throw<ArgumentException>()
                .WithParameterName("odataServiceUrl");
        }

        /// <summary>
        /// Tests that ForSidecar throws ArgumentException for empty URL.
        /// </summary>
        [TestMethod]
        public void ForSidecar_EmptyUrl_ThrowsArgumentException()
        {
            var act = () => McpServerConfiguration.ForSidecar("");

            act.Should().Throw<ArgumentException>()
                .WithParameterName("odataServiceUrl");
        }

        /// <summary>
        /// Tests that ForSidecar throws ArgumentException for whitespace URL.
        /// </summary>
        [TestMethod]
        public void ForSidecar_WhitespaceUrl_ThrowsArgumentException()
        {
            var act = () => McpServerConfiguration.ForSidecar("   ");

            act.Should().Throw<ArgumentException>()
                .WithParameterName("odataServiceUrl");
        }

        #endregion

        #region ForMiddleware Tests

        /// <summary>
        /// Tests that ForMiddleware creates properly configured instance with default path.
        /// </summary>
        [TestMethod]
        public void ForMiddleware_DefaultPath_CreatesProperConfiguration()
        {
            var config = McpServerConfiguration.ForMiddleware();

            config.DeploymentMode.Should().Be(McpDeploymentMode.Middleware);
            config.ServerInfo.Name.Should().Be("OData MCP Middleware");
            config.ServerInfo.Description.Should().Be("Embedded MCP server middleware");
            config.ServerInfo.Version.Should().Be("1.0.0");
            
            config.ODataService.MetadataPath.Should().Be("/$metadata");
            config.ODataService.AutoDiscoverMetadata.Should().BeTrue();
            config.ODataService.RefreshInterval.Should().Be(TimeSpan.FromMinutes(15));
            config.ODataService.UseHostContext.Should().BeTrue();
            
            config.Network.BasePath.Should().Be("/mcp");
            config.Network.UseHostConfiguration.Should().BeTrue();
            
            config.Authentication.Enabled.Should().BeFalse(); // Authentication disabled by default for simplicity
            config.Caching.Enabled.Should().BeTrue();
            config.Caching.MetadataTtl.Should().Be(TimeSpan.FromMinutes(30));
            config.Caching.ToolsTtl.Should().Be(TimeSpan.FromHours(1));
        }

        /// <summary>
        /// Tests that ForMiddleware creates properly configured instance with custom path.
        /// </summary>
        [TestMethod]
        public void ForMiddleware_CustomPath_CreatesProperConfiguration()
        {
            const string customPath = "/api/mcp";

            var config = McpServerConfiguration.ForMiddleware(customPath);

            config.DeploymentMode.Should().Be(McpDeploymentMode.Middleware);
            config.Network.BasePath.Should().Be(customPath);
            config.Network.UseHostConfiguration.Should().BeTrue();
        }

        #endregion

        #region ForDevelopment Tests

        /// <summary>
        /// Tests that ForDevelopment creates properly configured middleware instance.
        /// </summary>
        [TestMethod]
        public void ForDevelopment_DefaultMiddleware_CreatesProperConfiguration()
        {
            var config = McpServerConfiguration.ForDevelopment();

            config.DeploymentMode.Should().Be(McpDeploymentMode.Middleware);
            config.Authentication.Enabled.Should().BeFalse();
            config.Security.RequireHttps.Should().BeFalse();
            config.Security.EnableDetailedErrors.Should().BeTrue();
            config.Monitoring.LogLevel.Should().Be("Debug");
            config.Caching.MetadataTtl.Should().Be(TimeSpan.FromMinutes(5));
            config.ODataService.RefreshInterval.Should().Be(TimeSpan.FromMinutes(2));
            config.FeatureFlags.EnableDevelopmentEndpoints.Should().BeTrue();
        }

        /// <summary>
        /// Tests that ForDevelopment creates properly configured sidecar instance.
        /// </summary>
        [TestMethod]
        public void ForDevelopment_Sidecar_CreatesProperConfiguration()
        {
            var config = McpServerConfiguration.ForDevelopment(McpDeploymentMode.Sidecar);

            config.DeploymentMode.Should().Be(McpDeploymentMode.Sidecar);
            config.ODataService.BaseUrl.Should().Be("http://localhost:5000");
            config.Authentication.Enabled.Should().BeFalse();
            config.Security.RequireHttps.Should().BeFalse();
            config.Security.EnableDetailedErrors.Should().BeTrue();
            config.Monitoring.LogLevel.Should().Be("Debug");
            config.Caching.MetadataTtl.Should().Be(TimeSpan.FromMinutes(5));
            config.ODataService.RefreshInterval.Should().Be(TimeSpan.FromMinutes(2));
            config.FeatureFlags.EnableDevelopmentEndpoints.Should().BeTrue();
        }

        #endregion

        #region ForProduction Tests

        /// <summary>
        /// Tests that ForProduction creates properly configured sidecar instance.
        /// </summary>
        [TestMethod]
        public void ForProduction_DefaultSidecar_CreatesProperConfiguration()
        {
            var config = McpServerConfiguration.ForProduction();

            config.DeploymentMode.Should().Be(McpDeploymentMode.Sidecar);
            config.ODataService.BaseUrl.Should().Be("https://api.example.com");
            config.Authentication.Enabled.Should().BeFalse(); // Authentication disabled by default for simplicity
            config.Security.RequireHttps.Should().BeTrue();
            config.Security.EnableDetailedErrors.Should().BeFalse();
            config.Security.EnableRateLimiting.Should().BeTrue();
            config.Monitoring.LogLevel.Should().Be("Warning");
            config.Monitoring.EnableMetrics.Should().BeTrue();
            config.Monitoring.EnableHealthChecks.Should().BeTrue();
            config.Caching.MetadataTtl.Should().Be(TimeSpan.FromHours(4));
            config.Caching.ToolsTtl.Should().Be(TimeSpan.FromHours(8));
            config.ODataService.RefreshInterval.Should().Be(TimeSpan.FromHours(2));
        }

        /// <summary>
        /// Tests that ForProduction creates properly configured middleware instance.
        /// </summary>
        [TestMethod]
        public void ForProduction_Middleware_CreatesProperConfiguration()
        {
            var config = McpServerConfiguration.ForProduction(McpDeploymentMode.Middleware);

            config.DeploymentMode.Should().Be(McpDeploymentMode.Middleware);
            config.Authentication.Enabled.Should().BeFalse(); // Authentication disabled by default for simplicity
            config.Security.RequireHttps.Should().BeTrue();
            config.Security.EnableDetailedErrors.Should().BeFalse();
            config.Security.EnableRateLimiting.Should().BeTrue();
            config.Monitoring.LogLevel.Should().Be("Warning");
            config.Monitoring.EnableMetrics.Should().BeTrue();
            config.Monitoring.EnableHealthChecks.Should().BeTrue();
        }

        #endregion

        #region Validation Tests

        /// <summary>
        /// Tests that Validate returns no errors for valid sidecar configuration.
        /// </summary>
        [TestMethod]
        public void Validate_ValidSidecarConfiguration_ReturnsNoErrors()
        {
            var config = McpServerConfiguration.ForSidecar("https://api.example.com");

            var errors = config.Validate();

            errors.Should().BeEmpty();
        }

        /// <summary>
        /// Tests that Validate returns no errors for valid middleware configuration.
        /// </summary>
        [TestMethod]
        public void Validate_ValidMiddlewareConfiguration_ReturnsNoErrors()
        {
            var config = McpServerConfiguration.ForMiddleware();

            var errors = config.Validate();

            errors.Should().BeEmpty();
        }

        /// <summary>
        /// Tests that Validate returns error for sidecar configuration without BaseUrl.
        /// </summary>
        [TestMethod]
        public void Validate_SidecarWithoutBaseUrl_ReturnsError()
        {
            var config = new McpServerConfiguration
            {
                DeploymentMode = McpDeploymentMode.Sidecar,
                ODataService = new ODataServiceConfiguration { BaseUrl = "" }
            };

            var errors = config.Validate().ToList();

            errors.Should().Contain(e => e.Contains("BaseUrl is required for sidecar deployment mode"));
        }

        /// <summary>
        /// Tests that Validate returns error for sidecar with UseHostConfiguration.
        /// </summary>
        [TestMethod]
        public void Validate_SidecarWithUseHostConfiguration_ReturnsError()
        {
            var config = McpServerConfiguration.ForSidecar("https://api.example.com");
            config.Network.UseHostConfiguration = true;

            var errors = config.Validate().ToList();

            errors.Should().Contain(e => e.Contains("UseHostConfiguration should be false for sidecar deployment mode"));
        }

        /// <summary>
        /// Tests that Validate calls all sub-configuration validation methods.
        /// </summary>
        [TestMethod]
        public void Validate_CallsAllSubConfigurationValidation()
        {
            var config = new McpServerConfiguration();

            var errors = config.Validate().ToList();

            // Should have validation errors from all sub-configurations
            errors.Should().NotBeEmpty("validation should call all sub-configuration validation methods");
        }

        #endregion

        #region ApplyEnvironmentOverrides Tests

        /// <summary>
        /// Tests that ApplyEnvironmentOverrides handles null environment gracefully.
        /// </summary>
        [TestMethod]
        public void ApplyEnvironmentOverrides_NullEnvironment_DoesNotThrow()
        {
            var config = new McpServerConfiguration();

            var act = () => config.ApplyEnvironmentOverrides(null!);

            act.Should().NotThrow();
        }

        /// <summary>
        /// Tests that ApplyEnvironmentOverrides handles empty environment gracefully.
        /// </summary>
        [TestMethod]
        public void ApplyEnvironmentOverrides_EmptyEnvironment_DoesNotThrow()
        {
            var config = new McpServerConfiguration();

            var act = () => config.ApplyEnvironmentOverrides("");

            act.Should().NotThrow();
        }

        /// <summary>
        /// Tests that ApplyEnvironmentOverrides applies development overrides.
        /// </summary>
        [TestMethod]
        public void ApplyEnvironmentOverrides_Development_AppliesDevelopmentSettings()
        {
            var config = McpServerConfiguration.ForProduction();
            config.Authentication.Enabled = true;
            config.Security.RequireHttps = true;

            config.ApplyEnvironmentOverrides("Development");

            config.Authentication.Enabled.Should().BeFalse();
            config.Security.RequireHttps.Should().BeFalse();
            config.Security.EnableDetailedErrors.Should().BeTrue();
            config.Monitoring.LogLevel.Should().Be("Debug");
            config.Caching.MetadataTtl.Should().Be(TimeSpan.FromMinutes(5));
            config.FeatureFlags.EnableDevelopmentEndpoints.Should().BeTrue();
        }

        /// <summary>
        /// Tests that ApplyEnvironmentOverrides applies production overrides.
        /// </summary>
        [TestMethod]
        public void ApplyEnvironmentOverrides_Production_AppliesProductionSettings()
        {
            var config = McpServerConfiguration.ForDevelopment();

            config.ApplyEnvironmentOverrides("Production");

            config.Authentication.Enabled.Should().BeFalse(); // Authentication disabled by default for simplicity
            config.Security.RequireHttps.Should().BeTrue();
            config.Security.EnableDetailedErrors.Should().BeFalse();
            config.Security.EnableRateLimiting.Should().BeTrue();
            config.Monitoring.LogLevel.Should().Be("Warning");
            config.Monitoring.EnableMetrics.Should().BeTrue();
            config.Monitoring.EnableHealthChecks.Should().BeTrue();
            config.Caching.MetadataTtl.Should().Be(TimeSpan.FromHours(4));
        }

        /// <summary>
        /// Tests that ApplyEnvironmentOverrides is case insensitive.
        /// </summary>
        [TestMethod]
        public void ApplyEnvironmentOverrides_CaseInsensitive_AppliesCorrectSettings()
        {
            var config = McpServerConfiguration.ForProduction();

            config.ApplyEnvironmentOverrides("DEVELOPMENT");

            config.Authentication.Enabled.Should().BeFalse();
            config.Security.RequireHttps.Should().BeFalse();
        }

        #endregion

        #region GetStatistics Tests

        /// <summary>
        /// Tests that GetStatistics returns comprehensive statistics.
        /// </summary>
        [TestMethod]
        public void GetStatistics_ReturnsComprehensiveStatistics()
        {
            var config = McpServerConfiguration.ForSidecar("https://api.example.com");
            config.CustomProperties["TestKey"] = "TestValue";

            var stats = config.GetStatistics();

            stats.Should().ContainKey("DeploymentMode");
            stats.Should().ContainKey("ServerName");
            stats.Should().ContainKey("ServerVersion");
            stats.Should().ContainKey("AuthenticationEnabled");
            stats.Should().ContainKey("CachingEnabled");
            stats.Should().ContainKey("MetadataAutoDiscovery");
            stats.Should().ContainKey("ToolGenerationMode");
            stats.Should().ContainKey("NetworkPort");
            stats.Should().ContainKey("NetworkBasePath");
            stats.Should().ContainKey("SecurityHttpsRequired");
            stats.Should().ContainKey("MonitoringEnabled");
            stats.Should().ContainKey("CustomPropertiesCount");

            stats["DeploymentMode"].Should().Be("Sidecar");
            stats["ServerName"].Should().Be("OData MCP Sidecar");
            stats["CustomPropertiesCount"].Should().Be(1);
        }

        #endregion

        #region Clone Tests

        /// <summary>
        /// Tests that Clone creates deep copy with same values.
        /// </summary>
        [TestMethod]
        public void Clone_CreatesDeepCopyWithSameValues()
        {
            var original = McpServerConfiguration.ForSidecar("https://api.example.com");
            original.CustomProperties["TestKey"] = "TestValue";

            var clone = original.Clone();

            clone.Should().NotBeSameAs(original);
            clone.DeploymentMode.Should().Be(original.DeploymentMode);
            clone.ServerInfo.Should().NotBeSameAs(original.ServerInfo);
            clone.ServerInfo.Name.Should().Be(original.ServerInfo.Name);
            clone.ODataService.Should().NotBeSameAs(original.ODataService);
            clone.ODataService.BaseUrl.Should().Be(original.ODataService.BaseUrl);
            clone.CustomProperties.Should().NotBeSameAs(original.CustomProperties);
            clone.CustomProperties["TestKey"].Should().Be("TestValue");
        }

        #endregion

        #region MergeWith Tests

        /// <summary>
        /// Tests that MergeWith throws ArgumentNullException for null parameter.
        /// </summary>
        [TestMethod]
        public void MergeWith_NullOther_ThrowsArgumentNullException()
        {
            var config = new McpServerConfiguration();

            var act = () => config.MergeWith(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("other");
        }

        /// <summary>
        /// Tests that MergeWith merges configurations correctly.
        /// </summary>
        [TestMethod]
        public void MergeWith_ValidConfiguration_MergesCorrectly()
        {
            var config1 = McpServerConfiguration.ForSidecar("https://api1.example.com");
            config1.CustomProperties["Key1"] = "Value1";

            var config2 = McpServerConfiguration.ForSidecar("https://api2.example.com");
            config2.CustomProperties["Key2"] = "Value2";
            config2.CustomProperties["Key1"] = "OverriddenValue1";

            config1.MergeWith(config2);

            config1.ODataService.BaseUrl.Should().Be("https://api2.example.com");
            config1.CustomProperties["Key1"].Should().Be("OverriddenValue1");
            config1.CustomProperties["Key2"].Should().Be("Value2");
        }

        #endregion

        #region Custom Properties Tests

        /// <summary>
        /// Tests that CustomProperties can be modified.
        /// </summary>
        [TestMethod]
        public void CustomProperties_CanBeModified()
        {
            var config = new McpServerConfiguration();

            config.CustomProperties["TestKey"] = "TestValue";
            config.CustomProperties["NumericKey"] = 42;

            config.CustomProperties.Should().HaveCount(2);
            config.CustomProperties["TestKey"].Should().Be("TestValue");
            config.CustomProperties["NumericKey"].Should().Be(42);
        }

        #endregion
    }
}
