# Security Guide

This guide covers authentication, authorization, and security best practices for the Microsoft OData MCP Server.

## Overview

The OData MCP Server provides multiple layers of security:

1. **Authentication** - Verify who is making requests
2. **Authorization** - Control what authenticated users can do
3. **Transport Security** - Protect data in transit
4. **API Security** - Protect against common attacks
5. **Data Security** - Protect sensitive information

## Authentication

### Supported Authentication Methods

#### 1. JWT Bearer Tokens (Recommended)

```csharp
// Configure JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://login.microsoftonline.com/{tenant-id}/v2.0";
        options.Audience = "api://your-api-client-id";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

// Configure MCP to use authentication
builder.Services.AddODataMcpServer(options =>
{
    options.EnableAuthentication = true;
    options.AuthenticationScheme = JwtBearerDefaults.AuthenticationScheme;
});
```

#### 2. API Key Authentication

```csharp
// Custom API key authentication handler
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            return AuthenticateResult.Fail("API Key not provided");
        }

        var apiKey = apiKeyHeader.FirstOrDefault();
        
        // Validate API key (use secure storage in production)
        var validKey = await ValidateApiKeyAsync(apiKey);
        if (!validKey)
        {
            return AuthenticateResult.Fail("Invalid API Key");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "API Key User"),
            new Claim("api_key", apiKey)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

// Register the handler
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", null);
```

#### 3. Certificate Authentication

```csharp
builder.Services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
    .AddCertificate(options =>
    {
        options.RevocationMode = X509RevocationMode.Online;
        options.ValidateCertificateUse = true;
        options.ValidateValidityPeriod = true;
        
        options.Events = new CertificateAuthenticationEvents
        {
            OnCertificateValidated = context =>
            {
                // Additional validation
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, context.ClientCertificate.Subject),
                    new Claim("thumbprint", context.ClientCertificate.Thumbprint)
                };
                
                context.Principal = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, context.Scheme.Name));
                
                return Task.CompletedTask;
            }
        };
    });
```

### OAuth 2.0 / OpenID Connect

```csharp
// Configure OAuth 2.0 with OpenID Connect
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = "https://accounts.google.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    
    options.ClaimActions.MapJsonKey("picture", "picture");
    options.ClaimActions.MapJsonKey("locale", "locale");
});
```

## Authorization

### Role-Based Authorization

```csharp
// Configure authorization policies
builder.Services.AddAuthorization(options =>
{
    // Basic role requirements
    options.AddPolicy("ReadOnly", policy => 
        policy.RequireRole("Reader", "User", "Admin"));
    
    options.AddPolicy("ReadWrite", policy => 
        policy.RequireRole("Writer", "Admin"));
    
    options.AddPolicy("Admin", policy => 
        policy.RequireRole("Admin"));
    
    // Complex policies
    options.AddPolicy("PremiumUser", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("subscription", "premium");
        policy.RequireRole("User");
    });
});

// Apply to MCP tools
builder.Services.AddODataMcpServer(options =>
{
    options.ToolAuthorization = new Dictionary<string, string>
    {
        ["queryCustomers"] = "ReadOnly",
        ["createCustomer"] = "ReadWrite",
        ["deleteCustomer"] = "Admin"
    };
});
```

### Scope-Based Authorization

```csharp
// Configure scope-based authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("read:customers", policy =>
        policy.RequireClaim("scope", "read:customers"));
    
    options.AddPolicy("write:customers", policy =>
        policy.RequireClaim("scope", "write:customers"));
    
    options.AddPolicy("delete:customers", policy =>
        policy.RequireClaim("scope", "delete:customers"));
});

// Custom authorization handler for complex scenarios
public class EntityAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, string>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        string entityName)
    {
        // Check user has access to specific entity
        var userClaims = context.User.Claims;
        var allowedEntities = userClaims
            .Where(c => c.Type == "allowed_entity")
            .Select(c => c.Value);
        
        if (allowedEntities.Contains(entityName))
        {
            context.Succeed(requirement);
        }
        
        return Task.CompletedTask;
    }
}
```

### Dynamic Authorization

```csharp
public class DynamicToolAuthorizationService : IToolAuthorizationService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<DynamicToolAuthorizationService> _logger;
    
    public async Task<bool> IsAuthorizedAsync(
        ClaimsPrincipal user, 
        string toolName, 
        JsonDocument parameters)
    {
        // Check basic tool access
        var basicAuth = await _authorizationService.AuthorizeAsync(
            user, toolName, "ToolAccess");
        
        if (!basicAuth.Succeeded)
        {
            _logger.LogWarning("User {User} denied access to tool {Tool}", 
                user.Identity?.Name, toolName);
            return false;
        }
        
        // Check parameter-based authorization
        if (toolName == "queryCustomers" && parameters != null)
        {
            // Restrict data based on user's region
            var userRegion = user.FindFirst("region")?.Value;
            var queryRegion = parameters.RootElement
                .GetProperty("filter")
                .GetString();
            
            if (!string.IsNullOrEmpty(userRegion) && 
                !queryRegion.Contains($"Region eq '{userRegion}'"))
            {
                _logger.LogWarning("User {User} attempted cross-region access", 
                    user.Identity?.Name);
                return false;
            }
        }
        
        return true;
    }
}
```

## Transport Security

### HTTPS Configuration

```csharp
// Force HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// Configure HSTS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// Configure HTTPS
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.ServerCertificate = new X509Certificate2(
            "path/to/certificate.pfx", 
            "certificate-password");
        
        httpsOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
        httpsOptions.CheckCertificateRevocation = true;
        httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
    });
});
```

### CORS Configuration

```csharp
// Configure CORS for MCP endpoints
builder.Services.AddCors(options =>
{
    options.AddPolicy("McpCorsPolicy", policy =>
    {
        policy.WithOrigins(
                "https://app.example.com",
                "https://claude.ai",
                "https://localhost:3000"
            )
            .WithMethods("GET", "POST", "OPTIONS")
            .WithHeaders(
                "Authorization",
                "Content-Type",
                "X-API-Key",
                "X-Requested-With"
            )
            .WithExposedHeaders(
                "X-Total-Count",
                "X-Page-Number",
                "X-Page-Size"
            )
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))
            .AllowCredentials();
    });
});

app.UseCors("McpCorsPolicy");
```

## API Security

### Rate Limiting

```csharp
// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinute(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 50
            }));
    
    // Specific endpoint limits
    options.AddPolicy("McpTools", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name ?? "anonymous",
            factory: partition => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 50,
                Window = TimeSpan.FromMinute(1),
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));
});

app.UseRateLimiter();
```

### Input Validation

```csharp
public class SecureToolExecutor : IToolExecutor
{
    private readonly IValidator<ToolExecutionRequest> _validator;
    
    public async Task<McpToolResult> ExecuteAsync(ToolExecutionRequest request)
    {
        // Validate input
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }
        
        // Sanitize parameters
        var sanitizedParams = SanitizeParameters(request.Parameters);
        
        // Execute with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await ExecuteToolAsync(request.ToolName, sanitizedParams, cts.Token);
    }
    
    private JsonDocument SanitizeParameters(JsonDocument parameters)
    {
        // Remove potential script injections
        var sanitized = new Dictionary<string, object>();
        
        foreach (var property in parameters.RootElement.EnumerateObject())
        {
            var value = property.Value.GetString();
            if (!string.IsNullOrEmpty(value))
            {
                // Remove script tags and SQL injection attempts
                value = Regex.Replace(value, @"<script[^>]*>.*?</script>", "", 
                    RegexOptions.IgnoreCase);
                value = Regex.Replace(value, @"(\b(ALTER|CREATE|DELETE|DROP|EXEC(UTE)?|INSERT|MERGE|SELECT|UPDATE|UNION)\b)", 
                    "", RegexOptions.IgnoreCase);
            }
            
            sanitized[property.Name] = value;
        }
        
        return JsonDocument.Parse(JsonSerializer.Serialize(sanitized));
    }
}
```

### Security Headers

```csharp
// Add security headers middleware
app.Use(async (context, next) =>
{
    // Security headers
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Permissions-Policy", "geolocation=(), camera=(), microphone=()");
    
    // Content Security Policy
    context.Response.Headers.Add("Content-Security-Policy", 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self'; " +
        "connect-src 'self' https://api.example.com; " +
        "frame-ancestors 'none';");
    
    await next();
});
```

## Data Security

### Sensitive Data Protection

```csharp
public class DataProtectionService
{
    private readonly IDataProtector _protector;
    
    public DataProtectionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("ODataMcp.SensitiveData");
    }
    
    public string ProtectSensitiveData(string data)
    {
        return _protector.Protect(data);
    }
    
    public string UnprotectSensitiveData(string protectedData)
    {
        return _protector.Unprotect(protectedData);
    }
}

// Configure data protection
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"./keys"))
    .ProtectKeysWithCertificate(
        new X509Certificate2("certificate.pfx", "password"))
    .SetApplicationName("ODataMcpServer");
```

### Field-Level Security

```csharp
public class FieldLevelSecurityMiddleware
{
    private readonly Dictionary<string, List<string>> _sensitiveFields = new()
    {
        ["Customer"] = new() { "SSN", "CreditCardNumber", "BankAccount" },
        ["Employee"] = new() { "Salary", "PerformanceRating", "MedicalInfo" }
    };
    
    public async Task<JsonDocument> FilterSensitiveFieldsAsync(
        JsonDocument data, 
        string entityType, 
        ClaimsPrincipal user)
    {
        if (!_sensitiveFields.TryGetValue(entityType, out var fields))
        {
            return data;
        }
        
        // Check user permissions
        var canViewSensitive = user.IsInRole("Admin") || 
                              user.HasClaim("permission", "view:sensitive");
        
        if (canViewSensitive)
        {
            return data;
        }
        
        // Remove sensitive fields
        var filtered = RemoveFields(data, fields);
        return filtered;
    }
}
```

## Audit Logging

```csharp
public class SecurityAuditService
{
    private readonly ILogger<SecurityAuditService> _logger;
    
    public void LogSecurityEvent(SecurityEvent evt)
    {
        _logger.LogInformation("Security Event: {EventType} | User: {User} | " +
            "Resource: {Resource} | Result: {Result} | IP: {IPAddress}",
            evt.EventType,
            evt.UserName,
            evt.Resource,
            evt.Result,
            evt.IPAddress);
        
        // Also write to audit database
        WriteToAuditDatabase(evt);
    }
    
    private async Task WriteToAuditDatabase(SecurityEvent evt)
    {
        // Implementation for persistent audit trail
    }
}

// Usage in middleware
public class AuditMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTime.UtcNow;
        
        try
        {
            await _next(context);
            
            _auditService.LogSecurityEvent(new SecurityEvent
            {
                EventType = "ToolExecution",
                UserName = context.User?.Identity?.Name,
                Resource = context.Request.Path,
                Result = "Success",
                IPAddress = context.Connection.RemoteIpAddress?.ToString(),
                Duration = DateTime.UtcNow - start
            });
        }
        catch (Exception ex)
        {
            _auditService.LogSecurityEvent(new SecurityEvent
            {
                EventType = "ToolExecutionFailed",
                UserName = context.User?.Identity?.Name,
                Resource = context.Request.Path,
                Result = "Failed",
                Error = ex.Message,
                IPAddress = context.Connection.RemoteIpAddress?.ToString(),
                Duration = DateTime.UtcNow - start
            });
            
            throw;
        }
    }
}
```

## Security Best Practices

### 1. Principle of Least Privilege

```csharp
// Configure minimal permissions by default
builder.Services.AddODataMcpServer(options =>
{
    options.DefaultPolicy = "ReadOnly";
    options.RequireExplicitToolPermissions = true;
    options.DenyByDefault = true;
});
```

### 2. Token Refresh and Rotation

```csharp
public class TokenRefreshMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers["Authorization"]
            .FirstOrDefault()?.Split(" ").Last();
        
        if (!string.IsNullOrEmpty(token))
        {
            var principal = ValidateToken(token);
            var expiry = principal.FindFirst("exp")?.Value;
            
            if (IsNearExpiry(expiry))
            {
                var newToken = await RefreshTokenAsync(token);
                context.Response.Headers.Add("X-New-Token", newToken);
            }
        }
        
        await _next(context);
    }
}
```

### 3. Secure Configuration

```csharp
// Use Azure Key Vault for secrets
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());

// Or use local secrets in development
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}
```

## Security Checklist

- [ ] Enable HTTPS and HSTS
- [ ] Configure authentication (JWT, OAuth, etc.)
- [ ] Implement authorization policies
- [ ] Set up rate limiting
- [ ] Enable CORS with specific origins
- [ ] Add security headers
- [ ] Implement input validation
- [ ] Enable audit logging
- [ ] Configure data protection
- [ ] Set up monitoring and alerts
- [ ] Regular security updates
- [ ] Penetration testing
- [ ] Security training for developers

## Next Steps

- [Examples](examples.md) - See security configuration examples
- [Troubleshooting](troubleshooting.md) - Debug security issues
- [Configuration](configuration.md) - Detailed security settings
- [Integration Guide](integration-guide.md) - Secure integration patterns