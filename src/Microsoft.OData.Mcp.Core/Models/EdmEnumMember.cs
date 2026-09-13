// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents one named member of an <see cref="EdmEnumType"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Value"/> is the integral value declared in CSDL. It is the storage representation only;
    /// JSON payloads carry the member <see cref="Name"/> by default.
    /// </remarks>
    public sealed class EdmEnumMember
    {

        #region Properties

        /// <summary>
        /// Gets or sets the member name.
        /// </summary>
        /// <value>The name as declared in CSDL, for example <c>Red</c>.</value>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the integral value of the member.
        /// </summary>
        /// <value>The declared value, or the zero-based declaration index when CSDL omits <c>Value</c>.</value>
        public long Value { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEnumMember"/> class.
        /// </summary>
        public EdmEnumMember()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEnumMember"/> class with the specified name and value.
        /// </summary>
        /// <param name="name">The member name.</param>
        /// <param name="value">The integral value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
        [SetsRequiredMembers]
        public EdmEnumMember(string name, long value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            Name = name;
            Value = value;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether the specified object is equal to the current member.
        /// </summary>
        /// <param name="obj">The object to compare with the current member.</param>
        /// <returns><c>true</c> if the specified object has the same name and value; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmEnumMember other &&
                   Name == other.Name &&
                   Value == other.Value;
        }

        /// <summary>
        /// Returns a hash code for the current member.
        /// </summary>
        /// <returns>A hash code for the current member.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Value);
        }

        /// <summary>
        /// Returns a string representation of the member.
        /// </summary>
        /// <returns>The name and value, for example <c>Red = 0</c>.</returns>
        public override string ToString()
        {
            return $"{Name} = {Value}";
        }

        #endregion

    }

}
