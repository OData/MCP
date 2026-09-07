// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using CoreModel = Microsoft.OData.Mcp.Core.Models.EdmModel;
using CoreAction = Microsoft.OData.Mcp.Core.Models.EdmAction;
using CoreComplexType = Microsoft.OData.Mcp.Core.Models.EdmComplexType;
using CoreContainer = Microsoft.OData.Mcp.Core.Models.EdmEntityContainer;
using CoreEntitySet = Microsoft.OData.Mcp.Core.Models.EdmEntitySet;
using CoreEntityType = Microsoft.OData.Mcp.Core.Models.EdmEntityType;
using CoreFunction = Microsoft.OData.Mcp.Core.Models.EdmFunction;
using CoreNavigation = Microsoft.OData.Mcp.Core.Models.EdmNavigationProperty;
using CoreParameter = Microsoft.OData.Mcp.Core.Models.EdmParameter;
using CoreProperty = Microsoft.OData.Mcp.Core.Models.EdmProperty;
using CoreSingleton = Microsoft.OData.Mcp.Core.Models.EdmSingleton;

namespace Microsoft.OData.Mcp.AspNetCore.Adaptation
{

    /// <summary>
    /// Copies declared <see cref="IEdmModel"/> members into the Core EDM projection.
    /// CLR properties ignored on the OData model are never copied.
    /// </summary>
    public static class EdmModelAdapter
    {

        #region Public Methods

        /// <summary>
        /// Projects an <see cref="IEdmModel"/> into the Core EDM used by catalogs.
        /// </summary>
        /// <param name="model">The ASP.NET Core OData model.</param>
        /// <returns>
        /// The Core projection containing only declared members and vocabulary documentation.
        /// </returns>
        public static CoreModel ToCoreModel(IEdmModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            var core = new CoreModel(model.GetEdmVersion()?.ToString() ?? "4.0");

            foreach (var schema in model.SchemaElements)
            {
                switch (schema)
                {
                    case IEdmEntityType entityType:
                        core.AddEntityType(MapEntityType(model, entityType));
                        break;
                    case IEdmComplexType complexType:
                        core.AddComplexType(MapComplexType(model, complexType));
                        break;
                    case IEdmFunction function:
                        core.Functions.Add(MapFunction(model, function));
                        break;
                    case IEdmAction action:
                        core.Actions.Add(MapAction(model, action));
                        break;
                }
            }

            foreach (var container in model.EntityContainer is null ? [] : new[] { model.EntityContainer })
            {
                core.AddEntityContainer(MapContainer(model, container));
            }

            return core;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Copies Core.Description / Core.LongDescription onto a Core element.
        /// </summary>
        /// <param name="model">The source model.</param>
        /// <param name="target">The annotatable EDM element.</param>
        /// <param name="setDescription">Receives the description.</param>
        /// <param name="setLongDescription">Receives the long description.</param>
        internal static void ApplyVocabulary(IEdmModel model, IEdmVocabularyAnnotatable target, Action<string> setDescription, Action<string> setLongDescription)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(setDescription);
            ArgumentNullException.ThrowIfNull(setLongDescription);

            string? description = null;
            string? longDescription = null;
            string? descriptionExact = null;
            var descriptionHits = 0;

            foreach (var annotation in model.FindVocabularyAnnotations(target))
            {
                var termName = annotation.Term.Name;
                var value = ReadString(annotation.Value);
                if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(termName))
                {
                    continue;
                }

                if (termName.EndsWith("LongDescription", StringComparison.Ordinal))
                {
                    longDescription ??= value;
                    continue;
                }

                if (!termName.EndsWith("Description", StringComparison.Ordinal))
                {
                    continue;
                }

                descriptionHits++;
                description ??= value;
                if (termName.Length == "Description".Length || termName[^("Description".Length + 1)] == '.')
                {
                    descriptionExact = value;
                }
            }

            if (descriptionHits > 1)
            {
                description = descriptionExact;
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                setDescription(description);
            }

            if (!string.IsNullOrWhiteSpace(longDescription))
            {
                setLongDescription(longDescription);
            }
        }

        /// <summary>
        /// Maps a schema action.
        /// </summary>
        /// <param name="model">The source model.</param>
        /// <param name="action">The action.</param>
        /// <returns>
        /// The Core action.
        /// </returns>
        internal static CoreAction MapAction(IEdmModel model, IEdmAction action)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(action);

            var mapped = new CoreAction(action.Name, action.Namespace)
            {
                BindingParameterType = action.IsBound ? action.Parameters.FirstOrDefault()?.Type.FullName() : null,
                IsBound = action.IsBound,
                Name = action.Name,
                Namespace = action.Namespace,
                ReturnType = action.ReturnType?.FullName()
            };

            foreach (var parameter in action.Parameters)
            {
                mapped.Parameters.Add(MapParameter(model, parameter));
            }

            ApplyVocabulary(model, action, value => mapped.Description = value, value => mapped.LongDescription = value);

            return mapped;
        }

        /// <summary>
        /// Maps a complex type's declared properties only.
        /// </summary>
        /// <param name="model">The source model.</param>
        /// <param name="complexType">The complex type.</param>
        /// <returns>
        /// The Core complex type.
        /// </returns>
        internal static CoreComplexType MapComplexType(IEdmModel model, IEdmComplexType complexType)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(complexType);

            var mapped = new CoreComplexType(complexType.Name, complexType.Namespace)
            {
                Abstract = complexType.IsAbstract,
                BaseType = complexType.BaseType?.FullTypeName(),
                Name = complexType.Name,
                Namespace = complexType.Namespace,
                OpenType = complexType.IsOpen
            };

            foreach (var property in complexType.DeclaredStructuralProperties())
            {
                mapped.Properties.Add(MapProperty(model, property));
            }

            foreach (var navigation in complexType.DeclaredNavigationProperties())
            {
                mapped.NavigationProperties.Add(MapNavigation(model, navigation));
            }

            ApplyVocabulary(model, complexType, value => mapped.Description = value, value => mapped.LongDescription = value);

            return mapped;
        }

        /// <summary>
        /// Maps an entity container, including declared sets and singletons.
        /// </summary>
        /// <param name="model">The source model.</param>
        /// <param name="container">The container.</param>
        /// <returns>
        /// The Core container.
        /// </returns>
        internal static CoreContainer MapContainer(IEdmModel model, IEdmEntityContainer container)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(container);

            var mapped = new CoreContainer(container.Name, container.Namespace)
            {
                Name = container.Name,
                Namespace = container.Namespace
            };

            foreach (var set in container.EntitySets())
            {
                var coreSet = new CoreEntitySet(set.Name, set.EntityType().FullTypeName())
                {
                    EntityType = set.EntityType().FullTypeName(),
                    Name = set.Name
                };

                ApplyVocabulary(model, set, value => coreSet.Description = value, value => coreSet.LongDescription = value);
                mapped.AddEntitySet(coreSet);
            }

            foreach (var singleton in container.Singletons())
            {
                var coreSingleton = new CoreSingleton(singleton.Name, singleton.EntityType().FullTypeName())
                {
                    Name = singleton.Name,
                    Type = singleton.EntityType().FullTypeName()
                };

                ApplyVocabulary(model, singleton, value => coreSingleton.Description = value, value => coreSingleton.LongDescription = value);
                mapped.AddSingleton(coreSingleton);
            }

            ApplyVocabulary(model, container, value => mapped.Description = value, value => mapped.LongDescription = value);

            return mapped;
        }

        /// <summary>
        /// Maps an entity type's declared members and keys.
        /// </summary>
        /// <param name="model">The source model.</param>
        /// <param name="entityType">The entity type.</param>
        /// <returns>
        /// The Core entity type.
        /// </returns>
        internal static CoreEntityType MapEntityType(IEdmModel model, IEdmEntityType entityType)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(entityType);

            var mapped = new CoreEntityType(entityType.Name, entityType.Namespace)
            {
                Abstract = entityType.IsAbstract,
                BaseType = entityType.BaseType?.FullTypeName(),
                HasStream = entityType.HasStream,
                Name = entityType.Name,
                Namespace = entityType.Namespace,
                OpenType = entityType.IsOpen
            };

            foreach (var key in entityType.DeclaredKey ?? [])
            {
                mapped.Key.Add(key.Name);
            }

            foreach (var property in entityType.DeclaredStructuralProperties())
            {
                mapped.Properties.Add(MapProperty(model, property));
            }

            foreach (var navigation in entityType.DeclaredNavigationProperties())
            {
                mapped.NavigationProperties.Add(MapNavigation(model, navigation));
            }

            ApplyVocabulary(model, entityType, value => mapped.Description = value, value => mapped.LongDescription = value);

            return mapped;
        }

        /// <summary>
        /// Maps a schema function.
        /// </summary>
        /// <param name="model">The source model.</param>
        /// <param name="function">The function.</param>
        /// <returns>
        /// The Core function.
        /// </returns>
        internal static CoreFunction MapFunction(IEdmModel model, IEdmFunction function)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(function);

            var mapped = new CoreFunction(function.Name, function.Namespace)
            {
                BindingParameterType = function.IsBound ? function.Parameters.FirstOrDefault()?.Type.FullName() : null,
                IsBound = function.IsBound,
                IsComposable = function.IsComposable,
                Name = function.Name,
                Namespace = function.Namespace,
                ReturnType = function.ReturnType?.FullName()
            };

            foreach (var parameter in function.Parameters)
            {
                mapped.Parameters.Add(MapParameter(model, parameter));
            }

            ApplyVocabulary(model, function, value => mapped.Description = value, value => mapped.LongDescription = value);

            return mapped;
        }

        /// <summary>
        /// Maps a declared navigation property.
        /// </summary>
        /// <param name="model">The source model.</param>
        /// <param name="navigation">The navigation property.</param>
        /// <returns>
        /// The Core navigation.
        /// </returns>
        internal static CoreNavigation MapNavigation(IEdmModel model, IEdmNavigationProperty navigation)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(navigation);

            var mapped = new CoreNavigation(navigation.Name, navigation.Type.FullName())
            {
                ContainsTarget = navigation.ContainsTarget,
                Name = navigation.Name,
                Nullable = navigation.Type.IsNullable,
                Partner = navigation.Partner?.Name,
                Type = navigation.Type.FullName()
            };

            ApplyVocabulary(model, navigation, value => mapped.Description = value, value => mapped.LongDescription = value);

            return mapped;
        }

        /// <summary>
        /// Maps an operation parameter.
        /// </summary>
        /// <param name="model">The source model.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>
        /// The Core parameter.
        /// </returns>
        internal static CoreParameter MapParameter(IEdmModel model, IEdmOperationParameter parameter)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(parameter);

            var mapped = new CoreParameter(parameter.Name, parameter.Type.FullName())
            {
                Name = parameter.Name,
                Nullable = parameter.Type.IsNullable,
                Type = parameter.Type.FullName()
            };

            ApplyVocabulary(model, parameter, value => mapped.Description = value, value => mapped.LongDescription = value);

            return mapped;
        }

        /// <summary>
        /// Maps a declared structural property.
        /// </summary>
        /// <param name="model">The source model.</param>
        /// <param name="property">The property.</param>
        /// <returns>
        /// The Core property.
        /// </returns>
        internal static CoreProperty MapProperty(IEdmModel model, IEdmStructuralProperty property)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(property);

            var mapped = new CoreProperty(property.Name, property.Type.FullName())
            {
                Name = property.Name,
                Nullable = property.Type.IsNullable,
                Type = property.Type.FullName()
            };

            ApplyVocabulary(model, property, value => mapped.Description = value, value => mapped.LongDescription = value);

            return mapped;
        }

        /// <summary>
        /// Reads a string constant from a vocabulary expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>
        /// The string value, or <c>null</c>.
        /// </returns>
        internal static string? ReadString(IEdmExpression? expression)
        {
            return expression is IEdmStringConstantExpression constant ? constant.Value : null;
        }

        #endregion

    }

}
