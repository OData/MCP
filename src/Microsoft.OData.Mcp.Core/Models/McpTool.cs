using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Mcp.Core.Tools;

namespace Microsoft.OData.Mcp.Core.Models
{
    /// <summary>
    /// Represents an MCP (Model Context Protocol) tool that can be executed by AI models.
    /// </summary>
    /// <remarks>
    /// MCP tools define the interface between AI models and external systems, providing
    /// structured input/output schemas and execution logic for specific operations.
    /// </remarks>
    public sealed class McpTool
    {
        /// <summary>
        /// Gets or sets the unique name of the tool.
        /// </summary>
        /// <value>The tool name, which must be unique within the MCP server context.</value>
        /// <remarks>
        /// Tool names should be descriptive and follow consistent naming conventions.
        /// They are used by AI models to identify and invoke specific operations.
        /// </remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable description of what the tool does.
        /// </summary>
        /// <value>A detailed description of the tool's purpose and functionality.</value>
        /// <remarks>
        /// This description helps AI models understand when and how to use the tool.
        /// It should clearly explain the tool's purpose, expected inputs, and outcomes.
        /// </remarks>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JSON schema that defines the structure of the tool's input parameters.
        /// </summary>
        /// <value>A JSON schema object describing the expected input format.</value>
        /// <remarks>
        /// The input schema defines the parameters that must be provided when invoking the tool.
        /// It includes type information, validation rules, descriptions, and examples.
        /// </remarks>
        public object? InputSchema { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the tool.
        /// </summary>
        /// <value>A dictionary of custom metadata properties.</value>
        /// <remarks>
        /// Metadata can include information about the tool's capabilities, limitations,
        /// performance characteristics, or other implementation-specific details.
        /// </remarks>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets examples of how to use the tool.
        /// </summary>
        /// <value>A collection of usage examples for the tool.</value>
        /// <remarks>
        /// Examples help AI models understand the proper usage patterns and expected
        /// input/output formats for the tool.
        /// </remarks>
        public List<McpToolExample> Examples { get; set; } = new();

        /// <summary>
        /// Gets or sets the categories or tags associated with the tool.
        /// </summary>
        /// <value>A collection of category names or tags.</value>
        /// <remarks>
        /// Categories help organize tools and make them easier to discover. Common
        /// categories might include "data", "utility", "integration", etc.
        /// </remarks>
        public HashSet<string> Categories { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the tool is currently enabled and available for use.
        /// </summary>
        /// <value><c>true</c> if the tool is enabled; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Disabled tools are not presented to AI models for execution. This can be useful
        /// for temporarily disabling problematic tools or implementing feature flags.
        /// </remarks>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the version of the tool.
        /// </summary>
        /// <value>The version string for the tool.</value>
        /// <remarks>
        /// Tool versions help track changes and compatibility. They can be used to
        /// implement versioning strategies for tool evolution.
        /// </remarks>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the estimated execution time for the tool in milliseconds.
        /// </summary>
        /// <value>The estimated execution time, or null if unknown.</value>
        /// <remarks>
        /// This hint helps AI models make informed decisions about tool selection
        /// based on performance requirements and timeout constraints.
        /// </remarks>
        public int? EstimatedExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent executions allowed for this tool.
        /// </summary>
        /// <value>The maximum concurrency limit, or null for no limit.</value>
        /// <remarks>
        /// Concurrency limits help protect backend resources and ensure system stability
        /// when multiple AI models are using the same tools simultaneously.
        /// </remarks>
        public int? MaxConcurrentExecutions { get; set; }

        /// <summary>
        /// Creates a deep copy of the MCP tool.
        /// </summary>
        /// <returns>A new <see cref="McpTool"/> instance with copied values.</returns>
        public McpTool Clone()
        {
            return new McpTool
            {
                Name = Name,
                Description = Description,
                InputSchema = InputSchema, // Note: Shallow copy of schema object
                Metadata = new Dictionary<string, object>(Metadata),
                Examples = Examples.Select(e => e.Clone()).ToList(),
                Categories = new HashSet<string>(Categories),
                IsEnabled = IsEnabled,
                Version = Version,
                EstimatedExecutionTimeMs = EstimatedExecutionTimeMs,
                MaxConcurrentExecutions = MaxConcurrentExecutions
            };
        }

        /// <summary>
        /// Validates the tool definition for completeness and correctness.
        /// </summary>
        /// <returns>A collection of validation error messages, or empty if valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("Tool name cannot be null or empty");
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                errors.Add("Tool description cannot be null or empty");
            }

            if (EstimatedExecutionTimeMs.HasValue && EstimatedExecutionTimeMs.Value < 0)
            {
                errors.Add("Estimated execution time cannot be negative");
            }

            if (MaxConcurrentExecutions.HasValue && MaxConcurrentExecutions.Value < 1)
            {
                errors.Add("Maximum concurrent executions must be at least 1");
            }

            if (string.IsNullOrWhiteSpace(Version))
            {
                errors.Add("Tool version cannot be null or empty");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the tool.
        /// </summary>
        /// <returns>A string containing the tool's name and description.</returns>
        public override string ToString()
        {
            return $"{Name}: {Description}";
        }
    }
}