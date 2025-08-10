using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents a singleton in an OData entity container.
    /// </summary>
    /// <remarks>
    /// Singletons represent individual entity instances that are addressable as single resources
    /// rather than collections. They are useful for representing unique entities like service
    /// configuration, user profiles, or system settings that exist as single instances.
    /// </remarks>
    public sealed class EdmSingleton
    {

        #region Properties

        /// <summary>
        /// Gets or sets the name of the singleton.
        /// </summary>
        /// <value>The name used to address this singleton in OData URLs.</value>
        /// <remarks>
        /// The singleton name appears in the URL path when accessing the individual entity.
        /// </remarks>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the entity type of the singleton.
        /// </summary>
        /// <value>The fully qualified name of the entity type for this singleton.</value>
        /// <remarks>
        /// The singleton instance must be of this entity type or one of its derived types.
        /// This determines the structure and properties available for the singleton.
        /// </remarks>
        public required string Type { get; set; }

        /// <summary>
        /// Gets or sets the navigation property bindings for this singleton.
        /// </summary>
        /// <value>A collection of navigation property bindings that establish relationships with other entity sets or singletons.</value>
        /// <remarks>
        /// Navigation property bindings specify which entity set or singleton should be used when following
        /// a navigation property from this singleton. They establish the connections between related
        /// resources in the model.
        /// </remarks>
        public List<EdmNavigationPropertyBinding> NavigationPropertyBindings { get; set; } = [];

        /// <summary>
        /// Gets or sets the annotations for this singleton.
        /// </summary>
        /// <value>A dictionary of annotations that provide additional metadata about the singleton.</value>
        /// <remarks>
        /// Annotations can be used to specify additional behaviors, constraints, or metadata
        /// that are not captured by the standard OData model elements.
        /// </remarks>
        public Dictionary<string, object> Annotations { get; set; } = [];

        /// <summary>
        /// Gets a value indicating whether this singleton has any navigation property bindings.
        /// </summary>
        /// <value><c>true</c> if the singleton has navigation property bindings; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasNavigationPropertyBindings => NavigationPropertyBindings.Count > 0;

        /// <summary>
        /// Gets the short name of the entity type (without namespace).
        /// </summary>
        /// <value>The entity type name without the namespace prefix.</value>
        [JsonIgnore]
        public string TypeName
        {
            get
            {
                var lastDotIndex = Type.LastIndexOf('.');
                return lastDotIndex >= 0 ? Type[(lastDotIndex + 1)..] : Type;
            }
        }

        /// <summary>
        /// Gets the namespace of the entity type.
        /// </summary>
        /// <value>The namespace portion of the entity type, or an empty string if no namespace.</value>
        [JsonIgnore]
        public string TypeNamespace
        {
            get
            {
                var lastDotIndex = Type.LastIndexOf('.');
                return lastDotIndex >= 0 ? Type[..lastDotIndex] : string.Empty;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmSingleton"/> class.
        /// </summary>
        public EdmSingleton()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmSingleton"/> class with the specified name and type.
        /// </summary>
        /// <param name="name">The name of the singleton.</param>
        /// <param name="type">The entity type of the singleton.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="type"/> is null or whitespace.</exception>
        public EdmSingleton(string name, string type)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(type);

            Name = name;
            Type = type;
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
        /// Adds a navigation property binding to this singleton.
        /// </summary>
        /// <param name="navigationPropertyPath">The path of the navigation property.</param>
        /// <param name="target">The target entity set or singleton name.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="navigationPropertyPath"/> or <paramref name="target"/> is null or whitespace.</exception>
        public void AddNavigationPropertyBinding(string navigationPropertyPath, string target)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(navigationPropertyPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(target);

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
        /// Removes a navigation property binding from this singleton.
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
        /// Adds an annotation to this singleton.
        /// </summary>
        /// <param name="term">The annotation term.</param>
        /// <param name="value">The annotation value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="term"/> is null or whitespace.</exception>
        public void AddAnnotation(string term, object value)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(term);
            ArgumentNullException.ThrowIfNull(value);

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
        /// Returns a string representation of the singleton.
        /// </summary>
        /// <returns>A string containing the singleton name and type.</returns>
        public override string ToString()
        {
            return $"{Name} ({Type})";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current singleton.
        /// </summary>
        /// <param name="obj">The object to compare with the current singleton.</param>
        /// <returns><c>true</c> if the specified object is equal to the current singleton; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmSingleton other &&
                   Name == other.Name &&
                   Type == other.Type &&
                   NavigationPropertyBindings.SequenceEqual(other.NavigationPropertyBindings);
        }

        /// <summary>
        /// Returns a hash code for the current singleton.
        /// </summary>
        /// <returns>A hash code for the current singleton.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Type);
        }

        #endregion

    }

}
