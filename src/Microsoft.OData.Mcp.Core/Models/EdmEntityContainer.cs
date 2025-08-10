using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents an entity container in an OData model.
    /// </summary>
    /// <remarks>
    /// An entity container defines the scope of addressable resources in an OData service.
    /// It contains entity sets, singletons, function imports, and action imports that
    /// comprise the service's interface. Each OData service must have exactly one entity container.
    /// </remarks>
    public sealed class EdmEntityContainer
    {

        #region Properties

        /// <summary>
        /// Gets or sets the name of the entity container.
        /// </summary>
        /// <value>The local name of the entity container within its namespace.</value>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the namespace of the entity container.
        /// </summary>
        /// <value>The namespace that contains this entity container.</value>
        public required string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the base entity container.
        /// </summary>
        /// <value>The fully qualified name of the base entity container, or <c>null</c> if this container has no base.</value>
        /// <remarks>
        /// When specified, this entity container extends the base container, inheriting all its
        /// entity sets, singletons, and operations. This allows for modular composition of services.
        /// </remarks>
        public string? Extends { get; set; }

        /// <summary>
        /// Gets or sets the entity sets in this container.
        /// </summary>
        /// <value>A collection of entity sets that define the collections of entities available in the service.</value>
        /// <remarks>
        /// Entity sets are the primary addressable resources in an OData service, allowing clients
        /// to query, create, update, and delete entities.
        /// </remarks>
        public List<EdmEntitySet> EntitySets { get; set; } = [];

        /// <summary>
        /// Gets or sets the singletons in this container.
        /// </summary>
        /// <value>A collection of singletons that define individual entity resources.</value>
        /// <remarks>
        /// Singletons represent entities that exist as single instances rather than collections.
        /// They are useful for representing unique resources like service configuration or user profiles.
        /// </remarks>
        public List<EdmSingleton> Singletons { get; set; } = [];

        /// <summary>
        /// Gets or sets the function imports in this container.
        /// </summary>
        /// <value>A collection of function imports that expose functions as addressable resources.</value>
        /// <remarks>
        /// Function imports allow functions to be called as part of the OData service interface.
        /// They provide a way to expose custom operations that don't fit the standard CRUD pattern.
        /// </remarks>
        public List<EdmFunctionImport> FunctionImports { get; set; } = [];

        /// <summary>
        /// Gets or sets the action imports in this container.
        /// </summary>
        /// <value>A collection of action imports that expose actions as addressable resources.</value>
        /// <remarks>
        /// Action imports allow actions to be called as part of the OData service interface.
        /// Unlike functions, actions can have side effects and are typically invoked via POST requests.
        /// </remarks>
        public List<EdmActionImport> ActionImports { get; set; } = [];

        /// <summary>
        /// Gets or sets the annotations for this entity container.
        /// </summary>
        /// <value>A dictionary of annotations that provide additional metadata about the entity container.</value>
        /// <remarks>
        /// Annotations can be used to specify additional behaviors, constraints, or metadata
        /// that are not captured by the standard OData model elements.
        /// </remarks>
        public Dictionary<string, object> Annotations { get; set; } = [];

        /// <summary>
        /// Gets the fully qualified name of the entity container.
        /// </summary>
        /// <value>The namespace and name combined with a dot separator.</value>
        [JsonIgnore]
        public string FullName => $"{Namespace}.{Name}";

        /// <summary>
        /// Gets a value indicating whether this entity container extends another container.
        /// </summary>
        /// <value><c>true</c> if the entity container has a base container; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasBaseContainer => !string.IsNullOrWhiteSpace(Extends);

        /// <summary>
        /// Gets a value indicating whether this entity container has any entity sets.
        /// </summary>
        /// <value><c>true</c> if the entity container has entity sets; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasEntitySets => EntitySets.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this entity container has any singletons.
        /// </summary>
        /// <value><c>true</c> if the entity container has singletons; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasSingletons => Singletons.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this entity container has any function imports.
        /// </summary>
        /// <value><c>true</c> if the entity container has function imports; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasFunctionImports => FunctionImports.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this entity container has any action imports.
        /// </summary>
        /// <value><c>true</c> if the entity container has action imports; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasActionImports => ActionImports.Count > 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEntityContainer"/> class.
        /// </summary>
        public EdmEntityContainer()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEntityContainer"/> class with the specified name and namespace.
        /// </summary>
        /// <param name="name">The name of the entity container.</param>
        /// <param name="namespace">The namespace of the entity container.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="namespace"/> is null or whitespace.</exception>
        public EdmEntityContainer(string name, string @namespace)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);

            Name = name;
            Namespace = @namespace;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets an entity set by name.
        /// </summary>
        /// <param name="entitySetName">The name of the entity set to retrieve.</param>
        /// <returns>The entity set with the specified name, or <c>null</c> if not found.</returns>
        public EdmEntitySet? GetEntitySet(string entitySetName)
        {
            return EntitySets.FirstOrDefault(es => es.Name.Equals(entitySetName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets a singleton by name.
        /// </summary>
        /// <param name="singletonName">The name of the singleton to retrieve.</param>
        /// <returns>The singleton with the specified name, or <c>null</c> if not found.</returns>
        public EdmSingleton? GetSingleton(string singletonName)
        {
            return Singletons.FirstOrDefault(s => s.Name.Equals(singletonName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets a function import by name.
        /// </summary>
        /// <param name="functionImportName">The name of the function import to retrieve.</param>
        /// <returns>The function import with the specified name, or <c>null</c> if not found.</returns>
        public EdmFunctionImport? GetFunctionImport(string functionImportName)
        {
            return FunctionImports.FirstOrDefault(f => f.Name.Equals(functionImportName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets an action import by name.
        /// </summary>
        /// <param name="actionImportName">The name of the action import to retrieve.</param>
        /// <returns>The action import with the specified name, or <c>null</c> if not found.</returns>
        public EdmActionImport? GetActionImport(string actionImportName)
        {
            return ActionImports.FirstOrDefault(a => a.Name.Equals(actionImportName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Adds an entity set to this container.
        /// </summary>
        /// <param name="entitySet">The entity set to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entitySet"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an entity set with the same name already exists.</exception>
        public void AddEntitySet(EdmEntitySet entitySet)
        {
ArgumentNullException.ThrowIfNull(entitySet);

            if (GetEntitySet(entitySet.Name) is not null)
            {
                throw new InvalidOperationException($"An entity set named '{entitySet.Name}' already exists in the container.");
            }

            EntitySets.Add(entitySet);
        }

        /// <summary>
        /// Adds a singleton to this container.
        /// </summary>
        /// <param name="singleton">The singleton to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="singleton"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a singleton with the same name already exists.</exception>
        public void AddSingleton(EdmSingleton singleton)
        {
ArgumentNullException.ThrowIfNull(singleton);

            if (GetSingleton(singleton.Name) is not null)
            {
                throw new InvalidOperationException($"A singleton named '{singleton.Name}' already exists in the container.");
            }

            Singletons.Add(singleton);
        }

        /// <summary>
        /// Adds an annotation to this entity container.
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
        /// Returns a string representation of the entity container.
        /// </summary>
        /// <returns>The fully qualified name of the entity container.</returns>
        public override string ToString()
        {
            return FullName;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current entity container.
        /// </summary>
        /// <param name="obj">The object to compare with the current entity container.</param>
        /// <returns><c>true</c> if the specified object is equal to the current entity container; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmEntityContainer other &&
                   Name == other.Name &&
                   Namespace == other.Namespace &&
                   Extends == other.Extends &&
                   EntitySets.SequenceEqual(other.EntitySets) &&
                   Singletons.SequenceEqual(other.Singletons) &&
                   FunctionImports.SequenceEqual(other.FunctionImports) &&
                   ActionImports.SequenceEqual(other.ActionImports);
        }

        /// <summary>
        /// Returns a hash code for the current entity container.
        /// </summary>
        /// <returns>A hash code for the current entity container.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Namespace, Extends);
        }

        #endregion

    }

}
