# Microsoft.OData.Mcp.Tests.Core

Comprehensive unit tests for the Core library.

## Test Structure

### Configuration Tests
- McpServerConfigurationTests - Validates configuration binding and defaults
- ODataServiceConfigurationTests - Tests OData service settings

### Parsing Tests  
- CsdlParserTests - Tests CSDL/EDMX parsing functionality
- EdmModelParsingTests - Tests EDM model construction

### Tools Tests
- McpToolFactoryTests - Tests tool factory implementation
- QueryToolGeneratorTests - Tests query tool generation
- CrudToolGeneratorTests - Tests CRUD tool generation
- NavigationToolGeneratorTests - Tests navigation tool generation
- McpToolDefinitionTests - Tests tool definition models

### Routing Tests
- SpanRouteParserTests - Tests high-performance route parsing
- McpRouteMatcherTests - Tests route matching logic
- McpEndpointRegistryTests - Tests endpoint registration

### Server Tests
- ODataMcpToolsTests - Tests attribute-based tool definitions
- DynamicODataMcpToolsTests - Tests dynamic tool generation

## Running Tests

```bash
dotnet test --configuration Release
```

## Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```