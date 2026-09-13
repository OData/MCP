// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// One log message captured by a <see cref="CapturingLoggerProvider"/>.
    /// </summary>
    /// <remarks>
    /// Instances are created only by <see cref="CapturingLogger.Log{TState}"/>; nothing here validates its own
    /// property values because they are always supplied from a real <see cref="ILogger.Log{TState}"/> call.
    /// </remarks>
    public sealed class CapturedLogEntry
    {

        #region Properties

        /// <summary>
        /// Gets the logger category name the entry was written under.
        /// </summary>
        public required string Category { get; init; }

        /// <summary>
        /// Gets the event id the entry was written with.
        /// </summary>
        public required EventId EventId { get; init; }

        /// <summary>
        /// Gets the exception logged with the entry, or <see langword="null"/> when none was supplied.
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Gets the severity the entry was written at.
        /// </summary>
        public required LogLevel Level { get; init; }

        /// <summary>
        /// Gets the formatted message text.
        /// </summary>
        public required string Message { get; init; }

        #endregion

    }

}
