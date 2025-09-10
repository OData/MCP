# Troubleshooting Guide

This guide helps you diagnose and resolve common issues with the Microsoft OData MCP Server.

## Diagnostic Tools

### Enable Detailed Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.OData.Mcp": "Debug",
      "Microsoft.AspNetCore.Authentication": "Debug",
      "System.Net.Http": "Debug"
    }
  }
}
```

### Health Check Endpoint

```csharp
// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<ODataServiceHealthCheck>("odata_service")
    .AddCheck<McpServerHealthCheck>("mcp_server")
    .AddCheck<AuthenticationHealthCheck>("authentication");

// Map health endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### Debug Endpoints (Development Only)

```csharp
if (app.Environment.IsDevelopment())
{
    // Show current configuration
    app.MapGet("/debug/config", (IOptions<ODataMcpOptions> options) =>
        Results.Json(options.Value));
    
    // Show available tools
    app.MapGet("/debug/tools", async (IMcpToolFactory factory, IEdmModel model) =>
        Results.Json(await factory.GenerateToolsAsync(model)));
    
    // Test OData connection
    app.MapGet("/debug/test-odata", async (HttpClient client, IOptions<ODataMcpOptions> options) =>
    {
        var response = await client.GetAsync($"{options.Value.ServiceUrl}/$metadata");
        return Results.Ok(new
        {
            StatusCode = response.StatusCode,
            Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value),
            ContentLength = response.Content.Headers.ContentLength
        });
    });
}
```

## Common Issues and Solutions

### 1. MCP Server Not Starting

**Symptoms:**
- Application crashes on startup
- No MCP endpoints available
- Error: "Unable to start MCP server"

**Diagnosis:**
```bash
# Check application logs
dotnet run --verbosity detailed

# Check event viewer (Windows)
eventvwr.msc

# Check system logs (Linux)
journalctl -u your-service-name -f
```

**Solutions:**

1. **Check configuration:**
```csharp
// Validate configuration on startup
public class ConfigurationValidator : IHostedService
{
    private readonly IOptions<ODataMcpOptions> _options;
    private readonly ILogger<ConfigurationValidator> _logger;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        
        if (string.IsNullOrEmpty(options.ServiceUrl) && !options.UseLocalMetadata)
        {
            _logger.LogError("ServiceUrl is required when UseLocalMetadata is false");
            throw new InvalidOperationException("Invalid configuration");
        }
        
        if (!Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out _))
        {
            _logger.LogError("ServiceUrl '{Url}' is not a valid URL", options.ServiceUrl);
            throw new InvalidOperationException("Invalid ServiceUrl");
        }
        
        return Task.CompletedTask;
    }
}
```

2. **Check port conflicts:**
```bash
# Windows
netstat -ano | findstr :5000

# Linux/macOS
lsof -i :5000
```

3. **Verify dependencies:**
```xml
<!-- Ensure all required packages are installed -->
<PackageReference Include="Microsoft.OData.Mcp.Core" Version="1.0.0" />
<PackageReference Include="Microsoft.OData.Mcp.AspNetCore" Version="1.0.0" />
```

### 2. Authentication Failures

**Symptoms:**
- 401 Unauthorized errors
- "Token validation failed"
- "No authentication handler configured"

**Diagnosis:**
```csharp
// Add authentication debugging
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, 
                "Authentication failed: {Error}", 
                context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Token validated for user: {User}", 
                context.Principal?.Identity?.Name);
            return Task.CompletedTask;
        }
    };
});
```

**Solutions:**

1. **Verify token format:**
```csharp
// Test endpoint to decode token
app.MapGet("/debug/decode-token", (HttpContext context) =>
{
    var token = context.Request.Headers["Authorization"]
        .FirstOrDefault()?.Split(" ").Last();
    
    if (string.IsNullOrEmpty(token))
        return Results.BadRequest("No token provided");
    
    var handler = new JwtSecurityTokenHandler();
    var jsonToken = handler.ReadJwtToken(token);
    
    return Results.Json(new
    {
        Header = jsonToken.Header,
        Claims = jsonToken.Claims.Select(c => new { c.Type, c.Value }),
        ValidFrom = jsonToken.ValidFrom,
        ValidTo = jsonToken.ValidTo
    });
}).RequireHost("localhost");
```

2. **Check clock skew:**
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    // Allow 5 minutes clock skew
    ClockSkew = TimeSpan.FromMinutes(5),
    ValidateLifetime = true
};
```

3. **Verify authority and audience:**
```csharp
// Log token validation parameters
logger.LogInformation("Authority: {Authority}", options.Authority);
logger.LogInformation("Audience: {Audience}", options.Audience);
logger.LogInformation("Valid Issuers: {Issuers}", 
    string.Join(", ", options.TokenValidationParameters.ValidIssuers ?? new[] { "none" }));
```

### 3. OData Service Connection Issues

**Symptoms:**
- "Unable to connect to OData service"
- "Metadata parsing failed"
- Timeout errors

**Diagnosis:**
```csharp
// Add connection diagnostics
public class ODataConnectionDiagnostics
{
    public async Task<DiagnosticResult> DiagnoseConnectionAsync(string serviceUrl)
    {
        var result = new DiagnosticResult();
        
        // Test basic connectivity
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync(serviceUrl);
            result.CanConnect = response.IsSuccessStatusCode;
            result.StatusCode = response.StatusCode;
        }
        catch (Exception ex)
        {
            result.ConnectionError = ex.Message;
        }
        
        // Test metadata endpoint
        try
        {
            using var client = new HttpClient();
            var metadataUrl = $"{serviceUrl.TrimEnd('/')}/$metadata";
            var response = await client.GetAsync(metadataUrl);
            result.MetadataAvailable = response.IsSuccessStatusCode;
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                result.MetadataSize = content.Length;
                result.IsValidXml = IsValidXml(content);
            }
        }
        catch (Exception ex)
        {
            result.MetadataError = ex.Message;
        }
        
        return result;
    }
}
```

**Solutions:**

1. **Configure HTTP client:**
```csharp
builder.Services.AddHttpClient("ODataService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/xml");
    client.DefaultRequestHeaders.Add("User-Agent", "ODataMcpServer/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
    {
        // Log certificate errors in development
        if (builder.Environment.IsDevelopment() && errors != SslPolicyErrors.None)
        {
            logger.LogWarning("SSL Certificate Error: {Errors}", errors);
            return true; // Allow in development
        }
        return errors == SslPolicyErrors.None;
    }
});
```

2. **Handle proxy servers:**
```csharp
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    Proxy = new WebProxy
    {
        Address = new Uri("http://proxy.company.com:8080"),
        BypassProxyOnLocal = true,
        Credentials = new NetworkCredential("username", "password")
    },
    UseProxy = true
});
```

3. **Implement retry logic:**
```csharp
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => !msg.IsSuccessStatusCode)
    .WaitAndRetryAsync(
        3,
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            var logger = context.Values["logger"] as ILogger;
            logger?.LogWarning("Retry {Count} after {Delay}ms", 
                retryCount, timespan.TotalMilliseconds);
        }));
```

### 4. Tool Generation Problems

**Symptoms:**
- No tools appear in `/mcp/tools`
- "Failed to generate tools"
- Missing expected tools

**Diagnosis:**
```csharp
// Log tool generation process
public class DiagnosticToolFactory : IMcpToolFactory
{
    private readonly IMcpToolFactory _innerFactory;
    private readonly ILogger<DiagnosticToolFactory> _logger;
    
    public async Task<IEnumerable<McpTool>> GenerateToolsAsync(EdmModel model)
    {
        _logger.LogInformation("Starting tool generation for model with {Count} entity types", 
            model.EntityTypes.Count);
        
        try
        {
            var tools = await _innerFactory.GenerateToolsAsync(model);
            var toolList = tools.ToList();
            
            _logger.LogInformation("Generated {Count} tools", toolList.Count);
            
            foreach (var tool in toolList)
            {
                _logger.LogDebug("Generated tool: {Name} - {Description}", 
                    tool.Name, tool.Description);
            }
            
            return toolList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool generation failed");
            throw;
        }
    }
}
```

**Solutions:**

1. **Validate EDM model:**
```csharp
public class EdmModelValidator
{
    public ValidationResult Validate(IEdmModel model)
    {
        var errors = new List<string>();
        
        // Check for entity types
        if (!model.SchemaElements.OfType<IEdmEntityType>().Any())
        {
            errors.Add("No entity types found in model");
        }
        
        // Check for entity container
        var container = model.EntityContainer;
        if (container == null)
        {
            errors.Add("No entity container found");
        }
        else
        {
            // Check for entity sets
            if (!container.EntitySets().Any())
            {
                errors.Add("No entity sets found in container");
            }
        }
        
        // Validate entity types
        foreach (var entityType in model.SchemaElements.OfType<IEdmEntityType>())
        {
            if (!entityType.Key().Any())
            {
                errors.Add($"Entity type '{entityType.Name}' has no key properties");
            }
        }
        
        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }
}
```

2. **Custom tool filtering:**
```csharp
builder.Services.Configure<ODataMcpOptions>(options =>
{
    options.ToolFilter = (tool, context) =>
    {
        // Log filtered tools
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        
        if (tool.Name.StartsWith("Internal"))
        {
            logger.LogDebug("Filtering out internal tool: {Name}", tool.Name);
            return false;
        }
        
        return true;
    };
});
```

### 5. Performance Issues

**Symptoms:**
- Slow response times
- High memory usage
- CPU spikes

**Diagnosis:**
```csharp
// Add performance monitoring
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
    private readonly DiagnosticSource _diagnosticSource;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var startMemory = GC.GetTotalMemory(false);
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var endMemory = GC.GetTotalMemory(false);
            var memoryUsed = endMemory - startMemory;
            
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                _logger.LogWarning("Slow request: {Method} {Path} took {Duration}ms", 
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);
            }
            
            if (memoryUsed > 10_000_000) // 10MB
            {
                _logger.LogWarning("High memory usage: {Method} {Path} used {Memory:N0} bytes", 
                    context.Request.Method,
                    context.Request.Path,
                    memoryUsed);
            }
            
            // Write diagnostic event
            if (_diagnosticSource.IsEnabled("Microsoft.OData.Mcp.RequestTiming"))
            {
                _diagnosticSource.Write("Microsoft.OData.Mcp.RequestTiming", new
                {
                    context.Request.Path,
                    context.Request.Method,
                    Duration = stopwatch.ElapsedMilliseconds,
                    MemoryUsed = memoryUsed
                });
            }
        }
    }
}
```

**Solutions:**

1. **Enable caching:**
```csharp
// Configure aggressive caching
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000;
    options.CompactionPercentage = 0.25;
});

builder.Services.Configure<ODataMcpOptions>(options =>
{
    options.Caching = new CachingOptions
    {
        MetadataCacheDuration = TimeSpan.FromHours(24),
        ToolsCacheDuration = TimeSpan.FromHours(1),
        ResultCacheDuration = TimeSpan.FromMinutes(5),
        CacheKeyGenerator = (context) =>
        {
            var user = context.User?.Identity?.Name ?? "anonymous";
            var path = context.Request.Path;
            var query = context.Request.QueryString;
            return $"{user}:{path}{query}";
        }
    };
});
```

2. **Optimize queries:**
```csharp
public class QueryOptimizer
{
    public string OptimizeODataQuery(string query)
    {
        // Add $top if not present
        if (!query.Contains("$top"))
        {
            query += (query.Contains("?") ? "&" : "?") + "$top=100";
        }
        
        // Remove unnecessary expansions
        query = RemoveDeepExpansions(query);
        
        // Add count=false for better performance
        if (!query.Contains("$count"))
        {
            query += "&$count=false";
        }
        
        return query;
    }
}
```

### 6. CORS Issues

**Symptoms:**
- "CORS policy blocked"
- "No 'Access-Control-Allow-Origin' header"
- Preflight request failures

**Solutions:**

```csharp
// Comprehensive CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
    
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                "https://app.example.com",
                "https://www.example.com"
            )
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders(
                "Authorization",
                "Content-Type",
                "X-Requested-With",
                "X-API-Key"
            )
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(24));
    });
});

// Apply CORS policy
var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);
```

## Error Reference

### MCP-001: Service URL Not Configured
```
Error: ServiceUrl is required when UseLocalMetadata is false
```
**Solution:** Set `ODataMcp:ServiceUrl` in configuration

### MCP-002: Invalid Metadata
```
Error: Failed to parse OData metadata
```
**Solution:** Verify metadata endpoint returns valid CSDL XML

### MCP-003: Authentication Required
```
Error: Authentication is required for this operation
```
**Solution:** Provide valid authentication token or credentials

### MCP-004: Tool Not Found
```
Error: Tool '{toolName}' not found
```
**Solution:** Check available tools at `/mcp/tools` endpoint

### MCP-005: Rate Limit Exceeded
```
Error: Rate limit exceeded. Try again in {seconds} seconds
```
**Solution:** Implement retry logic with exponential backoff

## Debugging Checklist

- [ ] Enable detailed logging
- [ ] Check health endpoints
- [ ] Verify configuration values
- [ ] Test OData service connectivity
- [ ] Validate authentication tokens
- [ ] Check for CORS issues
- [ ] Monitor performance metrics
- [ ] Review error logs
- [ ] Test with minimal configuration
- [ ] Check for version conflicts

## Getting Help

If you're still experiencing issues:

1. **Check the logs** - Most issues are logged with detailed information
2. **Use debug endpoints** - Available in development mode
3. **Simplify configuration** - Start with minimal setup
4. **File an issue** - Include logs, configuration, and steps to reproduce

## Next Steps

- [Configuration Reference](configuration.md) - Detailed configuration options
- [Security Guide](security.md) - Security troubleshooting
- [API Reference](api-reference.md) - API documentation
- [Examples](examples.md) - Working examples