// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Benchmarks.Reports
{

    /// <summary>
    /// Every measured rendering for one service: the reference type in four formats and the whole model in five.
    /// </summary>
    public sealed class ServiceFormats
    {

        #region Properties

        /// <summary>
        /// The reference entity set, for example <c>Customers</c>.
        /// </summary>
        public string EntitySet { get; }

        /// <summary>
        /// How many entity sets the service declares.
        /// </summary>
        public int EntitySetCount { get; }

        /// <summary>
        /// Whole-model renderings, CSDL XML first.
        /// </summary>
        public IReadOnlyList<Artifact> Model { get; }

        /// <summary>
        /// The service display name.
        /// </summary>
        public string Service { get; }

        /// <summary>
        /// Reference-type renderings, CSDL XML first.
        /// </summary>
        public IReadOnlyList<Artifact> Type { get; }

        /// <summary>
        /// The reference entity type name, for example <c>Customer</c>.
        /// </summary>
        public string TypeName { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceFormats"/> class.
        /// </summary>
        public ServiceFormats(string service, string typeName, string entitySet, int entitySetCount, IReadOnlyList<Artifact> type, IReadOnlyList<Artifact> model)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(service);
            ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(entitySet);
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(model);

            Service = service;
            TypeName = typeName;
            EntitySet = entitySet;
            EntitySetCount = entitySetCount;
            Type = type;
            Model = model;
        }

        #endregion

    }

}
