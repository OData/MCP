using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Mcp.Authentication.Models;

namespace Microsoft.OData.Mcp.Authentication.Services
{

    /// <summary>
    /// Provides services for validating JWT tokens and extracting user context.
    /// </summary>
    /// <remarks>
    /// This service handles JWT token validation using Microsoft's IdentityModel libraries,
    /// including automatic discovery of validation keys and comprehensive claim extraction.
    /// </remarks>
    public sealed class TokenValidationService : ITokenValidationService
    {

        #region Fields

        internal readonly McpAuthenticationOptions _options;
        internal readonly ILogger<TokenValidationService> _logger;
        internal readonly JwtSecurityTokenHandler _tokenHandler;
        internal readonly IConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;
        internal TokenValidationParameters? _validationParameters;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenValidationService"/> class.
        /// </summary>
        /// <param name="options">The authentication options.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="logger"/> is null.</exception>
        public TokenValidationService(IOptions<McpAuthenticationOptions> options, ILogger<TokenValidationService> logger)
        {
ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(logger);

            _options = options.Value;
            _logger = logger;
            _tokenHandler = new JwtSecurityTokenHandler();

            if (!string.IsNullOrWhiteSpace(_options.JwtBearer.Authority))
            {
                var authority = _options.JwtBearer.Authority.TrimEnd('/');
                var configurationUrl = $"{authority}/.well-known/openid_configuration";
                
                _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    configurationUrl,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever())
                {
                    RefreshInterval = _options.MetadataCacheDuration
                };
            }

            InitializeValidationParameters();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates a JWT token and returns the principal if valid.
        /// </summary>
        /// <param name="token">The JWT token to validate.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous validation operation. The task result contains the claims principal if the token is valid, or null if invalid.</returns>
        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(token);

            try
            {
                var result = await ValidateTokenAsync(token, [], cancellationToken);
                return result.IsValid ? result.Principal : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed for security token");
                return null;
            }
        }

        /// <summary>
        /// Validates a JWT token with additional validation parameters.
        /// </summary>
        /// <param name="token">The JWT token to validate.</param>
        /// <param name="validationParameters">Additional validation parameters to apply.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous validation operation. The task result contains the validation result.</returns>
        public async Task<Models.TokenValidationResult> ValidateTokenAsync(string token, Dictionary<string, object> validationParameters, CancellationToken cancellationToken = default)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(token);
            ArgumentNullException.ThrowIfNull(validationParameters);

            try
            {
                _logger.LogDebug("Validating JWT token");

                // Get current validation parameters
                var currentValidationParameters = await GetValidationParametersAsync(cancellationToken);
                if (currentValidationParameters is null)
                {
                    return Models.TokenValidationResult.Failure("ValidationParametersNotAvailable", "Token validation parameters could not be obtained");
                }

                // Apply additional validation parameters
                var effectiveParameters = ApplyAdditionalValidationParameters(currentValidationParameters, validationParameters);

                // Validate the token
                var validationResult = await _tokenHandler.ValidateTokenAsync(token, effectiveParameters);
                
                if (validationResult.IsValid && validationResult.ClaimsIdentity is not null)
                {
                    var principal = new ClaimsPrincipal(validationResult.ClaimsIdentity);
                    
                    // Check required scopes if configured
                    if (_options.JwtBearer.RequiredScopes.Count > 0)
                    {
                        var userContext = ExtractUserContext(principal);
                        if (!HasRequiredScopes(userContext, _options.JwtBearer.RequiredScopes))
                        {
                            _logger.LogWarning("Token validation failed: missing required scopes. Required: {RequiredScopes}, Present: {PresentScopes}", 
                                string.Join(", ", _options.JwtBearer.RequiredScopes),
                                string.Join(", ", userContext.Scopes));
                            
                            return Models.TokenValidationResult.Failure("InsufficientScope", "Token does not contain required scopes");
                        }
                    }

                    var result = Models.TokenValidationResult.Success(principal);
                    result.AddMetadata("ValidatedAt", DateTime.UtcNow);
                    result.AddMetadata("TokenLength", token.Length);
                    
                    _logger.LogDebug("Token validation successful for subject: {Subject}", principal.FindFirst("sub")?.Value ?? "Unknown");
                    return result;
                }
                else
                {
                    var error = validationResult.Exception?.Message ?? "Token validation failed";
                    _logger.LogWarning("Token validation failed: {Error}", error);
                    return Models.TokenValidationResult.Failure("InvalidToken", error, validationResult.Exception);
                }
            }
            catch (SecurityTokenExpiredException ex)
            {
                _logger.LogWarning(ex, "Token validation failed: token expired");
                return Models.TokenValidationResult.Failure("TokenExpired", "The token has expired", ex);
            }
            catch (SecurityTokenNotYetValidException ex)
            {
                _logger.LogWarning(ex, "Token validation failed: token not yet valid");
                return Models.TokenValidationResult.Failure("TokenNotYetValid", "The token is not yet valid", ex);
            }
            catch (SecurityTokenInvalidAudienceException ex)
            {
                _logger.LogWarning(ex, "Token validation failed: invalid audience");
                return Models.TokenValidationResult.Failure("InvalidAudience", "The token audience is invalid", ex);
            }
            catch (SecurityTokenInvalidIssuerException ex)
            {
                _logger.LogWarning(ex, "Token validation failed: invalid issuer");
                return Models.TokenValidationResult.Failure("InvalidIssuer", "The token issuer is invalid", ex);
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                _logger.LogWarning(ex, "Token validation failed: invalid signature");
                return Models.TokenValidationResult.Failure("InvalidSignature", "The token signature is invalid", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token validation");
                return Models.TokenValidationResult.Failure("ValidationError", "An unexpected error occurred during token validation", ex);
            }
        }

        /// <summary>
        /// Extracts the user context from a validated claims principal.
        /// </summary>
        /// <param name="principal">The claims principal from a validated token.</param>
        /// <returns>The user context containing identity and authorization information.</returns>
        public UserContext ExtractUserContext(ClaimsPrincipal principal)
        {
ArgumentNullException.ThrowIfNull(principal);

            try
            {
                return UserContext.FromClaimsPrincipal(principal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract user context from claims principal");
                throw;
            }
        }

        /// <summary>
        /// Checks if a user has the required scopes for a specific operation.
        /// </summary>
        /// <param name="userContext">The user context to check.</param>
        /// <param name="requiredScopes">The scopes required for the operation.</param>
        /// <returns><c>true</c> if the user has at least one of the required scopes; otherwise, <c>false</c>.</returns>
        public bool HasRequiredScopes(UserContext userContext, IEnumerable<string> requiredScopes)
        {
ArgumentNullException.ThrowIfNull(userContext);
            ArgumentNullException.ThrowIfNull(requiredScopes);

            return userContext.HasAnyScope(requiredScopes);
        }

        /// <summary>
        /// Gets the authorization metadata from the JWT token for downstream services.
        /// </summary>
        /// <param name="token">The JWT token to extract metadata from.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the authorization metadata.</returns>
        public async Task<AuthorizationMetadata> GetAuthorizationMetadataAsync(string token)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(token);

            try
            {
                var principal = await ValidateTokenAsync(token);
                if (principal is null)
                {
                    throw new InvalidOperationException("Cannot extract authorization metadata from invalid token");
                }

                var userContext = ExtractUserContext(principal);
                return AuthorizationMetadata.FromUserContext(userContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract authorization metadata from token");
                throw;
            }
        }

        /// <summary>
        /// Determines if a token is expired based on its claims.
        /// </summary>
        /// <param name="principal">The claims principal from a validated token.</param>
        /// <returns><c>true</c> if the token is expired; otherwise, <c>false</c>.</returns>
        public bool IsTokenExpired(ClaimsPrincipal principal)
        {
ArgumentNullException.ThrowIfNull(principal);

            var expClaim = principal.FindFirst("exp");
            if (expClaim is null || !long.TryParse(expClaim.Value, out var exp))
            {
                return false; // No expiration claim
            }

            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            return DateTime.UtcNow >= expirationTime;
        }

        /// <summary>
        /// Gets the remaining lifetime of a token.
        /// </summary>
        /// <param name="principal">The claims principal from a validated token.</param>
        /// <returns>The remaining time before the token expires, or null if the token has no expiration.</returns>
        public TimeSpan? GetTokenLifetime(ClaimsPrincipal principal)
        {
ArgumentNullException.ThrowIfNull(principal);

            var expClaim = principal.FindFirst("exp");
            if (expClaim is null || !long.TryParse(expClaim.Value, out var exp))
            {
                return null; // No expiration claim
            }

            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            var remaining = expirationTime - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Initializes the token validation parameters.
        /// </summary>
        internal void InitializeValidationParameters()
        {
            _validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = _options.JwtBearer.ValidateIssuer,
                ValidateAudience = _options.JwtBearer.ValidateAudience,
                ValidateLifetime = _options.JwtBearer.ValidateLifetime,
                ValidateIssuerSigningKey = _options.JwtBearer.ValidateIssuerSigningKey,
                ClockSkew = _options.JwtBearer.ClockSkew,
                RequireExpirationTime = _options.JwtBearer.ValidateLifetime,
                RequireSignedTokens = _options.JwtBearer.ValidateIssuerSigningKey
            };

            // Set valid issuer if specified
            if (!string.IsNullOrWhiteSpace(_options.JwtBearer.Issuer))
            {
                _validationParameters.ValidIssuer = _options.JwtBearer.Issuer;
            }

            // Set valid audience if specified
            if (!string.IsNullOrWhiteSpace(_options.JwtBearer.Audience))
            {
                _validationParameters.ValidAudience = _options.JwtBearer.Audience;
            }

            // Apply additional validation parameters
            foreach (var kvp in _options.JwtBearer.AdditionalValidationParameters)
            {
                try
                {
                    var property = typeof(TokenValidationParameters).GetProperty(kvp.Key);
                    if (property is not null && property.CanWrite)
                    {
                        property.SetValue(_validationParameters, kvp.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to apply additional validation parameter: {Parameter}", kvp.Key);
                }
            }
        }

        /// <summary>
        /// Gets the current token validation parameters, including dynamically discovered keys.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The token validation parameters.</returns>
        internal async Task<TokenValidationParameters?> GetValidationParametersAsync(CancellationToken cancellationToken)
        {
            if (_validationParameters is null)
            {
                return null;
            }

            var parameters = _validationParameters.Clone();

            // If we have a configuration manager, get the current signing keys
            if (_configurationManager is not null)
            {
                try
                {
                    var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
                    parameters.IssuerSigningKeys = configuration.SigningKeys;

                    // Set issuer from discovered configuration if not explicitly set
                    if (string.IsNullOrWhiteSpace(parameters.ValidIssuer) && !string.IsNullOrWhiteSpace(configuration.Issuer))
                    {
                        parameters.ValidIssuer = configuration.Issuer;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to retrieve OpenID Connect configuration");
                    return null;
                }
            }
            else if (!string.IsNullOrWhiteSpace(_options.JwtBearer.MetadataAddress))
            {
                // Handle custom metadata address
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = _options.Timeout;
                    
                    var response = await httpClient.GetStringAsync(_options.JwtBearer.MetadataAddress, cancellationToken);
                    var jwks = new JsonWebKeySet(response);
                    parameters.IssuerSigningKeys = jwks.Keys;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to retrieve JWKS from metadata address: {MetadataAddress}", _options.JwtBearer.MetadataAddress);
                    return null;
                }
            }

            return parameters;
        }

        /// <summary>
        /// Applies additional validation parameters to the base parameters.
        /// </summary>
        /// <param name="baseParameters">The base validation parameters.</param>
        /// <param name="additionalParameters">Additional parameters to apply.</param>
        /// <returns>A new set of validation parameters with the additional parameters applied.</returns>
        internal static TokenValidationParameters ApplyAdditionalValidationParameters(
            TokenValidationParameters baseParameters, 
            Dictionary<string, object> additionalParameters)
        {
            var parameters = baseParameters.Clone();

            foreach (var kvp in additionalParameters)
            {
                try
                {
                    var property = typeof(TokenValidationParameters).GetProperty(kvp.Key);
                    if (property is not null && property.CanWrite)
                    {
                        property.SetValue(parameters, kvp.Value);
                    }
                }
                catch
                {
                    // Ignore errors when applying additional parameters
                }
            }

            return parameters;
        }

        #endregion
    }

}
