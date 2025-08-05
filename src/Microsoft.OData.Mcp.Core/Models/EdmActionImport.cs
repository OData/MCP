using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Models
{
    /// <summary>
    /// Represents an action import in an OData entity container.
    /// </summary>
    /// <remarks>
    /// Action imports expose actions as addressable resources in the OData service.
    /// They allow actions to be called through the service interface, providing custom
    /// operations that can have side effects. Actions are typically invoked using POST requests
    /// and can modify the state of the service.
    /// </remarks>
    public sealed class EdmActionImport
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the action import.
        /// </summary>
        /// <value>The name used to address this action import in OData URLs.</value>
        /// <remarks>
        /// The action import name appears in the URL path when invoking the action.
        /// </remarks>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the fully qualified name of the action being imported.
        /// </summary>
        /// <value>The namespace and name of the action that this import exposes.</value>
        /// <remarks>
        /// This references an action defined elsewhere in the model. The action import
        /// makes the action accessible as part of the entity container's interface.
        /// </remarks>
        public required string Action { get; set; }

        /// <summary>
        /// Gets or sets the entity set associated with this action import.
        /// </summary>
        /// <value>The name of the entity set, or <c>null</c> if the action doesn't return entities from a specific set.</value>
        /// <remarks>
        /// When the action returns entities, this property specifies which entity set
        /// those entities belong to. This is important for establishing the correct context
        /// for navigation properties and other operations.
        /// </remarks>
        public string? EntitySet { get; set; }

        /// <summary>
        /// Gets or sets the annotations for this action import.
        /// </summary>
        /// <value>A dictionary of annotations that provide additional metadata about the action import.</value>
        /// <remarks>
        /// Annotations can be used to specify additional behaviors, constraints, or metadata
        /// that are not captured by the standard OData model elements.
        /// </remarks>
        public Dictionary<string, object> Annotations { get; set; } = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmActionImport"/> class.
        /// </summary>
        public EdmActionImport()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmActionImport"/> class with the specified name and action.
        /// </summary>
        /// <param name="name">The name of the action import.</param>
        /// <param name="action">The fully qualified name of the action being imported.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="action"/> is null or whitespace.</exception>
        public EdmActionImport(string name, string action)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(action);
#else
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Action import name cannot be null or whitespace.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(action))
            {
                throw new ArgumentException("Action name cannot be null or whitespace.", nameof(action));
            }
#endif

            Name = name;
            Action = action;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds an annotation to this action import.
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
        /// Returns a string representation of the action import.
        /// </summary>
        /// <returns>A string containing the action import name and action.</returns>
        public override string ToString()
        {
            return $"{Name} -> {Action}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current action import.
        /// </summary>
        /// <param name="obj">The object to compare with the current action import.</param>
        /// <returns><c>true</c> if the specified object is equal to the current action import; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmActionImport other &&
                   Name == other.Name &&
                   Action == other.Action &&
                   EntitySet == other.EntitySet;
        }

        /// <summary>
        /// Returns a hash code for the current action import.
        /// </summary>
        /// <returns>A hash code for the current action import.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Action, EntitySet);
        }

        #endregion
    }
}