using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Models
{
    /// <summary>
    /// Represents a navigation property binding in an entity set.
    /// </summary>
    /// <remarks>
    /// Navigation property bindings establish the connection between navigation properties
    /// and the entity sets that contain the target entities. They are essential for
    /// defining how relationships are resolved in the OData service.
    /// </remarks>
    public sealed class EdmNavigationPropertyBinding
    {
        #region Properties

        /// <summary>
        /// Gets or sets the path of the navigation property.
        /// </summary>
        /// <value>The navigation property path, which may be a simple property name or a more complex path.</value>
        /// <remarks>
        /// The path identifies which navigation property this binding applies to. For simple cases,
        /// this is just the property name. For more complex scenarios involving inheritance or
        /// containment, the path may include type casts or additional segments.
        /// </remarks>
        /// <example>
        /// <list type="bullet">
        /// <item><description>"Orders" - Simple navigation property</description></item>
        /// <item><description>"Microsoft.Sample.Manager/DirectReports" - Type cast and navigation</description></item>
        /// <item><description>"Address/Country" - Navigation through complex type</description></item>
        /// </list>
        /// </example>
        public required string Path { get; set; }

        /// <summary>
        /// Gets or sets the target entity set name.
        /// </summary>
        /// <value>The name of the entity set that contains the target entities for this navigation property.</value>
        /// <remarks>
        /// When following this navigation property, the target entities will be found in the
        /// entity set specified by this property. The target entity set must be defined in
        /// the same entity container.
        /// </remarks>
        public required string Target { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmNavigationPropertyBinding"/> class.
        /// </summary>
        public EdmNavigationPropertyBinding()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmNavigationPropertyBinding"/> class
        /// with the specified path and target.
        /// </summary>
        /// <param name="path">The path of the navigation property.</param>
        /// <param name="target">The target entity set name.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> or <paramref name="target"/> is null or whitespace.</exception>
        public EdmNavigationPropertyBinding(string path, string target)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(target);

            Path = path;
            Target = target;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a string representation of the navigation property binding.
        /// </summary>
        /// <returns>A string showing the path and target relationship.</returns>
        public override string ToString()
        {
            return $"{Path} -> {Target}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current navigation property binding.
        /// </summary>
        /// <param name="obj">The object to compare with the current navigation property binding.</param>
        /// <returns><c>true</c> if the specified object is equal to the current navigation property binding; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            return obj is EdmNavigationPropertyBinding other &&
                   Path == other.Path &&
                   Target == other.Target;
        }

        /// <summary>
        /// Returns a hash code for the current navigation property binding.
        /// </summary>
        /// <returns>A hash code for the current navigation property binding.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Path, Target);
        }

        #endregion
    }
}