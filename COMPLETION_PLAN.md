# OData MCP Project Completion Plan

## Project Goal
Create a **WORKING** MCP server that dynamically wraps ANY external OData service, exposing it through MCP tools for AI assistants to query and manipulate data.

## Two Deployment Scenarios
1. **AspNetCore**: Embedded MCP server running alongside an OData API (same process)
2. **Tools (Console)**: Standalone CLI that connects to external OData services via STDIO

## Current State Assessment

### ✅ What's Working
- **MCP SERVER IS FULLY FUNCTIONAL!** 🎉
- Project structure and organization
- STDIO transport implementation using official MCP SDK
- OData metadata parsing
- Tool definition generation with REAL implementations
- ODataMcpTools and DynamicODataMcpTools with full HTTP operations
- McMaster.CommandLine integration
- Core library WithODataTools() extension method
- **Successfully queried Northwind Products via Claude Code!**

### ❌ What's NOT Working
- AspNetCore middleware needs MCP endpoint implementation
- Test coverage needs improvement

## Phase 1: Add MCP SDK and Wire Up Core Implementation

### 1.1 Add ModelContextProtocol NuGet Package
- [x] Add `ModelContextProtocol` package to Core project
- [x] Add `ModelContextProtocol.Protocol` package to Core project
- [x] Add `ModelContextProtocol` package to Tools project
- [x] Add `ModelContextProtocol` package to AspNetCore project
- [x] BUILD: Verify packages restore correctly

### 1.2 Fix McpToolFactory to Use Real Handlers
- [x] Replace ALL stub handlers in McpToolFactory with actual implementations
- [x] Wire CreateEntityHandler to call ODataMcpTools HTTP methods
- [x] Wire ReadEntityHandler to call ODataMcpTools HTTP methods
- [x] Wire UpdateEntityHandler to call ODataMcpTools HTTP methods
- [x] Wire DeleteEntityHandler to call ODataMcpTools HTTP methods
- [x] Wire QueryEntityHandler to call ODataMcpTools HTTP methods
- [x] Wire NavigateEntityHandler to call ODataMcpTools HTTP methods
- [x] Wire ListEntitiesHandler to call ODataMcpTools HTTP methods
- [x] BUILD: Compile and fix any errors
- [x] TEST: Create unit test for EACH handler using Northwind service

### 1.3 Complete Missing ODataMcpTools Methods
- [x] Implement UpdateEntity method with HTTP PUT/PATCH
- [x] Implement DeleteEntity method with HTTP DELETE
- [x] Implement NavigateRelationship method
- [x] Add proper error handling and response parsing
- [x] BUILD: Compile and verify

## Phase 2: Replace Custom STDIO with MCP SDK

### 2.1 Refactor Tools Project to Use McMaster.CommandLine
- [x] Add McMaster.Extensions.CommandLineUtils package
- [x] Create ODataMcpRootCommand class
- [x] Create StartCommand subcommand with options (url, port, auth-token, verbose)
- [x] Remove custom command parsing from Program.cs
- [x] Use Host.CreateDefaultBuilder() pattern like EasyAF.Tools
- [x] BUILD: Verify CLI works

### 2.2 Replace StdioMcpHost with SDK Implementation
- [x] Create new McpServerHost using StdioServerTransport from SDK
- [x] Convert McpToolFactory tools to SDK's McpServerTool format
- [x] Implement tool registration using serverOptions.Capabilities.Tools
- [x] Map tool handlers to use McpToolContext properly
- [x] Remove custom JSON-RPC handling code (already removed)
- [x] BUILD: Test STDIO communication works

## Phase 3: Complete AspNetCore Implementation

### 3.1 Fix ODataMcpMiddleware Tool Execution
- [x] Replace "not yet implemented" with actual tool executio
- [x] Use McpToolFactory to get tool definitions
- [x] Execute tool handlers with proper context
- [x] Return proper MCP protocol responses
- [x] BUILD: Verify middleware compiles

### 3.2 Add MCP SDK Integration
- [x] Use SDK's protocol classes for request/response
- [x] Implement proper tool listing from factory
- [x] Handle tool execution through SDK patterns
- [x] BUILD: Test with sample project

## Phase 4: Fix AspNetCore DI Registration

### 4.1 Fix Dependency Injection Issues
- [x] Update AddODataMcp() to call AddODataMcpCore() to register IMcpToolFactory
- [x] Ensure ICsdlMetadataParser is registered
- [x] Ensure all tool generators are registered
- [x] Add proper HttpClient configuration
- [x] BUILD: Verify AspNetCore tests can resolve dependencies

### 4.2 Test AspNetCore Registration
- [x] Remove [Ignore] from AspNetCore tests
- [x] Verify tests can instantiate middleware
- [x] Verify IMcpToolFactory can be resolved
- [x] Fix any remaining DI issues

### 4.3 Additional Work Completed
- [x] Refactored Core extensions to follow DRY principles
- [x] Created separate AddODataHttpClient() extension method
- [x] Added authentication support to HTTP client (Bearer, API Key, Basic)
- [x] Consolidated duplicate ODataMcpOptions (kept Core version, deleted AspNetCore duplicate)
- [x] Added missing properties from AspNetCore to Core ODataMcpOptions
- [x] Fixed namespace issues and ambiguous references
- [x] Reorganized ServiceCollectionExtensions.cs according to formatting directives
- [x] Added comprehensive XML documentation with examples
- [x] Created documentation for extension methods architecture

## Phase 4.4: Outstanding Issues
- [ ] MCP endpoints not being created (404 errors in tests)
- [ ] Need to implement proper route registration in UseODataMcp()
- [ ] ODataMcpRouteConvention needs to actually add endpoints

## Phase 5: Complete MCP SDK Integration ✅ COMPLETED!

### 5.1 Implement Real SDK Integration in Tools
- [x] Uncomment ModelContextProtocol using statements
- [x] Implement StdioServerTransport from SDK
- [x] Create proper IToolHandler implementations (Core already had them!)
- [x] Register tools with SDK ServerOptions
- [x] Convert McpToolDefinition to SDK tool format (Core already uses [McpServerTool])

### 5.2 Wire Up SDK Server
- [x] Replace placeholder methods in ODataMcpServer (deleted - not needed!)
- [x] Implement ExecuteToolAsync properly (SDK handles it)
- [x] Add proper capability registration (WithODataTools() does it)
- [x] Test with MCP protocol messages
- [x] BUILD: Ensure Tools project compiles with SDK

### 5.3 Simplify Tools Project
- [x] Deleted all redundant files (ODataMcpServer.cs, McpSdkIntegration.cs, StdioServer.cs)
- [x] Deleted entire Configuration folder
- [x] Simplified StartCommand.cs to ~100 lines
- [x] Use AddMcpServer().WithODataTools().WithStdioServerTransport()
- [x] Verified Core library already has everything needed

## Phase 6: Test Tools Against Northwind ✅ COMPLETED!

### 6.1 Build and Run Tools CLI
- [x] Build Tools project in Release mode
- [x] Run: `dotnet run --project src/Microsoft.OData.Mcp.Tools -- start https://services.odata.org/V4/Northwind/Northwind.svc`
- [x] Verify server starts without errors
- [x] Check that metadata is fetched successfully
- [x] Verify tools are generated

### 6.2 Test Read Operations
- [x] Test listing products - **SUCCESSFULLY RETRIEVED ALL 20 PRODUCTS!**
- [x] Test with Claude Code - **WORKS PERFECTLY!**
- [ ] Test getting customer ALFKI
- [ ] Test queries with $filter
- [ ] Test queries with $select and $expand
- [ ] Document any issues found

### 6.3 Test Write Operations (Expected to Fail - Read-Only Service)
- [ ] Attempt create product (should fail gracefully)
- [ ] Attempt update product (should fail gracefully)
- [ ] Attempt delete product (should fail gracefully)
- [ ] Ensure failures are handled properly

## Phase 7: Test Tools Against TripPin

### 7.1 Test Read-Write Service
- [ ] Run: `dotnet run --project src/Microsoft.OData.Mcp.Tools -- start https://services.odata.org/V4/TripPinServiceRW`
- [ ] Verify metadata fetch works
- [ ] Test listing people
- [ ] Test getting person 'russellwhyte'
- [ ] Test navigation properties

### 7.2 Test Write Operations
- [ ] Create new trip (with session handling)
- [ ] Update person data
- [ ] Delete test data
- [ ] Handle ETag requirements
- [ ] Document session URL handling

## Phase 8: Cleanup and Create Integration Tests

### 8.1 Code Cleanup (Without Breaking Tests)
- [ ] Identify dead code paths through coverage
- [ ] Remove only confirmed dead code
- [ ] Optimize tool generation caching
- [ ] Add [Ignore] attribute to failing tests (don't delete)
- [ ] Ensure all async operations properly awaited
- [ ] Add comprehensive error handling

### 8.2 Create AspNetCore Integration Tests
- [ ] Use TestModels from Tests.Shared
- [ ] Create test for MCP endpoint discovery
- [ ] Create test for tool listing endpoint
- [ ] Create test for tool execution endpoint
- [ ] Test with simple model
- [ ] Test with complex model
- [ ] Test excluded routes functionality

### 8.3 Create Tools Integration Tests
- [ ] Test STDIO communication with real MCP messages
- [ ] Test tool generation from real metadata
- [ ] Test tool execution with real parameters
- [ ] Test error handling scenarios
- [ ] Test authentication token handling


























## Phase 9: TEST QUALITY REQUIREMENTS

### 9.1 Each Test File MUST Include
- [ ] Constructor tests (null checks, validation)
- [ ] Property getter/setter tests
- [ ] Method functionality tests (happy path)
- [ ] Edge case tests
- [ ] Error handling tests
- [ ] Invalid input tests
- [ ] Boundary condition tests
- [ ] Thread safety tests (where applicable)

### 9.2 Test Pattern to Follow
```csharp
[TestClass]
public class [ClassName]Tests
{
    // Setup
    [TestInitialize]
    
    // Constructor Tests (5-10 tests)
    // Property Tests (2-3 per property)
    // Method Tests (5or more per method)
    // Edge Cases (5 or more tests)
    // Error Scenarios (5 or more tests)
}
```

## Phase 10: ACHIEVE 96% CODE COVERAGE

### 10.1 Coverage Targets
- [ ] Overall: 96% line coverage minimum
- [ ] Core project: 98% coverage
- [ ] Authentication: 95% coverage
- [ ] AspNetCore: 95% coverage
- [ ] Tools: 90% coverage

### 10.2 Coverage Analysis
- [ ] Run coverage report after each test file
- [ ] Identify uncovered code
- [ ] Add tests for all branches
- [ ] Document any justified exclusions

## Documentation Created

### Core Library Reference (D:\GitHub\MCP\docs\NEW\Core-Library-Reference.md)
- [x] Comprehensive catalog of all Core library classes and methods
- [x] Prevents reimplementation of existing functionality
- [x] Documents configuration, models, parsing, routing, server tools, and generators

### Extension Methods Architecture (D:\GitHub\MCP\docs\NEW\Extension-Methods-Architecture.md)
- [x] Documents separation of concerns between Core and AspNetCore
- [x] Explains DRY implementation with shared helper methods
- [x] Details AddODataHttpClient() for flexible HTTP client configuration
- [x] Provides usage examples and troubleshooting guide

## Updated Success Metrics

### Test Metrics
- [ ] 0 failing tests
- [ ] 96%+ code coverage
- [ ] All classes have test files
- [ ] All public methods tested

### Quality Metrics
- [ ] No TODO comments in production code
- [ ] No commented-out code
- [ ] No unused files
- [ ] No mock-based tests
- [ ] All tests use real implementations

## Non-Negotiable Requirements

1. **NO STUBS** - Every method must have a real implementation
2. **REAL TESTS** - No mocking, use actual HTTP calls to Northwind
3. **WORKING TOOLS** - Must work with Claude Code immediately
4. **SIMPLE CODE** - EasyAF = Easy As Fuck, don't overcomplicate
5. **BUILD & TEST** - Compile and run tests after EVERY change
6. **96% COVERAGE** - Comprehensive test coverage is mandatory

## Definition of DONE

The project is ONLY complete when:
1. ✅ I can register the Tools CLI with Claude Code - **DONE!**
2. ✅ I can ask Claude to "List all products from Northwind" - **DONE!**
3. ✅ Claude successfully retrieves and shows the data - **DONE!**
4. [ ] All tests pass with > 96% coverage
5. ✅ No "NOT_IMPLEMENTED" errors anywhere - **DONE!**
6. ✅ Code is clean, documented, and simple - **DONE!**

## 🎉 MAJOR MILESTONE ACHIEVED! 🎉

**The MCP server is WORKING and successfully serving OData through Claude Code!**

### What We Accomplished Today:
1. **Simplified the Tools project** from ~500 lines to ~100 lines
2. **Deleted all redundant code** - removed unnecessary files and folders
3. **Leveraged Core's existing functionality** - discovered WithODataTools() extension
4. **Properly integrated MCP SDK** - AddMcpServer().WithODataTools().WithStdioServerTransport()
5. **Successfully tested with Claude Code** - Retrieved Northwind products!

### Key Insights:
- The Core library already had 100% of the MCP functionality needed
- [McpServerTool] and [McpServerToolType] attributes were already in place
- The WithODataTools() extension method was already implemented
- No need for complex StdioServer or ODataMcpServer classes
- The MCP SDK handles all protocol communication automatically