// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Benchmarks.Infrastructure
{

    /// <summary>
    /// One live OData service as the benchmarks see it: the CSDL document as served, the parsed model, and the
    /// entity set used for per-type measurements.
    /// </summary>
    public sealed class LiveService
    {

        #region Properties

        /// <summary>
        /// The entity set used for per-type measurements, for example <c>Customers</c>.
        /// </summary>
        public required string EntitySet { get; init; }

        /// <summary>
        /// The entity type behind <see cref="EntitySet"/>.
        /// </summary>
        public required EdmEntityType EntityType { get; init; }

        /// <summary>
        /// The parsed Core model.
        /// </summary>
        public required EdmModel Model { get; init; }

        /// <summary>
        /// The display name, for example <c>Northwind</c>.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// The service root URL.
        /// </summary>
        public required string Url { get; init; }

        /// <summary>
        /// The <c>$metadata</c> document exactly as the service returned it.
        /// </summary>
        public required string Xml { get; init; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LiveService"/> class.
        /// </summary>
        [SetsRequiredMembers]
        public LiveService(string name, string url, string xml, EdmModel model, string entitySet)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ArgumentException.ThrowIfNullOrWhiteSpace(xml);
            ArgumentNullException.ThrowIfNull(model);
            ArgumentException.ThrowIfNullOrWhiteSpace(entitySet);

            var set = model.AllEntitySets.FirstOrDefault(candidate => candidate.Name == entitySet)
                ?? throw new InvalidOperationException($"{name} does not declare an entity set named '{entitySet}'.");
            var type = model.GetEntityType(set.EntityTypeName, set.EntityTypeNamespace)
                ?? throw new InvalidOperationException($"{name} does not declare the type behind '{entitySet}'.");

            Name = name;
            Url = url;
            Xml = xml;
            Model = model;
            EntitySet = entitySet;
            EntityType = type;
        }

        #endregion

    }

}
