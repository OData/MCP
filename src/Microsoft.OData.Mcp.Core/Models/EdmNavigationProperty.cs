using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{
    /// <summary>
    /// Represents a navigation property in an OData entity type.
    /// </summary>
    /// <remarks>
    /// Navigation properties define relationships between entity types, allowing traversal
    /// from one entity to related entities. They can represent both single-valued and
    /// collection-valued relationships.
    /// </remarks>
    public sealed class EdmNavigationProperty
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the navigation property.
        /// </summary>
        /// <value>The navigation property name as defined in the CSDL metadata.</value>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the type of the navigation property.
        /// </summary>
        /// <value>The target entity type, optionally wrapped in Collection() for to-many relationships.</value>
        /// <example>
        /// <list type="bullet">
        /// <item><description>"Namespace.Customer" for a to-one relationship</description></item>
        /// <item><description>"Collection(Namespace.Order)" for a to-many relationship</description></item>
        /// </list>
        /// </example>
        public required string Type { get; set; }

        /// <summary>
        /// Gets the target type name of the navigation property.
        /// </summary>
        /// <value>The target entity type name without Collection() wrapper.</value>
        [JsonIgnore]
        public string TargetTypeName
        {
            get
            {
                // Remove Collection() wrapper if present
                if (Type.StartsWith("Collection(") && Type.EndsWith(")"))
                {
                    return Type.Substring(11, Type.Length - 12);
                }
                return Type;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the navigation property is required (non-nullable).
        /// </summary>
        /// <value><c>true</c> if the navigation property is required; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool IsRequired => !Nullable;

        /// <summary>
        /// Gets or sets a value indicating whether the navigation property can contain null values.
        /// </summary>
        /// <value><c>true</c> if the navigation property is nullable; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// This property is typically relevant for to-one relationships. Collection-valued
        /// navigation properties are generally non-nullable (the collection itself, not its elements).
        /// </remarks>
        public bool Nullable { get; set; } = true;

        /// <summary>
        /// Gets or sets the name of the partner navigation property.
        /// </summary>
        /// <value>The name of the corresponding navigation property on the target entity type, or <c>null</c> if not specified.</value>
        /// <remarks>
        /// The partner property represents the inverse side of a bidirectional relationship.
        /// For example, if Customer has Orders navigation property, Order might have a Customer partner property.
        /// </remarks>
        public string? Partner { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the navigation property contains dependent entities.
        /// </summary>
        /// <value><c>true</c> if this navigation property contains dependent entities; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When true, this indicates that the target entities depend on the source entity for their existence.
        /// Deleting the source entity should cascade to delete the dependent entities.
        /// </remarks>
        public bool ContainsTarget { get; set; }

        /// <summary>
        /// Gets or sets the referential constraints for this navigation property.
        /// </summary>
        /// <value>A collection of referential constraints that define the foreign key relationships.</value>
        /// <remarks>
        /// Referential constraints specify how the navigation property relates to properties
        /// in the source and target entity types, effectively defining foreign key relationships.
        /// </remarks>
        public List<EdmReferentialConstraint> ReferentialConstraints { get; set; } = [];

        /// <summary>
        /// Gets or sets the on-delete action for this navigation property.
        /// </summary>
        /// <value>The action to take when the target entity is deleted, or <c>null</c> if not specified.</value>
        /// <remarks>
        /// Common values include "Cascade" for cascading deletes and "SetNull" for setting the
        /// foreign key to null. The specific behavior depends on the underlying data store.
        /// </remarks>
        public string? OnDelete { get; set; }

        /// <summary>
        /// Gets a value indicating whether this navigation property represents a collection.
        /// </summary>
        /// <value><c>true</c> if the navigation property is collection-valued; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool IsCollection => Type.StartsWith("Collection(", StringComparison.Ordinal);

        /// <summary>
        /// Gets the target entity type name.
        /// </summary>
        /// <value>The fully qualified name of the target entity type.</value>
        /// <remarks>
        /// For collection-valued navigation properties, this returns the element type within the Collection().
        /// For single-valued properties, this returns the type directly.
        /// </remarks>
        [JsonIgnore]
        public string TargetType
        {
            get
            {
                if (!IsCollection)
                {
                    return Type;
                }

                var start = Type.IndexOf('(') + 1;
                var end = Type.LastIndexOf(')');
                return Type[start..end];
            }
        }

        /// <summary>
        /// Gets the multiplicity of this navigation property.
        /// </summary>
        /// <value>A string indicating the relationship multiplicity.</value>
        /// <remarks>
        /// Returns "Many" for collection-valued properties, "One" for non-nullable single-valued properties,
        /// and "ZeroOrOne" for nullable single-valued properties.
        /// </remarks>
        [JsonIgnore]
        public string Multiplicity
        {
            get
            {
                if (IsCollection)
                {
                    return "Many";
                }

                return Nullable ? "ZeroOrOne" : "One";
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmNavigationProperty"/> class.
        /// </summary>
        public EdmNavigationProperty()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmNavigationProperty"/> class with the specified name and type.
        /// </summary>
        /// <param name="name">The name of the navigation property.</param>
        /// <param name="type">The type of the navigation property.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="type"/> is null or whitespace.</exception>
        public EdmNavigationProperty(string name, string type)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(type);
#else
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Navigation property name cannot be null or whitespace.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("Navigation property type cannot be null or whitespace.", nameof(type));
            }
#endif

            Name = name;
            Type = type;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a string representation of the navigation property.
        /// </summary>
        /// <returns>A string containing the navigation property name and type.</returns>
        public override string ToString()
        {
            return $"{Name}: {Type}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current navigation property.
        /// </summary>
        /// <param name="obj">The object to compare with the current navigation property.</param>
        /// <returns><c>true</c> if the specified object is equal to the current navigation property; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmNavigationProperty other &&
                   Name == other.Name &&
                   Type == other.Type &&
                   Nullable == other.Nullable &&
                   Partner == other.Partner &&
                   ContainsTarget == other.ContainsTarget &&
                   OnDelete == other.OnDelete &&
                   ReferentialConstraints.SequenceEqual(other.ReferentialConstraints);
        }

        /// <summary>
        /// Returns a hash code for the current navigation property.
        /// </summary>
        /// <returns>A hash code for the current navigation property.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Type, Nullable, Partner, ContainsTarget, OnDelete);
        }

        #endregion
    }
}