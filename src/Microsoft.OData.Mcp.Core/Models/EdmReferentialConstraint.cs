using System;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents a referential constraint that defines the relationship between properties
    /// in a navigation property.
    /// </summary>
    /// <remarks>
    /// Referential constraints specify how foreign key relationships work in OData,
    /// mapping properties from the source entity to properties in the target entity.
    /// They are analogous to foreign key constraints in relational databases.
    /// </remarks>
    public sealed class EdmReferentialConstraint
    {

        #region Properties

        /// <summary>
        /// Gets or sets the name of the property in the source entity type.
        /// </summary>
        /// <value>The name of the property that acts as the foreign key in the source entity.</value>
        /// <remarks>
        /// This property references a property in the entity type that contains the navigation property.
        /// </remarks>
        public required string Property { get; set; }

        /// <summary>
        /// Gets or sets the name of the referenced property in the target entity type.
        /// </summary>
        /// <value>The name of the property in the target entity that is referenced by the foreign key.</value>
        /// <remarks>
        /// This is typically a key property in the target entity type. The relationship
        /// is established by matching the value of the Property with the ReferencedProperty.
        /// </remarks>
        public required string ReferencedProperty { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmReferentialConstraint"/> class.
        /// </summary>
        public EdmReferentialConstraint()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmReferentialConstraint"/> class
        /// with the specified property names.
        /// </summary>
        /// <param name="property">The name of the property in the source entity type.</param>
        /// <param name="referencedProperty">The name of the referenced property in the target entity type.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="property"/> or <paramref name="referencedProperty"/> is null or whitespace.</exception>
        public EdmReferentialConstraint(string property, string referencedProperty)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(property);
            ArgumentException.ThrowIfNullOrWhiteSpace(referencedProperty);

            Property = property;
            ReferencedProperty = referencedProperty;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a string representation of the referential constraint.
        /// </summary>
        /// <returns>A string showing the property mapping relationship.</returns>
        public override string ToString()
        {
            return $"{Property} -> {ReferencedProperty}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current referential constraint.
        /// </summary>
        /// <param name="obj">The object to compare with the current referential constraint.</param>
        /// <returns><c>true</c> if the specified object is equal to the current referential constraint; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmReferentialConstraint other &&
                   Property == other.Property &&
                   ReferencedProperty == other.ReferencedProperty;
        }

        /// <summary>
        /// Returns a hash code for the current referential constraint.
        /// </summary>
        /// <returns>A hash code for the current referential constraint.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Property, ReferencedProperty);
        }

        #endregion

    }

}
