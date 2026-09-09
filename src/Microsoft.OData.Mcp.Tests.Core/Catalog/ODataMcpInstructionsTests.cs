// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Tests for the shared MCP server instructions string and its preface composition.
    /// </summary>
    [TestClass]
    public class ODataMcpInstructionsTests
    {

        #region Public Methods

        /// <summary>
        /// A null, empty, or whitespace preface yields the default text alone.
        /// </summary>
        [TestMethod]
        public void Compose_NullOrWhitespacePreface_ReturnsDefault()
        {
            ODataMcpInstructions.Compose(null).Should().Be(ODataMcpInstructions.Default);
            ODataMcpInstructions.Compose(string.Empty).Should().Be(ODataMcpInstructions.Default);
            ODataMcpInstructions.Compose("   \r\n\t").Should().Be(ODataMcpInstructions.Default);
        }

        /// <summary>
        /// A preface is trimmed and placed before the default text with one blank line between.
        /// </summary>
        [TestMethod]
        public void Compose_Preface_PrependsTrimmedWithBlankLine()
        {
            var composed = ODataMcpInstructions.Compose("  Contoso. \n");

            composed.Should().Be($"Contoso.\n\n{ODataMcpInstructions.Default}");
            composed.IndexOf("Contoso.").Should().BeLessThan(composed.IndexOf(ODataMcpInstructions.Default));
        }

        /// <summary>
        /// The default text carries the non-obvious rules the schema cannot say.
        /// </summary>
        [TestMethod]
        public void Default_ContainsNonObviousRules()
        {
            ODataMcpInstructions.Default.Should().NotBeNullOrWhiteSpace();
            ODataMcpInstructions.Default.Should().Contain("Do not read $metadata");
            ODataMcpInstructions.Default.Should().Contain("no $ prefix");
            ODataMcpInstructions.Default.Should().Contain("parameters");
            ODataMcpInstructions.Default.Should().NotContain("\r");
            ODataMcpInstructions.Default.Should().NotStartWith("\n");
            ODataMcpInstructions.Default.Should().NotEndWith("\n");
        }

        #endregion

    }

}
