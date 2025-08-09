using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{
    /// <summary>
    /// Represents a complete OData Entity Data Model (EDM).
    /// </summary>
    /// <remarks>
    /// The EDM defines the structure of data exposed by an OData service, including entity types,
    /// complex types, entity containers, and their relationships. This class serves as the root
    /// model that contains all metadata necessary to understand and interact with an OData service.
    /// </remarks>
    public sealed class EdmModel
    {
        #region Properties

        /// <summary>
        /// Gets or sets the version of the EDM.
        /// </summary>
        /// <value>The version string (e.g., "4.0", "4.01").</value>
        /// <remarks>
        /// This indicates which version of the OData standard the model conforms to.
        /// Different versions have different capabilities and syntax.
        /// </remarks>
        public string Version { get; set; } = "4.0";

        /// <summary>
        /// Gets or sets the entity types defined in this model.
        /// </summary>
        /// <value>A collection of entity types that define the structure of entities in the service.</value>
        /// <remarks>
        /// Entity types are the primary structural elements in the EDM, defining the properties
        /// and relationships that make up the data model.
        /// </remarks>
        public List<EdmEntityType> EntityTypes { get; set; } = [];

        /// <summary>
        /// Gets or sets the complex types defined in this model.
        /// </summary>
        /// <value>A collection of complex types that define reusable structured data elements.</value>
        /// <remarks>
        /// Complex types are structured types without keys that can be used as property types
        /// in entity types or other complex types.
        /// </remarks>
        public List<EdmComplexType> ComplexTypes { get; set; } = [];

        /// <summary>
        /// Gets or sets the entity containers defined in this model.
        /// </summary>
        /// <value>A collection of entity containers that define the service interface.</value>
        /// <remarks>
        /// Entity containers define the addressable resources in the OData service.
        /// Typically, there is one primary entity container per service.
        /// </remarks>
        public List<EdmEntityContainer> EntityContainers { get; set; } = [];

        /// <summary>
        /// Gets or sets the namespaces used in this model.
        /// </summary>
        /// <value>A collection of namespace strings that organize the types in the model.</value>
        /// <remarks>
        /// Namespaces provide organizational structure and help avoid naming conflicts
        /// between types from different sources.
        /// </remarks>
        public List<string> Namespaces { get; set; } = [];

        /// <summary>
        /// Gets or sets the functions defined in this model.
        /// </summary>
        /// <value>A collection of functions that can be called on the service.</value>
        /// <remarks>
        /// Functions are operations that can be called to retrieve data or perform calculations.
        /// They are side-effect free and can be composed with other query operations.
        /// </remarks>
        public List<EdmFunction> Functions { get; set; } = [];

        /// <summary>
        /// Gets or sets the actions defined in this model.
        /// </summary>
        /// <value>A collection of actions that can be invoked on the service.</value>
        /// <remarks>
        /// Actions are operations that may have side effects and are used to modify
        /// data or perform operations that cannot be expressed through standard CRUD operations.
        /// </remarks>
        public List<EdmAction> Actions { get; set; } = [];

        /// <summary>
        /// Gets or sets the annotations for this model.
        /// </summary>
        /// <value>A dictionary of annotations that provide additional metadata about the model.</value>
        /// <remarks>
        /// Annotations can be used to specify additional behaviors, constraints, or metadata
        /// that are not captured by the standard OData model elements.
        /// </remarks>
        public Dictionary<string, object> Annotations { get; set; } = [];

        /// <summary>
        /// Gets the primary entity container for this model.
        /// </summary>
        /// <value>The first entity container, or <c>null</c> if no containers are defined.</value>
        /// <remarks>
        /// Most OData services have a single entity container that serves as the primary
        /// interface. This property provides convenient access to that container.
        /// </remarks>
        [JsonIgnore]
        public EdmEntityContainer? PrimaryContainer => EntityContainers.FirstOrDefault();

        /// <summary>
        /// Gets the primary entity container for this model (alias for PrimaryContainer).
        /// </summary>
        /// <value>The first entity container, or <c>null</c> if no containers are defined.</value>
        [JsonIgnore]
        public EdmEntityContainer? EntityContainer => PrimaryContainer;

        /// <summary>
        /// Gets all entity sets from all entity containers.
        /// </summary>
        /// <value>A flattened collection of all entity sets in the model.</value>
        [JsonIgnore]
        public IEnumerable<EdmEntitySet> AllEntitySets => EntityContainers.SelectMany(c => c.EntitySets);

        /// <summary>
        /// Gets all singletons from all entity containers.
        /// </summary>
        /// <value>A flattened collection of all singletons in the model.</value>
        [JsonIgnore]
        public IEnumerable<EdmSingleton> AllSingletons => EntityContainers.SelectMany(c => c.Singletons);

        /// <summary>
        /// Gets a value indicating whether this model has any entity types.
        /// </summary>
        /// <value><c>true</c> if the model has entity types; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasEntityTypes => EntityTypes.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this model has any complex types.
        /// </summary>
        /// <value><c>true</c> if the model has complex types; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasComplexTypes => ComplexTypes.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this model has any entity containers.
        /// </summary>
        /// <value><c>true</c> if the model has entity containers; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public bool HasEntityContainers => EntityContainers.Count > 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmModel"/> class.
        /// </summary>
        public EdmModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmModel"/> class with the specified version.
        /// </summary>
        /// <param name="version">The version of the EDM.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="version"/> is null or whitespace.</exception>
        public EdmModel(string version)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(version);
#else
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Model version cannot be null or whitespace.", nameof(version));
            }
#endif

            Version = version;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets an entity type by its fully qualified name.
        /// </summary>
        /// <param name="fullName">The fully qualified name of the entity type (namespace.name).</param>
        /// <returns>The entity type with the specified name, or <c>null</c> if not found.</returns>
        public EdmEntityType? GetEntityType(string fullName)
        {
            return EntityTypes.FirstOrDefault(et => et.FullName.Equals(fullName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets an entity type by name and namespace.
        /// </summary>
        /// <param name="name">The name of the entity type.</param>
        /// <param name="namespace">The namespace of the entity type.</param>
        /// <returns>The entity type with the specified name and namespace, or <c>null</c> if not found.</returns>
        public EdmEntityType? GetEntityType(string name, string @namespace)
        {
            return EntityTypes.FirstOrDefault(et => 
                et.Name.Equals(name, StringComparison.Ordinal) && 
                et.Namespace.Equals(@namespace, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets a complex type by its fully qualified name.
        /// </summary>
        /// <param name="fullName">The fully qualified name of the complex type (namespace.name).</param>
        /// <returns>The complex type with the specified name, or <c>null</c> if not found.</returns>
        public EdmComplexType? GetComplexType(string fullName)
        {
            return ComplexTypes.FirstOrDefault(ct => ct.FullName.Equals(fullName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets a complex type by name and namespace.
        /// </summary>
        /// <param name="name">The name of the complex type.</param>
        /// <param name="namespace">The namespace of the complex type.</param>
        /// <returns>The complex type with the specified name and namespace, or <c>null</c> if not found.</returns>
        public EdmComplexType? GetComplexType(string name, string @namespace)
        {
            return ComplexTypes.FirstOrDefault(ct => 
                ct.Name.Equals(name, StringComparison.Ordinal) && 
                ct.Namespace.Equals(@namespace, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets an entity container by its fully qualified name.
        /// </summary>
        /// <param name="fullName">The fully qualified name of the entity container (namespace.name).</param>
        /// <returns>The entity container with the specified name, or <c>null</c> if not found.</returns>
        public EdmEntityContainer? GetEntityContainer(string fullName)
        {
            return EntityContainers.FirstOrDefault(ec => ec.FullName.Equals(fullName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets an entity container by name and namespace.
        /// </summary>
        /// <param name="name">The name of the entity container.</param>
        /// <param name="namespace">The namespace of the entity container.</param>
        /// <returns>The entity container with the specified name and namespace, or <c>null</c> if not found.</returns>
        public EdmEntityContainer? GetEntityContainer(string name, string @namespace)
        {
            return EntityContainers.FirstOrDefault(ec => 
                ec.Name.Equals(name, StringComparison.Ordinal) && 
                ec.Namespace.Equals(@namespace, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets an entity set by name from any entity container.
        /// </summary>
        /// <param name="entitySetName">The name of the entity set.</param>
        /// <returns>The entity set with the specified name, or <c>null</c> if not found.</returns>
        public EdmEntitySet? GetEntitySet(string entitySetName)
        {
            return AllEntitySets.FirstOrDefault(es => es.Name.Equals(entitySetName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets a singleton by name from any entity container.
        /// </summary>
        /// <param name="singletonName">The name of the singleton.</param>
        /// <returns>The singleton with the specified name, or <c>null</c> if not found.</returns>
        public EdmSingleton? GetSingleton(string singletonName)
        {
            return AllSingletons.FirstOrDefault(s => s.Name.Equals(singletonName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Adds an entity type to the model.
        /// </summary>
        /// <param name="entityType">The entity type to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityType"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an entity type with the same full name already exists.</exception>
        public void AddEntityType(EdmEntityType entityType)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(entityType);
#else
            if (entityType is null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }
#endif

            if (GetEntityType(entityType.FullName) is not null)
            {
                throw new InvalidOperationException($"An entity type named '{entityType.FullName}' already exists in the model.");
            }

            EntityTypes.Add(entityType);
            if (!Namespaces.Contains(entityType.Namespace))
            {
                Namespaces.Add(entityType.Namespace);
            }
        }

        /// <summary>
        /// Adds a complex type to the model.
        /// </summary>
        /// <param name="complexType">The complex type to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="complexType"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a complex type with the same full name already exists.</exception>
        public void AddComplexType(EdmComplexType complexType)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(complexType);
#else
            if (complexType is null)
            {
                throw new ArgumentNullException(nameof(complexType));
            }
#endif

            if (GetComplexType(complexType.FullName) is not null)
            {
                throw new InvalidOperationException($"A complex type named '{complexType.FullName}' already exists in the model.");
            }

            ComplexTypes.Add(complexType);
            if (!Namespaces.Contains(complexType.Namespace))
            {
                Namespaces.Add(complexType.Namespace);
            }
        }

        /// <summary>
        /// Adds an entity container to the model.
        /// </summary>
        /// <param name="entityContainer">The entity container to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityContainer"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an entity container with the same full name already exists.</exception>
        public void AddEntityContainer(EdmEntityContainer entityContainer)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(entityContainer);
#else
            if (entityContainer is null)
            {
                throw new ArgumentNullException(nameof(entityContainer));
            }
#endif

            if (GetEntityContainer(entityContainer.FullName) is not null)
            {
                throw new InvalidOperationException($"An entity container named '{entityContainer.FullName}' already exists in the model.");
            }

            EntityContainers.Add(entityContainer);
            if (!Namespaces.Contains(entityContainer.Namespace))
            {
                Namespaces.Add(entityContainer.Namespace);
            }
        }

        /// <summary>
        /// Adds an annotation to this model.
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
        /// Validates the model for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the model is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            // Validate that all entity types referenced in entity sets exist
            foreach (var entitySet in AllEntitySets)
            {
                if (GetEntityType(entitySet.EntityType) is null)
                {
                    errors.Add($"Entity set '{entitySet.Name}' references unknown entity type '{entitySet.EntityType}'.");
                }
            }

            // Validate that all entity types referenced in singletons exist
            foreach (var singleton in AllSingletons)
            {
                if (GetEntityType(singleton.Type) is null)
                {
                    errors.Add($"Singleton '{singleton.Name}' references unknown entity type '{singleton.Type}'.");
                }
            }

            // Validate that all navigation properties reference valid types
            foreach (var entityType in EntityTypes)
            {
                foreach (var navProp in entityType.NavigationProperties)
                {
                    var targetType = navProp.TargetType;
                    if (GetEntityType(targetType) is null)
                    {
                        errors.Add($"Navigation property '{navProp.Name}' in entity type '{entityType.FullName}' references unknown target type '{targetType}'.");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the model.
        /// </summary>
        /// <returns>A summary of the model contents.</returns>
        public override string ToString()
        {
            return $"EDM v{Version}: {EntityTypes.Count} entity types, {ComplexTypes.Count} complex types, {EntityContainers.Count} containers";
        }

        #endregion
    }
}