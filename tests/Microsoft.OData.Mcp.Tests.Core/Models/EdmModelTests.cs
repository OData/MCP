using FluentAssertions;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Tests.Core.Models
{
    /// <summary>
    /// Comprehensive tests for the EdmModel class and related EDM model classes.
    /// </summary>
    [TestClass]
    public class EdmModelTests
    {

        #region EdmModel Tests

        /// <summary>
        /// Tests that EdmModel constructor initializes collections correctly.
        /// </summary>
        [TestMethod]
        public void EdmModel_Constructor_InitializesCollections()
        {
            var model = new EdmModel();

            model.EntityTypes.Should().NotBeNull().And.BeEmpty();
            model.ComplexTypes.Should().NotBeNull().And.BeEmpty();
            model.Actions.Should().NotBeNull().And.BeEmpty();
            model.Functions.Should().NotBeNull().And.BeEmpty();
            model.EntityContainer.Should().BeNull();
            model.Namespace.Should().BeNull();
            model.SchemaVersion.Should().BeNull();
            model.Annotations.Should().NotBeNull().And.BeEmpty();
        }

        /// <summary>
        /// Tests that EdmModel properties can be set and retrieved.
        /// </summary>
        [TestMethod]
        public void EdmModel_Properties_CanBeSetAndRetrieved()
        {
            var model = new EdmModel
            {
                Namespace = "TestNamespace",
                SchemaVersion = "4.0"
            };

            var entityContainer = new EdmEntityContainer
            {
                Name = "TestContainer"
            };
            model.EntityContainer = entityContainer;

            model.Namespace.Should().Be("TestNamespace");
            model.SchemaVersion.Should().Be("4.0");
            model.EntityContainer.Should().BeSameAs(entityContainer);
        }

        /// <summary>
        /// Tests that EdmModel can add entity types.
        /// </summary>
        [TestMethod]
        public void EdmModel_CanAddEntityTypes()
        {
            var model = new EdmModel();
            var entityType = new EdmEntityType
            {
                Name = "Customer",
                Namespace = "TestNamespace"
            };

            model.EntityTypes.Add(entityType);

            model.EntityTypes.Should().HaveCount(1);
            model.EntityTypes.First().Should().BeSameAs(entityType);
        }

        /// <summary>
        /// Tests that EdmModel can add complex types.
        /// </summary>
        [TestMethod]
        public void EdmModel_CanAddComplexTypes()
        {
            var model = new EdmModel();
            var complexType = new EdmComplexType
            {
                Name = "Address",
                Namespace = "TestNamespace"
            };

            model.ComplexTypes.Add(complexType);

            model.ComplexTypes.Should().HaveCount(1);
            model.ComplexTypes.First().Should().BeSameAs(complexType);
        }

        #endregion

        #region EdmEntityType Tests

        /// <summary>
        /// Tests that EdmEntityType constructor initializes collections correctly.
        /// </summary>
        [TestMethod]
        public void EdmEntityType_Constructor_InitializesCollections()
        {
            var entityType = new EdmEntityType();

            entityType.Properties.Should().NotBeNull().And.BeEmpty();
            entityType.NavigationProperties.Should().NotBeNull().And.BeEmpty();
            entityType.Keys.Should().NotBeNull().And.BeEmpty();
            entityType.Name.Should().BeNull();
            entityType.Namespace.Should().BeNull();
            entityType.BaseType.Should().BeNull();
            entityType.IsAbstract.Should().BeFalse();
            entityType.IsOpenType.Should().BeFalse();
            entityType.HasStream.Should().BeFalse();
        }

        /// <summary>
        /// Tests that EdmEntityType properties can be set correctly.
        /// </summary>
        [TestMethod]
        public void EdmEntityType_Properties_CanBeSetCorrectly()
        {
            var entityType = new EdmEntityType
            {
                Name = "Customer",
                Namespace = "TestNamespace",
                IsAbstract = true,
                IsOpenType = true,
                HasStream = true
            };

            entityType.Name.Should().Be("Customer");
            entityType.Namespace.Should().Be("TestNamespace");
            entityType.IsAbstract.Should().BeTrue();
            entityType.IsOpenType.Should().BeTrue();
            entityType.HasStream.Should().BeTrue();
        }

        /// <summary>
        /// Tests that EdmEntityType can add properties.
        /// </summary>
        [TestMethod]
        public void EdmEntityType_CanAddProperties()
        {
            var entityType = new EdmEntityType();
            var property = new EdmProperty
            {
                Name = "CustomerId",
                Type = EdmPrimitiveType.Int32,
                IsNullable = false
            };

            entityType.Properties.Add(property);

            entityType.Properties.Should().HaveCount(1);
            entityType.Properties.First().Should().BeSameAs(property);
        }

        /// <summary>
        /// Tests that EdmEntityType can add navigation properties.
        /// </summary>
        [TestMethod]
        public void EdmEntityType_CanAddNavigationProperties()
        {
            var entityType = new EdmEntityType();
            var navProperty = new EdmNavigationProperty
            {
                Name = "Orders",
                Type = "Collection(Order)"
            };

            entityType.NavigationProperties.Add(navProperty);

            entityType.NavigationProperties.Should().HaveCount(1);
            entityType.NavigationProperties.First().Should().BeSameAs(navProperty);
        }

        /// <summary>
        /// Tests that EdmEntityType can set key properties.
        /// </summary>
        [TestMethod]
        public void EdmEntityType_CanSetKeyProperties()
        {
            var entityType = new EdmEntityType();
            var keyProperty = new EdmProperty
            {
                Name = "CustomerId",
                Type = EdmPrimitiveType.Int32,
                IsNullable = false
            };

            entityType.Properties.Add(keyProperty);
            entityType.Keys.Add("CustomerId");

            entityType.Keys.Should().HaveCount(1);
            entityType.Keys.First().Should().Be("CustomerId");
        }

        /// <summary>
        /// Tests that EdmEntityType can inherit from base type.
        /// </summary>
        [TestMethod]
        public void EdmEntityType_CanInheritFromBaseType()
        {
            var baseType = new EdmEntityType
            {
                Name = "Person",
                Namespace = "TestNamespace"
            };

            var derivedType = new EdmEntityType
            {
                Name = "Customer",
                Namespace = "TestNamespace",
                BaseType = baseType
            };

            derivedType.BaseType.Should().BeSameAs(baseType);
        }

        #endregion

        #region EdmProperty Tests

        /// <summary>
        /// Tests that EdmProperty can be created with all properties.
        /// </summary>
        [TestMethod]
        public void EdmProperty_CanBeCreatedWithAllProperties()
        {
            var property = new EdmProperty
            {
                Name = "CustomerName",
                Type = EdmPrimitiveType.String,
                IsNullable = true,
                MaxLength = 100,
                IsUnicode = true,
                Precision = 10,
                Scale = 2,
                DefaultValue = "Default Name",
                ConcurrencyMode = EdmConcurrencyMode.Fixed
            };

            property.Name.Should().Be("CustomerName");
            property.Type.Should().Be(EdmPrimitiveType.String);
            property.IsNullable.Should().BeTrue();
            property.MaxLength.Should().Be(100);
            property.IsUnicode.Should().BeTrue();
            property.Precision.Should().Be(10);
            property.Scale.Should().Be(2);
            property.DefaultValue.Should().Be("Default Name");
            property.ConcurrencyMode.Should().Be(EdmConcurrencyMode.Fixed);
        }

        /// <summary>
        /// Tests that EdmProperty has correct default values.
        /// </summary>
        [TestMethod]
        public void EdmProperty_HasCorrectDefaultValues()
        {
            var property = new EdmProperty();

            property.Name.Should().BeNull();
            property.Type.Should().BeNull();
            property.IsNullable.Should().BeTrue();
            property.MaxLength.Should().BeNull();
            property.IsUnicode.Should().BeNull();
            property.Precision.Should().BeNull();
            property.Scale.Should().BeNull();
            property.DefaultValue.Should().BeNull();
            property.ConcurrencyMode.Should().Be(EdmConcurrencyMode.None);
        }

        #endregion

        #region EdmComplexType Tests

        /// <summary>
        /// Tests that EdmComplexType initializes collections correctly.
        /// </summary>
        [TestMethod]
        public void EdmComplexType_Constructor_InitializesCollections()
        {
            var complexType = new EdmComplexType();

            complexType.Properties.Should().NotBeNull().And.BeEmpty();
            complexType.Name.Should().BeNull();
            complexType.Namespace.Should().BeNull();
            complexType.BaseType.Should().BeNull();
            complexType.IsAbstract.Should().BeFalse();
            complexType.IsOpenType.Should().BeFalse();
        }

        /// <summary>
        /// Tests that EdmComplexType can add properties.
        /// </summary>
        [TestMethod]
        public void EdmComplexType_CanAddProperties()
        {
            var complexType = new EdmComplexType();
            var property = new EdmProperty
            {
                Name = "Street",
                Type = EdmPrimitiveType.String
            };

            complexType.Properties.Add(property);

            complexType.Properties.Should().HaveCount(1);
            complexType.Properties.First().Should().BeSameAs(property);
        }

        #endregion

        #region EdmEntityContainer Tests

        /// <summary>
        /// Tests that EdmEntityContainer initializes collections correctly.
        /// </summary>
        [TestMethod]
        public void EdmEntityContainer_Constructor_InitializesCollections()
        {
            var container = new EdmEntityContainer();

            container.EntitySets.Should().NotBeNull().And.BeEmpty();
            container.Singletons.Should().NotBeNull().And.BeEmpty();
            container.ActionImports.Should().NotBeNull().And.BeEmpty();
            container.FunctionImports.Should().NotBeNull().And.BeEmpty();
            container.Name.Should().BeNull();
            container.Namespace.Should().BeNull();
        }

        /// <summary>
        /// Tests that EdmEntityContainer can add entity sets.
        /// </summary>
        [TestMethod]
        public void EdmEntityContainer_CanAddEntitySets()
        {
            var container = new EdmEntityContainer();
            var entitySet = new EdmEntitySet
            {
                Name = "Customers",
                EntityType = "Customer"
            };

            container.EntitySets.Add(entitySet);

            container.EntitySets.Should().HaveCount(1);
            container.EntitySets.First().Should().BeSameAs(entitySet);
        }

        /// <summary>
        /// Tests that EdmEntityContainer can add singletons.
        /// </summary>
        [TestMethod]
        public void EdmEntityContainer_CanAddSingletons()
        {
            var container = new EdmEntityContainer();
            var singleton = new EdmSingleton
            {
                Name = "Me",
                Type = "Person"
            };

            container.Singletons.Add(singleton);

            container.Singletons.Should().HaveCount(1);
            container.Singletons.First().Should().BeSameAs(singleton);
        }

        #endregion

        #region EdmEntitySet Tests

        /// <summary>
        /// Tests that EdmEntitySet can be created with all properties.
        /// </summary>
        [TestMethod]
        public void EdmEntitySet_CanBeCreatedWithAllProperties()
        {
            var entitySet = new EdmEntitySet
            {
                Name = "Customers",
                EntityType = "Customer",
                IncludeInServiceDocument = false
            };

            var binding = new EdmNavigationPropertyBinding
            {
                Path = "Orders",
                Target = "Orders"
            };
            entitySet.NavigationPropertyBindings.Add(binding);

            entitySet.Name.Should().Be("Customers");
            entitySet.EntityType.Should().Be("Customer");
            entitySet.IncludeInServiceDocument.Should().BeFalse();
            entitySet.NavigationPropertyBindings.Should().HaveCount(1);
            entitySet.NavigationPropertyBindings.First().Should().BeSameAs(binding);
        }

        /// <summary>
        /// Tests that EdmEntitySet has correct default values.
        /// </summary>
        [TestMethod]
        public void EdmEntitySet_HasCorrectDefaultValues()
        {
            var entitySet = new EdmEntitySet();

            entitySet.Name.Should().BeNull();
            entitySet.EntityType.Should().BeNull();
            entitySet.IncludeInServiceDocument.Should().BeTrue();
            entitySet.NavigationPropertyBindings.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region EdmNavigationProperty Tests

        /// <summary>
        /// Tests that EdmNavigationProperty can be created with all properties.
        /// </summary>
        [TestMethod]
        public void EdmNavigationProperty_CanBeCreatedWithAllProperties()
        {
            var constraint = new EdmReferentialConstraint
            {
                Property = "CustomerId",
                ReferencedProperty = "Id"
            };

            var navProperty = new EdmNavigationProperty
            {
                Name = "Customer",
                Type = "Customer",
                IsNullable = false,
                Partner = "Orders",
                ContainsTarget = true,
                OnDelete = EdmOnDeleteAction.Cascade
            };
            navProperty.ReferentialConstraints.Add(constraint);

            navProperty.Name.Should().Be("Customer");
            navProperty.Type.Should().Be("Customer");
            navProperty.IsNullable.Should().BeFalse();
            navProperty.Partner.Should().Be("Orders");
            navProperty.ContainsTarget.Should().BeTrue();
            navProperty.OnDelete.Should().Be(EdmOnDeleteAction.Cascade);
            navProperty.ReferentialConstraints.Should().HaveCount(1);
            navProperty.ReferentialConstraints.First().Should().BeSameAs(constraint);
        }

        #endregion

        #region EdmAction Tests

        /// <summary>
        /// Tests that EdmAction can be created with all properties.
        /// </summary>
        [TestMethod]
        public void EdmAction_CanBeCreatedWithAllProperties()
        {
            var parameter = new EdmParameter
            {
                Name = "customerId",
                Type = EdmPrimitiveType.Int32,
                IsNullable = false
            };

            var action = new EdmAction
            {
                Name = "ProcessOrder",
                Namespace = "TestNamespace",
                IsBound = true,
                EntitySetPath = "Customers/Orders",
                ReturnType = "Edm.Boolean"
            };
            action.Parameters.Add(parameter);

            action.Name.Should().Be("ProcessOrder");
            action.Namespace.Should().Be("TestNamespace");
            action.IsBound.Should().BeTrue();
            action.EntitySetPath.Should().Be("Customers/Orders");
            action.ReturnType.Should().Be("Edm.Boolean");
            action.Parameters.Should().HaveCount(1);
            action.Parameters.First().Should().BeSameAs(parameter);
        }

        #endregion

        #region EdmFunction Tests

        /// <summary>
        /// Tests that EdmFunction can be created with all properties.
        /// </summary>
        [TestMethod]
        public void EdmFunction_CanBeCreatedWithAllProperties()
        {
            var parameter = new EdmParameter
            {
                Name = "rating",
                Type = EdmPrimitiveType.Int32,
                IsNullable = false
            };

            var function = new EdmFunction
            {
                Name = "GetTopCustomers",
                Namespace = "TestNamespace",
                IsBound = false,
                IsComposable = true,
                EntitySetPath = "Customers",
                ReturnType = "Collection(Customer)"
            };
            function.Parameters.Add(parameter);

            function.Name.Should().Be("GetTopCustomers");
            function.Namespace.Should().Be("TestNamespace");
            function.IsBound.Should().BeFalse();
            function.IsComposable.Should().BeTrue();
            function.EntitySetPath.Should().Be("Customers");
            function.ReturnType.Should().Be("Collection(Customer)");
            function.Parameters.Should().HaveCount(1);
            function.Parameters.First().Should().BeSameAs(parameter);
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Tests creating a complete model with relationships.
        /// </summary>
        [TestMethod]
        public void EdmModel_CanCreateCompleteModelWithRelationships()
        {
            var model = new EdmModel
            {
                Namespace = "TestModel",
                SchemaVersion = "4.0"
            };

            // Create Customer entity type
            var customerType = new EdmEntityType
            {
                Name = "Customer",
                Namespace = "TestModel"
            };

            var customerIdProperty = new EdmProperty
            {
                Name = "Id",
                Type = EdmPrimitiveType.Int32,
                IsNullable = false
            };
            customerType.Properties.Add(customerIdProperty);
            customerType.Keys.Add("Id");

            var customerNameProperty = new EdmProperty
            {
                Name = "Name",
                Type = EdmPrimitiveType.String,
                IsNullable = false,
                MaxLength = 100
            };
            customerType.Properties.Add(customerNameProperty);

            // Create Order entity type
            var orderType = new EdmEntityType
            {
                Name = "Order",
                Namespace = "TestModel"
            };

            var orderIdProperty = new EdmProperty
            {
                Name = "Id",
                Type = EdmPrimitiveType.Int32,
                IsNullable = false
            };
            orderType.Properties.Add(orderIdProperty);
            orderType.Keys.Add("Id");

            // Add navigation properties
            var ordersNavProperty = new EdmNavigationProperty
            {
                Name = "Orders",
                Type = "Collection(TestModel.Order)",
                Partner = "Customer"
            };
            customerType.NavigationProperties.Add(ordersNavProperty);

            var customerNavProperty = new EdmNavigationProperty
            {
                Name = "Customer",
                Type = "TestModel.Customer",
                Partner = "Orders"
            };
            orderType.NavigationProperties.Add(customerNavProperty);

            // Add entity types to model
            model.EntityTypes.Add(customerType);
            model.EntityTypes.Add(orderType);

            // Create entity container
            var container = new EdmEntityContainer
            {
                Name = "Container",
                Namespace = "TestModel"
            };

            var customersSet = new EdmEntitySet
            {
                Name = "Customers",
                EntityType = "TestModel.Customer"
            };
            container.EntitySets.Add(customersSet);

            var ordersSet = new EdmEntitySet
            {
                Name = "Orders",
                EntityType = "TestModel.Order"
            };
            container.EntitySets.Add(ordersSet);

            model.EntityContainer = container;

            // Verify the complete model
            model.EntityTypes.Should().HaveCount(2);
            model.EntityContainer.EntitySets.Should().HaveCount(2);
            
            var customer = model.EntityTypes.First(e => e.Name == "Customer");
            customer.Properties.Should().HaveCount(2);
            customer.NavigationProperties.Should().HaveCount(1);
            customer.Keys.Should().HaveCount(1);

            var order = model.EntityTypes.First(e => e.Name == "Order");
            order.Properties.Should().HaveCount(1);
            order.NavigationProperties.Should().HaveCount(1);
        }

        #endregion
    }

    /// <summary>
    /// Test enum for concurrency mode.
    /// </summary>
    public enum EdmConcurrencyMode
    {
        None,
        Fixed
    }

    /// <summary>
    /// Test enum for on delete action.
    /// </summary>
    public enum EdmOnDeleteAction
    {
        None,
        Cascade,
        SetNull,
        SetDefault
    }
}