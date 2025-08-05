using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{
    /// <summary>
    /// Represents a property in an OData entity type or complex type.
    /// </summary>
    /// <remarks>
    /// Properties define the structure and data characteristics of entity types and complex types.
    /// They specify the name, type, and various constraints of the data elements.
    /// </remarks>
    public sealed class EdmProperty
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the property.
        /// </summary>
        /// <value>The property name as defined in the CSDL metadata.</value>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the type of the property.
        /// </summary>
        /// <value>The fully qualified type name (e.g., "Edm.String", "Namespace.EntityType").</value>
        public required string Type { get; set; }

        /// <summary>
        /// Gets the type name of the property (alias for Type property).
        /// </summary>
        /// <value>The fully qualified type name (e.g., "Edm.String", "Namespace.EntityType").</value>
        [JsonIgnore]
        public string TypeName => Type;

        /// <summary>
        /// Gets a value indicating whether the property is nullable (alias for Nullable property).
        /// </summary>
        /// <value><c>true</c> if the property is nullable; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool IsNullable => Nullable;

        /// <summary>
        /// Gets a value indicating whether the property has a default value.
        /// </summary>
        /// <value><c>true</c> if the property has a default value; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasDefaultValue => !string.IsNullOrEmpty(DefaultValue);

        /// <summary>
        /// Gets or sets the description of the property.
        /// </summary>
        /// <value>A human-readable description of the property's purpose.</value>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the property can contain null values.
        /// </summary>
        /// <value><c>true</c> if the property is nullable; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When not specified in the metadata, this property defaults to <c>true</c> for most types,
        /// except for key properties which are typically non-nullable.
        /// </remarks>
        public bool Nullable { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum length of the property value.
        /// </summary>
        /// <value>The maximum length, or <c>null</c> if not specified.</value>
        /// <remarks>
        /// This constraint applies primarily to string and binary properties.
        /// A value of "Max" in the metadata is represented as <c>null</c> here.
        /// </remarks>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Gets or sets the precision of the property value.
        /// </summary>
        /// <value>The precision, or <c>null</c> if not specified.</value>
        /// <remarks>
        /// Precision applies to decimal and temporal types, indicating the total number of digits.
        /// </remarks>
        public int? Precision { get; set; }

        /// <summary>
        /// Gets or sets the scale of the property value.
        /// </summary>
        /// <value>The scale, or <c>null</c> if not specified.</value>
        /// <remarks>
        /// Scale applies to decimal types, indicating the number of digits after the decimal point.
        /// A value of "Variable" in the metadata is represented as <c>null</c> here.
        /// </remarks>
        public int? Scale { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the property uses Unicode encoding.
        /// </summary>
        /// <value><c>true</c> if Unicode is enabled; <c>false</c> if disabled; <c>null</c> if not specified.</value>
        /// <remarks>
        /// This property applies to string properties and affects how the data is stored and transmitted.
        /// </remarks>
        public bool? Unicode { get; set; }

        /// <summary>
        /// Gets or sets the default value of the property.
        /// </summary>
        /// <value>The default value as a string, or <c>null</c> if not specified.</value>
        /// <remarks>
        /// The default value is represented as it appears in the CSDL metadata.
        /// Type-specific parsing is required when using this value.
        /// </remarks>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets the SRID (Spatial Reference System Identifier) for spatial properties.
        /// </summary>
        /// <value>The SRID value, or <c>null</c> if not applicable or not specified.</value>
        /// <remarks>
        /// This property is relevant only for geography and geometry types.
        /// </remarks>
        public string? SRID { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this property is part of the entity key.
        /// </summary>
        /// <value><c>true</c> if the property is a key property; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Key properties uniquely identify an entity instance and are typically non-nullable.
        /// </remarks>
        [JsonIgnore]
        public bool IsKey { get; set; }

        /// <summary>
        /// Gets a value indicating whether this property represents a primitive type.
        /// </summary>
        /// <value><c>true</c> if the property type is an EDM primitive type; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool IsPrimitive => Type.StartsWith("Edm.", StringComparison.Ordinal);

        /// <summary>
        /// Gets a value indicating whether this property represents a collection type.
        /// </summary>
        /// <value><c>true</c> if the property type is a collection; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool IsCollection => Type.StartsWith("Collection(", StringComparison.Ordinal);

        /// <summary>
        /// Gets the element type for collection properties.
        /// </summary>
        /// <value>The element type of the collection, or the type itself if not a collection.</value>
        [JsonIgnore]
        public string ElementType
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

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmProperty"/> class.
        /// </summary>
        public EdmProperty()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmProperty"/> class with the specified name and type.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="type">The type of the property.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="type"/> is null or whitespace.</exception>
        public EdmProperty(string name, string type)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(type);
#else
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Property name cannot be null or whitespace.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("Property type cannot be null or whitespace.", nameof(type));
            }
#endif

            Name = name;
            Type = type;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a string representation of the property.
        /// </summary>
        /// <returns>A string containing the property name and type.</returns>
        public override string ToString()
        {
            return $"{Name}: {Type}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current property.
        /// </summary>
        /// <param name="obj">The object to compare with the current property.</param>
        /// <returns><c>true</c> if the specified object is equal to the current property; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmProperty other &&
                   Name == other.Name &&
                   Type == other.Type &&
                   Nullable == other.Nullable &&
                   MaxLength == other.MaxLength &&
                   Precision == other.Precision &&
                   Scale == other.Scale &&
                   Unicode == other.Unicode &&
                   DefaultValue == other.DefaultValue &&
                   SRID == other.SRID &&
                   IsKey == other.IsKey;
        }

        /// <summary>
        /// Returns a hash code for the current property.
        /// </summary>
        /// <returns>A hash code for the current property.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Type, Nullable, MaxLength, Precision, Scale, Unicode, DefaultValue);
        }

        #endregion
    }
}