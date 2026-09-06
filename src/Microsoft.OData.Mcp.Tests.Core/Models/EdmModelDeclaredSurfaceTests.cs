// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Models
{

    /// <summary>
    /// Verifies Core EDM types expose an IEdmModel-shaped declared surface.
    /// </summary>
    [TestClass]
    public class EdmModelDeclaredSurfaceTests
    {

        #region Public Methods

        /// <summary>
        /// EntityContainer returns the first container.
        /// </summary>
        [TestMethod]
        public void EdmModel_EntityContainer_ReturnsFirstContainer()
        {
            var model = new EdmModel
            {
                EntityContainers =
                [
                    new EdmEntityContainer
                    {
                        Name = "Default",
                        Namespace = "Sample"
                    }
                ]
            };

            model.EntityContainer.Should().NotBeNull();
            model.EntityContainer!.Name.Should().Be("Default");
        }

        /// <summary>
        /// Properties on an entity type are the declared surface only.
        /// </summary>
        [TestMethod]
        public void EdmEntityType_Properties_AreTheDeclaredSurface()
        {
            var type = new EdmEntityType
            {
                Name = "Customer",
                Namespace = "Sample",
                Properties =
                [
                    new EdmProperty
                    {
                        Name = "Id",
                        Type = "Edm.Int32",
                        Nullable = false
                    }
                ]
            };

            type.Properties.Should().ContainSingle(property => property.Name == "Id");
            type.Properties.Should().NotContain(property => property.Name == "InternalSecret");
        }

        #endregion

    }

}
