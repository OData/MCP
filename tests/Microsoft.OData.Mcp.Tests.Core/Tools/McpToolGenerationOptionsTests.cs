using FluentAssertions;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Microsoft.OData.Mcp.Tests.Core.Tools
{
    /// <summary>
    /// Comprehensive tests for the McpToolGenerationOptions class.
    /// </summary>
    [TestClass]
    public class McpToolGenerationOptionsTests
    {

        #region Constructor Tests

        /// <summary>
        /// Tests that default constructor initializes all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Default_InitializesAllProperties()
        {
            var options = new McpToolGenerationOptions();

            options.OptimizeForPerformance.Should().BeFalse();
            options.IncludeDocumentation.Should().BeTrue();
            options.MaxToolsPerEntityType.Should().Be(50);
            options.EnableCaching.Should().BeTrue();
            options.GenerateExamples.Should().BeTrue();
            options.RequiredScopes.Should().NotBeNull().And.BeEmpty();
            options.RequiredRoles.Should().NotBeNull().And.BeEmpty();
            options.CustomProperties.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region Static Factory Methods Tests

        /// <summary>
        /// Tests that Default() creates proper default configuration.
        /// </summary>
        [TestMethod]
        public void Default_CreatesProperDefaultConfiguration()
        {
            var options = McpToolGenerationOptions.Default();

            options.Should().NotBeNull();
            options.OptimizeForPerformance.Should().BeFalse();
            options.IncludeDocumentation.Should().BeTrue();
            options.EnableCaching.Should().BeTrue();
            options.GenerateExamples.Should().BeTrue();
            options.MaxToolsPerEntityType.Should().Be(50);
        }

        /// <summary>
        /// Tests that Performance() creates performance-optimized configuration.
        /// </summary>
        [TestMethod]
        public void Performance_CreatesPerformanceOptimizedConfiguration()
        {
            var options = McpToolGenerationOptions.Performance();

            options.Should().NotBeNull();
            options.OptimizeForPerformance.Should().BeTrue();
            options.IncludeDocumentation.Should().BeFalse();
            options.GenerateExamples.Should().BeFalse();
            options.MaxToolsPerEntityType.Should().Be(20);
            options.EnableCaching.Should().BeTrue(); // Still enabled for performance
        }

        #endregion

        #region Property Tests

        /// <summary>
        /// Tests that all properties can be set and retrieved correctly.
        /// </summary>
        [TestMethod]
        public void Properties_CanBeSetAndRetrieved()
        {
            var options = new McpToolGenerationOptions
            {
                OptimizeForPerformance = true,
                IncludeDocumentation = false,
                MaxToolsPerEntityType = 25,
                EnableCaching = false,
                GenerateExamples = false
            };

            options.OptimizeForPerformance.Should().BeTrue();
            options.IncludeDocumentation.Should().BeFalse();
            options.MaxToolsPerEntityType.Should().Be(25);
            options.EnableCaching.Should().BeFalse();
            options.GenerateExamples.Should().BeFalse();
        }

        /// <summary>
        /// Tests that RequiredScopes collection can be modified.
        /// </summary>
        [TestMethod]
        public void RequiredScopes_CanBeModified()
        {
            var options = new McpToolGenerationOptions();

            options.RequiredScopes.Add("read");
            options.RequiredScopes.Add("write");

            options.RequiredScopes.Should().HaveCount(2);
            options.RequiredScopes.Should().Contain("read");
            options.RequiredScopes.Should().Contain("write");
        }

        /// <summary>
        /// Tests that RequiredRoles collection can be modified.
        /// </summary>
        [TestMethod]
        public void RequiredRoles_CanBeModified()
        {
            var options = new McpToolGenerationOptions();

            options.RequiredRoles.Add("admin");
            options.RequiredRoles.Add("user");

            options.RequiredRoles.Should().HaveCount(2);
            options.RequiredRoles.Should().Contain("admin");
            options.RequiredRoles.Should().Contain("user");
        }

        /// <summary>
        /// Tests that CustomProperties dictionary can be modified.
        /// </summary>
        [TestMethod]
        public void CustomProperties_CanBeModified()
        {
            var options = new McpToolGenerationOptions();

            options.CustomProperties["TestKey"] = "TestValue";
            options.CustomProperties["NumberKey"] = 42;

            options.CustomProperties.Should().HaveCount(2);
            options.CustomProperties["TestKey"].Should().Be("TestValue");
            options.CustomProperties["NumberKey"].Should().Be(42);
        }

        #endregion

        #region Clone Tests

        /// <summary>
        /// Tests that Clone creates deep copy with same values.
        /// </summary>
        [TestMethod]
        public void Clone_CreatesDeepCopyWithSameValues()
        {
            var original = new McpToolGenerationOptions
            {
                OptimizeForPerformance = true,
                IncludeDocumentation = false,
                MaxToolsPerEntityType = 25,
                EnableCaching = false,
                GenerateExamples = false
            };
            original.RequiredScopes.Add("read");
            original.RequiredRoles.Add("admin");
            original.CustomProperties["test"] = "value";

            var clone = original.Clone();

            clone.Should().NotBeSameAs(original);
            clone.OptimizeForPerformance.Should().Be(original.OptimizeForPerformance);
            clone.IncludeDocumentation.Should().Be(original.IncludeDocumentation);
            clone.MaxToolsPerEntityType.Should().Be(original.MaxToolsPerEntityType);
            clone.EnableCaching.Should().Be(original.EnableCaching);
            clone.GenerateExamples.Should().Be(original.GenerateExamples);
            
            clone.RequiredScopes.Should().NotBeSameAs(original.RequiredScopes);
            clone.RequiredScopes.Should().BeEquivalentTo(original.RequiredScopes);
            
            clone.RequiredRoles.Should().NotBeSameAs(original.RequiredRoles);
            clone.RequiredRoles.Should().BeEquivalentTo(original.RequiredRoles);
            
            clone.CustomProperties.Should().NotBeSameAs(original.CustomProperties);
            clone.CustomProperties.Should().BeEquivalentTo(original.CustomProperties);
        }

        /// <summary>
        /// Tests that modifications to clone don't affect original.
        /// </summary>
        [TestMethod]
        public void Clone_ModificationsToCloneDontAffectOriginal()
        {
            var original = new McpToolGenerationOptions();
            original.RequiredScopes.Add("read");

            var clone = original.Clone();
            clone.RequiredScopes.Add("write");
            clone.OptimizeForPerformance = true;

            original.RequiredScopes.Should().HaveCount(1);
            original.RequiredScopes.Should().Contain("read");
            original.RequiredScopes.Should().NotContain("write");
            original.OptimizeForPerformance.Should().BeFalse();
        }

        #endregion

        #region Validation Tests

        /// <summary>
        /// Tests that Validate returns no errors for valid options.
        /// </summary>
        [TestMethod]
        public void Validate_ValidOptions_ReturnsNoErrors()
        {
            var options = McpToolGenerationOptions.Default();

            var errors = options.Validate();

            errors.Should().BeEmpty();
        }

        /// <summary>
        /// Tests that Validate returns error for invalid MaxToolsPerEntityType.
        /// </summary>
        [TestMethod]
        public void Validate_InvalidMaxToolsPerEntityType_ReturnsError()
        {
            var options = new McpToolGenerationOptions
            {
                MaxToolsPerEntityType = 0
            };

            var errors = options.Validate().ToList();

            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("MaxToolsPerEntityType"));
        }

        /// <summary>
        /// Tests that Validate returns error for negative MaxToolsPerEntityType.
        /// </summary>
        [TestMethod]
        public void Validate_NegativeMaxToolsPerEntityType_ReturnsError()
        {
            var options = new McpToolGenerationOptions
            {
                MaxToolsPerEntityType = -1
            };

            var errors = options.Validate().ToList();

            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("MaxToolsPerEntityType"));
        }

        /// <summary>
        /// Tests that Validate returns error for excessive MaxToolsPerEntityType.
        /// </summary>
        [TestMethod]
        public void Validate_ExcessiveMaxToolsPerEntityType_ReturnsError()
        {
            var options = new McpToolGenerationOptions
            {
                MaxToolsPerEntityType = 1000
            };

            var errors = options.Validate().ToList();

            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("MaxToolsPerEntityType"));
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Tests creating performance options and validating them.
        /// </summary>
        [TestMethod]
        public void Integration_PerformanceOptionsValidation()
        {
            var options = McpToolGenerationOptions.Performance();
            
            // Should be valid
            var errors = options.Validate();
            errors.Should().BeEmpty();

            // Should have performance characteristics
            options.OptimizeForPerformance.Should().BeTrue();
            options.MaxToolsPerEntityType.Should().BeLessOrEqualTo(50);
        }

        /// <summary>
        /// Tests creating custom options with all features.
        /// </summary>
        [TestMethod]
        public void Integration_CustomOptionsWithAllFeatures()
        {
            var options = new McpToolGenerationOptions
            {
                OptimizeForPerformance = false,
                IncludeDocumentation = true,
                GenerateExamples = true,
                EnableCaching = true,
                MaxToolsPerEntityType = 30
            };
            
            options.RequiredScopes.Add("odata:read");
            options.RequiredScopes.Add("odata:write");
            options.RequiredRoles.Add("DataAdmin");
            options.CustomProperties["AllowAdvancedQueries"] = true;
            options.CustomProperties["MaxResultSize"] = 1000;

            // Should be valid
            var errors = options.Validate();
            errors.Should().BeEmpty();

            // Should have all configured features
            options.RequiredScopes.Should().HaveCount(2);
            options.RequiredRoles.Should().HaveCount(1);
            options.CustomProperties.Should().HaveCount(2);
        }

        #endregion
    }
}