# Legacy Code Archive

This folder contains code that was part of an earlier design iteration but is no longer used in the active system. The code has been moved here rather than deleted to preserve the design decisions and potentially valuable features for future reference.

## Contents

### Unused Tool Generation System
- **McpTool.cs** - Model class for tool metadata (no execution capability)
- **Generators/** - Tool generator classes that create metadata-only tools:
  - `CrudToolGenerator.cs` - Generates CRUD operation tool metadata
  - `QueryToolGenerator.cs` - Generates query tool metadata  
  - `NavigationToolGenerator.cs` - Generates navigation tool metadata
  - `*ToolGenerationOptions.cs` - Configuration classes for generators
  - `ToolNamingConvention.cs` - Enum for naming conventions

### Documentation
- **Features-To-Port-Analysis.md** - Analysis of valuable features that could be ported to the active system

## Why This Code is Legacy

This tool generation system was an earlier design that separated tool metadata from execution handlers. The fundamental limitation is that the `McpTool` class has no way to execute operations - it only contains:
- Name
- Description  
- InputSchema
- Metadata

The active system (`McpToolFactory`) creates `McpToolDefinition` objects that include both metadata AND executable handlers, making them actually functional.

## Status

- **Namespaces Updated**: All classes now use `Microsoft.OData.Mcp.Core.Legacy.*` namespaces
- **DI Registrations Removed**: No longer registered in dependency injection
- **References Removed**: No active code references these classes
- **Build Status**: Solution builds successfully without this code

## Features Worth Porting

See `Features-To-Port-Analysis.md` for a detailed analysis of valuable features from this system that don't exist in the active `McpToolFactory`, including:

1. **Naming Convention Support** (PascalCase, camelCase, snake_case, kebab-case)
2. **Property-Level Exclusion** (hide sensitive fields)
3. **Pre-Built Configuration Profiles** (HighSecurity, Development, etc.)
4. **Max Properties Per Tool** (complexity limiting)
5. **Query Options Granularity** (fine control over OData features)

## DO NOT

- **DO NOT** try to use these classes - they cannot execute operations
- **DO NOT** re-register them in DI - they're incomplete
- **DO NOT** copy code directly - port concepts to the active system instead

## Future Action

When implementing new features in `McpToolFactory`, review the configuration options and design patterns in this legacy code for inspiration, but implement fresh in the context of the working system.