// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Models
{

    /// <summary>
    /// Exercises the public Core EDM helpers that catalogs and the parser rely on.
    /// </summary>
    [TestClass]
    public class EdmModelSurfaceTests
    {

        #region Public Methods

        /// <summary>
        /// Action imports store annotations, equality, and string form.
        /// </summary>
        [TestMethod]
        public void EdmActionImport_AnnotationsAndEquality()
        {
            var first = new EdmActionImport
            {
                Action = "Trippin.Reset",
                Name = "Reset"
            };
            var import = new EdmActionImport("Reset", "Trippin.Reset")
            {
                EntitySet = "People",
                Name = "Reset",
                Action = "Trippin.Reset"
            };

            first.Name = "Reset";
            first.Action = "Trippin.Reset";
            first.EntitySet = "People";
            import.AddAnnotation("term", 1);
            import.GetAnnotation<int>("term").Should().Be(1);
            import.GetAnnotation<string>("missing").Should().BeNull();
            import.ToString().Should().Be("Reset -> Trippin.Reset");
            import.Equals(first).Should().BeTrue();
            import.Equals("no").Should().BeFalse();
            import.GetHashCode().Should().Be(first.GetHashCode());
        }

        /// <summary>
        /// Actions expose FullName with and without a namespace.
        /// </summary>
        [TestMethod]
        public void EdmAction_FullName_UsesNamespaceWhenPresent()
        {
            var unnamed = new EdmAction();
            var action = new EdmAction("Reset", "Trippin");

            unnamed.Name = "Reset";
            unnamed.FullName.Should().Be("Reset");
            action.FullName.Should().Be("Trippin.Reset");
            new EdmAction(null!, "Trippin").Name.Should().BeEmpty();
        }

        /// <summary>
        /// Complex types expose lookup helpers, flags, and equality.
        /// </summary>
        [TestMethod]
        public void EdmComplexType_LookupsAndEquality()
        {
            var type = new EdmComplexType
            {
                Name = "Location",
                Namespace = "Trippin"
            };
            var named = new EdmComplexType("Location", "Trippin")
            {
                Abstract = true,
                BaseType = "Trippin.Point",
                Name = "Location",
                Namespace = "Trippin",
                OpenType = true
            };
            named.Properties.Add(new EdmProperty("City", "Edm.String")
            {
                Name = "City",
                Type = "Edm.String"
            });
            named.NavigationProperties.Add(new EdmNavigationProperty("Photos", "Collection(Trippin.Photo)")
            {
                Name = "Photos",
                Type = "Collection(Trippin.Photo)"
            });

            type.Name = "Location";
            type.Namespace = "Trippin";
            named.IsAbstract.Should().BeTrue();
            named.HasBaseType.Should().BeTrue();
            named.HasNavigationProperties.Should().BeTrue();
            named.HasProperty("City").Should().BeTrue();
            named.HasNavigationProperty("Photos").Should().BeTrue();
            named.GetProperty("City")!.Name.Should().Be("City");
            named.GetNavigationProperty("Photos")!.Name.Should().Be("Photos");
            named.HasProperty("Missing").Should().BeFalse();
            named.HasNavigationProperty("Missing").Should().BeFalse();
            named.ToString().Should().Be("Trippin.Location");
            named.Equals(new EdmComplexType("Other", "Trippin")
            {
                Name = "Other",
                Namespace = "Trippin"
            }).Should().BeFalse();
            named.GetHashCode().Should().NotBe(0);
        }

        /// <summary>
        /// Entity containers expose lookups, duplicate guards, and equality.
        /// </summary>
        [TestMethod]
        public void EdmEntityContainer_LookupsDuplicateAndEquality()
        {
            var container = new EdmEntityContainer
            {
                Name = "Container",
                Namespace = "Trippin"
            };
            var named = new EdmEntityContainer("Container", "Trippin")
            {
                Extends = "Trippin.Base",
                Name = "Container",
                Namespace = "Trippin"
            };
            var people = new EdmEntitySet("People", "Trippin.Person")
            {
                Name = "People",
                EntityType = "Trippin.Person"
            };
            var me = new EdmSingleton("Me", "Trippin.Person")
            {
                Name = "Me",
                Type = "Trippin.Person"
            };

            container.Name = "Container";
            container.Namespace = "Trippin";
            named.AddEntitySet(people);
            named.AddSingleton(me);
            named.FunctionImports.Add(new EdmFunctionImport("GetNearest", "Trippin.GetNearest")
            {
                Name = "GetNearest",
                Function = "Trippin.GetNearest"
            });
            named.ActionImports.Add(new EdmActionImport("Reset", "Trippin.Reset")
            {
                Name = "Reset",
                Action = "Trippin.Reset"
            });
            named.HasBaseContainer.Should().BeTrue();
            named.HasEntitySets.Should().BeTrue();
            named.HasSingletons.Should().BeTrue();
            named.HasFunctionImports.Should().BeTrue();
            named.HasActionImports.Should().BeTrue();
            named.GetEntitySet("People")!.Name.Should().Be("People");
            named.GetSingleton("Me")!.Name.Should().Be("Me");
            named.GetFunctionImport("GetNearest")!.Name.Should().Be("GetNearest");
            named.GetActionImport("Reset")!.Name.Should().Be("Reset");
            var duplicateSet = () => named.AddEntitySet(people);
            duplicateSet.Should().Throw<InvalidOperationException>();
            var duplicateSingleton = () => named.AddSingleton(me);
            duplicateSingleton.Should().Throw<InvalidOperationException>();
            named.AddAnnotation("term", "value");
            named.GetAnnotation<string>("term").Should().Be("value");
            named.GetAnnotation<int>("term").Should().Be(0);
            named.ToString().Should().Be("Trippin.Container");
            named.Equals(container).Should().BeFalse();
            named.GetHashCode().Should().NotBe(0);
        }

        /// <summary>
        /// Entity sets expose bindings, annotations, and type name splitting.
        /// </summary>
        [TestMethod]
        public void EdmEntitySet_BindingsAndTypeName()
        {
            var set = new EdmEntitySet
            {
                EntityType = "Trippin.Person",
                Name = "People"
            };
            var named = new EdmEntitySet("People", "Person")
            {
                Name = "People",
                EntityType = "Person"
            };

            set.Name = "People";
            set.EntityType = "Trippin.Person";
            named.EntityTypeName.Should().Be("Person");
            named.EntityTypeNamespace.Should().BeEmpty();
            set.EntityTypeName.Should().Be("Person");
            set.EntityTypeNamespace.Should().Be("Trippin");
            named.AddNavigationPropertyBinding("Friends", "People");
            named.AddNavigationPropertyBinding("Friends", "Others");
            named.HasNavigationPropertyBindings.Should().BeTrue();
            named.GetNavigationPropertyBinding("Friends")!.Target.Should().Be("Others");
            named.RemoveNavigationPropertyBinding("Friends").Should().BeTrue();
            named.RemoveNavigationPropertyBinding("Friends").Should().BeFalse();
            named.AddAnnotation("term", true);
            named.GetAnnotation<bool>("term").Should().BeTrue();
            named.GetAnnotation<string>("missing").Should().BeNull();
            named.ToString().Should().Contain("People");
            named.Equals(set).Should().BeFalse();
            named.GetHashCode().Should().NotBe(0);
        }

        /// <summary>
        /// Entity types expose keys, equality, and default construction.
        /// </summary>
        [TestMethod]
        public void EdmEntityType_KeysFlagsAndEquality()
        {
            var type = new EdmEntityType
            {
                Name = "Person",
                Namespace = "Trippin"
            };
            var named = new EdmEntityType("Person", "Trippin")
            {
                Abstract = true,
                BaseType = "Trippin.Being",
                HasStream = true,
                Key = ["UserName"],
                Name = "Person",
                Namespace = "Trippin",
                OpenType = true
            };
            named.Properties.Add(new EdmProperty("UserName", "Edm.String")
            {
                Name = "UserName",
                Type = "Edm.String"
            });
            named.Properties.Add(new EdmProperty("FirstName", "Edm.String")
            {
                Name = "FirstName",
                Type = "Edm.String"
            });
            named.NavigationProperties.Add(new EdmNavigationProperty("Friends", "Collection(Trippin.Person)")
            {
                Name = "Friends",
                Type = "Collection(Trippin.Person)"
            });

            type.Name = "Person";
            type.Namespace = "Trippin";
            named.IsAbstract.Should().BeTrue();
            named.HasBaseType.Should().BeTrue();
            named.HasNavigationProperties.Should().BeTrue();
            named.KeyProperties.Select(property => property.Name).Should().Equal("UserName");
            named.NonKeyProperties.Select(property => property.Name).Should().Equal("FirstName");
            named.GetNavigationProperty("Friends")!.Name.Should().Be("Friends");
            named.HasNavigationProperty("Friends").Should().BeTrue();
            named.Equals(type).Should().BeFalse();
            named.Equals("no").Should().BeFalse();
            named.GetHashCode().Should().NotBe(0);
        }

        /// <summary>
        /// Function imports store annotations and equality.
        /// </summary>
        [TestMethod]
        public void EdmFunctionImport_AnnotationsAndEquality()
        {
            var first = new EdmFunctionImport
            {
                Function = "Trippin.GetNearest",
                Name = "GetNearest"
            };
            var import = new EdmFunctionImport("GetNearest", "Trippin.GetNearest")
            {
                EntitySet = "People",
                Function = "Trippin.GetNearest",
                IncludeInServiceDocument = false,
                Name = "GetNearest"
            };

            first.Name = "GetNearest";
            first.Function = "Trippin.GetNearest";
            first.EntitySet = "People";
            first.IncludeInServiceDocument = false;
            import.AddAnnotation("term", "x");
            import.GetAnnotation<string>("term").Should().Be("x");
            import.GetAnnotation<int>("term").Should().Be(0);
            import.ToString().Should().Be("GetNearest -> Trippin.GetNearest");
            import.Equals(first).Should().BeTrue();
            import.Equals("no").Should().BeFalse();
            import.GetHashCode().Should().Be(first.GetHashCode());
        }

        /// <summary>
        /// Functions expose FullName with and without a namespace.
        /// </summary>
        [TestMethod]
        public void EdmFunction_FullName_UsesNamespaceWhenPresent()
        {
            var unnamed = new EdmFunction();
            var function = new EdmFunction("GetNearest", "Trippin");

            unnamed.Name = "GetNearest";
            unnamed.FullName.Should().Be("GetNearest");
            function.FullName.Should().Be("Trippin.GetNearest");
            new EdmFunction(null!, null!).Name.Should().BeEmpty();
        }

        /// <summary>
        /// Models add types, resolve lookups, validate, and reject duplicates.
        /// </summary>
        [TestMethod]
        public void EdmModel_LookupsValidationAndDuplicates()
        {
            var model = new EdmModel("4.01");
            var person = new EdmEntityType("Person", "Trippin")
            {
                Name = "Person",
                Namespace = "Trippin"
            };
            person.NavigationProperties.Add(new EdmNavigationProperty("Friends", "Collection(Trippin.Person)")
            {
                Name = "Friends",
                Type = "Collection(Trippin.Person)"
            });
            var location = new EdmComplexType("Location", "Trippin")
            {
                Name = "Location",
                Namespace = "Trippin"
            };
            var container = new EdmEntityContainer("Container", "Trippin")
            {
                Name = "Container",
                Namespace = "Trippin"
            };
            container.AddEntitySet(new EdmEntitySet("People", "Trippin.Person")
            {
                Name = "People",
                EntityType = "Trippin.Person"
            });
            container.AddSingleton(new EdmSingleton("Me", "Trippin.Person")
            {
                Name = "Me",
                Type = "Trippin.Person"
            });
            container.AddEntitySet(new EdmEntitySet("Ghosts", "Trippin.Ghost")
            {
                Name = "Ghosts",
                EntityType = "Trippin.Ghost"
            });
            container.AddSingleton(new EdmSingleton("Nobody", "Trippin.Ghost")
            {
                Name = "Nobody",
                Type = "Trippin.Ghost"
            });
            person.NavigationProperties.Add(new EdmNavigationProperty("Unknown", "Trippin.Ghost")
            {
                Name = "Unknown",
                Type = "Trippin.Ghost"
            });

            model.AddComplexType(new EdmComplexType("City", "Geo")
            {
                Name = "City",
                Namespace = "Geo"
            });
            model.AddEntityContainer(new EdmEntityContainer("Maps", "Cartography")
            {
                Name = "Maps",
                Namespace = "Cartography"
            });
            model.AddEntityType(person);
            model.AddComplexType(location);
            model.AddEntityContainer(container);
            model.AddEntityType(new EdmEntityType("Person", "Other")
            {
                Name = "Person",
                Namespace = "Other"
            });
            model.AddComplexType(new EdmComplexType("Location", "Other")
            {
                Name = "Location",
                Namespace = "Other"
            });
            model.AddEntityContainer(new EdmEntityContainer("Container", "Other")
            {
                Name = "Container",
                Namespace = "Other"
            });

            model.HasEntityTypes.Should().BeTrue();
            model.HasComplexTypes.Should().BeTrue();
            model.HasEntityContainers.Should().BeTrue();
            model.AllSingletons.Select(item => item.Name).Should().Contain("Me");
            model.GetEntityType("Person", "Trippin")!.Namespace.Should().Be("Trippin");
            model.GetComplexType("Trippin.Location")!.Name.Should().Be("Location");
            model.GetComplexType("Location", "Trippin")!.Name.Should().Be("Location");
            model.GetEntityContainer("Trippin.Container")!.Name.Should().Be("Container");
            model.GetEntityContainer("Container", "Trippin")!.Name.Should().Be("Container");
            model.GetEntitySet("People")!.Name.Should().Be("People");
            model.GetSingleton("Me")!.Name.Should().Be("Me");
            model.AddAnnotation("term", 7);
            model.GetAnnotation<int>("term").Should().Be(7);
            model.GetAnnotation<string>("term").Should().BeNull();
            var errors = model.Validate().ToList();
            errors.Should().Contain(error => error.Contains("Ghosts"));
            errors.Should().Contain(error => error.Contains("Nobody"));
            errors.Should().Contain(error => error.Contains("Unknown"));
            model.AddEntityType(new EdmEntityType("Airport", "Trippin")
            {
                Name = "Airport",
                Namespace = "Trippin"
            });
            model.ToString().Should().Contain("EDM v4.01");
            var addNullComplex = () => model.AddComplexType(null!);
            addNullComplex.Should().Throw<ArgumentNullException>();
            var addNullContainer = () => model.AddEntityContainer(null!);
            addNullContainer.Should().Throw<ArgumentNullException>();
            var duplicateType = () => model.AddEntityType(person);
            duplicateType.Should().Throw<InvalidOperationException>();
            var duplicateComplex = () => model.AddComplexType(location);
            duplicateComplex.Should().Throw<InvalidOperationException>();
            var duplicateContainer = () => model.AddEntityContainer(container);
            duplicateContainer.Should().Throw<InvalidOperationException>();
        }

        /// <summary>
        /// Navigation property bindings expose equality and string form.
        /// </summary>
        [TestMethod]
        public void EdmNavigationPropertyBinding_Equality()
        {
            var first = new EdmNavigationPropertyBinding
            {
                Path = "Friends",
                Target = "People"
            };
            var binding = new EdmNavigationPropertyBinding("Friends", "People")
            {
                Path = "Friends",
                Target = "People"
            };

            first.Path = "Friends";
            first.Target = "People";
            binding.ToString().Should().Be("Friends -> People");
            binding.Equals(first).Should().BeTrue();
            binding.Equals("no").Should().BeFalse();
            binding.GetHashCode().Should().Be(first.GetHashCode());
        }

        /// <summary>
        /// Navigation properties expose multiplicity, required, and equality.
        /// </summary>
        [TestMethod]
        public void EdmNavigationProperty_MultiplicityAndEquality()
        {
            var empty = new EdmNavigationProperty
            {
                Name = "Friends",
                Type = "Collection(Trippin.Person)"
            };
            var many = new EdmNavigationProperty("Friends", "Collection(Trippin.Person)")
            {
                ContainsTarget = true,
                Name = "Friends",
                OnDelete = "Cascade",
                Partner = "Friends",
                Type = "Collection(Trippin.Person)"
            };
            var one = new EdmNavigationProperty("BestFriend", "Trippin.Person")
            {
                Name = "BestFriend",
                Nullable = false,
                Type = "Trippin.Person"
            };
            var optional = new EdmNavigationProperty("Manager", "Trippin.Person")
            {
                Name = "Manager",
                Type = "Trippin.Person"
            };

            empty.Name = "Friends";
            empty.Type = "Collection(Trippin.Person)";
            many.IsCollection.Should().BeTrue();
            many.IsRequired.Should().BeFalse();
            many.TargetType.Should().Be("Trippin.Person");
            many.Multiplicity.Should().Be("Many");
            one.IsRequired.Should().BeTrue();
            one.Multiplicity.Should().Be("One");
            optional.Multiplicity.Should().Be("ZeroOrOne");
            many.ToString().Should().Contain("Friends");
            many.Equals(empty).Should().BeFalse();
            many.GetHashCode().Should().NotBe(0);
        }

        /// <summary>
        /// Parameters construct from name and type.
        /// </summary>
        [TestMethod]
        public void EdmParameter_Constructors()
        {
            var empty = new EdmParameter();
            var parameter = new EdmParameter("binding", "Trippin.Person");

            empty.Name.Should().BeEmpty();
            parameter.Name.Should().Be("binding");
            parameter.Type.Should().Be("Trippin.Person");
            new EdmParameter(null!, null!).Name.Should().BeEmpty();
        }

        /// <summary>
        /// Enum types expose full name, member lookup, and equality; the model rejects duplicates.
        /// </summary>
        [TestMethod]
        public void EdmEnumType_LookupsAndEquality()
        {
            var color = new EdmEnumType("Color", "NS")
            {
                Members =
                [
                    new EdmEnumMember("Red", 0),
                    new EdmEnumMember("Green", 1)
                ]
            };
            var same = new EdmEnumType("Color", "NS")
            {
                Members =
                [
                    new EdmEnumMember("Red", 0),
                    new EdmEnumMember("Green", 1)
                ]
            };
            var flags = new EdmEnumType("Color", "NS")
            {
                IsFlags = true,
                Members = [new EdmEnumMember("Red", 0)]
            };
            var model = new EdmModel();

            color.FullName.Should().Be("NS.Color");
            color.UnderlyingType.Should().Be("Edm.Int32");
            color.IsFlags.Should().BeFalse();
            color.GetMember("Green")!.Value.Should().Be(1);
            color.GetMember("Blue").Should().BeNull();
            color.HasMember("Red").Should().BeTrue();
            color.ToString().Should().Be("NS.Color");
            color.Equals(same).Should().BeTrue();
            color.GetHashCode().Should().Be(same.GetHashCode());
            color.Equals(flags).Should().BeFalse();
            color.Equals("no").Should().BeFalse();
            new EdmEnumMember("Red", 0).Equals(new EdmEnumMember("Red", 0)).Should().BeTrue();
            new EdmEnumMember("Red", 0).ToString().Should().Be("Red = 0");

            model.AddEnumType(color);
            model.EnumTypes.Should().ContainSingle();
            model.Namespaces.Should().Contain("NS");
            model.GetEnumType("NS.Color").Should().BeSameAs(color);
            model.GetEnumType("Color", "NS").Should().BeSameAs(color);
            model.GetEnumType("NS.Missing").Should().BeNull();
            model.HasEnumTypes.Should().BeTrue();

            var duplicate = () => model.AddEnumType(same);
            duplicate.Should().Throw<InvalidOperationException>().WithMessage("*NS.Color*");

            var nullAdd = () => model.AddEnumType(null!);
            nullAdd.Should().Throw<ArgumentNullException>();

            var blankName = () => new EdmEnumType(" ", "NS");
            blankName.Should().Throw<ArgumentException>();

            var blankMember = () => new EdmEnumMember(" ", 0);
            blankMember.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// Properties expose collection, primitive, and equality helpers.
        /// </summary>
        [TestMethod]
        public void EdmProperty_CollectionPrimitiveAndEquality()
        {
            var empty = new EdmProperty
            {
                Name = "Name",
                Type = "Edm.String"
            };
            var primitive = new EdmProperty("Name", "Edm.String")
            {
                DefaultValue = "Ann",
                IsKey = true,
                MaxLength = 40,
                Name = "Name",
                Precision = 2,
                Scale = 1,
                SRID = "4326",
                Type = "Edm.String",
                Unicode = true
            };
            var collection = new EdmProperty("Tags", "Collection(Edm.String)")
            {
                Name = "Tags",
                Type = "Collection(Edm.String)"
            };

            empty.Name = "Name";
            empty.Type = "Edm.String";
            primitive.TypeName.Should().Be("Edm.String");
            primitive.IsNullable.Should().BeTrue();
            primitive.HasDefaultValue.Should().BeTrue();
            primitive.IsPrimitive.Should().BeTrue();
            primitive.IsCollection.Should().BeFalse();
            primitive.ElementType.Should().Be("Edm.String");
            collection.IsCollection.Should().BeTrue();
            collection.ElementType.Should().Be("Edm.String");
            primitive.ToString().Should().Be("Name: Edm.String");
            primitive.Computed.Should().BeFalse();
            primitive.Equals(empty).Should().BeFalse();
            primitive.Equals(new EdmProperty("Name", "Edm.String") { Computed = true, DefaultValue = "Ann", IsKey = true, MaxLength = 40, Name = "Name", Precision = 2, Scale = 1, SRID = "4326", Type = "Edm.String", Unicode = true }).Should().BeFalse("Computed participates in equality");
            primitive.Equals("no").Should().BeFalse();
            primitive.GetHashCode().Should().NotBe(0);
        }

        /// <summary>
        /// Referential constraints construct, stringify, and compare.
        /// </summary>
        [TestMethod]
        public void EdmReferentialConstraint_Equality()
        {
            var empty = new EdmReferentialConstraint
            {
                Property = "AirlineCode",
                ReferencedProperty = "AirlineCode"
            };
            var constraint = new EdmReferentialConstraint("AirlineCode", "AirlineCode")
            {
                Property = "AirlineCode",
                ReferencedProperty = "AirlineCode"
            };

            empty.Property = "AirlineCode";
            empty.ReferencedProperty = "AirlineCode";
            constraint.ToString().Should().Be("AirlineCode -> AirlineCode");
            constraint.Equals(empty).Should().BeTrue();
            constraint.Equals("no").Should().BeFalse();
            constraint.GetHashCode().Should().Be(empty.GetHashCode());
        }

        /// <summary>
        /// Singletons expose type name splitting, bindings, and equality.
        /// </summary>
        [TestMethod]
        public void EdmSingleton_BindingsAndTypeName()
        {
            var empty = new EdmSingleton
            {
                Name = "Me",
                Type = "Trippin.Person"
            };
            var named = new EdmSingleton("Me", "Person")
            {
                Name = "Me",
                Type = "Person"
            };

            empty.Name = "Me";
            empty.Type = "Trippin.Person";
            named.TypeName.Should().Be("Person");
            named.TypeNamespace.Should().BeEmpty();
            empty.TypeName.Should().Be("Person");
            empty.TypeNamespace.Should().Be("Trippin");
            named.AddNavigationPropertyBinding("Friends", "People");
            named.AddNavigationPropertyBinding("Friends", "Others");
            named.HasNavigationPropertyBindings.Should().BeTrue();
            named.GetNavigationPropertyBinding("Friends")!.Target.Should().Be("Others");
            named.RemoveNavigationPropertyBinding("Friends").Should().BeTrue();
            named.RemoveNavigationPropertyBinding("Friends").Should().BeFalse();
            named.AddAnnotation("term", 3);
            named.GetAnnotation<int>("term").Should().Be(3);
            named.GetAnnotation<string>("term").Should().BeNull();
            named.ToString().Should().Contain("Me");
            named.Equals(empty).Should().BeFalse();
            named.GetHashCode().Should().NotBe(0);
        }

        #endregion

    }

}
