// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Xml;
using FluentAssertions;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Mcp.AspNetCore.Adaptation;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Vocabularies
{

    /// <summary>
    /// <c>HasMcpTask</c> stamps <c>Microsoft.OData.Mcp.V1.Task</c> during <c>GetEdmModel</c>.
    /// </summary>
    [TestClass]
    public class HasMcpTaskTests
    {

        #region Public Methods

        /// <summary>
        /// <c>HasMcpTask</c> on a bound action makes <c>GetEdmModel</c> emit the Task term
        /// and reference the MCP vocabulary. The host does not touch the model after extraction.
        /// </summary>
        [TestMethod]
        public void HasMcpTask_GetEdmModel_StampsTaskTermAndReferencesVocabulary()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");
            builder.EntityType<Customer>().Action("Authorize").HasMcpTask();

            var model = builder.GetEdmModel();
            var action = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Authorize");
            var annotation = model.FindVocabularyAnnotations(action)
                .Single(item => item.Term.Namespace == McpVocabulary.Namespace && item.Term.Name == McpVocabulary.TaskTermName);

            model.FindTerm(McpVocabulary.TaskTerm).Should().NotBeNull();
            ((IEdmBooleanConstantExpression)annotation.Value).Value.Should().BeTrue();
            EdmModelAdapter.ToCoreModel(model).Actions.Single(item => item.Name == "Authorize").IsTask.Should().BeTrue();
        }

        /// <summary>
        /// An action without <c>HasMcpTask</c> stays one-shot.
        /// </summary>
        [TestMethod]
        public void HasMcpTask_SiblingAction_IsNotATask()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");
            builder.EntityType<Customer>().Action("Authorize").HasMcpTask();
            builder.EntityType<Customer>().Action("Reset");

            var core = EdmModelAdapter.ToCoreModel(builder.GetEdmModel());

            core.Actions.Single(item => item.Name == "Authorize").IsTask.Should().BeTrue();
            core.Actions.Single(item => item.Name == "Reset").IsTask.Should().BeFalse();
        }

        /// <summary>
        /// Optional elicit property names are stamped next to Task.
        /// </summary>
        [TestMethod]
        public void HasMcpTask_ElicitUrlProperties_AreCopied()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");
            builder.EntityType<Customer>().Action("Authorize").HasMcpTask("connectUri");

            var authorize = EdmModelAdapter.ToCoreModel(builder.GetEdmModel())
                .Actions.Single(item => item.Name == "Authorize");

            authorize.IsTask.Should().BeTrue();
            authorize.ElicitUrlProperties.Should().Equal("connectUri");
        }

        /// <summary>
        /// <c>$metadata</c> includes an <c>edmx:Reference</c> for the MCP vocabulary.
        /// </summary>
        [TestMethod]
        public void HasMcpTask_Csdl_WritesEdmxReference()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");
            builder.EntityType<Customer>().Action("Authorize").HasMcpTask();

            using var buffer = new StringWriter();
            using var writer = XmlWriter.Create(buffer);
            CsdlWriter.TryWriteCsdl(builder.GetEdmModel(), writer, CsdlTarget.OData, out _).Should().BeTrue();
            writer.Flush();

            var xml = buffer.ToString();
            xml.Should().Contain(McpVocabulary.ReferenceUri);
            xml.Should().Contain($"Namespace=\"{McpVocabulary.Namespace}\"");
            xml.Should().Contain("Term=\"Mcp.Task\"");
        }

        #endregion

    }

}
