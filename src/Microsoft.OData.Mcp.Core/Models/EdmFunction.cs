using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{
    /// <summary>
    /// Represents a function in the Entity Data Model.
    /// </summary>
    /// <remarks>
    /// Functions are operations that can be called to retrieve data or perform calculations.
    /// They are side-effect free and can be composed with other query operations.
    /// </remarks>
    public sealed class EdmFunction
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the function.
        /// </summary>
        /// <value>The function name.</value>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the namespace of the function.
        /// </summary>
        /// <value>The namespace containing this function.</value>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// Gets the fully qualified name of the function.
        /// </summary>
        /// <value>The namespace and name separated by a dot.</value>
        [JsonIgnore]
        public string FullName => string.IsNullOrWhiteSpace(Namespace) ? Name : $"{Namespace}.{Name}";

        /// <summary>
        /// Gets or sets the return type of the function.
        /// </summary>
        /// <value>The type returned by the function.</value>
        public string? ReturnType { get; set; }

        /// <summary>
        /// Gets or sets the parameters of the function.
        /// </summary>
        /// <value>A collection of parameters that the function accepts.</value>
        public List<EdmParameter> Parameters { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether the function is composable.
        /// </summary>
        /// <value><c>true</c> if the function is composable; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Composable functions can be used in query expressions and can be combined with other operations.
        /// </remarks>
        public bool IsComposable { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the function is bound.
        /// </summary>
        /// <value><c>true</c> if the function is bound to a type; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Bound functions are called on instances of a specific type.
        /// </remarks>
        public bool IsBound { get; set; } = false;

        /// <summary>
        /// Gets or sets the type that this function is bound to.
        /// </summary>
        /// <value>The type name that this function is bound to, if applicable.</value>
        public string? BindingParameterType { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmFunction"/> class.
        /// </summary>
        public EdmFunction()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmFunction"/> class with the specified name and namespace.
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="namespaceName">The namespace containing this function.</param>
        public EdmFunction(string name, string namespaceName)
        {
            Name = name ?? string.Empty;
            Namespace = namespaceName ?? string.Empty;
        }

        #endregion
    }
}