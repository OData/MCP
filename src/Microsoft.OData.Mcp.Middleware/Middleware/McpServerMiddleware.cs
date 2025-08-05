using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Authentication.Extensions;
using Microsoft.OData.Mcp.Authentication.Services;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.OData.Mcp.Middleware.Configuration;
using Microsoft.OData.Mcp.Middleware.Services;

namespace Microsoft.OData.Mcp.Middleware.Middleware
{
    /// <summary>
    /// ASP.NET Core middleware for integrating MCP server functionality into existing applications.
    /// </summary>
    /// <remarks>
    /// This middleware enables existing ASP.NET Core applications to expose MCP (Model Context Protocol)
    /// endpoints that provide AI-friendly access to OData services. It can be embedded directly into
    /// the application pipeline for seamless integration.
    /// </remarks>
    public sealed class McpServerMiddleware
    {
        #region Fields

        private readonly RequestDelegate _next;
        private readonly McpMiddlewareOptions _options;
        private readonly IMetadataDiscoveryService _metadataService;
        private readonly IMcpToolFactory _toolFactory;
        private readonly ITokenValidationService? _tokenValidationService;
        private readonly ILogger<McpServerMiddleware> _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServerMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="options">The middleware configuration options.</param>
        /// <param name="metadataService">The metadata discovery service.</param>
        /// <param name="toolFactory">The MCP tool factory.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="tokenValidationService">The token validation service (optional).</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public McpServerMiddleware(
            RequestDelegate next,
            IOptions<McpMiddlewareOptions> options,
            IMetadataDiscoveryService metadataService,
            IMcpToolFactory toolFactory,
            ILogger<McpServerMiddleware> logger,
            ITokenValidationService? tokenValidationService = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(next);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(metadataService);
            ArgumentNullException.ThrowIfNull(toolFactory);
            ArgumentNullException.ThrowIfNull(logger);
#else
            if (next is null)
            {
                throw new ArgumentNullException(nameof(next));
            }
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (metadataService is null)
            {
                throw new ArgumentNullException(nameof(metadataService));
            }
            if (toolFactory is null)
            {
                throw new ArgumentNullException(nameof(toolFactory));
            }
            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
#endif

            _next = next;
            _options = options.Value;
            _metadataService = metadataService;
            _toolFactory = toolFactory;
            _tokenValidationService = tokenValidationService;
            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Invokes the middleware to process the HTTP request.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.Enabled)
            {
                await _next(context);
                return;
            }

            var path = context.Request.Path.Value ?? string.Empty;

            // Check if this request should be processed by MCP middleware
            if (!_options.ShouldProcessPath(path))
            {
                await _next(context);
                return;
            }

            _logger.LogDebug("Processing MCP request for path: {Path}", path);

            try
            {
                // Add custom headers
                foreach (var header in _options.CustomHeaders)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }

                // Handle CORS if enabled
                if (_options.EnableCors)
                {
                    HandleCors(context);
                }

                // Route the request to appropriate handler
                var handled = await RouteRequestAsync(context);

                if (!handled)
                {
                    _logger.LogDebug("Request not handled by MCP middleware, passing to next middleware");
                    await _next(context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MCP request for path: {Path}", path);
                await HandleErrorAsync(context, ex);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Routes the request to the appropriate MCP handler.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>True if the request was handled; otherwise, false.</returns>
        private async Task<bool> RouteRequestAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var method = context.Request.Method;

            // Remove base path to get relative path
            var relativePath = path.Substring(_options.BasePath.Length).TrimStart('/');

            return relativePath.ToLowerInvariant() switch
            {
                "" or "info" => await HandleServerInfoAsync(context),
                "tools" when method == "GET" => await HandleListToolsAsync(context),
                "tools/execute" when method == "POST" => await HandleExecuteToolAsync(context),
                "metadata" when method == "GET" => await HandleMetadataAsync(context),
                "health" when method == "GET" && _options.IncludeHealthChecks => await HandleHealthAsync(context),
                _ when relativePath.StartsWith("tools/") && method == "GET" => await HandleGetToolAsync(context, relativePath),
                _ => false
            };
        }

        /// <summary>
        /// Handles server information requests.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>True if the request was handled.</returns>
        private async Task<bool> HandleServerInfoAsync(HttpContext context)
        {
            var info = new
            {
                name = "OData MCP Server",
                version = "1.0.0",
                description = "Model Context Protocol server for OData services",
                capabilities = new
                {
                    tools = true,
                    authentication = _options.Authentication.Enabled,
                    metadata_discovery = _options.AutoDiscoverMetadata,
                    caching = _options.EnableCaching
                },
                endpoints = new
                {
                    tools = $"{_options.BasePath}/tools",
                    metadata = $"{_options.BasePath}/metadata",
                    health = _options.IncludeHealthChecks ? $"{_options.BasePath}/health" : null
                },
                statistics = _metadataService.GetStatistics()
            };

            await WriteJsonResponseAsync(context, info, 200);
            return true;
        }

        /// <summary>
        /// Handles tool listing requests.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>True if the request was handled.</returns>
        private async Task<bool> HandleListToolsAsync(HttpContext context)
        {
            try
            {
                // Authenticate user if authentication is enabled
                var userContext = await AuthenticateUserAsync(context);
                if (_options.Authentication.Enabled && userContext is null)
                {
                    await WriteErrorResponseAsync(context, "Authentication required", 401);
                    return true;
                }

                // Get current model
                var model = _metadataService.CurrentModel;
                if (model is null)
                {
                    await WriteErrorResponseAsync(context, "OData metadata not available", 503);
                    return true;
                }

                // Generate tools from metadata
                var allTools = await _toolFactory.GenerateToolsAsync(model, _options.ToolGeneration);

                // Filter tools based on user authorization
                var authorizedTools = userContext is not null
                    ? _toolFactory.FilterToolsForUser(allTools, userContext.GetUserScopes(), userContext.GetUserRoles())
                    : allTools;

                var toolList = authorizedTools.Select(tool => new
                {
                    name = tool.Name,
                    description = tool.Description,
                    category = tool.Category,
                    operationType = tool.OperationType.ToString(),
                    targetEntityType = tool.TargetEntityType,
                    targetEntitySet = tool.TargetEntitySet,
                    requiresAuth = tool.RequiredScopes.Count > 0 || tool.RequiredRoles.Count > 0,
                    supportsBatch = tool.SupportsBatch,
                    isDeprecated = tool.IsDeprecated,
                    version = tool.Version
                }).ToList();

                var response = new
                {
                    tools = toolList,
                    totalCount = toolList.Count,
                    metadata = new
                    {
                        entityTypes = model.EntityTypes.Count,
                        lastUpdated = _metadataService.LastUpdated?.ToString("O")
                    }
                };

                await WriteJsonResponseAsync(context, response, 200);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing tools");
                await WriteErrorResponseAsync(context, "Failed to list tools", 500);
                return true;
            }
        }

        /// <summary>
        /// Handles tool execution requests.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>True if the request was handled.</returns>
        private async Task<bool> HandleExecuteToolAsync(HttpContext context)
        {
            try
            {
                // Authenticate user if authentication is enabled
                var userContext = await AuthenticateUserAsync(context);
                if (_options.Authentication.Enabled && userContext is null)
                {
                    await WriteErrorResponseAsync(context, "Authentication required", 401);
                    return true;
                }

                // Parse request body
                var requestBody = await JsonSerializer.DeserializeAsync<ToolExecutionRequest>(
                    context.Request.Body,
                    cancellationToken: context.RequestAborted);

                if (requestBody is null || string.IsNullOrWhiteSpace(requestBody.ToolName))
                {
                    await WriteErrorResponseAsync(context, "Invalid request body", 400);
                    return true;
                }

                // Get the tool
                var tool = _toolFactory.GetTool(requestBody.ToolName);
                if (tool is null)
                {
                    await WriteErrorResponseAsync(context, $"Tool '{requestBody.ToolName}' not found", 404);
                    return true;
                }

                // Check authorization
                if (userContext is not null)
                {
                    var userScopes = userContext.GetUserScopes();
                    var userRoles = userContext.GetUserRoles();
                    
                    if (!tool.IsAuthorizedForUser(userScopes, userRoles))
                    {
                        await WriteErrorResponseAsync(context, "Insufficient permissions for this tool", 403);
                        return true;
                    }
                }

                // Create tool context
                var toolContext = new McpToolContext(_metadataService.CurrentModel!)
                {
                    Model = _metadataService.CurrentModel!,
                    User = userContext,
                    CorrelationId = context.TraceIdentifier,
                    ServiceBaseUrl = GetServiceBaseUrl(context),
                    HttpClientFactory = context.RequestServices.GetService<IHttpClientFactory>(),
                    AuthToken = ExtractAuthToken(context),
                    CancellationToken = context.RequestAborted,
                    MaxExecutionTime = _options.RequestTimeout
                };

                // Execute the tool
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                cts.CancelAfter(_options.RequestTimeout);

                var parameters = requestBody.Parameters ?? JsonDocument.Parse("{}");
                var result = await tool.Handler(toolContext, parameters);

                // Return result
                var response = new
                {
                    success = result.IsSuccess,
                    data = result.Data?.RootElement,
                    error = result.ErrorMessage,
                    errorCode = result.ErrorCode,
                    metadata = result.Metadata,
                    warnings = result.Warnings,
                    executionDuration = result.ExecutionDuration.TotalMilliseconds,
                    correlationId = result.CorrelationId
                };

                await WriteJsonResponseAsync(context, response, result.StatusCode);
                return true;
            }
            catch (OperationCanceledException)
            {
                await WriteErrorResponseAsync(context, "Request timeout", 408);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool");
                await WriteErrorResponseAsync(context, "Tool execution failed", 500);
                return true;
            }
        }

        /// <summary>
        /// Handles individual tool information requests.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="relativePath">The relative path containing the tool name.</param>
        /// <returns>True if the request was handled.</returns>
        private async Task<bool> HandleGetToolAsync(HttpContext context, string relativePath)
        {
            var toolName = relativePath.Substring("tools/".Length);
            
            var tool = _toolFactory.GetTool(toolName);
            if (tool is null)
            {
                await WriteErrorResponseAsync(context, $"Tool '{toolName}' not found", 404);
                return true;
            }

            var toolInfo = new
            {
                name = tool.Name,
                description = tool.Description,
                category = tool.Category,
                operationType = tool.OperationType.ToString(),
                targetEntityType = tool.TargetEntityType,
                targetEntitySet = tool.TargetEntitySet,
                inputSchema = tool.InputSchema.RootElement,
                outputSchema = tool.OutputSchema?.RootElement,
                requiredScopes = tool.RequiredScopes,
                requiredRoles = tool.RequiredRoles,
                supportsBatch = tool.SupportsBatch,
                isDeprecated = tool.IsDeprecated,
                deprecationMessage = tool.DeprecationMessage,
                version = tool.Version,
                createdAt = tool.CreatedAt,
                examples = tool.Examples.Select(ex => new
                {
                    title = ex.Title,
                    description = ex.Description,
                    category = ex.Category,
                    difficulty = ex.Difficulty.ToString(),
                    input = ex.Input.RootElement,
                    expectedOutput = ex.ExpectedOutput?.RootElement,
                    tags = ex.Tags,
                    prerequisites = ex.Prerequisites,
                    requiresAuthentication = ex.RequiresAuthentication
                }),
                metadata = tool.Metadata
            };

            await WriteJsonResponseAsync(context, toolInfo, 200);
            return true;
        }

        /// <summary>
        /// Handles metadata requests.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>True if the request was handled.</returns>
        private async Task<bool> HandleMetadataAsync(HttpContext context)
        {
            var model = _metadataService.CurrentModel;
            if (model is null)
            {
                await WriteErrorResponseAsync(context, "OData metadata not available", 503);
                return true;
            }

            var metadata = new
            {
                entityTypes = model.EntityTypes.Select(et => new
                {
                    name = et.Name,
                    @namespace = et.Namespace,
                    fullName = et.FullName,
                    baseType = et.BaseType,
                    isAbstract = et.IsAbstract,
                    key = et.Key,
                    properties = et.Properties.Select(p => new
                    {
                        name = p.Name,
                        type = p.Type,
                        isNullable = p.IsNullable,
                        maxLength = p.MaxLength,
                        precision = p.Precision,
                        scale = p.Scale
                    }),
                    navigationProperties = et.NavigationProperties.Select(np => new
                    {
                        name = np.Name,
                        type = np.Type,
                        isCollection = np.IsCollection,
                        partner = np.Partner,
                        containsTarget = np.ContainsTarget
                    })
                }),
                complexTypes = model.ComplexTypes.Select(ct => new
                {
                    name = ct.Name,
                    @namespace = ct.Namespace,
                    fullName = ct.FullName,
                    baseType = ct.BaseType,
                    isAbstract = ct.IsAbstract,
                    properties = ct.Properties.Select(p => new
                    {
                        name = p.Name,
                        type = p.Type,
                        isNullable = p.IsNullable
                    })
                }),
                entityContainer = model.EntityContainer is not null ? new
                {
                    name = model.EntityContainer.Name,
                    @namespace = model.EntityContainer.Namespace,
                    entitySets = model.EntityContainer.EntitySets.Select(es => new
                    {
                        name = es.Name,
                        entityType = es.EntityType,
                        navigationPropertyBindings = es.NavigationPropertyBindings
                    })
                } : null,
                statistics = _metadataService.GetStatistics()
            };

            await WriteJsonResponseAsync(context, metadata, 200);
            return true;
        }

        /// <summary>
        /// Handles health check requests.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>True if the request was handled.</returns>
        private async Task<bool> HandleHealthAsync(HttpContext context)
        {
            var health = new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                middleware = new
                {
                    enabled = _options.Enabled,
                    basePath = _options.BasePath,
                    integrationMode = _options.IntegrationMode.ToString()
                },
                metadata = _metadataService.GetStatistics(),
                tools = new
                {
                    availableCount = _toolFactory.GetAvailableToolNames().Count()
                }
            };

            await WriteJsonResponseAsync(context, health, 200);
            return true;
        }

        /// <summary>
        /// Authenticates the user from the request context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The authenticated user principal, or null if authentication failed.</returns>
        private async Task<System.Security.Claims.ClaimsPrincipal?> AuthenticateUserAsync(HttpContext context)
        {
            if (!_options.Authentication.Enabled || _tokenValidationService is null)
            {
                return null;
            }

            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return null;
            }

            var token = authHeader.Substring("Bearer ".Length);
            return await _tokenValidationService.ValidateTokenAsync(token, context.RequestAborted);
        }

        /// <summary>
        /// Extracts the authentication token from the request.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The authentication token, or null if not present.</returns>
        private static string? ExtractAuthToken(HttpContext context)
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return null;
            }

            return authHeader.Substring("Bearer ".Length);
        }

        /// <summary>
        /// Gets the service base URL from the request context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The service base URL.</returns>
        private string GetServiceBaseUrl(HttpContext context)
        {
            if (!string.IsNullOrWhiteSpace(_options.ServiceRootUrl))
            {
                return _options.ServiceRootUrl;
            }

            var request = context.Request;
            return $"{request.Scheme}://{request.Host}{request.PathBase}";
        }

        /// <summary>
        /// Handles CORS for the request.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        private void HandleCors(HttpContext context)
        {
            if (!string.IsNullOrWhiteSpace(_options.CorsPolicyName))
            {
                // Use configured CORS policy - this would typically be handled by CORS middleware
                return;
            }

            // Add basic CORS headers
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";

            // Handle preflight requests
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = 200;
            }
        }

        /// <summary>
        /// Writes a JSON response to the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="data">The data to serialize.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        private static async Task WriteJsonResponseAsync(HttpContext context, object data, int statusCode = 200)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(json);
        }

        /// <summary>
        /// Writes an error response to the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="message">The error message.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        private async Task WriteErrorResponseAsync(HttpContext context, string message, int statusCode = 500)
        {
            var error = new
            {
                error = message,
                statusCode,
                timestamp = DateTime.UtcNow,
                path = context.Request.Path.Value,
                correlationId = context.TraceIdentifier
            };

            if (_options.IncludeDetailedErrors)
            {
                // Could include additional debug information here
            }

            await WriteJsonResponseAsync(context, error, statusCode);
        }

        /// <summary>
        /// Handles unhandled errors in the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="exception">The exception that occurred.</param>
        private async Task HandleErrorAsync(HttpContext context, Exception exception)
        {
            var statusCode = exception switch
            {
                ArgumentException => 400,
                UnauthorizedAccessException => 401,
                InvalidOperationException => 409,
                NotSupportedException => 501,
                TimeoutException => 408,
                _ => 500
            };

            var message = _options.IncludeDetailedErrors ? exception.Message : "An error occurred";

            await WriteErrorResponseAsync(context, message, statusCode);
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Represents a tool execution request.
        /// </summary>
        private sealed class ToolExecutionRequest
        {
            /// <summary>
            /// Gets or sets the name of the tool to execute.
            /// </summary>
            public string ToolName { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the parameters for the tool execution.
            /// </summary>
            public JsonDocument? Parameters { get; set; }
        }

        #endregion
    }
}
