// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.OData.Edm;
using Microsoft.OData.Mcp.AspNetCore.Adaptation;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Vocabularies
{

    /// <summary>
    /// <c>HasMcpElicitUrl</c> stamps <c>Microsoft.OData.Mcp.V1.ElicitUrlProperties</c> during
    /// <c>GetEdmModel</c>.
    /// </summary>
    [TestClass]
    public class HasMcpElicitUrlTests
    {

        #region Public Methods

        /// <summary>
        /// Chaining <c>HasMcpTask</c> and <c>HasMcpElicitUrl</c> stamps both terms.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitUrl_AfterHasMcpTask_StampsBothTerms()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Authorize").HasMcpTask().HasMcpElicitUrl("connectUri");

            var authorize = EdmModelAdapter.ToCoreModel(builder.GetEdmModel())
                .Actions.Single(item => item.Name == "Authorize");

            authorize.IsTask.Should().BeTrue();
            authorize.ElicitUrlProperties.Should().Equal("connectUri");
        }

        /// <summary>
        /// <c>$metadata</c> nests <c>ElicitUrlProperties</c> on the action, not an out-of-line Target.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitUrl_Csdl_WritesInlineCollection()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Authorize").HasMcpTask().HasMcpElicitUrl("connectUri");
            var model = builder.GetEdmModel();
            var xml = HasMcpTaskTests.WriteCsdl(model);

            xml.Should().Contain("Term=\"Mcp.ElicitUrlProperties\"");
            xml.Should().Contain("connectUri");

            var authorize = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Authorize");
            AssertElicitUrlIsInlineOnAction(xml, "Authorize", authorize.Namespace);
            new CsdlParser().ParseFromString(xml).Actions.Single(action => action.Name == "Authorize")
                .ElicitUrlProperties.Should().Equal("connectUri");
        }

        /// <summary>
        /// Empty elicit names are rejected.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitUrl_EmptyNames_Throws()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            var action = builder.EntityType<Customer>().Action("Authorize");

            var empty = () => action.HasMcpElicitUrl();
            var whitespace = () => action.HasMcpElicitUrl("  ", "\t");

            empty.Should().Throw<ArgumentException>().WithParameterName("propertyNames");
            whitespace.Should().Throw<ArgumentException>().WithParameterName("propertyNames");
        }

        /// <summary>
        /// <c>HasMcpElicitUrl</c> without <c>HasMcpTask</c> still stamps the elicit term.
        /// Runtime elicitation requires the Task term as well.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitUrl_WithoutTask_StampsElicitOnly()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Authorize").HasMcpElicitUrl("connectUri");

            var authorize = EdmModelAdapter.ToCoreModel(builder.GetEdmModel())
                .Actions.Single(item => item.Name == "Authorize");

            authorize.IsTask.Should().BeFalse();
            authorize.ElicitUrlProperties.Should().Equal("connectUri");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Asserts <c>Mcp.ElicitUrlProperties</c> is nested on <paramref name="actionName"/>.
        /// </summary>
        /// <param name="xml">Serialized CSDL.</param>
        /// <param name="actionName">The action short name.</param>
        /// <param name="schemaNamespace">The schema that declares the action.</param>
        internal static void AssertElicitUrlIsInlineOnAction(string xml, string actionName, string schemaNamespace)
        {
            var document = XDocument.Parse(xml);
            XNamespace edm = "http://docs.oasis-open.org/odata/ns/edm";
            var action = document.Descendants(edm + "Action")
                .Single(element => element.Attribute("Name")?.Value == actionName);

            ((XElement)action.Parent!).Attribute("Namespace")!.Value.Should().Be(schemaNamespace);
            var annotation = action.Elements(edm + "Annotation")
                .Single(element => element.Attribute("Term")?.Value is "Mcp.ElicitUrlProperties" or "Microsoft.OData.Mcp.V1.ElicitUrlProperties");
            annotation.Elements(edm + "Collection").Elements(edm + "String")
                .Select(element => element.Value)
                .Should().Equal("connectUri");
            document.Descendants(edm + "Annotations")
                .SelectMany(element => element.Elements(edm + "Annotation"))
                .Where(element => element.Attribute("Term")?.Value is "Mcp.ElicitUrlProperties" or "Microsoft.OData.Mcp.V1.ElicitUrlProperties")
                .Should().BeEmpty();
        }

        #endregion

    }

}
