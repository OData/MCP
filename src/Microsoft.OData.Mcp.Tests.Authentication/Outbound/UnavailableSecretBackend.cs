// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Latchkey;
using Latchkey.Backends;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// A real <see cref="ISecretBackend"/> that reproduces a credential store which became unusable after the
    /// cache over it was constructed — a Secret Service keyring that locked, a cache directory that lost its
    /// permissions — by failing every read and write with <see cref="LatchkeyBackendUnavailableException"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var backend = new UnavailableSecretBackend();
    /// var store = LatchkeyFactory.Create(new LatchkeyOptions
    /// {
    ///     ServiceName = ODataMcpAuthConstants.LatchkeyServiceName,
    ///     CustomBackend = backend
    /// });
    ///
    /// // backend.RemoveCallCount proves whether the code under test tried to delete the entry.
    /// </code>
    /// </example>
    /// <remarks>
    /// <see cref="Remove(string, string)"/> deliberately succeeds and counts its calls instead of throwing, so a
    /// test can tell the difference between "the cache never attempted a delete" and "the delete itself failed".
    /// Supplied through <see cref="LatchkeyOptions.CustomBackend"/>, which bypasses backend detection entirely.
    /// This is a hand-written backend, not a mock: no mocking framework is involved.
    /// </remarks>
    public sealed class UnavailableSecretBackend : ISecretBackend
    {

        #region Fields

        /// <summary>
        /// The number of times <see cref="Remove(string, string)"/> has been called.
        /// </summary>
        private int _removeCallCount;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating that this backend reports itself as usable, so that a caller reaches its
        /// failing read rather than skipping it during detection.
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the number of times <see cref="Remove(string, string)"/> has been called.
        /// </summary>
        public int RemoveCallCount
        {
            get
            {
                return _removeCallCount;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Records the call and reports that nothing was removed.
        /// </summary>
        /// <param name="service">The Latchkey service name the entry lives under.</param>
        /// <param name="key">The key of the entry to remove.</param>
        /// <returns>
        /// <see langword="false"/> always; this backend holds nothing.
        /// </returns>
        public bool Remove(string service, string key)
        {
            _removeCallCount++;

            return false;
        }

        /// <summary>
        /// Fails as an unusable store rather than reporting a missing entry.
        /// </summary>
        /// <param name="service">The Latchkey service name the entry lives under.</param>
        /// <param name="key">The key of the entry to read.</param>
        /// <returns>
        /// Never returns; always throws.
        /// </returns>
        /// <exception cref="LatchkeyBackendUnavailableException">Always thrown.</exception>
        public byte[]? Retrieve(string service, string key)
        {
            throw new LatchkeyBackendUnavailableException("The credential store is not usable.");
        }

        /// <summary>
        /// Fails as an unusable store rather than accepting the value.
        /// </summary>
        /// <param name="service">The Latchkey service name the entry lives under.</param>
        /// <param name="key">The key of the entry to write.</param>
        /// <param name="value">The bytes that would have been written.</param>
        /// <param name="label">The human-readable label the store would show an operator.</param>
        /// <exception cref="LatchkeyBackendUnavailableException">Always thrown.</exception>
        public void Store(string service, string key, ReadOnlySpan<byte> value, string label)
        {
            throw new LatchkeyBackendUnavailableException("The credential store is not usable.");
        }

        #endregion

    }

}
