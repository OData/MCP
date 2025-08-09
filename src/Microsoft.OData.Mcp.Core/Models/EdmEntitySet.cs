using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{
    /// <summary>
    /// Represents an entity set in an OData entity container.
    /// </summary>
    /// <remarks>
    /// Entity sets define collections of entities that can be accessed through the OData service.
    /// They provide the addressable resources that clients can query, create, update, and delete.
    /// Each entity set is associated with a specific entity type and may include navigation
    /// property bindings to establish relationships with other entity sets.
    /// </remarks>
    public sealed class EdmEntitySet
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the entity set.
        /// </summary>
        /// <value>The name used to address this entity set in OData URLs.</value>
        /// <remarks>
        /// The entity set name appears in the URL path when accessing the collection
        /// or individual entities within the set.
        /// </remarks>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the entity type of the entities in this set.
        /// </summary>
        /// <value>The fully qualified name of the entity type for entities in this set.</value>
        /// <remarks>
        /// All entities in the set must be instances of this entity type or its derived types.
        /// This determines the structure and properties available for entities in the set.
        /// </remarks>
        public required string EntityType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether change tracking is enabled for this entity set.
        /// </summary>
        /// <value><c>true</c> if change tracking is enabled; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When change tracking is enabled, the service can provide delta tokens and support
        /// for change tracking queries, allowing clients to efficiently retrieve only changed data.
        /// </remarks>
        public bool IncludeInServiceDocument { get; set; } = true;

        /// <summary>
        /// Gets or sets the navigation property bindings for this entity set.
        /// </summary>
        /// <value>A collection of navigation property bindings that establish relationships with other entity sets.</value>
        /// <remarks>
        /// Navigation property bindings specify which entity set should be used when following
        /// a navigation property from entities in this set. They establish the connections
        /// between related entity sets in the model.
        /// </remarks>
        public List<EdmNavigationPropertyBinding> NavigationPropertyBindings { get; set; } = [];

        /// <summary>
        /// Gets or sets the annotations for this entity set.
        /// </summary>
        /// <value>A dictionary of annotations that provide additional metadata about the entity set.</value>
        /// <remarks>
        /// Annotations can be used to specify additional behaviors, constraints, or metadata
        /// that are not captured by the standard OData model elements.
        /// </remarks>
        public Dictionary<string, object> Annotations { get; set; } = [];

        /// <summary>
        /// Gets a value indicating whether this entity set has any navigation property bindings.
        /// </summary>
        /// <value><c>true</c> if the entity set has navigation property bindings; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasNavigationPropertyBindings => NavigationPropertyBindings.Count > 0;

        /// <summary>
        /// Gets the short name of the entity type (without namespace).
        /// </summary>
        /// <value>The entity type name without the namespace prefix.</value>
        [JsonIgnore]
        public string EntityTypeName
        {
            get
            {
                var lastDotIndex = EntityType.LastIndexOf('.');
                return lastDotIndex >= 0 ? EntityType[(lastDotIndex + 1)..] : EntityType;
            }
        }

        /// <summary>
        /// Gets the namespace of the entity type.
        /// </summary>
        /// <value>The namespace portion of the entity type, or an empty string if no namespace.</value>
        [JsonIgnore]
        public string EntityTypeNamespace
        {
            get
            {
                var lastDotIndex = EntityType.LastIndexOf('.');
                return lastDotIndex >= 0 ? EntityType[..lastDotIndex] : string.Empty;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEntitySet"/> class.
        /// </summary>
        public EdmEntitySet()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEntitySet"/> class with the specified name and entity type.
        /// </summary>
        /// <param name="name">The name of the entity set.</param>
        /// <param name="entityType">The entity type of the entities in this set.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="entityType"/> is null or whitespace.</exception>
        public EdmEntitySet(string name, string entityType)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
#else
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Entity set name cannot be null or whitespace.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(entityType))
            {
                throw new ArgumentException("Entity type cannot be null or whitespace.", nameof(entityType));
            }
#endif

            Name = name;
            EntityType = entityType;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a navigation property binding by navigation property path.
        /// </summary>
        /// <param name="navigationPropertyPath">The path of the navigation property.</param>
        /// <returns>The navigation property binding with the specified path, or <c>null</c> if not found.</returns>
        public EdmNavigationPropertyBinding? GetNavigationPropertyBinding(string navigationPropertyPath)
        {
            return NavigationPropertyBindings.FirstOrDefault(b => 
                b.Path.Equals(navigationPropertyPath, StringComparison.Ordinal));
        }

        /// <summary>
        /// Adds a navigation property binding to this entity set.
        /// </summary>
        /// <param name="navigationPropertyPath">The path of the navigation property.</param>
        /// <param name="target">The target entity set name.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="navigationPropertyPath"/> or <paramref name="target"/> is null or whitespace.</exception>
        public void AddNavigationPropertyBinding(string navigationPropertyPath, string target)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(navigationPropertyPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(target);
#else
            if (string.IsNullOrWhiteSpace(navigationPropertyPath))
            {
                throw new ArgumentException("Navigation property path cannot be null or whitespace.", nameof(navigationPropertyPath));
            }
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("Target cannot be null or whitespace.", nameof(target));
            }
#endif

            var existing = GetNavigationPropertyBinding(navigationPropertyPath);
            if (existing is not null)
            {
                existing.Target = target;
            }
            else
            {
                NavigationPropertyBindings.Add(new EdmNavigationPropertyBinding(navigationPropertyPath, target)
                {
                    Path = navigationPropertyPath,
                    Target = target
                });
            }
        }

        /// <summary>
        /// Removes a navigation property binding from this entity set.
        /// </summary>
        /// <param name="navigationPropertyPath">The path of the navigation property to remove.</param>
        /// <returns><c>true</c> if the binding was removed; otherwise, <c>false</c>.</returns>
        public bool RemoveNavigationPropertyBinding(string navigationPropertyPath)
        {
            var binding = GetNavigationPropertyBinding(navigationPropertyPath);
            if (binding is not null)
            {
                return NavigationPropertyBindings.Remove(binding);
            }
            return false;
        }

        /// <summary>
        /// Adds an annotation to this entity set.
        /// </summary>
        /// <param name="term">The annotation term.</param>
        /// <param name="value">The annotation value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="term"/> is null or whitespace.</exception>
        public void AddAnnotation(string term, object value)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(term);
            ArgumentNullException.ThrowIfNull(value);
#else
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException("Annotation term cannot be null or whitespace.", nameof(term));
            }
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }
#endif

            Annotations[term] = value;
        }

        /// <summary>
        /// Gets an annotation value by term.
        /// </summary>
        /// <typeparam name="T">The type of the annotation value.</typeparam>
        /// <param name="term">The annotation term.</param>
        /// <returns>The annotation value, or the default value of <typeparamref name="T"/> if not found.</returns>
        public T? GetAnnotation<T>(string term)
        {
            if (Annotations.TryGetValue(term, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Returns a string representation of the entity set.
        /// </summary>
        /// <returns>A string containing the entity set name and entity type.</returns>
        public override string ToString()
        {
            return $"{Name} ({EntityType})";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current entity set.
        /// </summary>
        /// <param name="obj">The object to compare with the current entity set.</param>
        /// <returns><c>true</c> if the specified object is equal to the current entity set; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmEntitySet other &&
                   Name == other.Name &&
                   EntityType == other.EntityType &&
                   IncludeInServiceDocument == other.IncludeInServiceDocument &&
                   NavigationPropertyBindings.SequenceEqual(other.NavigationPropertyBindings);
        }

        /// <summary>
        /// Returns a hash code for the current entity set.
        /// </summary>
        /// <returns>A hash code for the current entity set.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, EntityType, IncludeInServiceDocument);
        }

        #endregion
    }
}