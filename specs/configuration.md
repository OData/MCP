# Configuration Reference

This document provides a comprehensive reference for all configuration options available in the Microsoft OData MCP Server.

## Configuration Sources

Configuration can be provided through multiple sources (in order of precedence):

1. Command-line arguments
2. Environment variables
3. `appsettings.{Environment}.json`
4. `appsettings.json`
5. User secrets (development only)
6. Default values

## Configuration Structure

### Root Configuration

```json
{
  "ODataMcp": {
    "ServiceUrl": "string",
    "UseLocalMetadata": false,
    "EnableAuthentication": true,
    "BasePath": "/mcp",
    "ServerInfo": {},
    "ToolGeneration": {},
    "Caching": {},
    "Security": {},
    "Network": {},
    "Monitoring": {},
    "RateLimiting": {}
  }
}
```

### Core Settings

#### ServiceUrl
- **Type**: `string`
- **Required**: Yes (unless UseLocalMetadata is true)
- **Description**: The base URL of the OData service
- **Example**: `"https://api.example.com/odata"`
- **Environment Variable**: `ODATAMCP__SERVICEURL`

#### UseLocalMetadata
- **Type**: `boolean`
- **Default**: `false`
- **Description**: Use metadata from local OData controllers instead of remote service
- **Environment Variable**: `ODATAMCP__USELOCALMETADATA`

#### EnableAuthentication
- **Type**: `boolean`
- **Default**: `true`
- **Description**: Enable authentication for MCP endpoints
- **Environment Variable**: `ODATAMCP__ENABLEAUTHENTICATION`

#### BasePath
- **Type**: `string`
- **Default**: `"/mcp"`
- **Description**: Base path for MCP endpoints
- **Environment Variable**: `ODATAMCP__BASEPATH`

### Server Information

```json
{
  "ODataMcp": {
    "ServerInfo": {
      "Name": "My OData MCP Server",
      "Version": "1.0.0",
      "Description": "MCP server for corporate OData services",
      "ContactEmail": "admin@example.com",
      "DocumentationUrl": "https://docs.example.com"
    }
  }
}
```

### Tool Generation Options

```json
{
  "ODataMcp": {
    "ToolGeneration": {
      "IncludeCrudTools": true,
      "IncludeQueryTools": true,
      "IncludeNavigationTools": true,
      "IncludeFunctionTools": true,
      "IncludeActionTools": true,
      "IncludeBatchOperations": false,
      "ToolNamingConvention": "CamelCase",
      "ToolPrefix": "",
      "ToolSuffix": "",
      "MaxResultsPerQuery": 100,
      "DefaultPageSize": 20,
      "EntityFilter": null,
      "PropertyFilter": null,
      "GenerateExamples": true,
      "IncludeDeprecated": false
    }
  }
}
```

#### ToolNamingConvention
- **Type**: `enum`
- **Values**: `"CamelCase"`, `"PascalCase"`, `"SnakeCase"`, `"KebabCase"`
- **Default**: `"CamelCase"`
- **Description**: Naming convention for generated tool names

#### EntityFilter
- **Type**: `string` (regex pattern)
- **Default**: `null`
- **Description**: Regular expression to filter which entities to expose
- **Example**: `"^(?!Internal).*"` (exclude entities starting with "Internal")

### Caching Configuration

```json
{
  "ODataMcp": {
    "Caching": {
      "Provider": "Memory",
      "MetadataCacheDuration": "24:00:00",
      "ResultCacheDuration": "00:05:00",
      "MaxCacheSize": 1000,
      "EnableCompression": true,
      "EvictionPolicy": "LRU",
      "Redis": {
        "ConnectionString": "localhost:6379",
        "InstanceName": "odata-mcp",
        "Database": 0
      },
      "Memory": {
        "SizeLimit": 104857600,
        "CompactionPercentage": 0.2
      }
    }
  }
}
```

#### Cache Providers
- **Memory**: In-memory caching (default)
- **Redis**: Distributed Redis cache
- **SqlServer**: SQL Server distributed cache
- **NCache**: NCache distributed cache

### Security Configuration

```json
{
  "ODataMcp": {
    "Security": {
      "Authentication": {
        "Schemes": ["Bearer", "ApiKey"],
        "Bearer": {
          "Authority": "https://login.microsoftonline.com/tenant",
          "Audience": "api://your-api",
          "RequireHttpsMetadata": true,
          "ValidateIssuer": true,
          "ValidateAudience": true,
          "ValidateLifetime": true
        },
        "ApiKey": {
          "HeaderName": "X-API-Key",
          "QueryParameterName": "api_key",
          "ValidateKey": true
        }
      },
      "Authorization": {
        "RequireAuthenticatedUser": true,
        "Policies": {
          "ReadOnly": {
            "RequiredScopes": ["odata.read"],
            "RequiredRoles": ["Reader", "Admin"]
          },
          "FullAccess": {
            "RequiredScopes": ["odata.write"],
            "RequiredRoles": ["Admin"]
          }
        },
        "EntityPolicies": {
          "Customers": "FullAccess",
          "Orders": "ReadOnly"
        }
      },
      "Cors": {
        "AllowedOrigins": ["https://app.example.com"],
        "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
        "AllowedHeaders": ["*"],
        "AllowCredentials": true,
        "MaxAge": 3600
      },
      "Headers": {
        "EnableHsts": true,
        "EnableXssProtection": true,
        "EnableContentTypeNosniff": true,
        "EnableFrameOptions": true,
        "FrameOptionsValue": "DENY",
        "ContentSecurityPolicy": "default-src 'self'"
      }
    }
  }
}
```

### Network Configuration

```json
{
  "ODataMcp": {
    "Network": {
      "HttpClient": {
        "Timeout": "00:00:30",
        "MaxConnectionsPerServer": 100,
        "EnableAutoRedirect": false,
        "MaxAutomaticRedirections": 5
      },
      "Proxy": {
        "UseProxy": false,
        "ProxyAddress": "http://proxy.example.com:8080",
        "BypassOnLocal": true,
        "Credentials": {
          "Username": "proxyuser",
          "Password": "encrypted:base64string"
        }
      },
      "Retry": {
        "MaxRetries": 3,
        "InitialDelay": "00:00:01",
        "MaxDelay": "00:00:30",
        "BackoffMultiplier": 2.0,
        "RetryableStatusCodes": [408, 429, 500, 502, 503, 504]
      },
      "CircuitBreaker": {
        "FailureThreshold": 5,
        "SamplingDuration": "00:01:00",
        "MinimumThroughput": 10,
        "DurationOfBreak": "00:00:30"
      }
    }
  }
}
```

### Rate Limiting Configuration

```json
{
  "ODataMcp": {
    "RateLimiting": {
      "EnableRateLimiting": true,
      "GlobalPolicy": {
        "PermitLimit": 1000,
        "Window": "00:01:00",
        "QueueProcessingOrder": "OldestFirst",
        "QueueLimit": 100
      },
      "PerUserPolicy": {
        "PermitLimit": 100,
        "Window": "00:01:00"
      },
      "PerIpPolicy": {
        "PermitLimit": 500,
        "Window": "00:01:00"
      },
      "EndpointPolicies": {
        "/mcp/tools/execute": {
          "PermitLimit": 50,
          "Window": "00:01:00"
        }
      }
    }
  }
}
```

### Monitoring Configuration

```json
{
  "ODataMcp": {
    "Monitoring": {
      "EnableMetrics": true,
      "EnableTracing": true,
      "EnableHealthChecks": true,
      "Metrics": {
        "Provider": "Prometheus",
        "Endpoint": "/metrics",
        "IncludeDefaultMetrics": true,
        "CustomMetrics": ["tool_executions", "cache_hits", "auth_failures"]
      },
      "Tracing": {
        "Provider": "OpenTelemetry",
        "ServiceName": "odata-mcp-server",
        "Endpoint": "http://localhost:4317",
        "SamplingRate": 0.1,
        "ExportTimeout": "00:00:10"
      },
      "HealthChecks": {
        "Endpoint": "/health",
        "DetailedOutput": false,
        "Checks": {
          "ODataService": {
            "Enabled": true,
            "Timeout": "00:00:05",
            "Tags": ["ready", "live"]
          },
          "Database": {
            "Enabled": true,
            "ConnectionString": "Server=.;Database=ODataMcp;Integrated Security=true"
          },
          "Redis": {
            "Enabled": true,
            "ConnectionString": "localhost:6379"
          }
        }
      }
    }
  }
}
```

## Environment Variables

All configuration values can be overridden using environment variables with the following naming convention:

- Replace `:` with `__` (double underscore)
- Prefix with `ODATAMCP_`

Examples:
```bash
# Basic settings
ODATAMCP__SERVICEURL=https://api.example.com/odata
ODATAMCP__ENABLEAUTHENTICATION=true

# Nested settings
ODATAMCP__TOOLGENERATION__MAXRESULTSPERQUERY=50
ODATAMCP__CACHING__PROVIDER=Redis
ODATAMCP__SECURITY__AUTHENTICATION__BEARER__AUTHORITY=https://login.microsoftonline.com/tenant
```

## Configuration Profiles

### Development Profile

```json
{
  "ODataMcp": {
    "EnableAuthentication": false,
    "Caching": {
      "Provider": "Memory",
      "MetadataCacheDuration": "00:05:00"
    },
    "Security": {
      "Cors": {
        "AllowedOrigins": ["*"],
        "AllowCredentials": false
      }
    },
    "Monitoring": {
      "Tracing": {
        "SamplingRate": 1.0
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.OData.Mcp": "Trace"
    }
  }
}
```

### Production Profile

```json
{
  "ODataMcp": {
    "EnableAuthentication": true,
    "Caching": {
      "Provider": "Redis",
      "MetadataCacheDuration": "24:00:00",
      "EnableCompression": true
    },
    "Security": {
      "Authentication": {
        "RequireHttpsMetadata": true
      },
      "Headers": {
        "EnableHsts": true,
        "EnableXssProtection": true,
        "EnableContentTypeNosniff": true,
        "EnableFrameOptions": true
      }
    },
    "RateLimiting": {
      "EnableRateLimiting": true
    },
    "Monitoring": {
      "EnableMetrics": true,
      "EnableTracing": true,
      "EnableHealthChecks": true
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.OData.Mcp": "Information"
    }
  }
}
```

## Configuration Validation

The server validates configuration on startup. Common validation rules:

1. **ServiceUrl** must be a valid URL when UseLocalMetadata is false
2. **BasePath** must start with `/`
3. **Cache durations** must be positive TimeSpan values
4. **Rate limits** must be positive integers
5. **Required authentication settings** when EnableAuthentication is true

## Dynamic Configuration

Some settings can be changed at runtime without restarting:

```csharp
// Using IOptionsSnapshot for dynamic updates
public class MyService
{
    private readonly IOptionsSnapshot<ODataMcpOptions> _options;
    
    public MyService(IOptionsSnapshot<ODataMcpOptions> options)
    {
        _options = options;
    }
    
    public void DoSomething()
    {
        // Gets the latest configuration
        var currentOptions = _options.Value;
    }
}
```

## Best Practices

1. **Use environment-specific files**: `appsettings.Development.json`, `appsettings.Production.json`
2. **Secure sensitive data**: Use user secrets, Azure Key Vault, or environment variables
3. **Validate configuration**: Implement `IValidateOptions<T>` for custom validation
4. **Monitor configuration changes**: Use `IOptionsMonitor<T>` for real-time updates
5. **Document custom settings**: Add XML comments to configuration classes

## Troubleshooting Configuration

### View Effective Configuration

```csharp
// Add this endpoint to see effective configuration (dev only!)
app.MapGet("/debug/config", (IOptions<ODataMcpOptions> options) =>
{
    return Results.Json(options.Value);
}).RequireHost("localhost");
```

### Common Issues

**Issue**: "Configuration section 'ODataMcp' not found"
- Ensure `appsettings.json` is copied to output directory
- Check file encoding (should be UTF-8)

**Issue**: Environment variables not working
- On Windows: Restart application after setting variables
- On Linux: Export variables or use systemd environment files

**Issue**: Changes not taking effect
- Check configuration precedence order
- Verify no command-line arguments overriding
- Clear cache if using distributed caching

## Next Steps

- [Integration Guide](integration-guide.md) - Integrate with your OData APIs
- [Security Setup](security.md) - Configure authentication in detail
- [Examples](examples.md) - See configuration examples