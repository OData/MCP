# Tool Surface

Agents succeed or fail on the catalog. One builder. Two tool families. The graph itself is resources ([PROTOCOL.md](./PROTOCOL.md)).

---

## 1. Goals

1. Predictable names.
2. Bounded context: 200 entity sets must not emit 1,000 tools.
3. Self-describing: CSDL docs in `title`/`description`; no `$` on query params.
4. Every listed tool executes real OData HTTP (except `shutdown_server`, which executes host lifetime).
5. Single pipeline.

---

## 2. Generic tools (always)

| Name | Title | Verb hints | Purpose |
|------|-------|------------|---------|
| `odata_list_entity_sets` | List entity sets | readOnly | Names, types, keys from the model |
| `odata_describe_type` | Describe type | readOnly | Properties, navs, keys, bound operations, enums ([TYPE-SHAPES.md](./TYPE-SHAPES.md)) |
| `odata_describe_model` | Describe model | readOnly | Whole-service map (`summary`) or complete dump; never EDMX |
| `odata_query` | Query entity set | readOnly | `filter`, `select`, `orderby`, `expand`, `top`, `skip`, `count` |
| `odata_get` | Get entity | readOnly | By key; optional `select`, `expand` |
| `odata_create` | Create entity | | POST JSON |
| `odata_update` | Update entity | idempotent | PATCH JSON |
| `odata_delete` | Delete entity | destructive, idempotent | DELETE by key |
| `odata_navigate` | Navigate | readOnly | Follow a nav from a key; same query options as `odata_query` |
| `odata_list_operations` | List operations | readOnly | **Unbound** operations only |
| `odata_call` | Call operation | | `name` + `parameters` object ([OPTIMIZATION.md](./OPTIMIZATION.md) §3) |

Do not register `odata_call_function` / `odata_call_action`. Descriptions and schemas: [OPTIMIZATION.md](./OPTIMIZATION.md) §2–3.

Prefer a resource URI or `entitySet` name as the target. Executor resolves `odata://…` or a bare set name.

---

## 3. Named tools (capped)

Per entity set, snake_case:

| Operation | Pattern | Example |
|-----------|---------|---------|
| List/query | `list_{set}` | `list_products` |
| Get | `get_{singular}` | `get_product` |
| Create | `create_{singular}` | `create_product` |
| Update | `update_{singular}` | `update_product` |
| Delete | `delete_{singular}` | `delete_product` |

Navigation is generic `odata_navigate` plus navigations on the type shape. Do not register `list_{set}_{nav}`.

Default `MaxNamedTools` = 150 including generics. Fill `IncludeEntitySets` first, then remaining sets alphabetically. Never emit a partial CRUD family for a set (if `list_products` is in, `get_product` is in for the enabled verbs).

Sets that do not get named tools remain fully usable via resources + generic tools.

---

## 4. Forbidden

| Pattern | Why |
|---------|-----|
| Two pipelines (`WithODataTools` + factory) | Historical poison |
| Full generic CRUD **and** named CRUD for every set | Same poison |
| Multiple naming dialects | Models thrash |
| Metadata-only tools | Legacy `McpTool` |
| `queryCustomers` / `Customer.query` | Dead names |
| `shutdown_server` on AspNetCore | Local Tools only |

---

## 5. Local Tools: `shutdown_server`

Always registered by `odata-mcp`.

| | |
|--|--|
| Name | `shutdown_server` |
| Title | Shut down MCP server |
| Params | `reason` (string, optional), `delay_seconds` (int, 0–10, default 2) |
| Hints | `readOnlyHint: false`, `destructiveHint: true`, `idempotentHint: false` |
| Behavior | Return a JSON ack, then cancel the host after the delay so the MCP response flushes |

This is a development control-plane tool. It is part of the local server spec.

---

## 6. Query parameters

No `$` in tool args. Executor adds `$filter`, `$select`, `$orderby`, `$expand`, `$top`, `$skip`, `$count`.

### 6.1 Why the executor always sends `$`

ASP.NET Core OData's `ODataOptions.EnableNoDollarQueryOptions` (and the matching `ODataUriParser` setting in Microsoft.OData.Core) makes the `$` prefix **optional**; it never turns `$` off. Verified 2026-09-09 against a Kestrel host on Microsoft.AspNetCore.OData 8.3.1 with three rows:

| Request | `EnableNoDollarQueryOptions = true` | `EnableNoDollarQueryOptions = false` |
|---|---|---|
| `?$top=1` | 200, one row | 200, one row |
| `?top=1` | 200, one row | **200, all three rows** |
| `?$filter=Id eq 2` | 200, row 2 | 200, row 2 |
| `?filter=Id eq 2` | 200, row 2 | **200, all three rows** |
| `?$top=1&filter=Id eq 3` | 200, row 3 | 200, row 1 |

The default is `true` (`new ODataOptions().EnableNoDollarQueryOptions`). The failure mode when it is `false` is silent: a bare option is **ignored with a 200**, not rejected with a 400, so the caller gets an unpaged or unfiltered result and no signal.

Consequences for this repo:

- `$` is the one form every OData server accepts, so both executors (`RemoteODataExecutor`, `InProcessODataExecutor`) always emit `$`-prefixed system query options. Neither reads `ODataOptions` and neither should: there is nothing to gain from emitting bare names and a silent-ignore failure to lose.
- The `$` prefixing lives only in the executors' `BuildRelativeUri`. Nothing else in Core or the hosts constructs system query options.
- OData **parameter aliases** (`@p`) are not system query options. `odata_call` puts them in the operation path (`Find(at=@at)?@at=<escaped json>`) and never routes them through query options, so they never receive a `$` prefix under either setting.
- Tool arguments stay `$`-free (`filter`, not `$filter`). A `$filter` key passed to a tool is not a query option; on create and update it is an unknown property and fails before HTTP ([TOOL-TEST-MANIFEST.md](./TOOL-TEST-MANIFEST.md) §3.1).

`key` is a string (OData parenthetical form for composites) and every `key` argument carries a description of the literal form ([OPTIMIZATION.md](./OPTIMIZATION.md) §3). Named create/update: JSON object of **declared** properties ([TYPE-SHAPES.md](./TYPE-SHAPES.md) §7). Generic create/update: `body` object (the runtime also accepts a string, the schema does not advertise it). `odata_call`: `parameters` object ([OPTIMIZATION.md](./OPTIMIZATION.md) §3). Never emit a property that is not on the EDM. Exclude binary/stream from generated schemas.

---

## 7. Descriptions

Normative copy: [OPTIMIZATION.md](./OPTIMIZATION.md) §2. CSDL docs when present; omit when absent. Do not invent a description from the member name.

---

## 8. Execution

```
tools/call → catalog handler → IOdataExecutor → structuredContent + short text
```

Honor `CancellationToken`. Size-guard responses; suggest `$select` / `$top` when over limit.

---

## 9. Multi-route

One MCP endpoint per OData prefix. Tool names need no route prefix. CLI uses a single `remote` route.

---

## 10. Server instructions

Normative copy and preface rules: [OPTIMIZATION.md](./OPTIMIZATION.md) §1. This file does not own the string.
