# Dynamic Tool Cleanup Checklist

## Overview
Consolidate MCP tool generation to use McpToolFactory as the single source of truth, removing redundant static tool implementations while maintaining flexibility through configuration options.

## Phase 1: Enhance McpToolFactory with Discovery/Utility Tools

### Add New Tool Handlers to McpToolFactory
- [ ] Add `DiscoverEntitySetsHandler` - List all available entity sets with their properties
  - [ ] Include entity type information
  - [ ] Include property count
  - [ ] Include navigation property information
  
- [ ] Add `DescribeEntityTypeHandler` - Get detailed schema for a specific entity type
  - [ ] Return all properties with types
  - [ ] Include key properties
  - [ ] Include navigation properties
  - [ ] Include any constraints or validation rules
  
- [ ] Add `GetMetadataHandler` - Retrieve raw CSDL metadata
  - [ ] Return full $metadata XML
  - [ ] Optional parameter to return as JSON representation
  
- [ ] Add `GenerateQueryExamplesHandler` - Generate sample OData queries for an entity
  - [ ] Basic filtering examples
  - [ ] Sorting examples
  - [ ] Pagination examples
  - [ ] Expansion examples
  
- [ ] Add `ValidateQueryHandler` - Validate OData query syntax
  - [ ] Parse and validate $filter expressions
  - [ ] Validate $orderby syntax
  - [ ] Check property names against schema
  - [ ] Return helpful error messages

### Create GenerateUtilityToolsAsync Method
- [ ] Create new method in McpToolFactory: `GenerateUtilityToolsAsync(EdmModel model, McpToolGenerationOptions options)`
- [ ] Generate discovery tools (always included)
- [ ] Generate validation tools (controlled by options)
- [ ] Integrate into main `GenerateToolsAsync` flow

## Phase 2: Implement Generic Query Tool with Smart Defaults

### Add Generic Query Tool Support
- [ ] Create `QueryEntitySetHandler` - Generic handler for any entity set
  - [ ] Accept entity set name as parameter
  - [ ] Support all OData query options
  - [ ] Use same smart defaults as specific handlers (binary field exclusion, etc.)
  
- [ ] Update McpToolGenerationOptions
  - [ ] Add `GenerateGenericQueryTool` boolean property (default: false)
  - [ ] Add `GenericQueryToolThreshold` int property (default: 100)
  - [ ] Add logic to auto-enable generic tool when entity count > threshold

### Implement -generic Flag in StartCommand
- [ ] Add `--generic` flag to StartCommand options
- [ ] When flag is set:
  - [ ] Generate ONLY discovery/utility tools
  - [ ] Generate ONE generic query tool
  - [ ] Skip entity-specific tool generation
- [ ] Update help text to explain the flag

## Phase 3: Remove Redundant Code

### Remove Static Tool Classes
- [ ] Delete `ODataMcpTools.cs` 
- [ ] Delete `DynamicODataMcpTools.cs`
- [ ] Remove `WithODataTools()` extension method from `ODataMcp_Core_ServiceCollectionExtensions.cs`
- [ ] Remove `[McpServerToolType]` attribute usage

### Update StartCommand
- [ ] Remove line 339: `builder.WithODataTools()`
- [ ] Remove any references to static tool registration
- [ ] Ensure only dynamic tools from McpToolFactory are used

### Clean Up DynamicToolGeneratorService
- [ ] Review if still needed
- [ ] Remove if redundant with McpToolFactory approach
- [ ] Update or remove line 344 in StartCommand

## Phase 4: Refactor for Testability and Maintainability

### Break McpToolFactory into Smaller Components

#### Create Separate Handler Classes
- [ ] Create `Handlers/` directory under Tools
- [ ] Extract handler methods into separate classes:
  - [ ] `EntityCrudHandlers.cs` - Create, Read, Update, Delete handlers
  - [ ] `EntityQueryHandlers.cs` - List and Query handlers  
  - [ ] `NavigationHandlers.cs` - Navigation property handlers
  - [ ] `DiscoveryHandlers.cs` - Discovery and metadata handlers
  - [ ] `ValidationHandlers.cs` - Query validation handlers

#### Refactor McpToolFactory
- [ ] Make McpToolFactory coordinate handler classes
- [ ] Keep handlers as internal classes that McpToolFactory instantiates
- [ ] Separate tool definition creation from handler logic
- [ ] Create `ToolDefinitionBuilder` helper class for building tool definitions
- [ ] Pass dependencies (HttpClientFactory, Logger) to handlers as needed

### Improve Code Organization
- [ ] Group related handler methods together using #regions
- [ ] Extract common validation logic into helper methods
- [ ] Extract URL building logic into helper methods
- [ ] Create consistent error handling patterns
- [ ] Follow defense-in-depth and fail-first programming

## Phase 5: Testing and Validation

### Unit Tests (No Mocking)
- [ ] Test handlers with real in-memory data structures
- [ ] Test tool generation with various options configurations
- [ ] Test binary field exclusion logic with sample EdmModel
- [ ] Test metadata parsing with actual CSDL XML samples
- [ ] Test generic vs specific tool generation logic
- [ ] Use MSTest v3, Breakdance, and FluentAssertions

### Integration Tests
- [ ] Test with Northwind service (< 100 entities)
- [ ] Create test service with > 100 entities to test threshold
- [ ] Test --generic flag functionality end-to-end
- [ ] Test tool discovery and execution against real services
- [ ] Test metadata caching and refresh scenarios

### Documentation Updates
- [ ] Update README with new tool generation approach
- [ ] Document --generic flag usage
- [ ] Document McpToolGenerationOptions
- [ ] Add examples for different scenarios
- [ ] Update API documentation with XML doc comments

## Implementation Steps (Immediate Action)

### Day 1: Refactor Structure
- [ ] Create Handlers directory
- [ ] Move existing handlers to EntityCrudHandlers.cs
- [ ] Move query handlers to EntityQueryHandlers.cs
- [ ] Add #regions for organization (Fields, Properties, Constructors, Public Methods, Private Methods)
- [ ] Test that everything still works

### Day 2: Add Discovery Tools
- [ ] Implement DiscoveryHandlers.cs
- [ ] Add discovery tool generation to McpToolFactory
- [ ] Test discovery tools with Northwind
- [ ] Add proper XML documentation

### Day 3: Add Generic Query Tool
- [ ] Implement generic QueryEntitySetHandler
- [ ] Add options for generic tool generation
- [ ] Add --generic flag to StartCommand
- [ ] Test generic tool functionality
- [ ] Use ArgumentException.ThrowIfNullOrWhiteSpace() for parameter validation

### Day 4: Clean Up
- [ ] Remove ODataMcpTools.cs and DynamicODataMcpTools.cs
- [ ] Remove WithODataTools() extension
- [ ] Update StartCommand to remove static tool registration
- [ ] Test everything still works
- [ ] Clean up any temporary files or scripts

### Day 5: Polish and Test
- [ ] Add remaining utility tools (validation, examples)
- [ ] Write integration tests using real services
- [ ] Update documentation with extensive XML doc comments
- [ ] Final testing with multiple services
- [ ] Verify all methods follow naming conventions

## Success Criteria

- [ ] Single source of truth for tool generation (McpToolFactory)
- [ ] No duplicate code paths for same functionality
- [ ] Tools are generated based on actual metadata
- [ ] Generic tool automatically added for large services (> 100 entities)
- [ ] --generic flag works correctly
- [ ] All handlers are in separate, focused classes
- [ ] Binary fields excluded by default
- [ ] Discovery tools always available
- [ ] Clean, maintainable, modular code structure
- [ ] No mocking used in any tests
- [ ] All public methods have XML documentation

## Code Quality Requirements

- [ ] Use `is null` or `is not null` instead of `== null` or `!= null`
- [ ] Use ArgumentException.ThrowIfNullOrWhiteSpace() for string parameters
- [ ] Use ArgumentNullException.ThrowIfNull() for object parameters
- [ ] Apply defense-in-depth and fail-first programming
- [ ] No private methods (use internal for testability)
- [ ] Order members by visibility (public, protected, internal)
- [ ] Order alphabetically within visibility groups
- [ ] Use pattern matching and switch expressions where possible

## Notes

- Keep McpToolGenerationOptions for web version compatibility
- Just remove unused code, no deprecation needed
- Focus on testability through smaller, focused handler classes
- Maintain backward compatibility for tool names where possible
- Consider performance implications of many tools vs generic tool
- Each handler class should be self-contained and testable without mocking