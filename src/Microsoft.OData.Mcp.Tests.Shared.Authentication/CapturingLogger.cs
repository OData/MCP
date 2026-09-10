// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// A real <see cref="ILogger"/> that formats every log message with the caller-supplied formatter and
    /// appends it to its owning <see cref="CapturingLoggerProvider"/>, so tests can assert on what a class under
    /// test actually logs without any mocking framework.
    /// </summary>
    /// <remarks>
    /// Scopes are no-ops (<see cref="NullScope"/>); this fixture does not need to track scope state to prove
    /// that a redactor keeps sensitive values out of a log sink.
    /// </remarks>
    public sealed class CapturingLogger : ILogger
    {

        #region Fields

        /// <summary>
        /// The category name this logger was created for.
        /// </summary>
        private readonly string _categoryName;

        /// <summary>
        /// The provider every captured entry is appended to.
        /// </summary>
        private readonly CapturingLoggerProvider _provider;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CapturingLogger"/> class.
        /// </summary>
        /// <param name="categoryName">The category name this logger was created for.</param>
        /// <param name="provider">The provider every captured entry is appended to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="categoryName"/> or <paramref name="provider"/> is <see langword="null"/>.</exception>
        public CapturingLogger(string categoryName, CapturingLoggerProvider provider)
        {
            ArgumentNullException.ThrowIfNull(categoryName);
            ArgumentNullException.ThrowIfNull(provider);

            _categoryName = categoryName;
            _provider = provider;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the shared no-op scope; this fixture does not track scope state.
        /// </summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>The shared <see cref="NullScope.Instance"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        /// <summary>
        /// Reports that every level is enabled, so tests never lose a message to level filtering.
        /// </summary>
        /// <param name="logLevel">The severity to check.</param>
        /// <returns><see langword="true"/> always.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <summary>
        /// Formats the message with <paramref name="formatter"/> and appends it to the owning
        /// <see cref="CapturingLoggerProvider"/>.
        /// </summary>
        /// <typeparam name="TState">The log state type.</typeparam>
        /// <param name="logLevel">The severity the entry was written at.</param>
        /// <param name="eventId">The event id the entry was written with.</param>
        /// <param name="state">The log state passed to <paramref name="formatter"/>.</param>
        /// <param name="exception">The exception logged with the entry, or <see langword="null"/> when none was supplied.</param>
        /// <param name="formatter">The delegate that renders <paramref name="state"/> and <paramref name="exception"/> to text.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="formatter"/> is <see langword="null"/>.</exception>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            _provider.Add(new CapturedLogEntry
            {
                Category = _categoryName,
                Level = logLevel,
                EventId = eventId,
                Message = formatter(state, exception),
                Exception = exception
            });
        }

        #endregion

    }

}
