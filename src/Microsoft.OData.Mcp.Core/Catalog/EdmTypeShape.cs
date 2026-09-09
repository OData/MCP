// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// The shape facts for one entity type that every describe surface renders from: exposed properties,
    /// required-on-create names, and the operations bound to the type.
    /// </summary>
    /// <remarks>
    /// Instances are computed once per type and cached for the catalog lifetime when the model is static
    /// (<see cref="ODataMcpCatalogOptions.IsDynamicModel"/> is <c>false</c>). Everything here comes from
    /// declared EDM members; CLR entity types are never walked.
    /// </remarks>
    public sealed class EdmTypeShape
    {

        #region Properties

        /// <summary>
        /// Gets the actions bound to this type or one of its base types, in declaration order.
        /// </summary>
        public IReadOnlyList<EdmAction> BoundActions { get; }

        /// <summary>
        /// Gets the functions bound to this type or one of its base types, in declaration order.
        /// </summary>
        public IReadOnlyList<EdmFunction> BoundFunctions { get; }

        /// <summary>
        /// Gets the entity set the shape was resolved through, or <c>null</c> when the type has no included set.
        /// </summary>
        public EdmEntitySet? EntitySet { get; }

        /// <summary>
        /// Gets the entity type.
        /// </summary>
        public EdmEntityType EntityType { get; }

        /// <summary>
        /// Gets the declared structural properties that appear in shapes and schemas (binary and stream excluded).
        /// </summary>
        public IReadOnlyList<EdmProperty> ExposedProperties { get; }

        /// <summary>
        /// Gets the fully qualified type name, which is also the cache key.
        /// </summary>
        public string FullName => EntityType.FullName;

        /// <summary>
        /// Gets the names of exposed properties that a POST must include.
        /// </summary>
        /// <remarks>
        /// A property is required on create when it is non-nullable, has no <c>DefaultValue</c>, and is not
        /// <see cref="EdmProperty.Computed"/>. Store generation is never inferred from the property type.
        /// </remarks>
        public IReadOnlyList<string> RequiredOnCreate { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmTypeShape"/> class.
        /// </summary>
        /// <param name="model">The Core EDM the type belongs to.</param>
        /// <param name="entityType">The entity type to shape.</param>
        /// <param name="entitySet">The entity set the type is reached through, or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="entityType"/> is null.</exception>
        public EdmTypeShape(EdmModel model, EdmEntityType entityType, EdmEntitySet? entitySet)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(entityType);

            EntityType = entityType;
            EntitySet = entitySet;
            ExposedProperties = [.. entityType.Properties.Where(ODataMcpCatalog.IsExposedProperty)];
            RequiredOnCreate = [.. ExposedProperties.Where(IsRequiredOnCreate).Select(property => property.Name)];

            var bindingTypes = ResolveBindingTypeNames(model, entityType);
            BoundActions = [.. model.Actions.Where(action => action.IsBound && BindsTo(action.BindingParameterType, bindingTypes))];
            BoundFunctions = [.. model.Functions.Where(function => function.IsBound && BindsTo(function.BindingParameterType, bindingTypes))];
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether a binding parameter type is a collection of one of the given type names.
        /// </summary>
        /// <param name="bindingParameterType">The operation's binding parameter type, or <c>null</c>.</param>
        /// <returns>
        /// <c>true</c> when the binding parameter is <c>Collection(...)</c>; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsCollectionBound(string? bindingParameterType)
        {
            return !string.IsNullOrWhiteSpace(bindingParameterType)
                && bindingParameterType.StartsWith("Collection(", StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether a property must be present on POST.
        /// </summary>
        /// <param name="property">The declared property.</param>
        /// <returns>
        /// <c>true</c> when the property is non-nullable, has no <c>DefaultValue</c>, and is not computed.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
        public static bool IsRequiredOnCreate(EdmProperty property)
        {
            ArgumentNullException.ThrowIfNull(property);

            return !property.Nullable
                && string.IsNullOrWhiteSpace(property.DefaultValue)
                && !property.Computed;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Determines whether a binding parameter type names one of the given types, directly or as a collection.
        /// </summary>
        /// <param name="bindingParameterType">The operation's binding parameter type.</param>
        /// <param name="typeNames">The full names of the type and its base types.</param>
        /// <returns>
        /// <c>true</c> when the binding parameter targets one of the names.
        /// </returns>
        internal static bool BindsTo(string? bindingParameterType, IReadOnlyCollection<string> typeNames)
        {
            if (string.IsNullOrWhiteSpace(bindingParameterType))
            {
                return false;
            }

            var target = IsCollectionBound(bindingParameterType)
                ? bindingParameterType["Collection(".Length..^1]
                : bindingParameterType;

            return typeNames.Contains(target, StringComparer.Ordinal);
        }

        /// <summary>
        /// Collects the full names of a type and every base type declared in the model.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="entityType">The starting type.</param>
        /// <returns>
        /// The type's full name followed by its base chain.
        /// </returns>
        internal static IReadOnlyCollection<string> ResolveBindingTypeNames(EdmModel model, EdmEntityType entityType)
        {
            var names = new List<string> { entityType.FullName };
            var current = entityType;
            while (current.HasBaseType && !names.Contains(current.BaseType!, StringComparer.Ordinal))
            {
                names.Add(current.BaseType!);
                var next = model.GetEntityType(current.BaseType!);
                if (next is null)
                {
                    break;
                }

                current = next;
            }

            return names;
        }

        #endregion

    }

}
