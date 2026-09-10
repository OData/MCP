// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// A no-op logging scope returned by <see cref="CapturingLogger.BeginScope{TState}"/>, because
    /// <see cref="CapturingLoggerProvider"/> does not track scope state.
    /// </summary>
    /// <remarks>
    /// <c>Microsoft.Extensions.Logging.Abstractions.NullScope</c> is <see langword="internal"/> to that
    /// assembly, so this fixture carries its own trivial equivalent.
    /// </remarks>
    internal sealed class NullScope : IDisposable
    {

        #region Fields

        /// <summary>
        /// The single reusable instance of this scope.
        /// </summary>
        public static readonly NullScope Instance = new();

        #endregion

        #region Public Methods

        /// <summary>
        /// Does nothing; this scope carries no resources to release.
        /// </summary>
        public void Dispose()
        {
        }

        #endregion

    }

}
