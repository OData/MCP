// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// An <see cref="IDictionary{TKey, TValue}"/> that does not implement
    /// <see cref="IReadOnlyDictionary{TKey, TValue}"/>, so <c>AsReadOnly</c> copies.
    /// </summary>
    internal sealed class NonReadOnlyJsonArguments : IDictionary<string, JsonElement>
    {

        #region Fields

        private readonly Dictionary<string, JsonElement> _items = new(StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <inheritdoc />
        public int Count => _items.Count;

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        public ICollection<string> Keys => _items.Keys;

        /// <inheritdoc />
        public ICollection<JsonElement> Values => _items.Values;

        /// <inheritdoc />
        public JsonElement this[string key]
        {
            get => _items[key];
            set => _items[key] = value;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public void Add(string key, JsonElement value)
        {
            _items.Add(key, value);
        }

        /// <inheritdoc />
        public void Add(KeyValuePair<string, JsonElement> item)
        {
            _items.Add(item.Key, item.Value);
        }

        /// <inheritdoc />
        public void Clear()
        {
            _items.Clear();
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<string, JsonElement> item)
        {
            return ((ICollection<KeyValuePair<string, JsonElement>>)_items).Contains(item);
        }

        /// <inheritdoc />
        public bool ContainsKey(string key)
        {
            return _items.ContainsKey(key);
        }

        /// <inheritdoc />
        public void CopyTo(KeyValuePair<string, JsonElement>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, JsonElement>>)_items).CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, JsonElement>> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        /// <inheritdoc />
        public bool Remove(string key)
        {
            return _items.Remove(key);
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<string, JsonElement> item)
        {
            return ((ICollection<KeyValuePair<string, JsonElement>>)_items).Remove(item);
        }

        /// <inheritdoc />
        public bool TryGetValue(string key, out JsonElement value)
        {
            return _items.TryGetValue(key, out value);
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

    }

}
