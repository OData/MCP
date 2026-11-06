// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using FluentAssertions;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Mcp.AspNetCore.Adaptation;
using Microsoft.OData.Mcp.AspNetCore.Vocabularies;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Vocabularies
{

    /// <summary>
    /// <c>[McpTask]</c> stamps <c>Microsoft.OData.Mcp.V1.Task</c> on a finished <see cref="IEdmModel"/>.
    /// </summary>
    [TestClass]
    public class McpTaskAttributeTests
    {

        #region Public Methods

        /// <summary>
        /// A bound action whose CLR method has <c>[McpTask]</c> gets the Task term. The host does
        /// not wrap Restier or ModelBuilder.
        /// </summary>
        [TestMethod]
        public void Apply_BoundAction_StampsTaskTerm()
        {
            var model = BuildModel();
            McpTaskAttributeAnnotations.Apply(model, [typeof(TaskedOperations)]);

            var action = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Authorize");
            var annotation = model.FindVocabularyAnnotations(action)
                .Single(item => item.Term.Namespace == McpVocabulary.Namespace && item.Term.Name == McpVocabulary.TaskTermName);

            ((IEdmBooleanConstantExpression)annotation.Value).Value.Should().BeTrue();
            EdmModelAdapter.ToCoreModel(model).Actions.Single(item => item.Name == "Authorize").IsTask.Should().BeTrue();
        }

        /// <summary>
        /// A sibling action without <c>[McpTask]</c> stays one-shot.
        /// </summary>
        [TestMethod]
        public void Apply_SiblingAction_IsNotATask()
        {
            var model = BuildModel();
            McpTaskAttributeAnnotations.Apply(model, [typeof(TaskedOperations)]);

            var core = EdmModelAdapter.ToCoreModel(model);
            core.Actions.Single(item => item.Name == "Authorize").IsTask.Should().BeTrue();
            core.Actions.Single(item => item.Name == "Reset").IsTask.Should().BeFalse();
        }

        /// <summary>
        /// Functions are not tasked even when the CLR method has <c>[McpTask]</c>.
        /// </summary>
        [TestMethod]
        public void Apply_Function_IsNotATask()
        {
            var model = BuildModel();
            McpTaskAttributeAnnotations.Apply(model, [typeof(TaskedOperations)]);

            var function = model.SchemaElements.OfType<IEdmFunction>().Single(item => item.Name == "MostValuable");
            model.FindVocabularyAnnotations(function)
                .Where(item => item.Term.Namespace == McpVocabulary.Namespace && item.Term.Name == McpVocabulary.TaskTermName)
                .Should().BeEmpty();
        }

        /// <summary>
        /// Optional elicit property names on the attribute are stamped next to Task.
        /// </summary>
        [TestMethod]
        public void Apply_ElicitUrlProperties_AreCopied()
        {
            var model = BuildModel();
            McpTaskAttributeAnnotations.Apply(model, [typeof(TaskedOperations)]);

            var pay = EdmModelAdapter.ToCoreModel(model).Actions.Single(item => item.Name == "Pay");
            pay.IsTask.Should().BeTrue();
            pay.ElicitUrlProperties.Should().Equal("connectUri");
        }

        /// <summary>
        /// <c>[McpTask(Description)]</c> stamps <c>Org.OData.Core.V1.Description</c>.
        /// </summary>
        [TestMethod]
        public void Apply_Description_StampsCoreDescription()
        {
            var model = BuildModel();
            McpTaskAttributeAnnotations.Apply(model, [typeof(TaskedOperations)]);

            var authorize = EdmModelAdapter.ToCoreModel(model).Actions.Single(item => item.Name == "Authorize");
            authorize.IsTask.Should().BeTrue();
            authorize.Description.Should().Be("Starts provider authorization.");
        }

        /// <summary>
        /// <c>[McpTask]</c> plus <c>[McpElicitUrl]</c> stamps both terms. The dedicated attribute
        /// is the first-class elicit mark.
        /// </summary>
        [TestMethod]
        public void Apply_McpElicitUrl_StampsElicitUrlProperties()
        {
            var model = BuildModel();
            McpTaskAttributeAnnotations.Apply(model, [typeof(TaskedOperations)]);

            var refund = EdmModelAdapter.ToCoreModel(model).Actions.Single(item => item.Name == "Refund");
            refund.IsTask.Should().BeTrue();
            refund.ElicitUrlProperties.Should().Equal("connectUri");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Convention model with bound Authorize/Pay, unbound Reset, and function MostValuable.
        /// </summary>
        /// <returns>
        /// The finished EdmLib model.
        /// </returns>
        internal static IEdmModel BuildModel()
        {
            var builder = new ODataConventionModelBuilder
            {
                Namespace = typeof(Customer).Namespace!
            };
            builder.EntitySet<Customer>("Customers");
            builder.EntityType<Customer>().Action("Authorize");
            builder.EntityType<Customer>().Action("Pay");
            builder.EntityType<Customer>().Action("Refund");
            builder.Action("Reset");
            builder.Function("MostValuable").Returns<int>();
            return builder.GetEdmModel();
        }

        #endregion

    }

}
