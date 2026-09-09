# Specs

## Authoritative specification

**→ [v3/](./v3/)** — OData MCP Platform v3 (MCP `2026-07-28` / SDK 2.x)

| Document | Purpose |
|----------|---------|
| [v3/README.md](./v3/README.md) | Index and hard rules |
| [v3/CRITICAL-ANALYSIS.md](./v3/CRITICAL-ANALYSIS.md) | Autopsy of the dual prototypes |
| [v3/ARCHITECTURE.md](./v3/ARCHITECTURE.md) | Packages, hosts, public API |
| [v3/PROTOCOL.md](./v3/PROTOCOL.md) | MCP 2 resources, tools, discovery |
| [v3/METADATA-STRATEGY.md](./v3/METADATA-STRATEGY.md) | Custom EDM, `IEdmModel` adapter, docs |
| [v3/TOOL-SURFACE.md](./v3/TOOL-SURFACE.md) | Generic + named tools, `shutdown_server` |
| [v3/TYPE-SHAPES.md](./v3/TYPE-SHAPES.md) | Declaration grammar and compact JSON for types and models |
| [v3/OPTIMIZATION.md](./v3/OPTIMIZATION.md) | Instructions, descriptions, schemas, pre-HTTP validation, baselines |
| [v3/OPTIMIZATION-PLAN.md](./v3/OPTIMIZATION-PLAN.md) | Task list that implemented the optimization spec |
| [v3/OPTIMIZATION-REPORT.md](./v3/OPTIMIZATION-REPORT.md) | Token measurements per round |
| [v3/ODATA-HOSTING.md](./v3/ODATA-HOSTING.md) | Web MCP inside ASP.NET Core OData apps |
| [v3/AUTHENTICATION.md](./v3/AUTHENTICATION.md) | Outbound OAuth and protected-resource metadata |
| [v3/TESTING.md](./v3/TESTING.md) | Test strategy: real HTTP, no mocks |
| [v3/TOOL-TEST-MANIFEST.md](./v3/TOOL-TEST-MANIFEST.md) | Per-tool contract and test inventory |
| [v3/INVENTORY.md](./v3/INVENTORY.md) | Concept vs delete for every prior type |
| [v3/EXECUTION-PLAN.md](./v3/EXECUTION-PLAN.md) | Agent task list for the v3 build |

Do **not** implement from documents outside `v3/`. Earlier planning material was removed from the repository; it remains in history before this folder was trimmed, and [v3/CRITICAL-ANALYSIS.md](./v3/CRITICAL-ANALYSIS.md) records what was learned from it.
