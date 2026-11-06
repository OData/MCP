// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using CloudNimble.McpEssentials.Core.Tasks.Elicitations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Mcp.AspNetCore.Adaptation;
using Microsoft.OData.Mcp.AspNetCore.Vocabularies;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Vocabularies
{

    /// <summary>
    /// <c>HasMcpElicitForm</c> stamps <c>Microsoft.OData.Mcp.V1.ElicitForm</c> during
    /// <c>GetEdmModel</c>.
    /// </summary>
    [TestClass]
    public class HasMcpElicitFormTests
    {

        #region Public Methods

        /// <summary>
        /// A live form instance is written as a compact island on the annotation.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitForm_GetEdmModel_WritesFormIsland()
        {
            var form = FormElicitationBuilder.Create("Ship this order")
                .String("trackingNumber").Required();
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship").HasMcpTask().HasMcpElicitForm(form);

            var model = builder.GetEdmModel();
            var xml = HasMcpTaskTests.WriteCsdl(model);

            xml.Should().Contain("Term=\"Mcp.ElicitForm\"");
            xml.Should().Contain("<Form ");
            xml.Should().Contain($"xmlns=\"{McpVocabulary.ReferenceUri}\"");
            xml.Should().Contain("trackingNumber");

            var ship = EdmModelAdapter.ToCoreModel(model).Actions.Single(item => item.Name == "Ship");
            ship.IsTask.Should().BeTrue();
            ship.ElicitForm.Should().NotBeNull();
            ship.ElicitForm!.Message.Should().Be("Ship this order");
            ship.ElicitForm.Fields.Should().ContainSingle(field => field.Name == "trackingNumber" && field.Required);

            new CsdlParser().ParseFromString(xml).Actions.Single(action => action.Name == "Ship")
                .ElicitForm!.Fields[0].Name.Should().Be("trackingNumber");
        }

        /// <summary>
        /// A second <c>HasMcpElicitForm</c> replaces the previous configuration.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitForm_SecondCall_ReplacesForm()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship")
                .HasMcpElicitForm(FormElicitationBuilder.Create("first").String("a"))
                .HasMcpElicitForm(FormElicitationBuilder.Create("second").String("b"));

            var ship = EdmModelAdapter.ToCoreModel(builder.GetEdmModel())
                .Actions.Single(item => item.Name == "Ship");

            ship.ElicitForm!.Message.Should().Be("second");
            ship.ElicitForm.Fields.Should().ContainSingle(field => field.Name == "b");
        }

        /// <summary>
        /// A keyed name stamps the term; Rebuild attaches the island from DI.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitForm_Name_RebuildStampsIsland()
        {
            var form = FormElicitationBuilder.Create("Ship this order").String("trackingNumber").Required();
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship").HasMcpElicitForm("ship");
            var model = (EdmModel)builder.GetEdmModel();

            var services = new ServiceCollection();
            services.AddFormElicitation("ship", form);
            using var provider = services.BuildServiceProvider();

            McpTaskAnnotations.ApplyNamedElicitForms(model, provider);

            var ship = EdmModelAdapter.ToCoreModel(model).Actions.Single(item => item.Name == "Ship");
            ship.ElicitForm!.Message.Should().Be("Ship this order");
            ship.ElicitForm.Fields[0].Name.Should().Be("trackingNumber");
        }

        /// <summary>
        /// A missing keyed form at Rebuild names the action and the key.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitForm_MissingName_ThrowsInvalidOperationException()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship").HasMcpElicitForm("ship");
            var model = (EdmModel)builder.GetEdmModel();
            using var provider = new ServiceCollection().BuildServiceProvider();

            var act = () => McpTaskAnnotations.ApplyNamedElicitForms(model, provider);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Ship*ship*");
        }

        /// <summary>
        /// Null arguments are rejected.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitForm_Null_ThrowsArgumentNullException()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            var action = builder.EntityType<Customer>().Action("Ship");

            var nullForm = () => action.HasMcpElicitForm((IFormElicitationSource)null!);
            var blankName = () => action.HasMcpElicitForm("  ");

            nullForm.Should().Throw<ArgumentNullException>().WithParameterName("form");
            blankName.Should().Throw<ArgumentException>().WithParameterName("name");
        }

        /// <summary>
        /// A null action is rejected.
        /// </summary>
        [TestMethod]
        public void HasMcpElicitForm_NullAction_ThrowsArgumentNullException()
        {
            ActionConfiguration action = null!;
            var form = () => action.HasMcpElicitForm(FormElicitationBuilder.Create("m"));
            var name = () => action.HasMcpElicitForm("ship");

            form.Should().Throw<ArgumentNullException>().WithParameterName("action");
            name.Should().Throw<ArgumentNullException>().WithParameterName("action");
        }

        /// <summary>
        /// Blank island XML is rejected.
        /// </summary>
        [TestMethod]
        public void StampElicitForm_BlankXml_ThrowsArgumentException()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship");
            var model = (EdmModel)builder.GetEdmModel();
            var action = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Ship");

            var nullModel = () => McpTaskAnnotations.StampElicitForm(null!, action, "<Form Message='m'/>");
            var nullTarget = () => McpTaskAnnotations.StampElicitForm(model, null!, "<Form Message='m'/>");
            var blank = () => McpTaskAnnotations.StampElicitForm(model, action, "  ");

            nullModel.Should().Throw<ArgumentNullException>().WithParameterName("model");
            nullTarget.Should().Throw<ArgumentNullException>().WithParameterName("target");
            blank.Should().Throw<ArgumentException>().WithParameterName("islandXml");
        }

        /// <summary>
        /// Blank keyed names are rejected.
        /// </summary>
        [TestMethod]
        public void StampElicitFormName_BlankName_ThrowsArgumentException()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship");
            var model = (EdmModel)builder.GetEdmModel();
            var action = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Ship");

            var nullModel = () => McpTaskAnnotations.StampElicitFormName(null!, action, "ship");
            var nullTarget = () => McpTaskAnnotations.StampElicitFormName(model, null!, "ship");
            var blank = () => McpTaskAnnotations.StampElicitFormName(model, action, "  ");

            nullModel.Should().Throw<ArgumentNullException>().WithParameterName("model");
            nullTarget.Should().Throw<ArgumentNullException>().WithParameterName("target");
            blank.Should().Throw<ArgumentException>().WithParameterName("name");
        }

        /// <summary>
        /// A model that does not reference the vocabulary cannot resolve the ElicitForm term.
        /// </summary>
        [TestMethod]
        public void RequireElicitFormTerm_MissingVocabulary_ThrowsInvalidOperationException()
        {
            var model = new EdmModel();
            var act = () => McpTaskAnnotations.RequireElicitFormTerm(model);
            act.Should().Throw<InvalidOperationException>().WithMessage("*ElicitForm*");
        }

        /// <summary>
        /// A null model is rejected.
        /// </summary>
        [TestMethod]
        public void RequireElicitFormTerm_Null_ThrowsArgumentNullException()
        {
            var act = () => McpTaskAnnotations.RequireElicitFormTerm(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("model");
        }

        /// <summary>
        /// A blank pending form name is skipped at Rebuild.
        /// </summary>
        [TestMethod]
        public void ApplyNamedElicitForms_WhitespacePendingName_IsSkipped()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship");
            var model = (EdmModel)builder.GetEdmModel();
            var action = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Ship");
            model.SetAnnotationValue(action, McpVocabulary.ReferenceUri, McpTaskAnnotations.PendingElicitFormName, "  ");
            using var provider = new ServiceCollection().BuildServiceProvider();

            var act = () => McpTaskAnnotations.ApplyNamedElicitForms(model, provider);
            act.Should().NotThrow();
            EdmModelAdapter.ToCoreModel(model).Actions.Single(item => item.Name == "Ship").ElicitForm.Should().BeNull();
        }

        /// <summary>
        /// ApplyNamedElicitForms rejects null arguments.
        /// </summary>
        [TestMethod]
        public void ApplyNamedElicitForms_Null_ThrowsArgumentNullException()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            var model = (EdmModel)builder.GetEdmModel();
            using var provider = new ServiceCollection().BuildServiceProvider();

            var nullModel = () => McpTaskAnnotations.ApplyNamedElicitForms(null!, provider);
            var nullServices = () => McpTaskAnnotations.ApplyNamedElicitForms(model, null!);

            nullModel.Should().Throw<ArgumentNullException>().WithParameterName("model");
            nullServices.Should().Throw<ArgumentNullException>().WithParameterName("services");
        }

        /// <summary>
        /// <c>[McpElicitForm]</c> without services records the keyed name.
        /// </summary>
        [TestMethod]
        public void Apply_McpElicitForm_WithoutServices_RecordsName()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship");
            var model = (EdmModel)builder.GetEdmModel();

            McpTaskAttributeAnnotations.Apply(model, [typeof(TaskedOperations)]);

            var action = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Ship");
            model.GetAnnotationValue(action, McpVocabulary.ReferenceUri, McpTaskAnnotations.PendingElicitFormName)
                .Should().Be("ship");
        }

        /// <summary>
        /// <c>[McpElicitForm]</c> with a keyed registration stamps the island.
        /// </summary>
        [TestMethod]
        public void Apply_McpElicitForm_WithKeyedForm_StampsIsland()
        {
            var form = FormElicitationBuilder.Create("Ship this order").String("trackingNumber").Required();
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship");
            var model = (EdmModel)builder.GetEdmModel();
            var services = new ServiceCollection();
            services.AddFormElicitation("ship", form);
            using var provider = services.BuildServiceProvider();

            McpTaskAttributeAnnotations.Apply(model, [typeof(TaskedOperations)], provider);

            var ship = EdmModelAdapter.ToCoreModel(model).Actions.Single(item => item.Name == "Ship");
            ship.ElicitForm.Should().NotBeNull();
            ship.ElicitForm!.Message.Should().Be("Ship this order");
            ship.ElicitForm.Fields[0].Name.Should().Be("trackingNumber");
        }

        /// <summary>
        /// <c>[McpElicitForm]</c> with a missing keyed registration fails first.
        /// </summary>
        [TestMethod]
        public void Apply_McpElicitForm_MissingKeyedForm_ThrowsInvalidOperationException()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship");
            var model = (EdmModel)builder.GetEdmModel();
            using var provider = new ServiceCollection().BuildServiceProvider();

            var act = () => McpTaskAttributeAnnotations.Apply(model, [typeof(TaskedOperations)], provider);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Ship*ship*");
        }

        /// <summary>
        /// Attribute stamping with a null model or type is rejected.
        /// </summary>
        [TestMethod]
        public void StampType_Null_ThrowsArgumentNullException()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            var model = (EdmModel)builder.GetEdmModel();

            var nullModel = () => McpTaskAttributeAnnotations.StampType(null!, typeof(TaskedOperations));
            var nullType = () => McpTaskAttributeAnnotations.StampType(model, null!);

            nullModel.Should().Throw<ArgumentNullException>().WithParameterName("model");
            nullType.Should().Throw<ArgumentNullException>().WithParameterName("type");
        }

        /// <summary>
        /// An empty term configuration writes the existing inline annotation.
        /// </summary>
        [TestMethod]
        public void TermConfiguration_NeitherFormNorName_WritesExistingInline()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship");
            var model = (EdmModel)builder.GetEdmModel();
            var action = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Ship");
            var config = new McpElicitFormTermConfiguration();

            config.TermName.Should().Be(McpVocabulary.ElicitFormTerm);
            config.ToEdmExpression().Should().BeOfType<EdmBooleanConstant>();
            var act = () => config.SetVocabularyAnnotations(model, action);
            act.Should().NotThrow();
        }

        /// <summary>
        /// Attribute stamping with null services stores the name rather than resolving DI.
        /// </summary>
        [TestMethod]
        public void StampElicitForm_NullServices_RecordsName()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship");
            var model = (EdmModel)builder.GetEdmModel();
            var action = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Ship");

            McpTaskAttributeAnnotations.StampElicitForm(model, action, "ship", services: null);

            model.GetAnnotationValue(action, McpVocabulary.ReferenceUri, McpTaskAnnotations.PendingElicitFormName)
                .Should().Be("ship");
        }

        /// <summary>
        /// Attribute stamping rejects a blank keyed name.
        /// </summary>
        [TestMethod]
        public void StampElicitForm_AttributeBlankName_ThrowsArgumentException()
        {
            var builder = HasMcpTaskTests.CreateBuilder();
            builder.EntityType<Customer>().Action("Ship");
            var model = (EdmModel)builder.GetEdmModel();
            var action = model.SchemaElements.OfType<IEdmAction>().Single(item => item.Name == "Ship");

            var nullModel = () => McpTaskAttributeAnnotations.StampElicitForm(null!, action, "ship", null);
            var nullAction = () => McpTaskAttributeAnnotations.StampElicitForm(model, null!, "ship", null);
            var blank = () => McpTaskAttributeAnnotations.StampElicitForm(model, action, "  ", null);

            nullModel.Should().Throw<ArgumentNullException>().WithParameterName("model");
            nullAction.Should().Throw<ArgumentNullException>().WithParameterName("action");
            blank.Should().Throw<ArgumentException>().WithParameterName("name");
        }

        #endregion

    }

}
