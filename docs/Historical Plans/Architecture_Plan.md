# OData MCP Architecture Reorganization Plan

## Executive Summary

This plan outlines the reorganization of the OData MCP project to clearly separate two distinct deployment scenarios:
- **Concept A**: Embedded MCP server running in the same API as the OData endpoint (lightweight, zero-configuration)
- **Concept B**: Standalone console-based MCP server that connects to external OData APIs

## Project Structure

### Current Structure (To Be Changed)
```
Microsoft.OData.Mcp/
├── src/
│   ├── Microsoft.OData.Mcp.Core/
│   ├── Microsoft.OData.Mcp.AspNetCore/
│   ├── Microsoft.OData.Mcp.Middleware/
│   ├── Microsoft.OData.Mcp.Sidecar/        # TO BE DELETED
│   └── Microsoft.OData.Mcp.Authentication/
```

### Target Structure
```
Microsoft.OData.Mcp/
├── src/
│   ├── Microsoft.OData.Mcp.Core/           # Shared functionality
│   ├── Microsoft.OData.Mcp.AspNetCore/     # Concept A: Embedded scenario
│   ├── Microsoft.OData.Mcp.Console/        # Concept B: Standalone CLI (renamed from Middleware)
│   └── Microsoft.OData.Mcp.Authentication/ # Shared auth components
├── samples/
│   ├── Microsoft.OData.Mcp.Sample/         # Demonstrates Concept A
│   └── Microsoft.OData.Mcp.Console.Sample/ # Demonstrates Concept B (new)
└── tests/
```

## Implementation Phases

### Phase 1: Delete Sidecar Project

**TODO:**
- [ ] Remove `Microsoft.OData.Mcp.Sidecar` project from solution
- [ ] Remove all project references to Sidecar from:
  - [ ] Microsoft.OData.Mcp.Sample.csproj
  - [ ] Any other projects referencing it
- [ ] Delete the Sidecar project directory
- [ ] **COMPILE CHECK**: Build solution to ensure no broken references

**Code to Salvage from Sidecar:**
- [ ] Move `StartupValidationService` to Console project
- [ ] Move `MetadataDiscoveryService` to Console project
- [ ] Move `ServiceInformationService` to Console project
- [ ] Extract useful health check implementations

### Phase 2: Reorganize Core Project

**Microsoft.OData.Mcp.Core** will contain only shared functionality.

**TODO:**
- [ ] Move `ODataMcpMiddleware` from Core to AspNetCore project
- [ ] **COMPILE CHECK**: Build Core project
- [ ] Clean up unused using statements and references
- [ ] Remove any AspNetCore-specific dependencies from Core

**Keep in Core:**
- `/Routing/` - High-performance route parsing
- `/Tools/` - Tool generation and caching
- `/Metadata/` - OData metadata processing
- `/Configuration/` - Shared configuration models (non-middleware specific)

**Clean Up:**
- [ ] Remove `/Middleware/` directory after moving ODataMcpMiddleware
- [ ] Review and remove any unused interfaces
- [ ] Ensure all remaining code is truly shared between both concepts

### Phase 3: Clean Up AspNetCore Project (Concept A)

**TODO:**
- [ ] Move `ODataMcpMiddleware` from Core to AspNetCore
- [ ] Update namespace from `Microsoft.OData.Mcp.Core.Middleware` to `Microsoft.OData.Mcp.AspNetCore.Middleware`
- [ ] **COMPILE CHECK**: Build AspNetCore project
- [ ] Remove any standalone/sidecar-specific code
- [ ] Clean up unused dependencies
- [ ] Ensure minimal footprint for embedded scenario

**Verify:**
- [ ] `AddODataMcp()` extension method works correctly
- [ ] Automatic route discovery functions properly
- [ ] No unnecessary overhead for embedded scenario

### Phase 4: Transform Middleware to Console Project (Concept B)

**TODO:**
- [ ] Rename project from `Microsoft.OData.Mcp.Middleware` to `Microsoft.OData.Mcp.Console`
- [ ] Update all namespaces from `*.Middleware` to `*.Console`
- [ ] **COMPILE CHECK**: Build after rename
- [ ] Transform `McpServerMiddleware` into standalone `McpServer` class
- [ ] Remove ASP.NET Core middleware dependencies where not needed
- [ ] Add command-line parsing infrastructure

**New CLI Features to Implement:**
- [ ] Command: `start` - Start MCP server for external OData service
- [ ] Option: `--port` - Specify server port
- [ ] Option: `--auth-token` - Provide authentication token
- [ ] Option: `--config` - Specify configuration file
- [ ] Option: `--verbose` - Enable detailed logging

**Code Migration:**
- [ ] Move relevant code from Sidecar:
  - [ ] `StartupValidationService`
  - [ ] `MetadataDiscoveryService`
  - [ ] `ConfigurationValidationService`
- [ ] **COMPILE CHECK**: Build Console project after each major migration

### Phase 5: Update Sample Projects

**Microsoft.OData.Mcp.Sample (Existing)**
- [ ] Remove Sidecar project reference
- [ ] Remove Middleware project reference
- [ ] **COMPILE CHECK**: Build Sample project
- [ ] Verify in-memory OData API works
- [ ] Verify MCP endpoints are automatically available
- [ ] Update README to explain Concept A usage

**Microsoft.OData.Mcp.Console.Sample (New)**
- [ ] Create new console sample project
- [ ] Add examples connecting to public OData APIs
- [ ] Include various authentication examples
- [ ] Add Docker compose configuration
- [ ] Create scripts for common scenarios
- [ ] **COMPILE CHECK**: Build new sample project

### Phase 6: Fix Unit Tests

**TODO for Each Test Project:**
- [ ] Update project references after reorganization
- [ ] Fix namespace changes
- [ ] **COMPILE CHECK**: Build each test project
- [ ] Run all tests and fix failures
- [ ] Remove tests for deleted code
- [ ] Add tests for new Console functionality

**Test Projects to Update:**
- [ ] Microsoft.OData.Mcp.Tests.Core
- [ ] Microsoft.OData.Mcp.Tests.AspNetCore
- [ ] Microsoft.OData.Mcp.Tests.Console (rename from Middleware)
- [ ] Microsoft.OData.Mcp.Tests.Authentication

### Phase 7: Clean Up Unused Code

**TODO:**
- [ ] Run code analysis to find unused methods/classes
- [ ] Remove redundant interfaces
- [ ] Consolidate duplicate implementations
- [ ] Remove unnecessary NuGet packages
- [ ] **COMPILE CHECK**: After each cleanup pass

**Areas to Review:**
- [ ] Authentication - remove sidecar-specific auth code
- [ ] Configuration - consolidate config models
- [ ] Services - remove duplicate service implementations
- [ ] Middleware - ensure no overlap between AspNetCore and Console

### Phase 8: Documentation Updates

**TODO:**
- [ ] Update main README.md with new architecture
- [ ] Create separate README for each project
- [ ] Document Concept A usage and benefits
- [ ] Document Concept B usage and benefits
- [ ] Update API documentation
- [ ] Create migration guide for existing users

## Code Migration Matrix

| Component | Current Location | New Location | Action | Priority |
|-----------|-----------------|--------------|--------|----------|
| ODataMcpMiddleware | Core/Middleware | AspNetCore/Middleware | Move | High |
| McpServerMiddleware | Middleware/Middleware | Console/Server | Refactor | High |
| IMetadataDiscoveryService | Middleware/Services | Core/Services | Move | High |
| MetadataDiscoveryService | Sidecar/Services | Console/Services | Move | High |
| StartupValidationService | Sidecar/Services | Console/Services | Move | Medium |
| ConfigurationValidationService | Sidecar/Services | Console/Services | Move | Medium |
| Health Checks | Middleware/HealthChecks | Console/HealthChecks | Move | Low |
| McpMiddlewareOptions | Middleware/Configuration | Console/Configuration | Rename | High |

## Compilation Checkpoints

**Critical Compilation Points:**
1. After deleting Sidecar project
2. After moving ODataMcpMiddleware
3. After renaming Middleware to Console
4. After updating all namespaces
5. After fixing project references
6. After each test project update
7. Final full solution build

**Build Commands to Run:**
```bash
# After each major change
dotnet build --configuration Debug

# Full solution build
dotnet build Microsoft.OData.Mcp.sln --configuration Debug

# Run tests after fixes
dotnet test --configuration Debug
```

## Success Criteria

1. **Clean Separation**: No overlapping functionality between AspNetCore and Console projects
2. **Compilation**: Solution builds without errors or warnings
3. **Tests Pass**: All unit tests pass after reorganization
4. **Samples Work**: Both sample projects demonstrate their respective concepts
5. **No Dead Code**: All unused code has been removed
6. **Clear Purpose**: Each project has a single, clear responsibility

## Risk Mitigation

1. **Frequent Compilation**: Compile after each significant change to catch issues early
2. **Git Commits**: Commit after each successful phase completion
3. **Backup**: Keep note of any complex code before deletion
4. **Test Coverage**: Ensure tests cover migrated functionality
5. **Documentation**: Update docs as you go, not at the end

## Next Steps After Completion

1. Performance testing of both deployment scenarios
2. Security review of authentication flows
3. Create NuGet packages for distribution
4. Write blog post explaining the architecture
5. Create video tutorials for both concepts