using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Http;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Tools
{
    /// <summary>
    /// Provides execution context for MCP tool operations.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the runtime context needed for tool execution,
    /// including user identity, request metadata, and service dependencies.
    /// </remarks>
    public sealed class McpToolContext
    {
        #region Properties

        /// <summary>
        /// Gets or sets the user identity making the request.
        /// </summary>
        /// <value>The claims principal representing the authenticated user, or null for anonymous requests.</value>
        /// <remarks>
        /// This contains all claims and identity information from the JWT token,
        /// including user ID, scopes, roles, and other custom claims.
        /// </remarks>
        public ClaimsPrincipal? User { get; set; }

        /// <summary>
        /// Gets or sets the correlation ID for this request.
        /// </summary>
        /// <value>A unique identifier for tracking this request across systems.</value>
        /// <remarks>
        /// The correlation ID is used for logging, tracing, and debugging purposes.
        /// It should be propagated to downstream services for end-to-end tracking.
        /// </remarks>
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the OData model for this execution context.
        /// </summary>
        /// <value>The complete OData model providing metadata context.</value>
        /// <remarks>
        /// The model contains all entity types, properties, relationships, and
        /// other metadata needed for tool execution and validation.
        /// </remarks>
        public required EdmModel Model { get; set; }

        /// <summary>
        /// Gets or sets the base URL of the OData service.
        /// </summary>
        /// <value>The base URL used for constructing OData requests.</value>
        /// <remarks>
        /// This URL is used when the tool needs to make HTTP requests to the
        /// underlying OData service for CRUD operations and queries.
        /// </remarks>
        public string? ServiceBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the HTTP client factory for making service requests.
        /// </summary>
        /// <value>Factory for creating HTTP clients with proper configuration.</value>
        /// <remarks>
        /// Used to create HTTP clients for communicating with the OData service
        /// when authentication token delegation is required.
        /// </remarks>
        public IHttpClientFactory? HttpClientFactory { get; set; }

        /// <summary>
        /// Gets or sets the authentication token to forward to the OData service.
        /// </summary>
        /// <value>The bearer token to include in OData service requests.</value>
        /// <remarks>
        /// This token is forwarded to the OData service when the tool performs
        /// operations that require authentication. The token delegation strategy
        /// determines how this token is processed.
        /// </remarks>
        public string? AuthToken { get; set; }

        /// <summary>
        /// Gets or sets custom properties for tool execution.
        /// </summary>
        /// <value>A dictionary of custom key-value pairs for tool-specific data.</value>
        /// <remarks>
        /// This allows tools to store and retrieve custom context information
        /// that may be needed during execution.
        /// </remarks>
        public Dictionary<string, object> Properties { get; set; } = new();

        /// <summary>
        /// Gets or sets the request timestamp.
        /// </summary>
        /// <value>The UTC timestamp when the tool request was initiated.</value>
        /// <remarks>
        /// Used for auditing, performance tracking, and timeout calculations.
        /// </remarks>
        public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the cancellation token for this request.
        /// </summary>
        /// <value>Token for cancelling long-running operations.</value>
        /// <remarks>
        /// Tools should respect this token and cancel operations appropriately
        /// when cancellation is requested.
        /// </remarks>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed execution time for the tool.
        /// </summary>
        /// <value>The maximum time the tool is allowed to execute.</value>
        /// <remarks>
        /// This provides a safety mechanism to prevent tools from running indefinitely.
        /// Tools should monitor their execution time and stop gracefully when approaching this limit.
        /// </remarks>
        public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolContext"/> class.
        /// </summary>
        public McpToolContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolContext"/> class with the specified model.
        /// </summary>
        /// <param name="model">The OData model for this context.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public McpToolContext(EdmModel model)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(model);
#else
            if (model is null)
            {
                throw new ArgumentNullException(nameof(model));
            }
#endif

            Model = model;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the user's OAuth2 scopes from the claims.
        /// </summary>
        /// <returns>A collection of OAuth2 scopes, or empty if no scopes are available.</returns>
        public IEnumerable<string> GetUserScopes()
        {
            if (User is null)
            {
                return Enumerable.Empty<string>();
            }

            var scopesClaim = User.FindFirst("scope")?.Value;
            if (string.IsNullOrWhiteSpace(scopesClaim))
            {
                return Enumerable.Empty<string>();
            }

            return scopesClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Gets the user's roles from the claims.
        /// </summary>
        /// <returns>A collection of user roles, or empty if no roles are available.</returns>
        public IEnumerable<string> GetUserRoles()
        {
            if (User is null)
            {
                return Enumerable.Empty<string>();
            }

            return User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        }

        /// <summary>
        /// Gets the user identifier from the claims.
        /// </summary>
        /// <returns>The user identifier, or null if not available.</returns>
        public string? GetUserId()
        {
            return User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? User?.FindFirst("sub")?.Value;
        }

        /// <summary>
        /// Gets the user's email from the claims.
        /// </summary>
        /// <returns>The user's email address, or null if not available.</returns>
        public string? GetUserEmail()
        {
            return User?.FindFirst(ClaimTypes.Email)?.Value 
                   ?? User?.FindFirst("email")?.Value;
        }

        /// <summary>
        /// Gets the user's display name from the claims.
        /// </summary>
        /// <returns>The user's display name, or null if not available.</returns>
        public string? GetUserName()
        {
            return User?.FindFirst(ClaimTypes.Name)?.Value 
                   ?? User?.FindFirst("name")?.Value
                   ?? User?.Identity?.Name;
        }

        /// <summary>
        /// Determines whether the user has the specified scope.
        /// </summary>
        /// <param name="scope">The scope to check for.</param>
        /// <returns><c>true</c> if the user has the specified scope; otherwise, <c>false</c>.</returns>
        public bool HasScope(string scope)
        {
            var userScopes = GetUserScopes();
            return userScopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the user has any of the specified scopes.
        /// </summary>
        /// <param name="scopes">The scopes to check for.</param>
        /// <returns><c>true</c> if the user has any of the specified scopes; otherwise, <c>false</c>.</returns>
        public bool HasAnyScope(IEnumerable<string> scopes)
        {
            var userScopes = GetUserScopes().ToList();
            return scopes.Any(scope => userScopes.Contains(scope, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether the user has the specified role.
        /// </summary>
        /// <param name="role">The role to check for.</param>
        /// <returns><c>true</c> if the user has the specified role; otherwise, <c>false</c>.</returns>
        public bool HasRole(string role)
        {
            var userRoles = GetUserRoles();
            return userRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a custom property value.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="key">The property key.</param>
        /// <returns>The property value if found and of the correct type; otherwise, the default value.</returns>
        public T? GetProperty<T>(string key)
        {
            if (Properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Sets a custom property value.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
        public void SetProperty(string key, object value)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
#else
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Property key cannot be null or whitespace.", nameof(key));
            }
#endif

            Properties[key] = value;
        }

        /// <summary>
        /// Creates an HTTP client for making requests to the OData service.
        /// </summary>
        /// <returns>An HTTP client configured for OData service requests, or null if no factory is available.</returns>
        /// <remarks>
        /// The returned client will include the authentication token if available.
        /// Callers are responsible for disposing the client.
        /// </remarks>
        public HttpClient? CreateServiceHttpClient()
        {
            if (HttpClientFactory is null)
            {
                return null;
            }

            var client = HttpClientFactory.CreateClient("ODataService");

            if (!string.IsNullOrWhiteSpace(AuthToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
            }

            client.DefaultRequestHeaders.Add("X-Correlation-ID", CorrelationId);

            return client;
        }

        /// <summary>
        /// Checks if the execution time limit has been exceeded.
        /// </summary>
        /// <returns><c>true</c> if the execution time limit has been exceeded; otherwise, <c>false</c>.</returns>
        public bool IsExecutionTimeLimitExceeded()
        {
            var elapsed = DateTime.UtcNow - RequestTimestamp;
            return elapsed > MaxExecutionTime;
        }

        /// <summary>
        /// Gets the remaining execution time.
        /// </summary>
        /// <returns>The remaining time before the execution limit is reached.</returns>
        public TimeSpan GetRemainingExecutionTime()
        {
            var elapsed = DateTime.UtcNow - RequestTimestamp;
            var remaining = MaxExecutionTime - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        #endregion
    }
}
