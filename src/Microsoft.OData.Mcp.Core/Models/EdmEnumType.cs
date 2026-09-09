// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents an enumeration type in an OData model.
    /// </summary>
    /// <remarks>
    /// Enumeration types declare a closed set of named members over an integral underlying type. The members
    /// are part of the type shape the calling AI reads, so a type with zero members is rejected at parse time.
    /// </remarks>
    public sealed class EdmEnumType
    {

        #region Properties

        /// <summary>
        /// Gets or sets the CSDL documentation summary or <c>Core.Description</c> for this type.
        /// </summary>
        /// <value>A human-readable summary from metadata, or <c>null</c> when none is declared.</value>
        public string? Description { get; set; }

        /// <summary>
        /// Gets the fully qualified name of the enumeration type.
        /// </summary>
        /// <value>The namespace and name combined with a dot separator.</value>
        [JsonIgnore]
        public string FullName => $"{Namespace}.{Name}";

        /// <summary>
        /// Gets or sets a value indicating whether members can be combined as bit flags.
        /// </summary>
        /// <value><c>true</c> when CSDL declares <c>IsFlags="true"</c>; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Flag enumerations accept comma-separated member names in JSON, for example <c>"Read,Write"</c>.
        /// </remarks>
        public bool IsFlags { get; set; }

        /// <summary>
        /// Gets or sets the CSDL long description or <c>Core.LongDescription</c> for this type.
        /// </summary>
        /// <value>A longer documentation string from metadata, or <c>null</c> when none is declared.</value>
        public string? LongDescription { get; set; }

        /// <summary>
        /// Gets or sets the declared members in declaration order.
        /// </summary>
        /// <value>The members of the enumeration. Never empty for a parsed model.</value>
        public List<EdmEnumMember> Members { get; set; } = [];

        /// <summary>
        /// Gets or sets the name of the enumeration type.
        /// </summary>
        /// <value>The local name of the type within its namespace.</value>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the namespace of the enumeration type.
        /// </summary>
        /// <value>The namespace that contains this type.</value>
        public required string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the integral EDM type that stores member values.
        /// </summary>
        /// <value>One of <c>Edm.Byte</c>, <c>Edm.SByte</c>, <c>Edm.Int16</c>, <c>Edm.Int32</c>, or <c>Edm.Int64</c>. Defaults to <c>Edm.Int32</c>.</value>
        /// <remarks>
        /// This is the storage type only. It does not describe the JSON wire format, which carries member names by default.
        /// </remarks>
        public string UnderlyingType { get; set; } = "Edm.Int32";

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEnumType"/> class.
        /// </summary>
        public EdmEnumType()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmEnumType"/> class with the specified name and namespace.
        /// </summary>
        /// <param name="name">The name of the enumeration type.</param>
        /// <param name="namespace">The namespace of the enumeration type.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="namespace"/> is null or whitespace.</exception>
        [SetsRequiredMembers]
        public EdmEnumType(string name, string @namespace)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);

            Name = name;
            Namespace = @namespace;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether the specified object is equal to the current enumeration type.
        /// </summary>
        /// <param name="obj">The object to compare with the current type.</param>
        /// <returns><c>true</c> if the specified object declares the same type; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmEnumType other &&
                   Name == other.Name &&
                   Namespace == other.Namespace &&
                   UnderlyingType == other.UnderlyingType &&
                   IsFlags == other.IsFlags &&
                   Members.SequenceEqual(other.Members);
        }

        /// <summary>
        /// Returns a hash code for the current enumeration type.
        /// </summary>
        /// <returns>A hash code for the current type.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Namespace, UnderlyingType, IsFlags);
        }

        /// <summary>
        /// Gets a member by name.
        /// </summary>
        /// <param name="memberName">The name of the member to retrieve.</param>
        /// <returns>The member with the specified name, or <c>null</c> if not found.</returns>
        public EdmEnumMember? GetMember(string memberName)
        {
            return Members.FirstOrDefault(member => member.Name.Equals(memberName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Determines whether the enumeration declares a member with the specified name.
        /// </summary>
        /// <param name="memberName">The name of the member to check.</param>
        /// <returns><c>true</c> if the member exists; otherwise, <c>false</c>.</returns>
        public bool HasMember(string memberName)
        {
            return Members.Any(member => member.Name.Equals(memberName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Returns a string representation of the enumeration type.
        /// </summary>
        /// <returns>The fully qualified name of the type.</returns>
        public override string ToString()
        {
            return FullName;
        }

        #endregion

    }

}
