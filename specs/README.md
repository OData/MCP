# Specs

## Authoritative specification (current)

**→ [v3/](./v3/)** — OData MCP Platform v3 (MCP `2026-07-28` / SDK 2.x)

| Document | Purpose |
|----------|---------|
| [v3/README.md](./v3/README.md) | Index and hard rules |
| [v3/CRITICAL-ANALYSIS.md](./v3/CRITICAL-ANALYSIS.md) | Autopsy of the dual prototypes |
| [v3/ARCHITECTURE.md](./v3/ARCHITECTURE.md) | Packages, hosts, public API |
| [v3/PROTOCOL.md](./v3/PROTOCOL.md) | MCP 2 resources, tools, discovery |
| [v3/METADATA-STRATEGY.md](./v3/METADATA-STRATEGY.md) | Custom EDM, `IEdmModel` adapter, docs |
| [v3/TOOL-SURFACE.md](./v3/TOOL-SURFACE.md) | Generic + named tools, `shutdown_server` |
| [v3/INVENTORY.md](./v3/INVENTORY.md) | Concept vs delete for every current type |
| [v3/EXECUTION-PLAN.md](./v3/EXECUTION-PLAN.md) | Agent task list |

Do **not** implement from documents outside `v3/`.

---

## Historical material (non-authoritative)

Everything else in this folder is archaeology:

- **E0 product fantasy** — root `getting-started.md`, `configuration.md`, `api-reference.md`, etc.
- **E1 magic REST** — `MAGICAL_ZERO_CONFIG.md` (intent survived; REST wire did not)
- **E2–E3 fix-it journals** — `COMPLETION_PLAN.md`, `DYNAMIC_TOOL_CLEANUP.md`
- **E4** — `NEW/Purpose.md` (quality bar still holds)

See [v3/CRITICAL-ANALYSIS.md](./v3/CRITICAL-ANALYSIS.md).
