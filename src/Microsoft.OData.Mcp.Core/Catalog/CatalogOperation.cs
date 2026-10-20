// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// A declared action or function used while building named operation tools.
    /// </summary>
    /// <remarks>
    /// Snapshots the fields the catalog needs so action and function builders share one path.
    /// Overloads are dropped before an instance is stored; <see cref="BindingType"/> is the non-collection binding type.
    /// </remarks>
    internal readonly struct CatalogOperation
    {

        #region Properties

        /// <summary>
        /// Gets the declared binding parameter type, or <c>null</c> when unbound.
        /// </summary>
        internal string? BindingParameterType { get; }

        /// <summary>
        /// Gets the non-collection binding type full name, or empty when unbound.
        /// </summary>
        internal string BindingType { get; }

        /// <summary>
        /// Gets a value indicating whether the operation is collection-bound.
        /// </summary>
        internal bool CollectionBound { get; }

        /// <summary>
        /// Gets the CSDL description.
        /// </summary>
        internal string? Description { get; }

        /// <summary>
        /// Gets a value indicating whether the operation is an action.
        /// </summary>
        internal bool IsAction { get; }

        /// <summary>
        /// Gets a value indicating whether the operation is bound.
        /// </summary>
        internal bool IsBound { get; }

        /// <summary>
        /// Gets the CSDL long description.
        /// </summary>
        internal string? LongDescription { get; }

        /// <summary>
        /// Gets the EDM short name.
        /// </summary>
        internal string Name { get; }

        /// <summary>
        /// Gets the declared parameters, including the binding parameter when bound.
        /// </summary>
        internal IReadOnlyList<EdmParameter> Parameters { get; }

        /// <summary>
        /// Gets the EDM return type, or <c>null</c> when the operation returns nothing.
        /// </summary>
        internal string? ReturnType { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CatalogOperation"/> struct from an action.
        /// </summary>
        /// <param name="action">The declared action.</param>
        internal CatalogOperation(EdmAction action)
        {
            ArgumentNullException.ThrowIfNull(action);

            BindingParameterType = action.BindingParameterType;
            BindingType = EdmTypeShape.UnwrapCollectionType(action.BindingParameterType) ?? string.Empty;
            CollectionBound = EdmTypeShape.IsCollectionBound(action.BindingParameterType);
            Description = action.Description;
            IsAction = true;
            IsBound = action.IsBound;
            LongDescription = action.LongDescription;
            Name = action.Name;
            Parameters = action.Parameters;
            ReturnType = action.ReturnType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CatalogOperation"/> struct from a function.
        /// </summary>
        /// <param name="function">The declared function.</param>
        internal CatalogOperation(EdmFunction function)
        {
            ArgumentNullException.ThrowIfNull(function);

            BindingParameterType = function.BindingParameterType;
            BindingType = EdmTypeShape.UnwrapCollectionType(function.BindingParameterType) ?? string.Empty;
            CollectionBound = EdmTypeShape.IsCollectionBound(function.BindingParameterType);
            Description = function.Description;
            IsAction = false;
            IsBound = function.IsBound;
            LongDescription = function.LongDescription;
            Name = function.Name;
            Parameters = function.Parameters;
            ReturnType = function.ReturnType;
        }

        #endregion

    }

    /// <summary>
    /// First-overload actions and functions grouped for named-tool emission.
    /// </summary>
    internal sealed class CatalogOperationIndex
    {

        #region Fields

        readonly HashSet<(bool Action, string Name, string Binding)> _seen = [];

        #endregion

        #region Properties

        /// <summary>
        /// Gets collection-bound actions keyed by the element type full name.
        /// </summary>
        internal Dictionary<string, List<CatalogOperation>> CollectionActionsByType { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets collection-bound functions keyed by the element type full name.
        /// </summary>
        internal Dictionary<string, List<CatalogOperation>> CollectionFunctionsByType { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets instance-bound actions keyed by the binding type full name.
        /// </summary>
        internal Dictionary<string, List<CatalogOperation>> InstanceActionsByType { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets instance-bound functions keyed by the binding type full name.
        /// </summary>
        internal Dictionary<string, List<CatalogOperation>> InstanceFunctionsByType { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets unbound actions in declaration order.
        /// </summary>
        internal List<CatalogOperation> UnboundActions { get; } = [];

        /// <summary>
        /// Gets unbound functions in declaration order.
        /// </summary>
        internal List<CatalogOperation> UnboundFunctions { get; } = [];

        #endregion

        #region Public Methods

        /// <summary>
        /// Stores one first-overload operation.
        /// </summary>
        /// <param name="operation">The operation.</param>
        internal void Add(CatalogOperation operation)
        {
            if (!_seen.Add((operation.IsAction, operation.Name, operation.BindingParameterType ?? string.Empty)))
            {
                return;
            }

            if (!operation.IsBound)
            {
                (operation.IsAction ? UnboundActions : UnboundFunctions).Add(operation);

                return;
            }

            if (string.IsNullOrWhiteSpace(operation.BindingType))
            {
                return;
            }

            var map = (operation.CollectionBound, operation.IsAction) switch
            {
                (true, true) => CollectionActionsByType,
                (true, false) => CollectionFunctionsByType,
                (false, true) => InstanceActionsByType,
                (false, false) => InstanceFunctionsByType
            };
            if (!map.TryGetValue(operation.BindingType, out var list))
            {
                list = [];
                map[operation.BindingType] = list;
            }

            list.Add(operation);
        }

        #endregion

    }

}
