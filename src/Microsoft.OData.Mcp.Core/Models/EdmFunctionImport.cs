// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents a function import in an OData entity container.
    /// </summary>
    /// <remarks>
    /// Function imports expose functions as addressable resources in the OData service.
    /// They allow functions to be called through the service interface, providing custom
    /// operations that don't fit the standard CRUD pattern. Functions are side-effect free
    /// and can be invoked using GET requests.
    /// </remarks>
    public sealed class EdmFunctionImport
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the function import.
        /// </summary>
        /// <value>The name used to address this function import in OData URLs.</value>
        /// <remarks>
        /// The function import name appears in the URL path when invoking the function.
        /// </remarks>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the fully qualified name of the function being imported.
        /// </summary>
        /// <value>The namespace and name of the function that this import exposes.</value>
        /// <remarks>
        /// This references a function defined elsewhere in the model. The function import
        /// makes the function accessible as part of the entity container's interface.
        /// </remarks>
        public required string Function { get; set; }

        /// <summary>
        /// Gets or sets the entity set associated with this function import.
        /// </summary>
        /// <value>The name of the entity set, or <c>null</c> if the function doesn't return entities from a specific set.</value>
        /// <remarks>
        /// When the function returns entities, this property specifies which entity set
        /// those entities belong to. This is important for establishing the correct context
        /// for navigation properties and other operations.
        /// </remarks>
        public string? EntitySet { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this function import is included in the service document.
        /// </summary>
        /// <value><c>true</c> if the function import should be included in the service document; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When true, the function import will be listed in the service document, making it
        /// discoverable by clients. When false, clients must know the function import name
        /// in advance to use it.
        /// </remarks>
        public bool IncludeInServiceDocument { get; set; } = true;

        /// <summary>
        /// Gets or sets the annotations for this function import.
        /// </summary>
        /// <value>A dictionary of annotations that provide additional metadata about the function import.</value>
        /// <remarks>
        /// Annotations can be used to specify additional behaviors, constraints, or metadata
        /// that are not captured by the standard OData model elements.
        /// </remarks>
        public Dictionary<string, object> Annotations { get; set; } = [];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmFunctionImport"/> class.
        /// </summary>
        public EdmFunctionImport()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmFunctionImport"/> class with the specified name and function.
        /// </summary>
        /// <param name="name">The name of the function import.</param>
        /// <param name="function">The fully qualified name of the function being imported.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="function"/> is null or whitespace.</exception>
        public EdmFunctionImport(string name, string function)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(function);

            Name = name;
            Function = function;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds an annotation to this function import.
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
        /// Returns a string representation of the function import.
        /// </summary>
        /// <returns>A string containing the function import name and function.</returns>
        public override string ToString()
        {
            return $"{Name} -> {Function}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current function import.
        /// </summary>
        /// <param name="obj">The object to compare with the current function import.</param>
        /// <returns><c>true</c> if the specified object is equal to the current function import; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmFunctionImport other &&
                   Name == other.Name &&
                   Function == other.Function &&
                   EntitySet == other.EntitySet &&
                   IncludeInServiceDocument == other.IncludeInServiceDocument;
        }

        /// <summary>
        /// Returns a hash code for the current function import.
        /// </summary>
        /// <returns>A hash code for the current function import.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Function, EntitySet, IncludeInServiceDocument);
        }

        #endregion

    }

}
