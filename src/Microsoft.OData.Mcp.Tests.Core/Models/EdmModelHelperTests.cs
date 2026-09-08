// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Models
{

    /// <summary>
    /// Coverage for Core EDM helpers used by catalogs.
    /// </summary>
    [TestClass]
    public class EdmModelHelperTests
    {

        #region Public Methods

        /// <summary>
        /// GetEntityType resolves by full name.
        /// </summary>
        [TestMethod]
        public void GetEntityType_ByFullName_ReturnsType()
        {
            var model = new EdmModel();
            var type = new EdmEntityType("Person", "Trippin")
            {
                Name = "Person",
                Namespace = "Trippin",
                Key = ["UserName"]
            };
            type.Properties.Add(new EdmProperty("UserName", "Edm.String")
            {
                Name = "UserName",
                Type = "Edm.String"
            });
            model.AddEntityType(type);

            model.GetEntityType("Trippin.Person")!.Name.Should().Be("Person");
            type.HasProperty("UserName").Should().BeTrue();
            type.GetProperty("UserName")!.Name.Should().Be("UserName");
            type.HasNavigationProperty("Friends").Should().BeFalse();
            type.FullName.Should().Be("Trippin.Person");
            type.ToString().Should().Contain("Person");
        }

        /// <summary>
        /// Entity set helpers expose the short type name.
        /// </summary>
        [TestMethod]
        public void EdmEntitySet_EntityTypeName_StripsNamespace()
        {
            var set = new EdmEntitySet("People", "Trippin.Person")
            {
                Name = "People",
                EntityType = "Trippin.Person"
            };

            set.EntityTypeName.Should().Be("Person");
            set.EntityTypeNamespace.Should().Be("Trippin");
        }

        /// <summary>
        /// Navigation target type strips Collection().
        /// </summary>
        [TestMethod]
        public void EdmNavigationProperty_TargetTypeName_StripsCollection()
        {
            var navigation = new EdmNavigationProperty("Friends", "Collection(Trippin.Person)")
            {
                Name = "Friends",
                Type = "Collection(Trippin.Person)"
            };

            navigation.TargetTypeName.Should().Be("Trippin.Person");
            new EdmNavigationProperty("BestFriend", "Trippin.Person")
            {
                Name = "BestFriend",
                Type = "Trippin.Person"
            }.TargetTypeName.Should().Be("Trippin.Person");
        }

        /// <summary>
        /// Primitive type names exist for JSON mapping.
        /// </summary>
        [TestMethod]
        public void EdmPrimitiveType_HasStringAndBinary()
        {
            System.Enum.GetNames<EdmPrimitiveType>().Should().Contain("String");
            System.Enum.GetNames<EdmPrimitiveType>().Should().Contain("Binary");
        }

        #endregion

    }

}
