# Microsoft.OData.Mcp.Core Library Reference

## Purpose
This document provides a comprehensive reference to avoid reimplementing existing functionality in the Core library. It catalogs all classes, interfaces, and functionality already implemented.

## Configuration Namespace (`Microsoft.OData.Mcp.Core.Configuration`)

### Authentication & Security

#### BasicAuthenticationCredentials
- **Purpose**: Stores username/password for basic authentication
- **Properties**: Username, Password
- **Methods**: Validate(), Clone()

#### OAuth2Configuration
- **Purpose**: OAuth2 client credentials flow configuration
- **Properties**: TokenEndpoint, ClientId, ClientSecret, Scopes
- **Methods**: Validate(), Clone()

#### ODataAuthenticationConfiguration
- **Purpose**: Authentication configuration for OData service connections
- **Properties**: Type (ODataAuthenticationType), ApiKey, ApiKeyHeader, BearerToken, BasicAuth, OAuth2
- **Methods**: Validate(), Clone(), MergeWith()

#### ODataAuthenticationType (Enum)
- **Values**: None, ApiKey, Bearer, Basic, OAuth2
- **Purpose**: Defines authentication types for OData services

### Caching System

#### CachingConfiguration
- **Purpose**: Comprehensive caching behavior configuration
- **Key Properties**: 
  - Enabled, ProviderType (CacheProviderType), MetadataTtl, ToolsTtl, QueryResultsTtl
  - MaxSizeMb, MaxEntries, EvictionPolicy, KeyPrefix, EnableStatistics, EnableWarming
  - DistributedCache (DistributedCacheConfiguration), Compression (CacheCompressionConfiguration)
- **Methods**: Validate(), Clone(), MergeWith(), GetMetadataKey(), GetToolsKey(), GetQueryResultKey()
- **Static Methods**: ForDevelopment(), ForProduction(), Disabled()

#### CacheCompressionConfiguration
- **Purpose**: Cache data compression settings
- **Properties**: Enabled, Algorithm, MinimumSize, CompressionLevel
- **Methods**: Validate(), Clone(), MergeWith()
- **Static Methods**: FastCompression(), MaximumCompression(), Disabled()

#### CacheEvictionPolicy (Enum)
- **Values**: LeastRecentlyUsed, LeastFrequentlyUsed, FirstInFirstOut, Random, TimeToLive
- **Purpose**: Defines cache eviction strategies

#### CacheProviderType (Enum)
- **Values**: Memory, Distributed, Redis, SqlServer, Custom
- **Purpose**: Defines cache storage backends

#### DistributedCacheConfiguration
- **Purpose**: Distributed caching configuration
- **Properties**: ConnectionString, InstanceName, DefaultSlidingExpiration, DefaultAbsoluteExpiration
- **Methods**: Validate(), Clone(), MergeWith()
- **Static Methods**: ForRedis(), ForSqlServer()

### Security & Access Control

#### SecurityConfiguration
- **Purpose**: Security policies and restrictions
- **Properties**: RequireHttps, EnableDetailedErrors, EnableRateLimiting, IpRestriction, DataProtection, SecurityHeaders, RateLimit
- **Methods**: Validate(), Clone(), MergeWith()

#### IpRestrictionConfiguration
- **Purpose**: IP-based access control
- **Properties**: Enabled, AllowedIpRanges, BlockedIpRanges
- **Methods**: Validate(), Clone(), MergeWith()

#### DataProtectionConfiguration
- **Purpose**: Data encryption and protection settings
- **Properties**: EncryptSensitiveData, EncryptionKey, KeyRotationPeriod
- **Methods**: Validate(), Clone(), MergeWith()
- **Static Methods**: ForProduction()

#### InputValidationConfiguration
- **Purpose**: Input validation and sanitization
- **Properties**: EnableStrictValidation, AllowSpecialCharacters, MaxStringLength
- **Methods**: Validate(), Clone(), MergeWith()
- **Static Methods**: Lenient(), Strict()

#### RateLimitingConfiguration
- **Purpose**: Request throttling and rate limiting
- **Properties**: RequestsPerMinute, BurstLimit, TimeWindow
- **Methods**: Validate(), Clone(), MergeWith()
- **Static Methods**: ForProduction()

### Network & Transport

#### NetworkConfiguration
- **Purpose**: Network endpoints, ports, and transport configuration
- **Key Properties**:
  - Host, Port, BasePath, EnableHttps, HttpsPort, UseHostConfiguration
  - MaxConcurrentConnections, ConnectionTimeout, RequestTimeout, KeepAliveTimeout, MaxRequestBodySize
  - Ssl (SslConfiguration), Cors (CorsConfiguration), Compression (CompressionConfiguration)
- **Methods**: Validate(), GetBaseUrl(), GetEndpointUrl(), Clone(), MergeWith()

#### SslConfiguration
- **Purpose**: SSL/TLS certificate configuration
- **Properties**: CertificatePath, CertificatePassword, StoreLocation, StoreName, Thumbprint, SubjectName
- **Methods**: Validate(), Clone(), MergeWith()

#### CertificateStoreLocation (Enum)
- **Values**: CurrentUser, LocalMachine
- **Purpose**: Certificate store locations

#### CorsConfiguration
- **Purpose**: Cross-Origin Resource Sharing configuration
- **Properties**: Enabled, AllowedOrigins, AllowedMethods, AllowedHeaders, AllowCredentials, MaxAge
- **Methods**: Validate(), Clone(), MergeWith()

#### CompressionConfiguration
- **Purpose**: HTTP response compression
- **Properties**: Enabled, Algorithms, MinimumSize, MimeTypes
- **Methods**: Validate(), Clone(), MergeWith()

### Service Configuration

#### McpServerConfiguration
- **Purpose**: Unified MCP server configuration
- **Key Properties**:
  - DeploymentMode (McpDeploymentMode), ServerInfo (McpServerInfo)
  - ODataService (ODataServiceConfiguration), Authentication (McpAuthenticationOptions)
  - ToolGeneration (McpToolGenerationOptions), Network, Caching, Monitoring, Security, FeatureFlags
- **Methods**: Validate(), Clone(), MergeWith(), ApplyEnvironmentOverrides(), GetStatistics()
- **Static Methods**: ForSidecar(), ForMiddleware(), ForDevelopment(), ForProduction()

#### McpDeploymentMode (Enum)
- **Values**: Sidecar, Middleware, Hybrid
- **Purpose**: Defines deployment patterns

#### McpServerInfo
- **Purpose**: Server identification and capability information
- **Key Properties**:
  - Name, Description, Version, Vendor, Contact, License
  - DocumentationUrl, RepositoryUrl, McpProtocolVersion, Capabilities, Metadata
  - InstanceId, StartedAt, BuildInfo
- **Methods**: Validate(), AddCapability(), RemoveCapability(), HasCapability(), AddMetadata(), GetMetadata<T>(), GetUptime(), Clone(), MergeWith()

#### ODataServiceConfiguration
- **Purpose**: OData service connection configuration
- **Key Properties**:
  - BaseUrl, MetadataPath, AutoDiscoverMetadata, RefreshInterval, RequestTimeout
  - MaxRetryAttempts, RetryDelay, UseHostContext, Authentication
  - DefaultHeaders, SupportedODataVersions, MaxPageSize, FollowNextLinks, MaxPages, ValidateSSL
- **Methods**: Validate(), GetMetadataUrl(), GetEntitySetUrl(), AddDefaultHeader(), RemoveDefaultHeader(), Clone(), MergeWith()

#### BuildInfo
- **Purpose**: Build and version information
- **Properties**: BuildNumber, CommitHash, Branch, BuildTimestamp, Configuration, TargetFramework
- **Methods**: Clone()

### Monitoring & Observability

#### MonitoringConfiguration
- **Purpose**: Logging, metrics, and health monitoring
- **Key Properties**:
  - LogLevel, EnableStructuredLogging, LogRequestResponse, LogSensitiveData
  - EnableMetrics, EnableHealthChecks, EnableTracing, TracingSamplingRate
  - MetricsInterval, HealthCheckInterval, HealthCheckTimeout
  - OpenTelemetry, ApplicationInsights, CustomMetrics, LogFilters, Alerting
- **Methods**: Validate(), AddCustomMetric(), AddLogFilter(), Clone(), MergeWith()
- **Static Methods**: ForDevelopment(), ForProduction(), Minimal()

#### OpenTelemetryConfiguration
- **Purpose**: OpenTelemetry observability configuration
- **Properties**: Enabled, OtlpEndpoint, ServiceName, ServiceVersion, ResourceAttributes
- **Methods**: Validate(), Clone(), MergeWith()

#### ApplicationInsightsConfiguration
- **Purpose**: Azure Application Insights integration
- **Properties**: Enabled, ConnectionString, InstrumentationKey, SamplingPercentage
- **Methods**: Validate(), Clone(), MergeWith()

#### MetricDefinition
- **Purpose**: Custom metric definition
- **Properties**: Name, Type (MetricType), Description, Unit, Tags
- **Methods**: Validate(), Clone()

#### MetricType (Enum)
- **Values**: Counter, Gauge, Histogram, Summary
- **Purpose**: Defines metric types

#### LogFilter
- **Purpose**: Log category filtering
- **Properties**: Category, Level
- **Methods**: Validate(), Clone()

#### AlertingConfiguration
- **Purpose**: Automated alerting configuration
- **Properties**: Enabled, Rules
- **Methods**: Validate(), Clone(), MergeWith()

#### AlertRule
- **Purpose**: Alert rule definition
- **Properties**: Name, Metric, Threshold, Operator
- **Methods**: Clone()

### Feature Management

#### FeatureFlagsConfiguration
- **Purpose**: Feature toggle management
- **Key Properties**: Multiple feature flags (EnableDevelopmentEndpoints, EnableExperimentalFeatures, EnableBatchOperations, etc.)
- **Collections**: CustomFlags, FlagMetadata
- **Methods**: 
  - Validate(), IsEnabled(), SetCustomFlag(), RemoveCustomFlag()
  - AddFlagMetadata(), GetFlagMetadata<T>(), GetEnabledFlags(), GetStatistics()
  - Clone(), MergeWith()
- **Static Methods**: ForDevelopment(), ForProduction(), Minimal()

## Root Level Classes (`Microsoft.OData.Mcp.Core`)

#### ODataMcpOptions
- **Purpose**: Configuration options for OData MCP integration
- **Properties**: 
  - AutoRegisterRoutes, ExcludeRoutes, EnableDynamicModels, ToolNamingPattern
  - MaxToolsPerEntity, CacheDuration, UseAggressiveCaching, EnableRequestLogging
  - MaxRequestSize, IncludeMetadata, DefaultPageSize, MaxPageSize, EnableCors, AllowedOrigins
- **Methods**: (Configuration properties only)

## Extensions Namespace (`Microsoft.Extensions.DependencyInjection`)

#### ServiceCollectionExtensions
- **Purpose**: Extension methods for configuring OData MCP Server services in DI container
- **Methods**: 
  - AddODataMcpServerCore(IConfiguration), AddODataMcpServerCore(Action<McpServerConfiguration>)
  - WithODataTools(IMcpServerBuilder)
- **Services Registered**: ICsdlMetadataParser, IMcpToolFactory, Tool Generators, HTTP Client, MCP Tools

## Models Namespace (`Microsoft.OData.Mcp.Core.Models`)

### EDM (Entity Data Model) Classes

#### EdmModel
- **Purpose**: Complete OData Entity Data Model representation
- **Properties**: 
  - Version, EntityTypes, ComplexTypes, EntityContainers, Namespaces, Functions, Actions
  - Annotations, PrimaryContainer/EntityContainer, AllEntitySets, AllSingletons
- **Methods**: 
  - GetEntityType(), GetComplexType(), GetEntityContainer(), GetEntitySet(), GetSingleton()
  - AddEntityType(), AddComplexType(), AddEntityContainer(), AddAnnotation(), GetAnnotation<T>()
  - Validate(), ToString()

#### EdmEntityType
- **Purpose**: Entity type definition with properties, keys, and relationships
- **Properties**:
  - Name, Namespace, BaseType, Abstract/IsAbstract, OpenType, HasStream
  - Properties (List<EdmProperty>), NavigationProperties (List<EdmNavigationProperty>), Key
  - FullName, KeyProperties, NonKeyProperties, HasNavigationProperties, HasBaseType
- **Methods**: GetProperty(), GetNavigationProperty(), HasProperty(), HasNavigationProperty(), ToString(), Equals(), GetHashCode()

#### EdmEntitySet
- **Purpose**: Entity set definition for addressable collections
- **Properties**: 
  - Name, EntityType, IncludeInServiceDocument, NavigationPropertyBindings, Annotations
  - HasNavigationPropertyBindings, EntityTypeName, EntityTypeNamespace
- **Methods**: 
  - GetNavigationPropertyBinding(), AddNavigationPropertyBinding(), RemoveNavigationPropertyBinding()
  - AddAnnotation(), GetAnnotation<T>(), ToString(), Equals(), GetHashCode()

#### McpTool
- **Purpose**: MCP tool definition for AI model interaction
- **Properties**: 
  - Name, Description, InputSchema, Metadata, Examples, Categories
  - IsEnabled, Version, EstimatedExecutionTimeMs, MaxConcurrentExecutions
- **Methods**: Clone(), Validate(), ToString()

### Additional EDM Classes
- **EdmComplexType**: Complex type definitions
- **EdmEntityContainer**: Entity container with entity sets and singletons
- **EdmProperty**: Property definitions with type information
- **EdmNavigationProperty**: Navigation property for relationships
- **EdmNavigationPropertyBinding**: Navigation property bindings
- **EdmReferentialConstraint**: Referential constraint definitions
- **EdmParameter**: Parameter definitions for functions/actions
- **EdmFunction, EdmAction**: Function and action definitions
- **EdmFunctionImport, EdmActionImport**: Import definitions
- **EdmSingleton**: Singleton resource definitions
- **EdmPrimitiveType**: Primitive type definitions

## Parsing Namespace (`Microsoft.OData.Mcp.Core.Parsing`)

#### ICsdlMetadataParser
- **Purpose**: Interface for parsing OData CSDL XML documents
- **Methods**: ParseFromString(), ParseFromStream(), ParseFromFile()
- **Returns**: EdmModel from CSDL XML

#### CsdlParser
- **Purpose**: Concrete implementation of CSDL XML parsing
- **Properties**: Logger, XML Namespaces (EdmNamespace, EdmxNamespace)
- **Methods**: 
  - ParseFromString(), ParseFromStream(), ParseFromFile() (public interface methods)
  - ParseDocument(), ParseSchema(), ParseEntityType(), ParseComplexType() (internal parsing)
  - ParseProperty(), ParseNavigationProperty(), ParseEntityContainer() (internal parsing)
  - ParseEntitySet(), ParseSingleton(), ParseFunctionImport(), ParseActionImport() (internal parsing)
- **Features**: Full CSDL v4.0+ support, comprehensive XML parsing, logging integration

## Tools Namespace (`Microsoft.OData.Mcp.Core.Tools`)

#### IMcpToolFactory
- **Purpose**: Interface for dynamic MCP tool generation from OData metadata
- **Methods**: 
  - GenerateToolsAsync(), GenerateEntityToolsAsync(), GenerateCrudToolsAsync()
  - GenerateQueryToolsAsync(), GenerateNavigationToolsAsync(), GenerateEntitySetToolsAsync()
  - ValidateTools(), GetTool(), GetAvailableToolNames(), FilterToolsForUser()

#### McpToolFactory
- **Purpose**: Concrete implementation of dynamic tool generation with HTTP execution
- **Dependencies**: ILogger, IHttpClientFactory
- **Properties**: Generated tools dictionary, HTTP client factory
- **Methods**: All interface methods plus comprehensive tool generation and execution
- **Tool Handlers**: 
  - CreateEntityHandler, ReadEntityHandler, UpdateEntityHandler, DeleteEntityHandler
  - QueryEntityHandler, NavigateEntityHandler, ListEntitiesHandler
- **Schema Generation**: Input schema generation for different operation types
- **Features**: 
  - Full CRUD operations via HTTP
  - OData query support ($filter, $select, $expand, etc.)
  - Navigation property traversal
  - Authorization filtering
  - Validation and error handling
  - JSON schema generation
  - Example generation

### Tool Support Classes
- **McpToolDefinition**: Complete tool definition with execution context
- **McpToolContext**: Execution context with service URLs, cancellation, correlation
- **McpToolResult**: Tool execution results with success/error states
- **McpToolExample**: Usage examples for AI model guidance
- **McpToolGenerationOptions**: Comprehensive options for tool generation
- **McpToolOperationType** (Enum): Create, Read, Update, Delete, Query, Navigate
- **McpToolExampleDifficulty** (Enum): Beginner, Intermediate, Advanced, Expert

### Tool Generators
- **IQueryToolGenerator, ICrudToolGenerator, INavigationToolGenerator**: Specialized generators
- **QueryToolGenerator, CrudToolGenerator, NavigationToolGenerator**: Implementations
- **CrudToolGenerationOptions, NavigationToolGenerationOptions**: Generator-specific options
- **ToolNamingConvention**: Tool naming strategies

## Routing Namespace (`Microsoft.OData.Mcp.Core.Routing`)

#### IMcpEndpointRegistry
- **Purpose**: Interface for MCP endpoint registration and discovery

#### McpEndpointRegistry
- **Purpose**: Concrete endpoint registry implementation

#### McpRouteMatcher
- **Purpose**: Route matching for MCP requests

#### IODataOptionsProvider
- **Purpose**: Provider interface for OData options

#### ODataRouteOptionsResolver
- **Purpose**: Resolves OData route options

#### SpanRouteParser
- **Purpose**: High-performance route parsing using spans

#### McpCommand
- **Purpose**: Command definitions for MCP operations

#### McpRouteEntry
- **Purpose**: Route entry definitions

## Server Namespace (`Microsoft.OData.Mcp.Core.Server`)

#### ODataMcpTools
- **Purpose**: Static MCP tools for OData operations

#### DynamicODataMcpTools
- **Purpose**: Dynamic MCP tools generated from metadata

## Services Namespace (`Microsoft.OData.Mcp.Core.Services`)

#### DynamicModelRefreshService
- **Purpose**: Service for refreshing OData models dynamically

---

## Summary

The Microsoft.OData.Mcp.Core library provides a comprehensive foundation for:

1. **Configuration Management**: Extensive configuration system with validation, merging, and environment-specific settings
2. **OData Integration**: Complete CSDL parsing and EDM model representation
3. **Dynamic Tool Generation**: Automatic MCP tool creation from OData metadata
4. **HTTP Operations**: Real HTTP-based CRUD, query, and navigation operations
5. **Security & Authorization**: Comprehensive security features, authentication, and authorization
6. **Caching & Performance**: Advanced caching with multiple providers and compression
7. **Monitoring & Observability**: Full logging, metrics, tracing, and health checking
8. **Network & Transport**: Complete network configuration with SSL, CORS, compression
9. **Extensibility**: Plugin architecture and customization points

This library eliminates the need to reimplement any of these core functionalities.