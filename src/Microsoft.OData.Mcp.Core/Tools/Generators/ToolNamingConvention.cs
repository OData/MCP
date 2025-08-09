namespace Microsoft.OData.Mcp.Core.Tools.Generators
{

    /// <summary>
    /// Naming conventions for generated tool names.
    /// </summary>
    /// <remarks>
    /// Different naming conventions provide flexibility in how tool names are formatted
    /// to match organizational standards, programming language conventions, or
    /// AI model preferences. Consistent naming helps AI models predict and
    /// understand tool functionality.
    /// </remarks>
    public enum ToolNamingConvention
    {

        /// <summary>
        /// Use PascalCase naming (e.g., CreateCustomer, UpdateOrder).
        /// </summary>
        /// <remarks>
        /// PascalCase uses uppercase letters at the beginning of each word
        /// without separators. This is the standard convention for class
        /// and method names in C# and other .NET languages.
        /// </remarks>
        PascalCase,

        /// <summary>
        /// Use camelCase naming (e.g., createCustomer, updateOrder).
        /// </summary>
        /// <remarks>
        /// CamelCase uses lowercase for the first word and uppercase for
        /// subsequent words without separators. This is common in JavaScript
        /// and JSON APIs, making it familiar to many AI models.
        /// </remarks>
        CamelCase,

        /// <summary>
        /// Use snake_case naming (e.g., create_customer, update_order).
        /// </summary>
        /// <remarks>
        /// Snake_case uses lowercase letters with underscores as word separators.
        /// This convention is popular in Python, Ruby, and many REST APIs,
        /// and is often preferred for its readability.
        /// </remarks>
        SnakeCase,

        /// <summary>
        /// Use kebab-case naming (e.g., create-customer, update-order).
        /// </summary>
        /// <remarks>
        /// Kebab-case uses lowercase letters with hyphens as word separators.
        /// This convention is common in URLs, CLI tools, and some configuration
        /// systems, though less common for API method names.
        /// </remarks>
        KebabCase

    }

}
