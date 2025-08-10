using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents a complex type in an OData model.
    /// </summary>
    /// <remarks>
    /// Complex types are structured types that consist of a set of properties but do not have a key.
    /// They are used to define reusable data structures that can be used as property types in
    /// entity types or other complex types. Complex types cannot exist independently; they must
    /// be contained within an entity or another complex type.
    /// </remarks>
    public sealed class EdmComplexType
    {

        #region Properties

        /// <summary>
        /// Gets or sets the name of the complex type.
        /// </summary>
        /// <value>The local name of the complex type within its namespace.</value>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the namespace of the complex type.
        /// </summary>
        /// <value>The namespace that contains this complex type.</value>
        public required string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the base type of the complex type.
        /// </summary>
        /// <value>The fully qualified name of the base complex type, or <c>null</c> if this type has no base type.</value>
        /// <remarks>
        /// When specified, this complex type inherits all properties from the base type.
        /// The inheritance hierarchy must be consistent within the model.
        /// </remarks>
        public string? BaseType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this complex type is abstract.
        /// </summary>
        /// <value><c>true</c> if the complex type is abstract; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Abstract complex types cannot be instantiated directly but can serve as base types
        /// for other complex types. They are useful for defining common properties and behaviors.
        /// </remarks>
        public bool Abstract { get; set; }

        /// <summary>
        /// Gets a value indicating whether this complex type is abstract.
        /// </summary>
        /// <value><c>true</c> if the complex type is abstract; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// This is an alias for the <see cref="Abstract"/> property for compatibility.
        /// </remarks>
        [JsonIgnore]
        public bool IsAbstract => Abstract;

        /// <summary>
        /// Gets or sets a value indicating whether this complex type is open.
        /// </summary>
        /// <value><c>true</c> if the complex type is open; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Open complex types allow additional properties beyond those explicitly defined
        /// in the metadata. This provides flexibility for dynamic scenarios where the
        /// complete structure may not be known at design time.
        /// </remarks>
        public bool OpenType { get; set; }

        /// <summary>
        /// Gets or sets the properties of the complex type.
        /// </summary>
        /// <value>A collection of structural properties that define the data elements of the complex type.</value>
        /// <remarks>
        /// These properties represent the data that can be stored and retrieved for instances
        /// of this complex type. Unlike entity types, complex types do not have key properties.
        /// </remarks>
        public List<EdmProperty> Properties { get; set; } = [];

        /// <summary>
        /// Gets or sets the navigation properties of the complex type.
        /// </summary>
        /// <value>A collection of navigation properties that define relationships to entities.</value>
        /// <remarks>
        /// Navigation properties in complex types enable navigation from the complex type
        /// to related entities, but the complex type itself cannot be the target of navigation.
        /// </remarks>
        public List<EdmNavigationProperty> NavigationProperties { get; set; } = [];

        /// <summary>
        /// Gets the fully qualified name of the complex type.
        /// </summary>
        /// <value>The namespace and name combined with a dot separator.</value>
        [JsonIgnore]
        public string FullName => $"{Namespace}.{Name}";

        /// <summary>
        /// Gets a value indicating whether this complex type has any navigation properties.
        /// </summary>
        /// <value><c>true</c> if the complex type has navigation properties; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasNavigationProperties => NavigationProperties.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this complex type inherits from another complex type.
        /// </summary>
        /// <value><c>true</c> if the complex type has a base type; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasBaseType => !string.IsNullOrWhiteSpace(BaseType);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmComplexType"/> class.
        /// </summary>
        public EdmComplexType()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmComplexType"/> class with the specified name and namespace.
        /// </summary>
        /// <param name="name">The name of the complex type.</param>
        /// <param name="namespace">The namespace of the complex type.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="namespace"/> is null or whitespace.</exception>
        public EdmComplexType(string name, string @namespace)
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
        /// Determines whether the complex type has a property with the specified name.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        /// <returns><c>true</c> if the complex type has the property; otherwise, <c>false</c>.</returns>
        public bool HasProperty(string propertyName)
        {
            return Properties.Any(p => p.Name.Equals(propertyName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Determines whether the complex type has a navigation property with the specified name.
        /// </summary>
        /// <param name="propertyName">The name of the navigation property to check.</param>
        /// <returns><c>true</c> if the complex type has the navigation property; otherwise, <c>false</c>.</returns>
        public bool HasNavigationProperty(string propertyName)
        {
            return NavigationProperties.Any(p => p.Name.Equals(propertyName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Returns a string representation of the complex type.
        /// </summary>
        /// <returns>The fully qualified name of the complex type.</returns>
        public override string ToString()
        {
            return FullName;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current complex type.
        /// </summary>
        /// <param name="obj">The object to compare with the current complex type.</param>
        /// <returns><c>true</c> if the specified object is equal to the current complex type; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmComplexType other &&
                   Name == other.Name &&
                   Namespace == other.Namespace &&
                   BaseType == other.BaseType &&
                   Abstract == other.Abstract &&
                   OpenType == other.OpenType &&
                   Properties.SequenceEqual(other.Properties) &&
                   NavigationProperties.SequenceEqual(other.NavigationProperties);
        }

        /// <summary>
        /// Returns a hash code for the current complex type.
        /// </summary>
        /// <returns>A hash code for the current complex type.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Namespace, BaseType, Abstract, OpenType);
        }

        #endregion

    }

}
