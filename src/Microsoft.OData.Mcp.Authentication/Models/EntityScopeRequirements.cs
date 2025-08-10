using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Defines scope requirements for operations on a specific entity type.
    /// </summary>
    /// <remarks>
    /// Entity scope requirements allow fine-grained authorization control at the
    /// entity level, enabling different access policies for different types of
    /// data within the same OData service.
    /// </remarks>
    public sealed class EntityScopeRequirements
    {

        #region Properties

        /// <summary>
        /// Gets or sets the scopes required for reading entities of this type.
        /// </summary>
        /// <value>A collection of scopes that allow read access to the entity.</value>
        /// <remarks>
        /// Users must have at least one of these scopes to query, retrieve, or
        /// navigate to entities of this type.
        /// </remarks>
        public List<string> ReadScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the scopes required for creating entities of this type.
        /// </summary>
        /// <value>A collection of scopes that allow create access to the entity.</value>
        /// <remarks>
        /// Users must have at least one of these scopes to create new instances
        /// of this entity type.
        /// </remarks>
        public List<string> CreateScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the scopes required for updating entities of this type.
        /// </summary>
        /// <value>A collection of scopes that allow update access to the entity.</value>
        /// <remarks>
        /// Users must have at least one of these scopes to modify existing
        /// instances of this entity type.
        /// </remarks>
        public List<string> UpdateScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the scopes required for deleting entities of this type.
        /// </summary>
        /// <value>A collection of scopes that allow delete access to the entity.</value>
        /// <remarks>
        /// Users must have at least one of these scopes to delete instances
        /// of this entity type.
        /// </remarks>
        public List<string> DeleteScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the scopes required for querying entities of this type.
        /// </summary>
        /// <value>A collection of scopes that allow query access to the entity.</value>
        /// <remarks>
        /// Users must have at least one of these scopes to perform complex
        /// queries, filtering, and sorting on this entity type.
        /// </remarks>
        public List<string> QueryScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the scopes required for navigating to related entities.
        /// </summary>
        /// <value>A collection of scopes that allow navigation to related entities.</value>
        /// <remarks>
        /// Users must have at least one of these scopes to follow navigation
        /// properties from this entity type to related entities.
        /// </remarks>
        public List<string> NavigateScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets custom scope requirements for specific operations.
        /// </summary>
        /// <value>A mapping of custom operation names to their required scopes.</value>
        /// <remarks>
        /// This allows defining scope requirements for custom operations beyond
        /// the standard CRUD operations. The operation names should match those
        /// used in the MCP tool definitions.
        /// </remarks>
        public Dictionary<string, List<string>> CustomOperationScopes { get; set; } = [];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityScopeRequirements"/> class.
        /// </summary>
        public EntityScopeRequirements()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityScopeRequirements"/> class with the same scopes for all operations.
        /// </summary>
        /// <param name="allOperationsScopes">The scopes required for all operations on this entity.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="allOperationsScopes"/> is null.</exception>
        public EntityScopeRequirements(IEnumerable<string> allOperationsScopes)
        {
ArgumentNullException.ThrowIfNull(allOperationsScopes);

            var scopes = allOperationsScopes.ToList();
            ReadScopes = new List<string>(scopes);
            CreateScopes = new List<string>(scopes);
            UpdateScopes = new List<string>(scopes);
            DeleteScopes = new List<string>(scopes);
            QueryScopes = new List<string>(scopes);
            NavigateScopes = new List<string>(scopes);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityScopeRequirements"/> class with separate read and write scopes.
        /// </summary>
        /// <param name="readScopes">The scopes required for read operations.</param>
        /// <param name="writeScopes">The scopes required for write operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="readScopes"/> or <paramref name="writeScopes"/> is null.</exception>
        public EntityScopeRequirements(IEnumerable<string> readScopes, IEnumerable<string> writeScopes)
        {
ArgumentNullException.ThrowIfNull(readScopes);
            ArgumentNullException.ThrowIfNull(writeScopes);

            var readScopesList = readScopes.ToList();
            var writeScopesList = writeScopes.ToList();

            ReadScopes = new List<string>(readScopesList);
            QueryScopes = new List<string>(readScopesList);
            NavigateScopes = new List<string>(readScopesList);

            CreateScopes = new List<string>(writeScopesList);
            UpdateScopes = new List<string>(writeScopesList);
            DeleteScopes = new List<string>(writeScopesList);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the required scopes for a specific operation.
        /// </summary>
        /// <param name="operation">The operation name (e.g., "read", "create", "update", "delete", "query", "navigate").</param>
        /// <returns>The required scopes for the operation, or an empty collection if no specific requirement exists.</returns>
        public IEnumerable<string> GetScopesForOperation(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                return Enumerable.Empty<string>();
            }

            return operation.ToLowerInvariant() switch
            {
                "read" => ReadScopes,
                "create" => CreateScopes,
                "update" => UpdateScopes,
                "delete" => DeleteScopes,
                "query" => QueryScopes,
                "navigate" => NavigateScopes,
                _ => CustomOperationScopes.TryGetValue(operation, out var scopes) ? scopes : Enumerable.Empty<string>()
            };
        }

        /// <summary>
        /// Sets the required scopes for a specific operation.
        /// </summary>
        /// <param name="operation">The operation name.</param>
        /// <param name="scopes">The required scopes for the operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="operation"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scopes"/> is null.</exception>
        public void SetScopesForOperation(string operation, IEnumerable<string> scopes)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            ArgumentNullException.ThrowIfNull(scopes);

            var scopesList = scopes.ToList();

            switch (operation.ToLowerInvariant())
            {
                case "read":
                    ReadScopes = scopesList;
                    break;
                case "create":
                    CreateScopes = scopesList;
                    break;
                case "update":
                    UpdateScopes = scopesList;
                    break;
                case "delete":
                    DeleteScopes = scopesList;
                    break;
                case "query":
                    QueryScopes = scopesList;
                    break;
                case "navigate":
                    NavigateScopes = scopesList;
                    break;
                default:
                    CustomOperationScopes[operation] = scopesList;
                    break;
            }
        }

        /// <summary>
        /// Adds a custom operation with its required scopes.
        /// </summary>
        /// <param name="operationName">The name of the custom operation.</param>
        /// <param name="scopes">The required scopes for the operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="operationName"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scopes"/> is null.</exception>
        public void AddCustomOperation(string operationName, IEnumerable<string> scopes)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
            ArgumentNullException.ThrowIfNull(scopes);

            CustomOperationScopes[operationName] = scopes.ToList();
        }

        /// <summary>
        /// Determines whether any scopes are defined for this entity.
        /// </summary>
        /// <returns><c>true</c> if any scopes are defined; otherwise, <c>false</c>.</returns>
        public bool HasAnyScopes()
        {
            return ReadScopes.Count > 0 ||
                   CreateScopes.Count > 0 ||
                   UpdateScopes.Count > 0 ||
                   DeleteScopes.Count > 0 ||
                   QueryScopes.Count > 0 ||
                   NavigateScopes.Count > 0 ||
                   CustomOperationScopes.Count > 0;
        }

        /// <summary>
        /// Gets all unique scopes defined for this entity across all operations.
        /// </summary>
        /// <returns>A collection of all unique scopes defined for this entity.</returns>
        public IEnumerable<string> GetAllScopes()
        {
            var allScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            allScopes.UnionWith(ReadScopes);
            allScopes.UnionWith(CreateScopes);
            allScopes.UnionWith(UpdateScopes);
            allScopes.UnionWith(DeleteScopes);
            allScopes.UnionWith(QueryScopes);
            allScopes.UnionWith(NavigateScopes);

            foreach (var customScopes in CustomOperationScopes.Values)
            {
                allScopes.UnionWith(customScopes);
            }

            return allScopes;
        }

        /// <summary>
        /// Validates the entity scope requirements for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the requirements are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            // Validate that scope lists don't contain empty or whitespace-only entries
            var scopeLists = new Dictionary<string, List<string>>
            {
                ["ReadScopes"] = ReadScopes,
                ["CreateScopes"] = CreateScopes,
                ["UpdateScopes"] = UpdateScopes,
                ["DeleteScopes"] = DeleteScopes,
                ["QueryScopes"] = QueryScopes,
                ["NavigateScopes"] = NavigateScopes
            };

            foreach (var kvp in scopeLists)
            {
                var invalidScopes = kvp.Value.Where(s => string.IsNullOrWhiteSpace(s)).ToList();
                if (invalidScopes.Count > 0)
                {
                    errors.Add($"{kvp.Key} contains {invalidScopes.Count} empty or whitespace-only scope(s).");
                }
            }

            // Validate custom operations
            foreach (var kvp in CustomOperationScopes)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    errors.Add("Custom operation name cannot be null or whitespace.");
                }

                var invalidScopes = kvp.Value.Where(s => string.IsNullOrWhiteSpace(s)).ToList();
                if (invalidScopes.Count > 0)
                {
                    errors.Add($"Custom operation '{kvp.Key}' contains {invalidScopes.Count} empty or whitespace-only scope(s).");
                }
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the entity scope requirements.
        /// </summary>
        /// <returns>A summary of the scope requirements for this entity.</returns>
        public override string ToString()
        {
            if (!HasAnyScopes())
            {
                return "No scope requirements defined";
            }

            var scopeCount = GetAllScopes().Count();
            var operationCount = 6 + CustomOperationScopes.Count; // 6 standard operations + custom
            
            return $"{scopeCount} unique scopes across {operationCount} operations";
        }

        #endregion

    }

}
