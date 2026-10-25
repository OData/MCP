// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Microsoft.OData.Mcp.Core.Vocabularies;

namespace Microsoft.OData.Mcp.AspNetCore.Vocabularies
{

    /// <summary>
    /// Stamps <c>[McpTask]</c> onto matching EDM actions on a finished model.
    /// </summary>
    public static class McpTaskAttributeAnnotations
    {

        #region Fields

        internal const BindingFlags OperationBindingFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        internal const string RestierApiBuilderTypeName = "Microsoft.Restier.Core.RestierApiBuilder, Microsoft.Restier.Core";

        #endregion

        #region Public Methods

        /// <summary>
        /// Stamps <c>Microsoft.OData.Mcp.V1.Task</c> on each EDM action whose CLR method on
        /// <paramref name="sourceTypes"/> has <see cref="McpTaskAttribute"/>.
        /// </summary>
        /// <param name="model">The finished service model from route discovery.</param>
        /// <param name="sourceTypes">Restier API types and/or MVC controllers.</param>
        /// <example>
        /// <code>
        /// McpTaskAttributeAnnotations.Apply(edmModel, [typeof(MyApi)]);
        /// </code>
        /// </example>
        /// <remarks>
        /// No-ops when <paramref name="model"/> is not a mutable <see cref="EdmModel"/>.
        /// Methods are matched to <see cref="IEdmAction"/> by name and bound/unbound shape.
        /// Functions are never stamped.
        /// </remarks>
        public static void Apply(IEdmModel model, IEnumerable<Type> sourceTypes)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(sourceTypes);

            if (model is not EdmModel edm)
            {
                return;
            }

            foreach (var type in sourceTypes)
            {
                StampType(edm, type);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Short name of a CLR type, unwrapping a single-argument enumerable.
        /// </summary>
        /// <param name="type">The CLR type.</param>
        /// <returns>
        /// The element or type name.
        /// </returns>
        internal static string ClrElementName(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (type != typeof(string)
                && type.IsGenericType
                && type.GetGenericArguments().Length == 1
                && typeof(IEnumerable).IsAssignableFrom(type))
            {
                return type.GetGenericArguments()[0].Name;
            }

            return type.Name;
        }

        /// <summary>
        /// Returns whether <paramref name="clr"/> names the same type as <paramref name="edm"/>.
        /// </summary>
        /// <param name="clr">A CLR parameter type, possibly <c>IEnumerable&lt;T&gt;</c>.</param>
        /// <param name="edm">An EDM type, possibly a collection.</param>
        /// <returns>
        /// <c>true</c> when the short names match.
        /// </returns>
        internal static bool ClrMatchesEdmType(Type clr, IEdmType edm)
        {
            ArgumentNullException.ThrowIfNull(clr);
            ArgumentNullException.ThrowIfNull(edm);

            return string.Equals(ClrElementName(clr), EdmElementName(edm), StringComparison.Ordinal);
        }

        /// <summary>
        /// MVC controller types from <see cref="ApplicationPartManager"/>, plus Restier API types
        /// registered on <c>Action&lt;RestierApiBuilder&gt;</c> when Restier is loaded.
        /// </summary>
        /// <param name="services">The application service provider.</param>
        /// <returns>
        /// Distinct CLR types to scan for <see cref="McpTaskAttribute"/>.
        /// </returns>
        internal static List<Type> CollectSourceTypes(IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);

            var types = new List<Type>();
            types.AddRange(TryGetControllerTypes(services));
            types.AddRange(TryGetRestierApiTypes(services));
            return [.. types.Distinct()];
        }

        /// <summary>
        /// The EDM action on <paramref name="model"/> that matches <paramref name="method"/> by
        /// name and bound/unbound shape, or <see langword="null"/>.
        /// </summary>
        /// <param name="model">The service model.</param>
        /// <param name="method">A CLR method with <see cref="McpTaskAttribute"/>.</param>
        /// <returns>
        /// The matching action, or <see langword="null"/>.
        /// </returns>
        internal static IEdmAction? FindMatchingAction(IEdmModel model, MethodInfo method)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(method);

            return model.SchemaElements.OfType<IEdmAction>()
                .FirstOrDefault(action =>
                    string.Equals(action.Name, method.Name, StringComparison.Ordinal)
                    && MethodBindsLike(model, method, action));
        }

        /// <summary>
        /// Short name of an EDM type, unwrapping a collection.
        /// </summary>
        /// <param name="type">The EDM type.</param>
        /// <returns>
        /// The element or type name, or <see langword="null"/>.
        /// </returns>
        internal static string? EdmElementName(IEdmType type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (type is IEdmCollectionType collection)
            {
                type = collection.ElementType.Definition;
            }

            return (type as IEdmNamedElement)?.Name;
        }

        /// <summary>
        /// Returns whether <paramref name="method"/> has the same bound/unbound shape as
        /// <paramref name="action"/>.
        /// </summary>
        /// <param name="model">The service model, used to recognize entity-typed CLR parameters.</param>
        /// <param name="method">The CLR method.</param>
        /// <param name="action">The EDM action.</param>
        /// <returns>
        /// <c>true</c> when the shapes match.
        /// </returns>
        internal static bool MethodBindsLike(IEdmModel model, MethodInfo method, IEdmAction action)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(action);

            var first = method.GetParameters().FirstOrDefault();
            if (action.IsBound)
            {
                var binding = action.Parameters.FirstOrDefault();
                return first is not null
                    && binding is not null
                    && ClrMatchesEdmType(first.ParameterType, binding.Type.Definition);
            }

            if (first is null)
            {
                return true;
            }

            var elementName = ClrElementName(first.ParameterType);
            return model.SchemaElements.OfType<IEdmEntityType>()
                .All(entity => !string.Equals(entity.Name, elementName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Stamps each <c>[McpTask]</c> method on <paramref name="type"/> that matches an action.
        /// </summary>
        /// <param name="model">The mutable service model.</param>
        /// <param name="type">A Restier API or controller type.</param>
        internal static void StampType(EdmModel model, Type type)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(type);

            foreach (var method in type.GetMethods(OperationBindingFlags))
            {
                if (method.IsSpecialName || method.DeclaringType == typeof(object))
                {
                    continue;
                }

                var task = method.GetCustomAttribute<McpTaskAttribute>(true);
                var elicit = method.GetCustomAttribute<McpElicitUrlAttribute>(true);
                if (task is null && elicit is null)
                {
                    continue;
                }

                var action = FindMatchingAction(model, method);
                if (action is null)
                {
                    continue;
                }

                var elicitNames = elicit is { PropertyNames.Length: > 0 }
                    ? elicit.PropertyNames
                    : task?.ElicitUrlProperties;
                if (task is not null)
                {
                    McpTaskAnnotations.StampTask(model, action, elicitNames);
                }
                else if (elicitNames is { Length: > 0 })
                {
                    McpTaskAnnotations.EnsureReferenced(model);
                    McpTaskAnnotations.StampElicitUrlProperties(model, action, elicitNames);
                }
            }
        }

        /// <summary>
        /// MVC controller CLR types from <paramref name="services"/>, or none.
        /// </summary>
        /// <param name="services">The application services.</param>
        /// <returns>
        /// Controller types, possibly empty.
        /// </returns>
        internal static IEnumerable<Type> TryGetControllerTypes(IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);

            var manager = services.GetService<ApplicationPartManager>();
            if (manager is null)
            {
                yield break;
            }

            var feature = new ControllerFeature();
            manager.PopulateFeature(feature);
            foreach (var controller in feature.Controllers)
            {
                yield return controller.AsType();
            }
        }

        /// <summary>
        /// Restier API types from the singleton <c>Action&lt;RestierApiBuilder&gt;</c>, or none
        /// when Restier is not loaded.
        /// </summary>
        /// <param name="services">The application services.</param>
        /// <returns>
        /// API types keyed in <c>RestierApiBuilder.Apis</c>, possibly empty.
        /// </returns>
        internal static IEnumerable<Type> TryGetRestierApiTypes(IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);

            var builderType = Type.GetType(RestierApiBuilderTypeName);
            if (builderType is null)
            {
                yield break;
            }

            var configure = services.GetService(typeof(Action<>).MakeGenericType(builderType));
            if (configure is null)
            {
                yield break;
            }

            object builder;
            try
            {
                builder = Activator.CreateInstance(builderType)!;
                configure.GetType().GetMethod(nameof(Action.Invoke))!.Invoke(configure, [builder]);
            }
            catch (TargetInvocationException)
            {
                yield break;
            }

            if (builderType.GetProperty("Apis", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(builder)
                is not IDictionary apis)
            {
                yield break;
            }

            foreach (var key in apis.Keys)
            {
                if (key is Type type)
                {
                    yield return type;
                }
            }
        }

        #endregion

    }

}
