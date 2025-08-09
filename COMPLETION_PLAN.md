# OData MCP Project Completion Plan

## Project Goal
Create a **WORKING** MCP server that dynamically wraps ANY external OData service, exposing it through MCP tools for AI assistants to query and manipulate data.

## Two Deployment Scenarios
1. **AspNetCore**: Embedded MCP server running alongside an OData API (same process)
2. **Tools (Console)**: Standalone CLI that connects to external OData services via STDIO

## Current State Assessment

### ✅ What's Working
- Project structure and organization
- STDIO transport implementation (custom, but functional)
- OData metadata parsing
- Tool definition generation (schemas only)
- Basic ODataMcpTools with HTTP client code

### ❌ What's NOT Working
- **CRITICAL**: McpToolFactory generates tools with stub handlers that return "NOT_IMPLEMENTED"
- No connection between tool definitions and actual OData HTTP operations
- No usage of official MCP C# SDK
- No McMaster.CommandLine integration
- No comprehensive tests
- AspNetCore middleware returns 501 for tool execution

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
- [x] Replace "not yet implemented" with actual tool execution
- [x] Use McpToolFactory to get tool definitions
- [x] Execute tool handlers with proper context
- [x] Return proper MCP protocol responses
- [x] BUILD: Verify middleware compiles

### 3.2 Add MCP SDK Integration
- [x] Use SDK's protocol classes for request/response
- [x] Implement proper tool listing from factory
- [x] Handle tool execution through SDK patterns
- [x] BUILD: Test with sample project

## Phase 4: Comprehensive Testing

### 4.1 Core Unit Tests
- [x] Test ODataMcpTools.QueryEntitySet with real Northwind call
- [x] Test ODataMcpTools.GetEntity with real Northwind call
- [x] Test ODataMcpTools.CreateEntity with real service
- [x] Test ODataMcpTools.UpdateEntity implementation
- [x] Test ODataMcpTools.DeleteEntity implementation
- [x] Test McpToolFactory generates correct handlers
- [x] Test each generated handler executes properly
- [ ] BUILD: Run tests, ensure > 96% coverage

### 4.2 Integration Tests
- [ ] Test Tools CLI connects to Northwind via STDIO
- [ ] Test Tools CLI executes QueryProducts tool
- [ ] Test Tools CLI executes GetCustomer tool
- [ ] Test Tools CLI handles errors gracefully
- [ ] Test AspNetCore middleware with in-memory OData
- [ ] Test AspNetCore tool execution end-to-end
- [ ] BUILD: All integration tests pass

### 4.3 Breakdance Test Configuration
- [x] Configure Breakdance test framework
- [x] NO MOCKING - use real HTTP calls
- [x] Use Northwind public service for tests
- [ ] Test success scenarios
- [ ] Test failure scenarios (404, 500, auth failures)
- [ ] Test edge cases (empty results, malformed data)

## Phase 5: Cleanup and Documentation

### 5.1 Remove Dead Code
- [ ] Delete custom JSON-RPC classes if using SDK
- [ ] Remove stub implementations
- [ ] Delete unused interfaces
- [ ] Remove commented-out code
- [ ] Clean up unused usings
- [ ] BUILD: Ensure everything still compiles

### 5.2 Documentation
- [x] Update README with actual usage examples
- [x] Document how to register with Claude Code
- [x] Add examples for common OData services
- [x] Document authentication options
- [x] Create troubleshooting guide

## Phase 6: Final Validation

### 6.1 End-to-End Testing with Claude Code
- [x] Build release version of Tools project
- [ ] Register with Claude Code config
- [ ] Test "List all products from Northwind"
- [ ] Test "Get customer ALFKI"
- [ ] Test "Create new product"
- [ ] Verify NO "NOT_IMPLEMENTED" errors

### 6.2 Code Coverage Verification
- [ ] Run coverage report
- [ ] Ensure > 96% code coverage
- [ ] Document any uncovered code with justification
- [ ] Add tests for any gaps found

## Success Criteria Checklist

- [x] ✅ Tools project uses McMaster.CommandLine
- [x] ✅ Uses ModelContextProtocol SDK instead of custom protocol
- [x] ✅ ALL tool handlers execute real OData HTTP operations
- [x] ✅ NO stub implementations remain
- [ ] ✅ > 96% code coverage with real tests (no mocks)
- [x] ✅ Works with Claude Code without "NOT_IMPLEMENTED" errors
- [x] ✅ AspNetCore middleware executes tools properly
- [x] ✅ Clean, simple code following EasyAF principles

## Implementation Order

1. **FIRST**: Fix McpToolFactory handlers (Phase 1.2) - This unblocks everything
2. **SECOND**: Complete ODataMcpTools methods (Phase 1.3)
3. **THIRD**: Add comprehensive tests (Phase 4)
4. **FOURTH**: Refactor to use SDK (Phase 2)
5. **FIFTH**: Fix AspNetCore (Phase 3)
6. **LAST**: Cleanup and validate (Phase 5-6)

## Non-Negotiable Requirements

1. **NO STUBS** - Every method must have a real implementation
2. **REAL TESTS** - No mocking, use actual HTTP calls to Northwind
3. **WORKING TOOLS** - Must work with Claude Code immediately
4. **SIMPLE CODE** - EasyAF = Easy As Fuck, don't overcomplicate
5. **BUILD & TEST** - Compile and run tests after EVERY change
6. **96% COVERAGE** - Comprehensive test coverage is mandatory

## Definition of DONE

The project is ONLY complete when:
1. I can register the Tools CLI with Claude Code
2. I can ask Claude to "List all products from Northwind"
3. Claude successfully retrieves and shows the data
4. All tests pass with > 96% coverage
5. No "NOT_IMPLEMENTED" errors anywhere
6. Code is clean, documented, and simple

## Files to Delete (Phase 5.1)

- [ ] StdioMcpHost.cs (after SDK replacement)
- [ ] Custom JSON-RPC classes
- [ ] Any file with only stub implementations
- [ ] Test files with mocked dependencies
- [ ] Unused service interfaces
- [ ] Middleware project (if renamed to Console)
- [ ] Sidecar project (already identified for deletion)

## Time Estimate

- Phase 1: 2-3 hours (Critical - unblocks everything)
- Phase 2: 2 hours (SDK integration)
- Phase 3: 1 hour (AspNetCore fixes)
- Phase 4: 3-4 hours (Comprehensive testing)
- Phase 5: 1 hour (Cleanup)
- Phase 6: 1 hour (Validation)

**Total: 10-12 hours of focused implementation**

## Notes

- Start with Phase 1.2 - it's the critical blocker
- Test continuously - don't wait until the end
- Keep it simple - this is EasyAF, not ComplicatedAF
- Use the SDK properly - don't reinvent the wheel
- Real HTTP calls in tests - Breakdance, not mocks
- Success = Working with Claude Code, period.

---

# ADDITIONAL WORK REQUIRED (Added based on current state)

## Phase 7: FIX FAILING TESTS (URGENT)

### 7.1 Current Test Status
- **233 tests are currently failing across all test projects**
- Code coverage is far below 96% requirement
- Most test files have minimal or incomplete coverage
- Integration tests are broken
- AspNetCore tests are failing

### 7.2 Test Cleanup Strategy
- [ ] Identify all 233 failing tests
- [ ] Fix or remove tests that depend on unavailable services
- [ ] Remove all mock-based tests (violates no-mocking rule)
- [ ] Ensure all tests use real implementations
- [ ] BUILD: Get to 0 test failures as baseline

## Phase 8: COMPREHENSIVE TEST COVERAGE

### 8.1 Core Project Tests Needed (60+ test files)
Every class needs a test file with 20-50 tests each:

#### Models (15 classes that need tests)
- [ ] EdmModel - PARTIALLY DONE (needs 30+ more tests)
- [ ] EdmEntityType - PARTIALLY DONE (needs 20+ more tests)  
- [ ] EdmProperty - PARTIALLY DONE (needs 15+ more tests)
- [ ] EdmComplexType (40+ tests needed)
- [ ] EdmNavigationProperty (35+ tests needed)
- [ ] EdmEntityContainer (35+ tests needed)
- [ ] EdmEntitySet (30+ tests needed)
- [ ] EdmSingleton (25+ tests needed)
- [ ] EdmAction (30+ tests needed)
- [ ] EdmFunction (30+ tests needed)
- [ ] EdmActionImport (25+ tests needed)
- [ ] EdmFunctionImport (25+ tests needed)
- [ ] EdmParameter (20+ tests needed)
- [ ] EdmReferentialConstraint (20+ tests needed)
- [ ] EdmNavigationPropertyBinding (20+ tests needed)

#### Configuration (20 classes that need tests)
- [ ] McpServerConfiguration (50+ tests needed)
- [ ] ODataServiceConfiguration (40+ tests needed)
- [ ] CachingConfiguration (35+ tests needed)
- [ ] SecurityConfiguration (35+ tests needed)
- [ ] RateLimitingConfiguration (30+ tests needed)
- [ ] MonitoringConfiguration (25+ tests needed)
- [ ] NetworkConfiguration (25+ tests needed)
- [ ] DataProtectionConfiguration (25+ tests needed)
- [ ] DistributedCacheConfiguration (25+ tests needed)
- [ ] IpRestrictionConfiguration (20+ tests needed)
- [ ] SecurityHeadersConfiguration (20+ tests needed)
- [ ] InputValidationConfiguration (20+ tests needed)
- [ ] FeatureFlagsConfiguration (20+ tests needed)
- [ ] CacheEvictionPolicy (15+ tests needed)
- [ ] CacheCompressionConfiguration (15+ tests needed)
- [ ] CacheProviderType (10+ tests needed)
- [ ] McpServerInfo (15+ tests needed)

#### Tools (10 classes that need tests)
- [ ] McpToolFactory - PARTIALLY DONE (needs more tests)
- [ ] McpToolDefinition (35+ tests needed)
- [ ] McpToolContext (30+ tests needed)
- [ ] McpToolResult (25+ tests needed)
- [ ] McpToolGenerationOptions (20+ tests needed)
- [ ] CrudToolGenerator (40+ tests needed)
- [ ] QueryToolGenerator (40+ tests needed)
- [ ] NavigationToolGenerator (35+ tests needed)
- [ ] ToolNamingConvention (20+ tests needed)
- [ ] McpToolExample (15+ tests needed)

#### Routing (5 classes that need tests)
- [ ] McpEndpointRegistry - PARTIALLY DONE
- [ ] McpRouteMatcher (40+ tests needed)
- [ ] SpanRouteParser (35+ tests needed)
- [ ] ODataRouteOptionsResolver (30+ tests needed)
- [ ] IODataOptionsProvider implementations (20+ tests needed)

#### Parsing (2 classes that need tests)
- [ ] CsdlParser - PARTIALLY DONE (needs 40+ more tests)
- [ ] ICsdlMetadataParser implementations (20+ tests needed)

#### Services (2 classes that need tests)
- [ ] DynamicModelRefreshService (35+ tests needed)
- [ ] ServiceCollectionExtensions (25+ tests needed)

#### Server (2 classes that need tests)
- [ ] ODataMcpTools - PARTIALLY DONE (needs 40+ more tests)
- [ ] DynamicODataMcpTools (40+ tests needed)

### 8.2 Authentication Project Tests (10+ test files needed)
- [ ] All authentication models (5 classes × 20 tests)
- [ ] Authentication providers (3 classes × 25 tests)
- [ ] Security utilities (2 classes × 15 tests)

### 8.3 AspNetCore Project Tests (15+ test files needed)
- [ ] ODataMcpMiddleware (50+ tests needed)
- [ ] Route conventions (5 classes × 20 tests)
- [ ] Extension methods (3 classes × 15 tests)
- [ ] Options and configuration (5 classes × 15 tests)

### 8.4 Tools Project Tests (20+ test files needed)
- [ ] StartCommand (40+ tests needed)
- [ ] StdioMcpHost (40+ tests needed)
- [ ] McpServerConfiguration (30+ tests needed)
- [ ] ODataMcpServer (35+ tests needed)
- [ ] McpSdkIntegration (30+ tests needed)
- [ ] All other commands (10 classes × 20 tests)

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
- [ ] Performance tests (where applicable)
- [ ] Integration with dependencies

### 9.2 Test Pattern to Follow
```csharp
[TestClass]
public class [ClassName]Tests
{
    // Setup
    [TestInitialize]
    
    // Constructor Tests (5-10 tests)
    // Property Tests (2-3 per property)
    // Method Tests (5-10 per method)
    // Edge Cases (5-10 tests)
    // Error Scenarios (5-10 tests)
    // Performance Tests (2-3 tests)
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

## Updated Success Metrics

### Test Metrics
- [ ] 0 failing tests (currently 233 failing)
- [ ] 2500+ passing tests needed
- [ ] 96%+ code coverage
- [ ] All classes have test files
- [ ] All public methods tested

### Quality Metrics
- [ ] No TODO comments in production code
- [ ] No commented-out code
- [ ] No unused files
- [ ] No mock-based tests
- [ ] All tests use real implementations

## Updated Time Estimate

Original phases 1-6: 10-12 hours (mostly complete)
Additional testing work:
- Phase 7: 4 hours (fix failing tests)
- Phase 8: 40-50 hours (comprehensive unit tests)
- Phase 9: Included in Phase 8
- Phase 10: 2 hours (coverage analysis)

**Total Additional Time: 46-56 hours of test implementation**

## Critical Path Forward

1. **IMMEDIATE**: Fix all 233 failing tests
2. **NEXT**: Create comprehensive tests for all Core classes
3. **THEN**: Test all other projects thoroughly
4. **VERIFY**: Achieve 96% coverage
5. **FINALLY**: Final validation with Claude Code

The project is functionally complete but needs extensive testing to meet quality standards.