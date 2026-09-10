// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Diagnostics
{

    /// <summary>
    /// One step of a <see cref="ServiceProbe"/>: what was tried, how it ended, and what the service said.
    /// </summary>
    public sealed class ProbeStep
    {

        #region Fields

        /// <summary>
        /// A step that was never attempted.
        /// </summary>
        public static readonly ProbeStep NotRun = new(ProbeOutcome.Skipped, "not attempted");

        #endregion

        #region Properties

        /// <summary>
        /// Every <c>WWW-Authenticate</c> value the response carried. Empty unless the outcome is
        /// <see cref="ProbeOutcome.Unauthorized"/> or <see cref="ProbeOutcome.Forbidden"/>.
        /// </summary>
        public IReadOnlyList<string> Challenges { get; }

        /// <summary>
        /// One line a person can read, for example <c>Categories?$top=1 → 200</c>.
        /// </summary>
        public string Detail { get; }

        /// <summary>
        /// The outcome.
        /// </summary>
        public ProbeOutcome Outcome { get; }

        /// <summary>
        /// The HTTP status code when a response was received, otherwise <c>null</c>.
        /// </summary>
        public int? StatusCode { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbeStep"/> class.
        /// </summary>
        /// <param name="outcome">The outcome.</param>
        /// <param name="detail">One readable line.</param>
        /// <param name="statusCode">The HTTP status code, if any.</param>
        /// <param name="challenges">The <c>WWW-Authenticate</c> values, if any.</param>
        public ProbeStep(ProbeOutcome outcome, string detail, int? statusCode = null, IReadOnlyList<string>? challenges = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(detail);

            Outcome = outcome;
            Detail = detail;
            StatusCode = statusCode;
            Challenges = challenges ?? [];
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Whether the step ended with an authentication or authorization refusal.
        /// </summary>
        /// <returns>
        /// <c>true</c> for <see cref="ProbeOutcome.Unauthorized"/> and <see cref="ProbeOutcome.Forbidden"/>.
        /// </returns>
        public bool IsSecured()
        {
            return Outcome is ProbeOutcome.Unauthorized or ProbeOutcome.Forbidden;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Outcome}: {Detail}";
        }

        #endregion

    }

}
