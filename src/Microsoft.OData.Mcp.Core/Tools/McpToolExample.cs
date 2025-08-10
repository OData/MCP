// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Microsoft.OData.Mcp.Core.Tools
{

    /// <summary>
    /// Represents an example usage pattern for an MCP tool.
    /// </summary>
    /// <remarks>
    /// Examples help AI models understand how to use tools effectively and provide
    /// better assistance to users by demonstrating common usage patterns.
    /// </remarks>
    public sealed class McpToolExample
    {

        #region Properties

        /// <summary>
        /// Gets or sets the title of the example.
        /// </summary>
        /// <value>A brief, descriptive title for the example.</value>
        /// <remarks>
        /// The title should clearly indicate what the example demonstrates,
        /// such as "Create a new customer" or "Query products by category".
        /// </remarks>
        public required string Title { get; set; }

        /// <summary>
        /// Gets or sets the description of the example.
        /// </summary>
        /// <value>A detailed description explaining the example's purpose and context.</value>
        /// <remarks>
        /// The description should provide context about when and why this
        /// example would be useful, including any prerequisites or assumptions.
        /// </remarks>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the example input parameters.
        /// </summary>
        /// <value>A JSON document containing the example input parameters.</value>
        /// <remarks>
        /// The input should be a complete, valid example that demonstrates
        /// the tool's parameter schema and typical usage patterns.
        /// </remarks>
        public required JsonDocument Input { get; set; }

        /// <summary>
        /// Gets or sets the expected output for this example.
        /// </summary>
        /// <value>A JSON document showing the expected result, or null if not provided.</value>
        /// <remarks>
        /// Expected output helps users understand what to expect from the tool
        /// and can be used for testing and validation purposes.
        /// </remarks>
        public JsonDocument? ExpectedOutput { get; set; }

        /// <summary>
        /// Gets or sets the scenario category for this example.
        /// </summary>
        /// <value>The category that groups related examples together.</value>
        /// <remarks>
        /// Common categories include "Basic Usage", "Advanced Queries", "Error Handling",
        /// "Performance Optimization", etc. This helps organize examples by complexity or purpose.
        /// </remarks>
        public string Category { get; set; } = "Basic Usage";

        /// <summary>
        /// Gets or sets the difficulty level of this example.
        /// </summary>
        /// <value>The complexity level of the example.</value>
        /// <remarks>
        /// Difficulty levels help users and AI models choose appropriate examples
        /// based on their experience and needs.
        /// </remarks>
        public McpToolExampleDifficulty Difficulty { get; set; } = McpToolExampleDifficulty.Beginner;

        /// <summary>
        /// Gets or sets the tags associated with this example.
        /// </summary>
        /// <value>A collection of tags for categorizing and searching examples.</value>
        /// <remarks>
        /// Tags provide flexible categorization beyond the main category,
        /// allowing for cross-cutting concerns like "authentication", "pagination", "bulk-operations", etc.
        /// </remarks>
        public List<string> Tags { get; set; } = [];

        /// <summary>
        /// Gets or sets additional notes about this example.
        /// </summary>
        /// <value>Free-form notes providing additional context or warnings.</value>
        /// <remarks>
        /// Notes can include tips, warnings about edge cases, performance considerations,
        /// or links to related documentation.
        /// </remarks>
        public string? Notes { get; set; }

        /// <summary>
        /// Gets or sets the prerequisites for this example.
        /// </summary>
        /// <value>A list of conditions that must be met for this example to work.</value>
        /// <remarks>
        /// Prerequisites might include required permissions, data setup,
        /// configuration settings, or other dependencies.
        /// </remarks>
        public List<string> Prerequisites { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether this example requires authentication.
        /// </summary>
        /// <value><c>true</c> if authentication is required; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// This flag helps users and AI models understand whether they need
        /// to provide authentication credentials to use this example.
        /// </remarks>
        public bool RequiresAuthentication { get; set; }

        /// <summary>
        /// Gets or sets the expected execution time for this example.
        /// </summary>
        /// <value>The approximate time this example should take to execute.</value>
        /// <remarks>
        /// This helps set expectations and can be used for performance monitoring
        /// and timeout configuration.
        /// </remarks>
        public TimeSpan? ExpectedExecutionTime { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolExample"/> class.
        /// </summary>
        public McpToolExample()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolExample"/> class with basic information.
        /// </summary>
        /// <param name="title">The example title.</param>
        /// <param name="input">The example input parameters.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
        public McpToolExample(string title, JsonDocument input)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentNullException.ThrowIfNull(input);

            Title = title;
            Input = input;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a basic example with the specified title and input.
        /// </summary>
        /// <param name="title">The example title.</param>
        /// <param name="input">The input object to serialize to JSON.</param>
        /// <param name="description">Optional description of the example.</param>
        /// <returns>A new tool example.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
        public static McpToolExample Create(string title, object input, string? description = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentNullException.ThrowIfNull(input);

            var json = JsonSerializer.Serialize(input);
            var document = JsonDocument.Parse(json);

            return new McpToolExample
            {
                Title = title,
                Input = document,
                Description = description ?? string.Empty
            };
        }

        /// <summary>
        /// Creates an example with expected output.
        /// </summary>
        /// <param name="title">The example title.</param>
        /// <param name="input">The input object to serialize to JSON.</param>
        /// <param name="expectedOutput">The expected output object to serialize to JSON.</param>
        /// <param name="description">Optional description of the example.</param>
        /// <returns>A new tool example with expected output.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="expectedOutput"/> is null.</exception>
        public static McpToolExample CreateWithOutput(string title, object input, object expectedOutput, string? description = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(expectedOutput);

            var inputJson = JsonSerializer.Serialize(input);
            var inputDocument = JsonDocument.Parse(inputJson);

            var outputJson = JsonSerializer.Serialize(expectedOutput);
            var outputDocument = JsonDocument.Parse(outputJson);

            return new McpToolExample
            {
                Title = title,
                Input = inputDocument,
                ExpectedOutput = outputDocument,
                Description = description ?? string.Empty
            };
        }

        /// <summary>
        /// Adds a tag to this example.
        /// </summary>
        /// <param name="tag">The tag to add.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tag"/> is null or whitespace.</exception>
        public void AddTag(string tag)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tag);

            if (!Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                Tags.Add(tag);
            }
        }

        /// <summary>
        /// Adds multiple tags to this example.
        /// </summary>
        /// <param name="tags">The tags to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tags"/> is null.</exception>
        public void AddTags(params string[] tags)
        {
            ArgumentNullException.ThrowIfNull(tags);

            foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                AddTag(tag);
            }
        }

        /// <summary>
        /// Adds a prerequisite to this example.
        /// </summary>
        /// <param name="prerequisite">The prerequisite to add.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="prerequisite"/> is null or whitespace.</exception>
        public void AddPrerequisite(string prerequisite)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prerequisite);

            if (!Prerequisites.Contains(prerequisite, StringComparer.OrdinalIgnoreCase))
            {
                Prerequisites.Add(prerequisite);
            }
        }

        /// <summary>
        /// Determines whether this example has the specified tag.
        /// </summary>
        /// <param name="tag">The tag to check for.</param>
        /// <returns><c>true</c> if the example has the specified tag; otherwise, <c>false</c>.</returns>
        public bool HasTag(string tag)
        {
            return Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the input as a formatted JSON string.
        /// </summary>
        /// <param name="indent">Whether to indent the JSON for readability.</param>
        /// <returns>The input parameters as a JSON string.</returns>
        public string GetInputJson(bool indent = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indent
            };

            return JsonSerializer.Serialize(Input.RootElement, options);
        }

        /// <summary>
        /// Gets the expected output as a formatted JSON string.
        /// </summary>
        /// <param name="indent">Whether to indent the JSON for readability.</param>
        /// <returns>The expected output as a JSON string, or null if no expected output is defined.</returns>
        public string? GetExpectedOutputJson(bool indent = true)
        {
            if (ExpectedOutput is null)
            {
                return null;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = indent
            };

            return JsonSerializer.Serialize(ExpectedOutput.RootElement, options);
        }

        /// <summary>
        /// Validates the example for completeness and correctness.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the example is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Title))
            {
                errors.Add("Example title is required.");
            }

            if (Input is null)
            {
                errors.Add("Example input is required.");
            }

            if (RequiresAuthentication && !Prerequisites.Any(p => p.ToLowerInvariant().Contains("auth")))
            {
                errors.Add("Authentication is required but no authentication prerequisites are specified.");
            }

            return errors;
        }

        /// <summary>
        /// Creates a deep copy of this example.
        /// </summary>
        /// <returns>A new instance that is a copy of this example.</returns>
        public McpToolExample Clone()
        {
            // Create a deep copy of the JsonDocument by serializing and deserializing
            var inputJson = JsonSerializer.Serialize(Input);
            var inputCopy = JsonDocument.Parse(inputJson);

            JsonDocument? outputCopy = null;
            if (ExpectedOutput is not null)
            {
                var outputJson = JsonSerializer.Serialize(ExpectedOutput);
                outputCopy = JsonDocument.Parse(outputJson);
            }

            return new McpToolExample
            {
                Title = Title,
                Description = Description,
                Input = inputCopy,
                ExpectedOutput = outputCopy,
                Category = Category,
                Difficulty = Difficulty,
                Tags = new List<string>(Tags),
                Notes = Notes,
                Prerequisites = new List<string>(Prerequisites),
                RequiresAuthentication = RequiresAuthentication,
                ExpectedExecutionTime = ExpectedExecutionTime
            };
        }

        /// <summary>
        /// Returns a string representation of the example.
        /// </summary>
        /// <returns>A summary of the example.</returns>
        public override string ToString()
        {
            var difficulty = Difficulty != McpToolExampleDifficulty.Beginner ? $" [{Difficulty}]" : string.Empty;
            var auth = RequiresAuthentication ? " [Auth Required]" : string.Empty;
            
            return $"{Title}{difficulty}{auth} - {Category}";
        }

        #endregion

    }

}
