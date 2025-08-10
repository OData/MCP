// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents an entity type in an OData model.
    /// </summary>
    /// <remarks>
    /// Entity types define the structure of entities in an OData service, including their
    /// properties, navigation properties, and key definitions. They form the foundation
    /// of the entity data model and determine how data is structured and accessed.
    /// </remarks>
    public sealed class EdmEntityType
    {

        #region Properties

        /// <summary>
        /// Gets or sets the name of the entity type.
        /// </summary>
        /// <value>The local name of the entity type within its namespace.</value>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the namespace of the entity type.
        /// </summary>
        /// <value>The namespace that contains this entity type.</value>
        public required string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the base type of the entity type.
        /// </summary>
        /// <value>The fully qualified name of the base entity type, or <c>null</c> if this type has no base type.</value>
        /// <remarks>
        /// When specified, this entity type inherits all properties and navigation properties
        /// from the base type. The inheritance hierarchy must be consistent within the model.
        /// </remarks>
        public string? BaseType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this entity type is abstract.
        /// </summary>
        /// <value><c>true</c> if the entity type is abstract; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Abstract entity types cannot be instantiated directly but can serve as base types
        /// for other entity types. They are useful for defining common properties and behaviors.
        /// </remarks>
        public bool Abstract { get; set; }

        /// <summary>
        /// Gets a value indicating whether this entity type is abstract.
        /// </summary>
        /// <value><c>true</c> if the entity type is abstract; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// This is an alias for the <see cref="Abstract"/> property for compatibility.
        /// </remarks>
        [JsonIgnore]
        public bool IsAbstract => Abstract;

        /// <summary>
        /// Gets or sets a value indicating whether this entity type is open.
        /// </summary>
        /// <value><c>true</c> if the entity type is open; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Open entity types allow additional properties beyond those explicitly defined
        /// in the metadata. This provides flexibility for dynamic scenarios where the
        /// complete structure may not be known at design time.
        /// </remarks>
        public bool OpenType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this entity type has a stream.
        /// </summary>
        /// <value><c>true</c> if the entity type has a stream; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When true, instances of this entity type can have an associated media resource
        /// (such as a photo or document) that can be accessed via streaming operations.
        /// </remarks>
        public bool HasStream { get; set; }

        /// <summary>
        /// Gets or sets the properties of the entity type.
        /// </summary>
        /// <value>A collection of structural properties that define the data elements of the entity.</value>
        /// <remarks>
        /// These properties represent the data that can be stored and retrieved for instances
        /// of this entity type. They include both key and non-key properties.
        /// </remarks>
        public List<EdmProperty> Properties { get; set; } = [];

        /// <summary>
        /// Gets or sets the navigation properties of the entity type.
        /// </summary>
        /// <value>A collection of navigation properties that define relationships to other entities.</value>
        /// <remarks>
        /// Navigation properties enable traversal between related entities and define the
        /// relationship structure of the data model.
        /// </remarks>
        public List<EdmNavigationProperty> NavigationProperties { get; set; } = [];

        /// <summary>
        /// Gets or sets the key properties of the entity type.
        /// </summary>
        /// <value>A collection of property names that form the entity key.</value>
        /// <remarks>
        /// Key properties uniquely identify instances of the entity type. They are used
        /// for addressing individual entities and establishing relationships.
        /// </remarks>
        public List<string> Key { get; set; } = [];

        /// <summary>
        /// Gets the fully qualified name of the entity type.
        /// </summary>
        /// <value>The namespace and name combined with a dot separator.</value>
        [JsonIgnore]
        public string FullName => $"{Namespace}.{Name}";

        /// <summary>
        /// Gets the key properties as <see cref="EdmProperty"/> objects.
        /// </summary>
        /// <value>A collection of properties that form the entity key.</value>
        [JsonIgnore]
        public IEnumerable<EdmProperty> KeyProperties => Properties.Where(p => Key.Contains(p.Name));

        /// <summary>
        /// Gets the non-key properties of the entity type.
        /// </summary>
        /// <value>A collection of properties that are not part of the entity key.</value>
        [JsonIgnore]
        public IEnumerable<EdmProperty> NonKeyProperties => Properties.Where(p => !Key.Contains(p.Name));

        /// <summary>
        /// Gets a value indicating whether this entity type has any navigation properties.
        /// </summary>
        /// <value><c>true</c> if the entity type has navigation properties; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasNavigationProperties => NavigationProperties.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this entity type inherits from another entity type.
        /// </summary>
        /// <value><c>true</c> if the entity type has a base type; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasBaseType => !string.IsNullOrWhiteSpace(BaseType);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEntityType"/> class.
        /// </summary>
        public EdmEntityType()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEntityType"/> class with the specified name and namespace.
        /// </summary>
        /// <param name="name">The name of the entity type.</param>
        /// <param name="namespace">The namespace of the entity type.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="namespace"/> is null or whitespace.</exception>
        public EdmEntityType(string name, string @namespace)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);

            Name = name;
            Namespace = @namespace;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a property by name.
        /// </summary>
        /// <param name="propertyName">The name of the property to retrieve.</param>
        /// <returns>The property with the specified name, or <c>null</c> if not found.</returns>
        public EdmProperty? GetProperty(string propertyName)
        {
            return Properties.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets a navigation property by name.
        /// </summary>
        /// <param name="propertyName">The name of the navigation property to retrieve.</param>
        /// <returns>The navigation property with the specified name, or <c>null</c> if not found.</returns>
        public EdmNavigationProperty? GetNavigationProperty(string propertyName)
        {
            return NavigationProperties.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Determines whether the entity type has a property with the specified name.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        /// <returns><c>true</c> if the entity type has the property; otherwise, <c>false</c>.</returns>
        public bool HasProperty(string propertyName)
        {
            return Properties.Any(p => p.Name.Equals(propertyName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Determines whether the entity type has a navigation property with the specified name.
        /// </summary>
        /// <param name="propertyName">The name of the navigation property to check.</param>
        /// <returns><c>true</c> if the entity type has the navigation property; otherwise, <c>false</c>.</returns>
        public bool HasNavigationProperty(string propertyName)
        {
            return NavigationProperties.Any(p => p.Name.Equals(propertyName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Returns a string representation of the entity type.
        /// </summary>
        /// <returns>The fully qualified name of the entity type.</returns>
        public override string ToString()
        {
            return FullName;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current entity type.
        /// </summary>
        /// <param name="obj">The object to compare with the current entity type.</param>
        /// <returns><c>true</c> if the specified object is equal to the current entity type; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmEntityType other &&
                   Name == other.Name &&
                   Namespace == other.Namespace &&
                   BaseType == other.BaseType &&
                   Abstract == other.Abstract &&
                   OpenType == other.OpenType &&
                   HasStream == other.HasStream &&
                   Properties.SequenceEqual(other.Properties) &&
                   NavigationProperties.SequenceEqual(other.NavigationProperties) &&
                   Key.SequenceEqual(other.Key);
        }

        /// <summary>
        /// Returns a hash code for the current entity type.
        /// </summary>
        /// <returns>A hash code for the current entity type.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Namespace, BaseType, Abstract, OpenType, HasStream);
        }

        #endregion

    }

}
