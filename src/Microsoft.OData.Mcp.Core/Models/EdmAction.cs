using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Models
{
    /// <summary>
    /// Represents an action in the Entity Data Model.
    /// </summary>
    /// <remarks>
    /// Actions are operations that may have side effects and are used to modify
    /// data or perform operations that cannot be expressed through standard CRUD operations.
    /// </remarks>
    public sealed class EdmAction
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the action.
        /// </summary>
        /// <value>The action name.</value>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the namespace of the action.
        /// </summary>
        /// <value>The namespace containing this action.</value>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// Gets the fully qualified name of the action.
        /// </summary>
        /// <value>The namespace and name separated by a dot.</value>
        [JsonIgnore]
        public string FullName => string.IsNullOrWhiteSpace(Namespace) ? Name : $"{Namespace}.{Name}";

        /// <summary>
        /// Gets or sets the return type of the action.
        /// </summary>
        /// <value>The type returned by the action, if any.</value>
        public string? ReturnType { get; set; }

        /// <summary>
        /// Gets or sets the parameters of the action.
        /// </summary>
        /// <value>A collection of parameters that the action accepts.</value>
        public List<EdmParameter> Parameters { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the action is bound.
        /// </summary>
        /// <value><c>true</c> if the action is bound to a type; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Bound actions are called on instances of a specific type.
        /// </remarks>
        public bool IsBound { get; set; } = false;

        /// <summary>
        /// Gets or sets the type that this action is bound to.
        /// </summary>
        /// <value>The type name that this action is bound to, if applicable.</value>
        public string? BindingParameterType { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmAction"/> class.
        /// </summary>
        public EdmAction()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmAction"/> class with the specified name and namespace.
        /// </summary>
        /// <param name="name">The action name.</param>
        /// <param name="namespaceName">The namespace containing this action.</param>
        public EdmAction(string name, string namespaceName)
        {
            Name = name ?? string.Empty;
            Namespace = namespaceName ?? string.Empty;
        }

        #endregion
    }
}