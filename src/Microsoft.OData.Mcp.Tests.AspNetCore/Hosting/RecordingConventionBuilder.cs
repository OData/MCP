// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// Captures endpoint conventions applied by the MCP startup filter.
    /// </summary>
    internal sealed class RecordingConventionBuilder : IEndpointConventionBuilder
    {

        #region Properties

        /// <summary>
        /// Gets the applied conventions.
        /// </summary>
        public List<Action<EndpointBuilder>> Conventions { get; } = [];

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public void Add(Action<EndpointBuilder> convention)
        {
            Conventions.Add(convention);
        }

        #endregion

    }

}
