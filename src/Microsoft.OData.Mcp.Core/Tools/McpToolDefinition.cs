using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Core.Tools
{
    /// <summary>
    /// Represents a complete MCP tool definition with metadata and implementation details.
    /// </summary>
    /// <remarks>
    /// This class encapsulates all information needed to register and execute an MCP tool,
    /// including its schema, parameters, authorization requirements, and execution context.
    /// </remarks>
    public sealed class McpToolDefinition
    {
        #region Properties

        /// <summary>
        /// Gets or sets the unique name of the tool.
        /// </summary>
        /// <value>The tool name as it will appear in the MCP protocol.</value>
        /// <remarks>
        /// Tool names must be unique within the MCP server and should follow naming
        /// conventions that clearly indicate their purpose and target entity.
        /// </remarks>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the human-readable description of the tool.
        /// </summary>
        /// <value>A description explaining what the tool does and how to use it.</value>
        /// <remarks>
        /// This description is used by AI models to understand the tool's purpose
        /// and determine when it should be used.
        /// </remarks>
        public required string Description { get; set; }

        /// <summary>
        /// Gets or sets the tool category.
        /// </summary>
        /// <value>The category that groups related tools together.</value>
        /// <remarks>
        /// Common categories include "CRUD", "Query", "Navigation", and "Batch".
        /// Categories help organize tools and can be used for filtering and authorization.
        /// </remarks>
        public required string Category { get; set; }

        /// <summary>
        /// Gets or sets the operation type for this tool.
        /// </summary>
        /// <value>The type of operation this tool performs.</value>
        /// <remarks>
        /// Operation types are used for authorization and auditing. They indicate
        /// the level of data access and modification the tool performs.
        /// </remarks>
        public McpToolOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the target entity type for entity-specific tools.
        /// </summary>
        /// <value>The fully qualified name of the target entity type, or null for general tools.</value>
        /// <remarks>
        /// This is used for entity-specific tools to identify which OData entity
        /// type the tool operates on. General tools that work across entities
        /// should leave this null.
        /// </remarks>
        public string? TargetEntityType { get; set; }

        /// <summary>
        /// Gets or sets the target entity set for entity set-specific tools.
        /// </summary>
        /// <value>The name of the target entity set, or null for entity type tools.</value>
        /// <remarks>
        /// This is used for tools that operate on specific entity sets, which
        /// may have different permissions or configurations than the entity type.
        /// </remarks>
        public string? TargetEntitySet { get; set; }

        /// <summary>
        /// Gets or sets the input schema for the tool parameters.
        /// </summary>
        /// <value>A JSON schema document defining the tool's input parameters.</value>
        /// <remarks>
        /// This schema is used by MCP clients to validate input and provide
        /// type-safe parameter binding. It should follow JSON Schema specification.
        /// </remarks>
        public required JsonDocument InputSchema { get; set; }

        /// <summary>
        /// Gets or sets the output schema for the tool results.
        /// </summary>
        /// <value>A JSON schema document defining the tool's output format.</value>
        /// <remarks>
        /// This schema describes the structure of the tool's return value,
        /// helping clients understand and process the results.
        /// </remarks>
        public JsonDocument? OutputSchema { get; set; }

        /// <summary>
        /// Gets or sets the required OAuth2 scopes for this tool.
        /// </summary>
        /// <value>A collection of OAuth2 scopes that users must have to use this tool.</value>
        /// <remarks>
        /// Users must have at least one of these scopes to execute the tool.
        /// This provides fine-grained authorization control based on OAuth2 tokens.
        /// </remarks>
        public List<string> RequiredScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the required roles for this tool.
        /// </summary>
        /// <value>A collection of roles that users must have to use this tool.</value>
        /// <remarks>
        /// This provides role-based authorization control in addition to or
        /// instead of scope-based authorization.
        /// </remarks>
        public List<string> RequiredRoles { get; set; } = [];

        /// <summary>
        /// Gets or sets additional metadata for the tool.
        /// </summary>
        /// <value>A dictionary of custom metadata key-value pairs.</value>
        /// <remarks>
        /// This can include information such as rate limits, caching behavior,
        /// or other tool-specific configuration data.
        /// </remarks>
        public Dictionary<string, object> Metadata { get; set; } = [];

        /// <summary>
        /// Gets or sets example usage scenarios for the tool.
        /// </summary>
        /// <value>A collection of example usage patterns.</value>
        /// <remarks>
        /// Examples help AI models understand how to use the tool effectively
        /// and provide better assistance to users.
        /// </remarks>
        public List<McpToolExample> Examples { get; set; } = [];

        /// <summary>
        /// Gets or sets the tool implementation handler.
        /// </summary>
        /// <value>The function that executes the tool logic.</value>
        /// <remarks>
        /// This is the actual implementation that gets called when the tool
        /// is invoked. It receives the tool context and parameters, and returns
        /// the execution result.
        /// </remarks>
        public required Func<McpToolContext, JsonDocument, Task<McpToolResult>> Handler { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this tool supports batch operations.
        /// </summary>
        /// <value><c>true</c> if the tool can process multiple items in a single call; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Batch-enabled tools can improve performance by processing multiple
        /// operations in a single request to the underlying OData service.
        /// </remarks>
        public bool SupportsBatch { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this tool is deprecated.
        /// </summary>
        /// <value><c>true</c> if the tool is deprecated; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Deprecated tools are still functional but should be avoided in favor
        /// of newer alternatives. They may be removed in future versions.
        /// </remarks>
        public bool IsDeprecated { get; set; }

        /// <summary>
        /// Gets or sets the deprecation message for deprecated tools.
        /// </summary>
        /// <value>A message explaining why the tool is deprecated and what should be used instead.</value>
        /// <remarks>
        /// This message is shown to users when they attempt to use a deprecated tool,
        /// guiding them to better alternatives.
        /// </remarks>
        public string? DeprecationMessage { get; set; }

        /// <summary>
        /// Gets or sets the version of the tool.
        /// </summary>
        /// <value>The version string for this tool definition.</value>
        /// <remarks>
        /// Tool versions help track changes and ensure compatibility. They should
        /// follow semantic versioning principles.
        /// </remarks>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the time when this tool definition was created.
        /// </summary>
        /// <value>The UTC timestamp when the tool was generated.</value>
        /// <remarks>
        /// This timestamp is used for caching, versioning, and audit purposes.
        /// </remarks>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolDefinition"/> class.
        /// </summary>
        public McpToolDefinition()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a basic CRUD tool definition.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="description">The tool description.</param>
        /// <param name="operationType">The operation type.</param>
        /// <param name="entityType">The target entity type.</param>
        /// <param name="inputSchema">The input schema.</param>
        /// <param name="handler">The tool handler.</param>
        /// <param name="entitySet">The optional entity set name for the entity type.</param>
        /// <returns>A new tool definition for CRUD operations.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="inputSchema"/> or <paramref name="handler"/> is null.</exception>
        public static McpToolDefinition CreateCrudTool(
            string name,
            string description,
            McpToolOperationType operationType,
            string entityType,
            JsonDocument inputSchema,
            Func<McpToolContext, JsonDocument, Task<McpToolResult>> handler,
            string? entitySet = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
            ArgumentNullException.ThrowIfNull(inputSchema);
            ArgumentNullException.ThrowIfNull(handler);

            return new McpToolDefinition
            {
                Name = name,
                Description = description,
                Category = "CRUD",
                OperationType = operationType,
                TargetEntityType = entityType,
                TargetEntitySet = entitySet,
                InputSchema = inputSchema,
                Handler = handler
            };
        }

        /// <summary>
        /// Creates a query tool definition.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="description">The tool description.</param>
        /// <param name="inputSchema">The input schema.</param>
        /// <param name="handler">The tool handler.</param>
        /// <returns>A new tool definition for query operations.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="inputSchema"/> or <paramref name="handler"/> is null.</exception>
        public static McpToolDefinition CreateQueryTool(
            string name,
            string description,
            JsonDocument inputSchema,
            Func<McpToolContext, JsonDocument, Task<McpToolResult>> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            ArgumentNullException.ThrowIfNull(inputSchema);
            ArgumentNullException.ThrowIfNull(handler);

            return new McpToolDefinition
            {
                Name = name,
                Description = description,
                Category = "Query",
                OperationType = McpToolOperationType.Read,
                InputSchema = inputSchema,
                Handler = handler,
                SupportsBatch = true
            };
        }

        /// <summary>
        /// Adds an example to the tool definition.
        /// </summary>
        /// <param name="title">The example title.</param>
        /// <param name="description">The example description.</param>
        /// <param name="input">The example input parameters.</param>
        /// <param name="expectedOutput">The expected output (optional).</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
        public void AddExample(string title, string description, JsonDocument input, JsonDocument? expectedOutput = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentNullException.ThrowIfNull(input);

            Examples.Add(new McpToolExample
            {
                Title = title,
                Description = description ?? string.Empty,
                Input = input,
                ExpectedOutput = expectedOutput
            });
        }

        /// <summary>
        /// Adds metadata to the tool definition.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
        public void AddMetadata(string key, object value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            Metadata[key] = value;
        }

        /// <summary>
        /// Gets metadata value by key.
        /// </summary>
        /// <typeparam name="T">The type of the metadata value.</typeparam>
        /// <param name="key">The metadata key.</param>
        /// <returns>The metadata value if found and of the correct type; otherwise, the default value.</returns>
        public T? GetMetadata<T>(string key)
        {
            if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Determines whether the tool is authorized for the specified user context.
        /// </summary>
        /// <param name="userScopes">The user's OAuth2 scopes.</param>
        /// <param name="userRoles">The user's roles.</param>
        /// <returns><c>true</c> if the user is authorized to use this tool; otherwise, <c>false</c>.</returns>
        public bool IsAuthorizedForUser(IEnumerable<string> userScopes, IEnumerable<string> userRoles)
        {
            var scopes = userScopes?.ToList() ?? [];
            var roles = userRoles?.ToList() ?? [];

            // Check required scopes
            if (RequiredScopes.Count > 0)
            {
                var hasRequiredScope = RequiredScopes.Any(scope => 
                    scopes.Contains(scope, StringComparer.OrdinalIgnoreCase));
                
                if (!hasRequiredScope)
                {
                    return false;
                }
            }

            // Check required roles
            if (RequiredRoles.Count > 0)
            {
                var hasRequiredRole = RequiredRoles.Any(role => 
                    roles.Contains(role, StringComparer.OrdinalIgnoreCase));
                
                if (!hasRequiredRole)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates the tool definition for completeness and correctness.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the tool is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("Tool name is required.");
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                errors.Add("Tool description is required.");
            }

            if (string.IsNullOrWhiteSpace(Category))
            {
                errors.Add("Tool category is required.");
            }

            if (InputSchema is null)
            {
                errors.Add("Input schema is required.");
            }

            if (Handler is null)
            {
                errors.Add("Tool handler is required.");
            }

            if (IsDeprecated && string.IsNullOrWhiteSpace(DeprecationMessage))
            {
                errors.Add("Deprecation message is required for deprecated tools.");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the tool definition.
        /// </summary>
        /// <returns>A summary of the tool definition.</returns>
        public override string ToString()
        {
            var target = !string.IsNullOrWhiteSpace(TargetEntityType) ? $" ({TargetEntityType})" : string.Empty;
            var deprecated = IsDeprecated ? " [DEPRECATED]" : string.Empty;
            
            return $"{Name}: {Category} - {OperationType}{target}{deprecated}";
        }

        #endregion
    }
}
