# Examples and Use Cases

This document provides practical examples of using the Microsoft OData MCP Server in real-world scenarios.

## E-Commerce Platform Integration

### Scenario
An e-commerce platform wants to enable AI assistants to help customer service representatives query orders, update customer information, and manage inventory.

### Implementation

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure OData MCP for e-commerce
builder.Services.AddODataMcpServer(options =>
{
    options.ServiceUrl = "https://api.ecommerce.com/odata";
    options.EnableAuthentication = true;
    
    // Customize tool generation for e-commerce needs
    options.ToolGeneration = new ToolGenerationOptions
    {
        ToolPrefix = "shop_",
        GenerateExamples = true,
        MaxResultsPerQuery = 50
    };
    
    // Entity-specific permissions
    options.EntityAuthorization = new Dictionary<string, string>
    {
        ["Orders"] = "CustomerService",
        ["Customers"] = "CustomerService",
        ["Products"] = "ReadOnly",
        ["Inventory"] = "InventoryManager"
    };
});

// Add authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.ecommerce.com";
        options.Audience = "ecommerce-api";
    });

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CustomerService", policy =>
        policy.RequireRole("CustomerServiceRep", "Manager"));
    
    options.AddPolicy("InventoryManager", policy =>
        policy.RequireRole("InventoryManager", "Manager"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseODataMcp();

app.Run();
```

### AI Assistant Queries

The AI can now help with queries like:

```javascript
// Find recent orders for a customer
{
  "tool": "shop_queryOrders",
  "parameters": {
    "filter": "CustomerId eq 'CUST123' and OrderDate gt 2024-01-01",
    "orderby": "OrderDate desc",
    "expand": "OrderItems($expand=Product)",
    "top": 10
  }
}

// Update customer address
{
  "tool": "shop_updateCustomer",
  "parameters": {
    "id": "CUST123",
    "data": {
      "ShippingAddress": {
        "Street": "123 New Street",
        "City": "Seattle",
        "State": "WA",
        "ZipCode": "98101"
      }
    }
  }
}

// Check inventory levels
{
  "tool": "shop_queryInventory",
  "parameters": {
    "filter": "QuantityOnHand lt ReorderPoint",
    "select": "ProductId,ProductName,QuantityOnHand,ReorderPoint",
    "orderby": "QuantityOnHand asc"
  }
}
```

## Healthcare Data Access

### Scenario
A healthcare provider wants to enable secure AI-assisted access to patient records while maintaining HIPAA compliance.

### Implementation

```csharp
public class HealthcareStartup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // Configure OData MCP with healthcare-specific security
        services.AddODataMcpServer(options =>
        {
            options.ServiceUrl = "https://ehr.hospital.com/odata";
            options.EnableAuthentication = true;
            
            // Healthcare-specific configuration
            options.Security = new SecurityOptions
            {
                RequireEncryption = true,
                AuditAllAccess = true,
                EnableFieldLevelSecurity = true
            };
        });
        
        // Add HIPAA-compliant authentication
        services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });
        
        // Custom authorization for patient data
        services.AddScoped<IAuthorizationHandler, PatientDataAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("PatientDataAccess", policy =>
            {
                policy.Requirements.Add(new PatientDataRequirement());
            });
        });
        
        // Add audit logging
        services.AddScoped<IToolExecutionFilter, HipaaAuditFilter>();
    }
}

// Custom authorization handler
public class PatientDataAuthorizationHandler : AuthorizationHandler<PatientDataRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PatientDataRequirement requirement)
    {
        var user = context.User;
        var httpContext = context.Resource as HttpContext;
        
        // Check if user has valid healthcare provider credentials
        if (!user.HasClaim("provider_npi"))
        {
            return Task.CompletedTask;
        }
        
        // Check if accessing own patients
        var patientId = ExtractPatientId(httpContext);
        if (IsAuthorizedForPatient(user, patientId))
        {
            context.Succeed(requirement);
        }
        
        return Task.CompletedTask;
    }
}

// HIPAA audit filter
public class HipaaAuditFilter : IToolExecutionFilter
{
    private readonly IAuditService _auditService;
    
    public async Task OnExecutingAsync(ToolExecutionContext context)
    {
        await _auditService.LogAccessAttemptAsync(new AuditEntry
        {
            UserId = context.User.Identity.Name,
            UserNPI = context.User.FindFirst("provider_npi")?.Value,
            Action = context.ToolName,
            Resource = "PatientData",
            Parameters = SanitizeParameters(context.Parameters),
            Timestamp = DateTime.UtcNow,
            IPAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString()
        });
    }
    
    public async Task OnExecutedAsync(ToolExecutedContext context)
    {
        await _auditService.LogAccessResultAsync(new AuditResult
        {
            AuditId = context.AuditId,
            Success = context.Result.Success,
            RecordsAccessed = CountRecords(context.Result.Data),
            Duration = context.Duration
        });
    }
}
```

### Healthcare AI Queries

```javascript
// Find patients with specific conditions
{
  "tool": "queryPatients",
  "parameters": {
    "filter": "Conditions/any(c: c/Code eq 'E11.9')", // Type 2 diabetes
    "select": "PatientId,FirstName,LastName,DateOfBirth",
    "top": 20
  }
}

// Get patient medication history
{
  "tool": "getPatientMedications",
  "parameters": {
    "patientId": "PAT-12345",
    "filter": "Status eq 'Active'",
    "expand": "Prescriber,Pharmacy"
  }
}
```

## Financial Services Integration

### Scenario
A bank wants to provide AI-powered financial analysis tools while maintaining strict security and compliance.

### Implementation

```csharp
public class FinancialServicesConfig
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // Configure OData MCP for financial services
        builder.Services.AddODataMcpServer(options =>
        {
            options.ServiceUrl = "https://api.bank.com/odata";
            
            // Financial-specific security
            options.Security = new SecurityOptions
            {
                RequireMutualTls = true,
                EnableRateLimiting = true,
                RequireSignedRequests = true
            };
            
            // Compliance settings
            options.Compliance = new ComplianceOptions
            {
                EnablePiiRedaction = true,
                EnableTransactionLimits = true,
                RequireAuditTrail = true
            };
        });
        
        // Multi-factor authentication
        builder.Services.AddAuthentication()
            .AddJwtBearer()
            .AddCertificate();
        
        // Transaction signing
        builder.Services.AddScoped<IRequestSigner, HmacRequestSigner>();
        
        // Fraud detection
        builder.Services.AddScoped<IToolExecutionFilter, FraudDetectionFilter>();
    }
}

// Fraud detection filter
public class FraudDetectionFilter : IToolExecutionFilter
{
    private readonly IFraudDetectionService _fraudService;
    
    public async Task OnExecutingAsync(ToolExecutionContext context)
    {
        if (IsHighRiskOperation(context.ToolName))
        {
            var riskScore = await _fraudService.AnalyzeRequestAsync(new FraudAnalysisRequest
            {
                UserId = context.User.Identity.Name,
                Operation = context.ToolName,
                Parameters = context.Parameters,
                UserLocation = context.HttpContext.GetUserLocation(),
                DeviceFingerprint = context.HttpContext.GetDeviceFingerprint()
            });
            
            if (riskScore > 0.8)
            {
                throw new SecurityException("Transaction blocked due to high risk score");
            }
            
            if (riskScore > 0.5)
            {
                // Require additional authentication
                context.Properties["RequireMfa"] = true;
            }
        }
    }
}

// PII redaction middleware
public class PiiRedactionMiddleware
{
    public async Task<JsonDocument> RedactPiiAsync(JsonDocument data)
    {
        var redacted = new Dictionary<string, object>();
        
        foreach (var property in data.RootElement.EnumerateObject())
        {
            if (IsPiiField(property.Name))
            {
                redacted[property.Name] = RedactValue(property.Value);
            }
            else
            {
                redacted[property.Name] = property.Value.Clone();
            }
        }
        
        return JsonDocument.Parse(JsonSerializer.Serialize(redacted));
    }
    
    private string RedactValue(JsonElement value)
    {
        var str = value.GetString();
        if (string.IsNullOrEmpty(str)) return str;
        
        // Keep first and last characters for partial matching
        if (str.Length <= 4) return "****";
        return $"{str[0]}{"*".PadLeft(str.Length - 2, '*')}{str[^1]}";
    }
}
```

### Financial AI Queries

```javascript
// Analyze spending patterns
{
  "tool": "queryTransactions",
  "parameters": {
    "filter": "AccountId eq 'ACC-12345' and TransactionDate ge 2024-01-01",
    "apply": "groupby((Category), aggregate(Amount with sum as TotalSpent))",
    "orderby": "TotalSpent desc"
  }
}

// Risk assessment for loan application
{
  "tool": "assessCreditRisk",
  "parameters": {
    "customerId": "CUST-67890",
    "loanAmount": 50000,
    "loanType": "Personal",
    "term": 60
  }
}
```

## Manufacturing IoT Integration

### Scenario
A manufacturing company wants to use AI to monitor equipment health and optimize production.

### Implementation

```csharp
public class ManufacturingIoTConfig
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddODataMcpServer(options =>
        {
            options.ServiceUrl = "https://iot.factory.com/odata";
            
            // Real-time data handling
            options.RealTimeOptions = new RealTimeOptions
            {
                EnableWebSockets = true,
                EnableServerSentEvents = true,
                UpdateInterval = TimeSpan.FromSeconds(5)
            };
            
            // Time-series optimization
            options.TimeSeriesOptions = new TimeSeriesOptions
            {
                DefaultAggregationWindow = TimeSpan.FromMinutes(5),
                EnableDownsampling = true,
                RetentionPeriod = TimeSpan.FromDays(90)
            };
        });
        
        // Add SignalR for real-time updates
        services.AddSignalR();
        
        // Background service for alerts
        services.AddHostedService<EquipmentMonitoringService>();
    }
}

// Real-time monitoring hub
public class EquipmentHub : Hub
{
    private readonly IODataMcpServer _mcpServer;
    
    public async Task SubscribeToEquipment(string equipmentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"equipment-{equipmentId}");
        
        // Send current status
        var result = await _mcpServer.ExecuteToolAsync("getEquipmentStatus", new
        {
            id = equipmentId
        });
        
        await Clients.Caller.SendAsync("EquipmentStatus", result.Data);
    }
    
    public async Task<object> GetProductionMetrics(DateTime start, DateTime end)
    {
        var result = await _mcpServer.ExecuteToolAsync("queryProductionMetrics", new
        {
            filter = $"Timestamp ge {start:o} and Timestamp le {end:o}",
            apply = "aggregate(UnitsProduced with sum as TotalUnits, " +
                   "DefectCount with sum as TotalDefects, " +
                   "DowntimeMinutes with sum as TotalDowntime)"
        });
        
        return result.Data;
    }
}

// Background monitoring service
public class EquipmentMonitoringService : BackgroundService
{
    private readonly IODataMcpServer _mcpServer;
    private readonly IHubContext<EquipmentHub> _hubContext;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Check equipment health
            var unhealthyEquipment = await _mcpServer.ExecuteToolAsync(
                "queryEquipmentHealth", new
                {
                    filter = "HealthScore lt 70 or Status eq 'Warning'",
                    expand = "RecentAlerts,MaintenanceHistory"
                });
            
            foreach (var equipment in unhealthyEquipment.Data.EnumerateArray())
            {
                var equipmentId = equipment.GetProperty("EquipmentId").GetString();
                
                // Notify subscribers
                await _hubContext.Clients
                    .Group($"equipment-{equipmentId}")
                    .SendAsync("HealthAlert", equipment);
                
                // Check if maintenance is needed
                if (ShouldScheduleMaintenance(equipment))
                {
                    await ScheduleMaintenanceAsync(equipment);
                }
            }
            
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

### Manufacturing AI Queries

```javascript
// Predictive maintenance query
{
  "tool": "analyzeEquipmentHealth",
  "parameters": {
    "equipmentId": "PRESS-001",
    "metrics": ["Temperature", "Vibration", "Pressure"],
    "timeRange": "PT24H",
    "anomalyThreshold": 2.5
  }
}

// Production optimization
{
  "tool": "optimizeProductionSchedule",
  "parameters": {
    "productionLine": "LINE-A",
    "startDate": "2024-02-01",
    "endDate": "2024-02-07",
    "constraints": {
      "maxChangeOvers": 3,
      "maintenanceWindows": ["2024-02-03T02:00:00Z"]
    }
  }
}
```

## Education Platform Integration

### Scenario
An online education platform wants to use AI to help instructors analyze student performance and provide personalized recommendations.

### Implementation

```csharp
public class EducationPlatformConfig
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddODataMcpServer(options =>
        {
            options.ServiceUrl = "https://lms.university.edu/odata";
            
            // Privacy settings for student data
            options.PrivacyOptions = new PrivacyOptions
            {
                AnonymizeStudentData = true,
                RequireConsent = true,
                EnableRightToForget = true
            };
        });
        
        // Analytics engine
        services.AddScoped<IStudentAnalyticsEngine, StudentAnalyticsEngine>();
        
        // Recommendation service
        services.AddScoped<IRecommendationService, AiRecommendationService>();
    }
}

// Student analytics implementation
public class StudentAnalyticsEngine : IStudentAnalyticsEngine
{
    private readonly IODataMcpServer _mcpServer;
    
    public async Task<StudentPerformanceReport> AnalyzeStudentAsync(string studentId)
    {
        // Get student's course enrollments
        var enrollments = await _mcpServer.ExecuteToolAsync("queryEnrollments", new
        {
            filter = $"StudentId eq '{studentId}' and Status eq 'Active'",
            expand = "Course,Grades,Assignments"
        });
        
        // Analyze performance trends
        var performanceTrends = await _mcpServer.ExecuteToolAsync("analyzeGradeTrends", new
        {
            studentId = studentId,
            timeframe = "CurrentSemester",
            includeAttendance = true
        });
        
        // Get peer comparison (anonymized)
        var peerComparison = await _mcpServer.ExecuteToolAsync("compareToPeers", new
        {
            studentId = studentId,
            anonymize = true,
            metrics = new[] { "GPA", "AssignmentCompletion", "ParticipationScore" }
        });
        
        return new StudentPerformanceReport
        {
            StudentId = studentId,
            OverallGPA = CalculateGPA(enrollments.Data),
            Trends = ParseTrends(performanceTrends.Data),
            PeerComparison = ParseComparison(peerComparison.Data),
            Recommendations = await GenerateRecommendationsAsync(studentId)
        };
    }
}

// AI-powered recommendations
public class AiRecommendationService : IRecommendationService
{
    public async Task<IEnumerable<LearningRecommendation>> GenerateRecommendationsAsync(
        string studentId, 
        AnalysisContext context)
    {
        var recommendations = new List<LearningRecommendation>();
        
        // Identify struggling areas
        var strugglingTopics = await IdentifyStrugglingAreasAsync(studentId);
        
        foreach (var topic in strugglingTopics)
        {
            recommendations.Add(new LearningRecommendation
            {
                Type = "SupplementalResource",
                Priority = "High",
                Title = $"Additional resources for {topic.Name}",
                Description = $"Based on your performance in {topic.Course}, " +
                             "these resources can help improve your understanding",
                Resources = await FindResourcesAsync(topic)
            });
        }
        
        // Study habit recommendations
        var studyPattern = await AnalyzeStudyPatternAsync(studentId);
        if (studyPattern.NeedsImprovement)
        {
            recommendations.Add(new LearningRecommendation
            {
                Type = "StudyHabit",
                Priority = "Medium",
                Title = "Optimize your study schedule",
                Description = GenerateStudyRecommendation(studyPattern)
            });
        }
        
        return recommendations;
    }
}
```

### Education AI Queries

```javascript
// Identify at-risk students
{
  "tool": "identifyAtRiskStudents",
  "parameters": {
    "courseId": "CS101",
    "riskFactors": ["LowAttendance", "MissedAssignments", "DecliningGrades"],
    "threshold": 0.7,
    "includeRecommendations": true
  }
}

// Generate personalized learning path
{
  "tool": "generateLearningPath",
  "parameters": {
    "studentId": "STU-12345",
    "targetSkills": ["DataStructures", "Algorithms"],
    "preferredLearningStyle": "Visual",
    "timeframe": "8weeks"
  }
}
```

## Multi-Language Support Example

### Supporting Multiple Programming Languages

```csharp
// Configure MCP for different language contexts
public class MultiLanguageExample
{
    public void ConfigurePythonClient()
    {
        // Python client configuration
        """python
        from mcp_client import McpClient
        
        # Initialize client
        client = McpClient(
            server_url="https://api.example.com/mcp",
            auth_token="your-token"
        )
        
        # Query customers
        result = client.execute_tool(
            "queryCustomers",
            filter="Country eq 'USA'",
            select="CustomerId,CompanyName,ContactName",
            top=10
        )
        
        for customer in result['value']:
            print(f"{customer['CompanyName']} - {customer['ContactName']}")
        """
    }
    
    public void ConfigureJavaScriptClient()
    {
        // JavaScript/TypeScript client
        """typescript
        import { McpClient } from '@odata/mcp-client';
        
        const client = new McpClient({
            serverUrl: 'https://api.example.com/mcp',
            authToken: process.env.MCP_TOKEN
        });
        
        // Async query with error handling
        async function getTopCustomers() {
            try {
                const result = await client.executeTool('queryCustomers', {
                    filter: "Country eq 'USA'",
                    select: 'CustomerId,CompanyName,Revenue',
                    orderby: 'Revenue desc',
                    top: 10
                });
                
                return result.value;
            } catch (error) {
                console.error('Failed to query customers:', error);
                throw error;
            }
        }
        """
    }
}
```

## Performance Testing Example

```csharp
[TestClass]
public class McpPerformanceTests
{
    private IODataMcpServer _server;
    
    [TestMethod]
    public async Task LoadTest_ConcurrentRequests()
    {
        // Simulate 100 concurrent requests
        var tasks = Enumerable.Range(0, 100).Select(i => 
            Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                
                var result = await _server.ExecuteToolAsync("queryOrders", new
                {
                    filter = $"OrderDate ge {DateTime.UtcNow.AddDays(-30):o}",
                    top = 50
                });
                
                stopwatch.Stop();
                
                return new
                {
                    RequestId = i,
                    Duration = stopwatch.ElapsedMilliseconds,
                    Success = result.Success,
                    RecordCount = result.Data.GetArrayLength()
                };
            }));
        
        var results = await Task.WhenAll(tasks);
        
        // Analyze results
        var avgDuration = results.Average(r => r.Duration);
        var successRate = results.Count(r => r.Success) / 100.0;
        
        Assert.IsTrue(avgDuration < 500, $"Average duration {avgDuration}ms exceeds 500ms");
        Assert.IsTrue(successRate > 0.99, $"Success rate {successRate} below 99%");
    }
}
```

## Next Steps

- [API Reference](api-reference.md) - Detailed API documentation
- [Troubleshooting](troubleshooting.md) - Debug common issues
- [Security Guide](security.md) - Implement security best practices
- [Configuration](configuration.md) - Advanced configuration options