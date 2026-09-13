// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// A real <see cref="ILoggerProvider"/> that collects every log message written through it, so tests can
    /// assert on log output — including that a redactor never lets a sensitive value reach a sink — without any
    /// mocking framework.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = new CapturingLoggerProvider();
    /// using var factory = LoggerFactory.Create(builder =&gt; builder.AddProvider(provider));
    /// var logger = factory.CreateLogger("Test");
    ///
    /// logger.LogInformation("token {Token}", OutboundAuthLogRedactor.DescribeToken(token));
    ///
    /// provider.AllText.Should().NotContain("SECRET-ACCESS");
    /// </code>
    /// </example>
    /// <remarks>
    /// <see cref="Entries"/> and <see cref="AllText"/> are safe to read while logging happens concurrently
    /// because <see cref="Add(CapturedLogEntry)"/> appends to a <see cref="ConcurrentQueue{T}"/>.
    /// </remarks>
    public sealed class CapturingLoggerProvider : ILoggerProvider
    {

        #region Fields

        /// <summary>
        /// The log entries captured so far, in the order they were written.
        /// </summary>
        private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

        #endregion

        #region Properties

        /// <summary>
        /// Gets every captured message joined with newlines, in capture order.
        /// </summary>
        public string AllText
        {
            get
            {
                return string.Join("\n", _entries.Select(static entry => entry.Message));
            }
        }

        /// <summary>
        /// Gets a snapshot of every log entry captured so far, in the order they were written.
        /// </summary>
        public IReadOnlyList<CapturedLogEntry> Entries
        {
            get
            {
                return [.. _entries];
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a <see cref="CapturingLogger"/> for <paramref name="categoryName"/> that appends every
        /// message it logs to this provider.
        /// </summary>
        /// <param name="categoryName">The category name of the logger to create.</param>
        /// <returns>A new <see cref="CapturingLogger"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="categoryName"/> is <see langword="null"/>.</exception>
        public ILogger CreateLogger(string categoryName)
        {
            ArgumentNullException.ThrowIfNull(categoryName);

            return new CapturingLogger(categoryName, this);
        }

        /// <summary>
        /// Does nothing; this provider owns no unmanaged or disposable resources.
        /// </summary>
        public void Dispose()
        {
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Appends a captured entry. Called by every <see cref="CapturingLogger"/> this provider creates.
        /// </summary>
        /// <param name="entry">The entry to append.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is <see langword="null"/>.</exception>
        internal void Add(CapturedLogEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            _entries.Enqueue(entry);
        }

        #endregion

    }

}
