// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Configuration options for OAuth2 scope-based authorization.
    /// </summary>
    /// <remarks>
    /// These options control how OAuth2 scopes are used to authorize access to
    /// different MCP tools and operations. Scope-based authorization provides
    /// fine-grained access control beyond basic authentication.
    /// </remarks>
    public sealed class ScopeAuthorizationOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether scope-based authorization is enabled.
        /// </summary>
        /// <value><c>true</c> if scope-based authorization is enabled; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, the MCP server will check token scopes before allowing
        /// access to tools and operations. When disabled, all authenticated users
        /// have access to all available tools.
        /// </remarks>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the claim name that contains the scopes in JWT tokens.
        /// </summary>
        /// <value>The name of the claim that contains scope information.</value>
        /// <remarks>
        /// Different authorization servers use different claim names for scopes.
        /// Common values include "scope", "scp", and "permissions". The claim
        /// can contain a space-separated string or an array of scope values.
        /// </remarks>
        public string ScopeClaimName { get; set; } = "scope";

        /// <summary>
        /// Gets or sets the scope separator character for space-separated scope claims.
        /// </summary>
        /// <value>The character used to separate multiple scopes in a single claim value.</value>
        /// <remarks>
        /// When scopes are provided as a space-separated string, this character
        /// is used to split them into individual scope values. Space is the
        /// standard separator according to OAuth2 specifications.
        /// </remarks>
        public char ScopeSeparator { get; set; } = ' ';

        /// <summary>
        /// Gets or sets the required scopes for different MCP operations.
        /// </summary>
        /// <value>A mapping of operation types to their required scopes.</value>
        /// <remarks>
        /// This mapping defines which scopes are required for different types
        /// of MCP operations. Users must have at least one of the required
        /// scopes to perform the operation.
        /// </remarks>
        public Dictionary<string, List<string>> RequiredScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the tool-specific scope requirements.
        /// </summary>
        /// <value>A mapping of tool names to their required scopes.</value>
        /// <remarks>
        /// This provides fine-grained control over individual MCP tools.
        /// Tool-specific requirements override general operation requirements
        /// for the specified tools.
        /// </remarks>
        public Dictionary<string, List<string>> ToolScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the entity-specific scope requirements.
        /// </summary>
        /// <value>A mapping of entity types to their required scopes for different operations.</value>
        /// <remarks>
        /// This allows different entities to have different access requirements.
        /// For example, sensitive entities might require higher-privilege scopes
        /// than general-purpose entities.
        /// </remarks>
        public Dictionary<string, EntityScopeRequirements> EntityScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the default scopes required when no specific requirement is defined.
        /// </summary>
        /// <value>A collection of scopes required for operations without specific scope requirements.</value>
        /// <remarks>
        /// These scopes are used as a fallback when no specific scope requirements
        /// are defined for an operation, tool, or entity. This ensures a baseline
        /// level of access control.
        /// </remarks>
        public List<string> DefaultRequiredScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the behavior when required scopes are missing.
        /// </summary>
        /// <value>The action to take when a user lacks required scopes.</value>
        /// <remarks>
        /// Different behaviors provide different user experiences and security
        /// postures. Denying access is most secure, while filtering tools
        /// provides a better user experience at the cost of complexity.
        /// </remarks>
        public ScopeEnforcementBehavior EnforcementBehavior { get; set; } = ScopeEnforcementBehavior.DenyAccess;

        /// <summary>
        /// Gets or sets a value indicating whether to log scope authorization decisions.
        /// </summary>
        /// <value><c>true</c> if scope decisions should be logged; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Logging scope decisions helps with troubleshooting authorization issues
        /// and provides audit trails for security compliance. However, it may
        /// generate significant log volume in high-traffic scenarios.
        /// </remarks>
        public bool LogAuthorizationDecisions { get; set; } = true;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeAuthorizationOptions"/> class.
        /// </summary>
        public ScopeAuthorizationOptions()
        {
            InitializeDefaultScopeRequirements();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the required scopes for a specific operation.
        /// </summary>
        /// <param name="operation">The operation name.</param>
        /// <returns>The required scopes, or the default scopes if no specific requirement exists.</returns>
        public IEnumerable<string> GetRequiredScopesForOperation(string operation)
        {
            if (RequiredScopes.TryGetValue(operation, out var scopes))
            {
                return scopes;
            }

            return DefaultRequiredScopes;
        }

        /// <summary>
        /// Gets the required scopes for a specific tool.
        /// </summary>
        /// <param name="toolName">The tool name.</param>
        /// <returns>The required scopes, or the default scopes if no specific requirement exists.</returns>
        public IEnumerable<string> GetRequiredScopesForTool(string toolName)
        {
            if (ToolScopes.TryGetValue(toolName, out var scopes))
            {
                return scopes;
            }

            return DefaultRequiredScopes;
        }

        /// <summary>
        /// Gets the required scopes for a specific entity and operation.
        /// </summary>
        /// <param name="entityType">The entity type name.</param>
        /// <param name="operation">The operation being performed on the entity.</param>
        /// <returns>The required scopes, or the default scopes if no specific requirement exists.</returns>
        public IEnumerable<string> GetRequiredScopesForEntity(string entityType, string operation)
        {
            if (EntityScopes.TryGetValue(entityType, out var entityRequirements))
            {
                var scopes = entityRequirements.GetScopesForOperation(operation);
                if (scopes.Any())
                {
                    return scopes;
                }
            }

            return GetRequiredScopesForOperation(operation);
        }

        /// <summary>
        /// Adds or updates scope requirements for an operation.
        /// </summary>
        /// <param name="operation">The operation name.</param>
        /// <param name="scopes">The required scopes for the operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="operation"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scopes"/> is null.</exception>
        public void SetRequiredScopesForOperation(string operation, IEnumerable<string> scopes)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            ArgumentNullException.ThrowIfNull(scopes);

            RequiredScopes[operation] = scopes.ToList();
        }

        /// <summary>
        /// Adds or updates scope requirements for a tool.
        /// </summary>
        /// <param name="toolName">The tool name.</param>
        /// <param name="scopes">The required scopes for the tool.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="toolName"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scopes"/> is null.</exception>
        public void SetRequiredScopesForTool(string toolName, IEnumerable<string> scopes)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
            ArgumentNullException.ThrowIfNull(scopes);

            ToolScopes[toolName] = scopes.ToList();
        }

        /// <summary>
        /// Validates the scope authorization options for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the options are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (Enabled)
            {
                if (string.IsNullOrWhiteSpace(ScopeClaimName))
                {
                    errors.Add("ScopeClaimName cannot be null or empty when scope authorization is enabled.");
                }

                // Validate entity scope requirements
                foreach (var kvp in EntityScopes)
                {
                    var entityErrors = kvp.Value.Validate();
                    errors.AddRange(entityErrors.Select(e => $"Entity '{kvp.Key}': {e}"));
                }
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the scope authorization options.
        /// </summary>
        /// <returns>A summary of the scope authorization configuration.</returns>
        public override string ToString()
        {
            if (!Enabled)
            {
                return "Scope Authorization: Disabled";
            }

            return $"Scope Authorization: Claim={ScopeClaimName}, Behavior={EnforcementBehavior}, Operations={RequiredScopes.Count}";
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Initializes the default scope requirements for common operations.
        /// </summary>
        internal void InitializeDefaultScopeRequirements()
        {
            // Standard CRUD operations
            RequiredScopes["read"] = ["odata.read", "read"];
            RequiredScopes["create"] = ["odata.write", "write"];
            RequiredScopes["update"] = ["odata.write", "write"];
            RequiredScopes["delete"] = ["odata.delete", "write"];

            // Query operations
            RequiredScopes["query"] = ["odata.read", "read"];
            RequiredScopes["navigate"] = ["odata.read", "read"];

            // Administrative operations
            RequiredScopes["admin"] = ["odata.admin", "admin"];
        }

        #endregion

    }

}
