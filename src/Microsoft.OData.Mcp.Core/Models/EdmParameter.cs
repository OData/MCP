namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents a parameter in the Entity Data Model.
    /// </summary>
    /// <remarks>
    /// Parameters are used to define inputs to functions and actions in the OData model.
    /// </remarks>
    public sealed class EdmParameter
    {

        #region Properties

        /// <summary>
        /// Gets or sets the name of the parameter.
        /// </summary>
        /// <value>The parameter name.</value>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the parameter.
        /// </summary>
        /// <value>The parameter type.</value>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the parameter is nullable.
        /// </summary>
        /// <value><c>true</c> if the parameter accepts null values; otherwise, <c>false</c>.</value>
        public bool Nullable { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum length for string parameters.
        /// </summary>
        /// <value>The maximum length, or null if not specified.</value>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Gets or sets the precision for numeric parameters.
        /// </summary>
        /// <value>The precision, or null if not specified.</value>
        public int? Precision { get; set; }

        /// <summary>
        /// Gets or sets the scale for decimal parameters.
        /// </summary>
        /// <value>The scale, or null if not specified.</value>
        public int? Scale { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmParameter"/> class.
        /// </summary>
        public EdmParameter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmParameter"/> class with the specified name and type.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="type">The parameter type.</param>
        public EdmParameter(string name, string type)
        {
            Name = name ?? string.Empty;
            Type = type ?? string.Empty;
        }

        #endregion

    }

}
