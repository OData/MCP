// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Diagnostics
{

    /// <summary>
    /// Everything a <see cref="ServiceProbe"/> learned about one service: the three steps, the parsed model when
    /// there was one, the entity set that was sampled, and the overall verdict.
    /// </summary>
    public sealed class ServiceProbeResult
    {

        #region Properties

        /// <summary>
        /// Every <c>WWW-Authenticate</c> value seen on a 401 or 403, metadata first.
        /// </summary>
        public IReadOnlyList<string> Challenges => [.. Metadata.Challenges, .. Data.Challenges];

        /// <summary>
        /// Step 2: <c>GET {root}{EntitySet}?$top=1</c>.
        /// </summary>
        public ProbeStep Data { get; }

        /// <summary>
        /// The entity set that was sampled, or <c>null</c> when the model declared none.
        /// </summary>
        public string? EntitySet { get; }

        /// <summary>
        /// The qualified name of the type behind <see cref="EntitySet"/>, or <c>null</c>.
        /// </summary>
        public string? EntityType { get; }

        /// <summary>
        /// Step 1: <c>GET {root}$metadata</c> and parse.
        /// </summary>
        public ProbeStep Metadata { get; }

        /// <summary>
        /// The parsed model when step 1 passed, otherwise <c>null</c>.
        /// </summary>
        public EdmModel? Model { get; }

        /// <summary>
        /// Step 3: does the sampled row match the model?
        /// </summary>
        public ProbeStep Results { get; }

        /// <summary>
        /// The normalized service root, always ending in <c>/</c>.
        /// </summary>
        public Uri ServiceRoot { get; }

        /// <summary>
        /// The HTTP status of the step that refused access, when one did. Used to drive OAuth discovery.
        /// </summary>
        public int? SecuredStatusCode => Metadata.IsSecured() ? Metadata.StatusCode : Data.IsSecured() ? Data.StatusCode : null;

        /// <summary>
        /// The overall conclusion.
        /// </summary>
        public ServiceVerdict Verdict { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceProbeResult"/> class.
        /// </summary>
        public ServiceProbeResult(Uri serviceRoot, ProbeStep metadata, ProbeStep data, ProbeStep results, EdmModel? model, string? entitySet, string? entityType)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(results);

            ServiceRoot = serviceRoot;
            Metadata = metadata;
            Data = data;
            Results = results;
            Model = model;
            EntitySet = entitySet;
            EntityType = entityType;
            Verdict = Decide(metadata, data, results);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Derives the verdict from the three steps.
        /// </summary>
        /// <param name="metadata">Step 1.</param>
        /// <param name="data">Step 2.</param>
        /// <param name="results">Step 3.</param>
        /// <returns>
        /// The verdict. Security refusals win over reachability, which wins over readability.
        /// </returns>
        public static ServiceVerdict Decide(ProbeStep metadata, ProbeStep data, ProbeStep results)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(results);

            if (metadata.IsSecured())
            {
                return ServiceVerdict.MetadataSecured;
            }

            if (metadata.Outcome is ProbeOutcome.NotFound or ProbeOutcome.Failed)
            {
                return ServiceVerdict.Unreachable;
            }

            if (metadata.Outcome is ProbeOutcome.Unreadable)
            {
                return ServiceVerdict.Unreadable;
            }

            if (data.IsSecured())
            {
                return ServiceVerdict.DataSecured;
            }

            if (data.Outcome is ProbeOutcome.NotFound or ProbeOutcome.Failed)
            {
                return ServiceVerdict.Unreachable;
            }

            if (data.Outcome is ProbeOutcome.Unreadable || results.Outcome is ProbeOutcome.Unreadable)
            {
                return ServiceVerdict.Unreadable;
            }

            return ServiceVerdict.Ready;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Verdict} ({ServiceRoot}): metadata {Metadata.Outcome}, data {Data.Outcome}, results {Results.Outcome}";
        }

        #endregion

    }

}
