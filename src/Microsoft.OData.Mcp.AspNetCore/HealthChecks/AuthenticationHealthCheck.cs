// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Authentication.Models;
using Microsoft.OData.Mcp.Authentication.Services;

namespace Microsoft.OData.Mcp.AspNetCore.HealthChecks
{

    /// <summary>
    /// Health check for the authentication system.
    /// </summary>
    /// <remarks>
    /// This health check verifies that the authentication components are functioning
    /// correctly, including token validation services, authority connectivity, and
    /// configuration validity.
    /// </remarks>
    public sealed class AuthenticationHealthCheck : IHealthCheck
    {

        #region Fields

        internal readonly McpAuthenticationOptions _authOptions;
        internal readonly ITokenValidationService? _tokenValidationService;
        internal readonly ILogger<AuthenticationHealthCheck> _logger;
        internal readonly IHttpClientFactory? _httpClientFactory;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationHealthCheck"/> class.
        /// </summary>
        /// <param name="authOptions">The authentication options.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="tokenValidationService">The token validation service (optional).</param>
        /// <param name="httpClientFactory">The HTTP client factory (optional).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="authOptions"/> or <paramref name="logger"/> is null.</exception>
        public AuthenticationHealthCheck(
            IOptions<McpAuthenticationOptions> authOptions,
            ILogger<AuthenticationHealthCheck> logger,
            ITokenValidationService? tokenValidationService = null,
            IHttpClientFactory? httpClientFactory = null)
        {
ArgumentNullException.ThrowIfNull(authOptions);
            ArgumentNullException.ThrowIfNull(logger);

            _authOptions = authOptions.Value;
            _tokenValidationService = tokenValidationService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks the health of the authentication system.
        /// </summary>
        /// <param name="context">The health check context.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous health check operation.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting authentication health check");

                var healthData = new Dictionary<string, object>();
                var issues = new List<string>();

                // Check if authentication is enabled
                if (!_authOptions.Enabled)
                {
                    healthData["authentication_enabled"] = false;
                    healthData["status"] = "Disabled";
                    return HealthCheckResult.Healthy("Authentication is disabled", healthData);
                }

                healthData["authentication_enabled"] = true;

                // Check configuration validity
                CheckConfigurationValidity(healthData, issues);

                // Check authority connectivity
                await CheckAuthorityConnectivityAsync(healthData, issues, cancellationToken);

                // Check token validation service
                CheckTokenValidationService(healthData, issues);

                // Check token delegation configuration
                CheckTokenDelegationConfiguration(healthData, issues);

                // Determine overall health status
                var status = issues.Count switch
                {
                    0 => HealthStatus.Healthy,
                    var count when count <= 2 => HealthStatus.Degraded,
                    _ => HealthStatus.Unhealthy
                };

                var description = status switch
                {
                    HealthStatus.Healthy => "Authentication system is operating normally",
                    HealthStatus.Degraded => $"Authentication system is degraded: {string.Join(", ", issues)}",
                    HealthStatus.Unhealthy => $"Authentication system is unhealthy: {string.Join(", ", issues)}",
                    _ => "Authentication system status unknown"
                };

                _logger.LogDebug("Authentication health check completed with status: {Status}", status);

                return new HealthCheckResult(status, description, data: healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication health check failed with exception");
                return HealthCheckResult.Unhealthy("Authentication health check failed", ex);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Checks the validity of the authentication configuration.
        /// </summary>
        /// <param name="healthData">Dictionary to store health check data.</param>
        /// <param name="issues">List to collect any issues found.</param>
        internal void CheckConfigurationValidity(Dictionary<string, object> healthData, List<string> issues)
        {
            try
            {
                var validationErrors = _authOptions.Validate().ToList();
                
                healthData["configuration_valid"] = validationErrors.Count == 0;
                healthData["validation_errors_count"] = validationErrors.Count;

                if (validationErrors.Count > 0)
                {
                    issues.Add($"Configuration validation failed ({validationErrors.Count} errors)");
                    healthData["validation_errors"] = validationErrors;
                }

                // Check specific configuration components
                healthData["jwt_bearer_configured"] = !string.IsNullOrWhiteSpace(_authOptions.JwtBearer.Authority);
                healthData["token_delegation_enabled"] = _authOptions.TokenDelegation.Enabled;
                healthData["scope_authorization_enabled"] = _authOptions.ScopeAuthorization.Enabled;
                healthData["https_required"] = _authOptions.RequireHttps;
            }
            catch (Exception ex)
            {
                issues.Add("Configuration check failed");
                healthData["configuration_check_error"] = ex.Message;
                _logger.LogWarning(ex, "Configuration validity check failed");
            }
        }

        /// <summary>
        /// Checks connectivity to the authentication authority.
        /// </summary>
        /// <param name="healthData">Dictionary to store health check data.</param>
        /// <param name="issues">List to collect any issues found.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task CheckAuthorityConnectivityAsync(Dictionary<string, object> healthData, List<string> issues, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_authOptions.JwtBearer.Authority))
            {
                healthData["authority_connectivity"] = "Not configured";
                return;
            }

            var startTime = DateTime.UtcNow;

            try
            {
                if (_httpClientFactory is not null)
                {
                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.Timeout = _authOptions.Timeout;

                    var discoveryUrl = $"{_authOptions.JwtBearer.Authority.TrimEnd('/')}/.well-known/openid_configuration";
                    
                    _logger.LogDebug("Checking authority connectivity: {DiscoveryUrl}", discoveryUrl);

                    var response = await httpClient.GetAsync(discoveryUrl, cancellationToken);
                    var responseTime = DateTime.UtcNow - startTime;

                    healthData["authority_url"] = _authOptions.JwtBearer.Authority;
                    healthData["discovery_url"] = discoveryUrl;
                    healthData["authority_response_time_ms"] = responseTime.TotalMilliseconds;
                    healthData["authority_status_code"] = (int)response.StatusCode;

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        healthData["authority_connectivity"] = "Success";
                        healthData["discovery_document_size"] = content.Length;

                        // Check if response time is concerning (example threshold: 5 seconds)
                        if (responseTime.TotalSeconds > 5)
                        {
                            issues.Add("Authority response time is slow");
                        }
                    }
                    else
                    {
                        issues.Add($"Authority not reachable (HTTP {response.StatusCode})");
                        healthData["authority_connectivity"] = "Failed";
                    }
                }
                else
                {
                    healthData["authority_connectivity"] = "HttpClientFactory not available";
                    issues.Add("Cannot check authority connectivity - HttpClientFactory not configured");
                }
            }
            catch (TaskCanceledException)
            {
                issues.Add("Authority connectivity check timed out");
                healthData["authority_connectivity"] = "Timeout";
                healthData["authority_response_time_ms"] = (DateTime.UtcNow - startTime).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                issues.Add("Authority connectivity check failed");
                healthData["authority_connectivity"] = "Error";
                healthData["authority_error"] = ex.Message;
                _logger.LogWarning(ex, "Authority connectivity check failed for URL: {AuthorityUrl}", _authOptions.JwtBearer.Authority);
            }
        }

        /// <summary>
        /// Checks the token validation service availability and functionality.
        /// </summary>
        /// <param name="healthData">Dictionary to store health check data.</param>
        /// <param name="issues">List to collect any issues found.</param>
        internal void CheckTokenValidationService(Dictionary<string, object> healthData, List<string> issues)
        {
            try
            {
                if (_tokenValidationService is null)
                {
                    issues.Add("Token validation service not available");
                    healthData["token_validation_service"] = "Not available";
                    return;
                }

                healthData["token_validation_service"] = "Available";
                healthData["token_validation_service_type"] = _tokenValidationService.GetType().Name;

                // Additional checks could be performed here, such as:
                // - Validating a test token
                // - Checking service configuration
                // - Verifying required dependencies
            }
            catch (Exception ex)
            {
                issues.Add("Token validation service check failed");
                healthData["token_validation_service"] = "Error";
                healthData["token_validation_error"] = ex.Message;
                _logger.LogWarning(ex, "Token validation service check failed");
            }
        }

        /// <summary>
        /// Checks the token delegation configuration and dependencies.
        /// </summary>
        /// <param name="healthData">Dictionary to store health check data.</param>
        /// <param name="issues">List to collect any issues found.</param>
        internal void CheckTokenDelegationConfiguration(Dictionary<string, object> healthData, List<string> issues)
        {
            try
            {
                var delegationOptions = _authOptions.TokenDelegation;
                
                healthData["token_delegation_enabled"] = delegationOptions.Enabled;

                if (delegationOptions.Enabled)
                {
                    healthData["delegation_strategy"] = delegationOptions.Strategy.ToString();
                    healthData["target_services_count"] = delegationOptions.TargetServices.Count;
                    healthData["token_caching_enabled"] = delegationOptions.CacheTokens;

                    // Validate delegation configuration
                    var delegationErrors = delegationOptions.Validate().ToList();
                    if (delegationErrors.Count > 0)
                    {
                        issues.Add($"Token delegation configuration issues ({delegationErrors.Count} errors)");
                        healthData["delegation_validation_errors"] = delegationErrors;
                    }

                    // Check if required services are configured for complex delegation strategies
                    if (delegationOptions.Strategy == TokenForwardingStrategy.Exchange || 
                        delegationOptions.Strategy == TokenForwardingStrategy.OnBehalfOf)
                    {
                        if (delegationOptions.TokenExchange.ClientCredentials is null)
                        {
                            issues.Add("Client credentials not configured for token exchange");
                        }

                        if (string.IsNullOrWhiteSpace(delegationOptions.TokenExchange.TokenEndpoint) &&
                            string.IsNullOrWhiteSpace(_authOptions.JwtBearer.Authority))
                        {
                            issues.Add("Token exchange endpoint not configured");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add("Token delegation configuration check failed");
                healthData["token_delegation_check_error"] = ex.Message;
                _logger.LogWarning(ex, "Token delegation configuration check failed");
            }
        }

        #endregion

    }

}
