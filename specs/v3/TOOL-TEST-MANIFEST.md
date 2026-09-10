# Tool Test Manifest — OData MCP Platform v3

**Status:** Living (authoritative)  
**Revised:** 2026-09-09  
**Scope:** Every MCP tool this product registers, plus the non-tool MCP handlers those tools depend on.  
**This file is a test design spec.** Do not implement product code from it. Do not invent tools that are not registered. Do not skip tools that are registered.

Optimization status per [OPTIMIZATION-PLAN.md](./OPTIMIZATION-PLAN.md): every contract below is the **optimized** one — `odata_describe_type`, the `resources/read` type card, `odata_describe_model`, `odata_list_operations` (unbound only), named `create_*`/`update_*` schemas (typed, `required`), `outputSchema` on the two list tools, `odata_call` with a `parameters` object, pre-HTTP EdmType validation on create, update, and call, `initialize.instructions` from `ODataMcpInstructions`, and the tool description copy of [OPTIMIZATION.md](./OPTIMIZATION.md) §2. Target contracts: [TYPE-SHAPES.md](./TYPE-SHAPES.md), [OPTIMIZATION.md](./OPTIMIZATION.md). Update this file in the same PR as the catalog change.

Grounded in:

- `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpCatalog.cs` (`BuildGenericTools`, `BuildNamedFamily`, `BuildTools`)
- `src/Microsoft.OData.Mcp.Core/Catalog/ODataToolRuntime.cs` (`InvokeAsync` switch + `InvokeNamedAsync`)
- `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpCatalogOptions.cs`
- `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpHandlerExtensions.cs`
- `src/Microsoft.OData.Mcp.Core/Execution/RemoteODataExecutor.cs`
- `src/Microsoft.OData.Mcp.AspNetCore/Execution/InProcessODataExecutor.cs`
- `src/Microsoft.OData.Mcp.Tools/Hosting/ToolsMcpHost.cs` (`shutdown_server`)
- `specs/v3/TOOL-SURFACE.md`, `TESTING.md`, `PROTOCOL.md`, `ODATA-HOSTING.md`

---

## Table of contents

1. [How to use this manifest](#1-how-to-use-this-manifest)
2. [Registered tools (source of truth)](#2-registered-tools-source-of-truth)
3. [Global rules every case inherits](#3-global-rules-every-case-inherits)
4. [Hosts, surfaces, and fixtures](#4-hosts-surfaces-and-fixtures)
5. [Shared assertion helpers](#5-shared-assertion-helpers)
6. [`odata_list_entity_sets`](#6-odata_list_entity_sets)
7. [`odata_describe_type`](#7-odata_describe_type)
7.1. [`odata_describe_model`](#71-odata_describe_model)
8. [`odata_query`](#8-odata_query)
9. [`odata_get`](#9-odata_get)
10. [`odata_create`](#10-odata_create)
11. [`odata_update`](#11-odata_update)
12. [`odata_delete`](#12-odata_delete)
13. [`odata_navigate`](#13-odata_navigate)
14. [`odata_list_operations`](#14-odata_list_operations)
15. [`odata_call`](#15-odata_call)
16. [Named `list_{set}`](#16-named-list_set)
17. [Named `get_{type}`](#17-named-get_type)
18. [Named `create_{type}`](#18-named-create_type)
19. [Named `update_{type}`](#19-named-update_type)
20. [Named `delete_{type}`](#20-named-delete_type)
21. [`shutdown_server` (Tools host only)](#21-shutdown_server-tools-host-only)
22. [MCP protocol handlers (not tools)](#22-mcp-protocol-handlers-not-tools)
23. [Combinatorial / matrix](#23-combinatorial--matrix)
24. [Gaps vs current tests](#24-gaps-vs-current-tests)
25. [Case index (counts)](#25-case-index-counts)

---

## 1. How to use this manifest

Each case name is a proposed `[TestMethod]`. Implementers copy the name, put it in the project named in **Project**, and assert the **Expect** column against a real OData HTTP twin when one is listed.

A case that lists multiple hosts or surfaces is **N implementations**, not one parameterized skip. Example: `OdataQuery_OmitTop_DoesNotInjectDefaultTop` on AspNetCore×OData8, AspNetCore×Restier, Tools×Northwind, and Tools×TripPin is four methods (or one theory that still runs all four cells and fails per cell).

**Never** collapse “the other tools work the same.” Named CRUD, `odata_navigate`, `odata_update`, `odata_delete`, `odata_call`, and JSON-RPC `tools/call` are first-class. `odata_query` coverage does not satisfy them.

`TOOL-SURFACE.md` mentions a named navigate pattern `list_{set}_{nav}` and Phase-2 names `odata_call_function` / `odata_call_action`. **Those names are not registered.** Catalog named families are only `list_*` / `get_*` / `create_*` / `update_*` / `delete_*`. Operations go through `odata_list_operations` + `odata_call`. Do not write tests that expect `list_products_category` or `odata_call_function` to appear in `tools/list`.

---

## 2. Registered tools (source of truth)

From `ODataMcpCatalog.BuildGenericTools` (always, first in `tools/list`):

| Name | Title | Hints | Input schema (no `$` on query keys) | Required |
|------|-------|-------|-------------------------------------|----------|
| `odata_list_entity_sets` | List entity sets | readOnly, idempotent | `{}` `additionalProperties:false` | none |
| `odata_describe_type` | Describe type | readOnly, idempotent | `{ name: string, format?: "text" \| "json" }` — `text` (default) returns the [TYPE-SHAPES.md](./TYPE-SHAPES.md) §1.1 declaration as `content[0].text` with **no** `structuredContent`; `json` returns the §1.2 compact object as `structuredContent` | `name` |
| `odata_describe_model` | Describe model | readOnly, idempotent | `{ detail?: "summary" \| "complete", format?: "text" \| "json" \| "mermaid", sets?: string[] }` — eleventh generic, registered right after `odata_describe_type` | none |
| `odata_query` | Query entity set | readOnly, idempotent | `entitySet`, `filter`, `select`, `orderby`, `expand`, `top` (number), `skip` (number), `count` (boolean) | `entitySet` |
| `odata_get` | Get entity | readOnly, idempotent | `entitySet`, `key` (described: `Key as text: ALFKI or 10248; strings are quoted for you. Composite: OrderID=10248,ProductID=11.`), `select`, `expand` | `entitySet`, `key` |
| `odata_create` | Create entity | (openWorld) | `entitySet`, `body` (`type: object` in schema, described `The entity as a JSON object, not a string.`; runtime also accepts a JSON string or remaining properties). When the set's type is declared and the body parses as a JSON object it is **validated before HTTP** (§10): required-on-create, unknown properties on closed types, JSON kind per EDM type, enum membership, `MaxLength`, `null` on non-nullable | `entitySet`, `body` (schema); runtime synthesizes `body` from leftover properties if omitted |
| `odata_update` | Update entity | idempotent | `entitySet`, `key` (same key description as `odata_get`), `body` (`type: object`, described `Changed properties as a JSON object, not a string.`). Same pre-HTTP validation as create minus the required-on-create check | `entitySet`, `key`, `body` |
| `odata_delete` | Delete entity | destructive, idempotent | `entitySet`, `key` (same key description as `odata_get`) | `entitySet`, `key` |
| `odata_navigate` | Navigate | readOnly, idempotent | `entitySet`, `key` (same key description as `odata_get`), `navigation`, plus `filter`, `select`, `orderby`, `expand`, `top` (number), `skip` (number), `count` (boolean) | `entitySet`, `key`, `navigation` |
| `odata_list_operations` | List operations | readOnly, idempotent | `{}` `additionalProperties:false` — returns **unbound** operations only as `{ operations: { Name: signature } }`, or `{}` when none | none |
| `odata_call` | Call operation | (openWorld) | `name`, `parameters` (JSON **object** keyed by declared parameter names; description ends `Omit when the operation takes none.`), `entitySet`, `key` (same key description as `odata_get`); `additionalProperties: false`. No `body`; any other top-level key is `Unexpected argument '{k}'. Put operation arguments in parameters as a JSON object.` | `name`; instance-bound also `entitySet`+`key`; collection-bound `entitySet` only; unbound neither |

From `ODataMcpCatalog.BuildNamedFamily` (only when `MaxNamedTools - genericCount` has room for the **entire** family; never a partial family):

| Name | Binds | Input schema | Required |
|------|-------|--------------|----------|
| `list_{set_snake}` | `EntitySetName` | same query options as `odata_query` minus `entitySet`. Description ends `Query parameter names do not include $.` plus `Filter enums as NS.Enum'{value}', ...` when the type exposes enum properties | none |
| `get_{type_snake}` | `EntitySetName` | `{ key, select?, expand? }` — `key.description` names the type's key: `CustomerID as text; strings are quoted for you.` or, for composites, `Name=value pairs: OrderID=...,ProductID=...; strings are quoted for you.` | `key` |
| `create_{type_snake}` | `EntitySetName` | JSON object of **declared non-binary/non-stream** properties (not a `body` string). Each property: JSON type (`["type","null"]` when nullable), `enum` member names for enum types (values when `EnumJsonFormat=Integer`), `items` for collections, `object` for complex, `maxLength` only when EDM `MaxLength ≤ 16`, `description` only from CSDL | `required` = required-on-create (non-nullable, no `DefaultValue`, not `Core.Computed`); omitted when empty. Runtime POSTs serialized leftover properties |
| `update_{type_snake}` | `EntitySetName` | `{ key }` + the same property map as create; no `body` | `key` only |
| `delete_{type_snake}` | `EntitySetName` | `{ key }` | `key` |

`create_*` / `update_*` / `delete_*` are omitted from the family when `IncludeCreate` / `IncludeUpdate` / `IncludeDelete` is `false`. The remaining members still ship as one family; the cap is checked against that reduced family size.

From `ToolsMcpHost` extra handler (Tools/local only, **not** in `ODataMcpCatalog.Tools`):

| Name | Title | Hints | Params |
|------|-------|-------|--------|
| `shutdown_server` | Shut down MCP server | destructive, not readOnly, not idempotent, openWorld false | `reason` (string, optional), `delay_seconds` (int 0–10, default **2**) |

`InvokeNamedAsync` dispatches by **prefix** (`list_`, `get_`, `create_`, `update_`, `delete_`) after a catalog lookup that requires `EntitySetName`. A catalog tool whose name does not start with those prefixes and is not a generic name is an error (`Unknown named tool`).

Handlers that are not tools (still must be tested; see §22):

| MCP method | Catalog mapping |
|------------|-----------------|
| `initialize` | `instructions` = `ODataMcpInstructions.Compose(ODataMcpCatalogOptions.InstructionsPreface)`: the trimmed preface, one blank line, then `ODataMcpInstructions.Default` (identical on both hosts; the preface can only prepend). AspNetCore configures `McpServerOptions` from `ODataMcpHostOptions.Catalog` at `AddODataMcp`; Tools configures it before `Build()`. Tests: `ServerInstructionsHostTests`, `AddODataMcp_ServerInstructions_DefaultWithoutPreface`, `ToolsMcpHost_ServerInstructions_AreTheSharedDefault`, `ODataMcpInstructionsTests`. |
| `tools/list` | catalog tools + optional extra (`shutdown_server` on Tools) |
| `tools/call` | `ODataToolRuntime.InvokeAsync` (extra handler first on Tools) |
| `resources/list` | `$metadata` then entity-set type cards, `odata://{route}/…`, capped by `MaxResources` |
| `resources/read` | `$metadata` → CSDL XML; entity set URI → type card JSON, **not** the collection |
| `resources/templates/list` | `odata://{route}/{entitySet}` and `odata://{route}/{entitySet}({key})` |
| `completion/complete` | argument name `entitySet` only; declared set names, `MaxCompletionValues` |

---

## 3. Global rules every case inherits

1. **Never mock** `HttpClient`, OData, MCP, or metadata. Recording executors in Core unit tests are **not** a substitute for the host/live cases in this file.
2. **MCP is a passthrough.** Do not invent `top`, `filter`, `$orderby`, or any query option the caller did not send. No `DefaultTop`. The OData server owns paging and payload size.
3. **Query option keys have no `$`.** The executor adds `$` on the wire (`$filter`, `$select`, `$orderby`, `$expand`, `$top`, `$skip`, `$count`).
4. **Resources use `odata://`.** AspNetCore route name is the OData prefix (`odata`, `shop`). Tools host route name is `remote`.
5. **`shutdown_server` is Tools/local only.** AspNetCore `tools/list` must not contain it. Calling it on AspNetCore is `Unknown tool 'shutdown_server'.`
6. **Named tools never split a CRUD family.** If `list_products` is advertised, `get_product` is advertised; create/update/delete follow the include flags as a unit that either fits the remaining cap or is omitted entirely.
7. **One OData hosting major version per test process.** OData 8 convention lives in `Microsoft.OData.Mcp.Tests.AspNetCore`. Restier OData 7 lives in `Microsoft.OData.Mcp.Tests.AspNetCore.Restier`. Do not load both controller types in one process.
8. **OData 4xx/5xx become tool `IsError`.** Error text always includes `OData request failed with status {code}.` When `Retry-After` is present, text includes `Retry-After: {value}.` Body is appended. This is **not** the same as MCP HTTP 429 on `POST {prefix}/mcp`.
9. **Outbound auth attaches `Authorization` on the named `"OData"` client.** `ODataOutboundAuthHandler` discovers and refreshes a Bearer per [AUTHENTICATION.md](./AUTHENTICATION.md); `RemoteODataExecutor` itself does not parse challenges or retry. `--auth-token` remains the escape hatch: when set, it short-circuits discovery and the handler pastes that value as `Authorization: Bearer {token}`.
10. **Size guards are MCP-side, not OData overrides.** `MaxFilterLength` (2048), `MaxExpandLength` (512), `MaxSelectLength` (1024), `MaxRequestBodyBytes` (262144), `MaxResponseBytes` (1048576). Oversized query/body → `ArgumentException` mapped to `IsError` **without** an OData round-trip. Oversized **response** → `IsError` suggesting select and top.
11. **Binary/stream properties never appear** in named create schemas, type cards, or `odata_describe_type` properties.
12. **Ignored / `ExcludeEntitySets` members never appear** in named tools, resources, or `odata_list_entity_sets`.
13. **JSON-RPC `tools/call` is required** for each generic tool on Streamable HTTP (`POST {prefix}/mcp`). An empty `{}` POST that only checks “not 404” is not a tool test.
14. **Always pass `-c Debug` or `-c Release`** to `dotnet` when running the suites.

### 3.1 Runtime argument quirks implementers must assert (not “fix in the test”)

These are current `ODataToolRuntime` behaviors. Tests lock them so a rewrite cannot silently change the contract:

| Quirk | Behavior |
|-------|----------|
| `$filter` as an argument name | **Ignored.** `ReadQueryOptions` only copies `filter`/`select`/`orderby`/`expand`/`top`/`skip`/`count`. A `$filter` key never becomes a query option. The call proceeds without that filter. |
| Extra unknown properties on query/get | **Ignored** (not an error). They are not forwarded. |
| `body` JSON object vs JSON string | String → `GetString()`. Object/array/number → `GetRawText()`. Empty string body is posted as empty (not `{}`). Null `body` is treated as missing. Bodies that do not parse as a JSON object (malformed, array, number, `null`) skip validation and are forwarded for the service to answer. |
| Missing `body` on `odata_create` | Runtime serializes leftover properties (everything except `entitySet`/`key`) and POSTs that JSON. Schema still lists `body` as required; runtime is more lenient. Leftover **query option names** (`filter`, `top`, …) therefore become properties and, on a closed type, fail before HTTP as `Unknown property` (or as missing required-on-create when nothing else is sent). |
| Named `create_*` | If `body` is absent, leftover properties (not query option names, not `key`/`entitySet`) are serialized into `body`, then validated like any create. |
| Pre-HTTP entity validation (create/update) | When the target set's type is declared and the body is a JSON object: create checks required-on-create first (`Missing required properties on {Type}: A, B. Required on create: …` — skipped when `EnforceRequiredOnCreate=false`); then every member: keys containing `@` and navigation names pass through; unknown members on **closed** types → `Unknown property '{p}' on {Type}. Declared: …`; JSON kind per EDM type (`must be a JSON integer/number/boolean/string/object/array`, `a JSON number or a numeric string` for Int64/Decimal, `a GUID string`); `null` on non-nullable → `cannot be null`; enum members (`must be one of A, B; 'x' is not a member`); `MaxLength` (`exceeds MaxLength n`). Open types accept extra members. Undeclared sets are forwarded untouched. |
| `top`/`skip` as strings `"1"` | Forwarded as the string `1` (or `"1"` if JSON string). Executor URI-escapes the value. OData servers generally accept `$top=1`. |
| `top` as bool or array | `GetRawText()` produces `true` or `[1,2]`. OData should 400. Tool `IsError` with status 400. |
| `count` as string `"true"` | Forwarded as `true` without quotes if it was a JSON string `"true"` → the string `true`. |
| Numeric / GUID / bool keys | `FormatKey` leaves them unquoted (`42`, guid, `true`). |
| String keys | Quoted; apostrophes doubled (`O'Brien` → `'O''Brien'`). |
| Already-quoted keys | Left as-is (`'ALFKI'` stays `'ALFKI'`). |
| Composite keys | Passed as a **single string** (`OrderID=10248,ProductID=11`). If the whole string is not numeric/guid/bool, `FormatKey` **quotes the entire composite**, which is wrong for OData. Cases below document both the desired wire form `EntitySet(OrderID=10248,ProductID=11)` and the current quoting trap — the test must fail the product if the wire path is `Order_Details('OrderID=10248,ProductID=11')` unless/until composite formatting is implemented. **Do not silently accept the quoted form.** |
| Bound `odata_call` | Actions are matched **before** functions (`Actions.FirstOrDefault` then functions). Name match is case-insensitive. Binding is enforced from the EDM: instance-bound → `{Name} is bound to {Type}. Pass entitySet and key. Signature: …`; collection-bound (binding parameter `Collection(...)`) → `{Name} is collection-bound. Pass entitySet; omit key. Signature: …` and the path is `{entitySet}/{Name}`; unbound with `entitySet` or `key` → `{Name} is unbound. Omit entitySet and key.` |
| Function / action parameters | Arguments live only in `parameters` (object). Parameter names match by **exact spelling**; the binding parameter is never an argument. `Unknown parameter '{p}'. Declared: a, b.`, `Missing parameter '{p}'. Signature: …` for non-nullable parameters, and JSON-kind / enum / `MaxLength` checks (`Parameter '{p}' must be …. Signature: …`) all fail before HTTP. Functions: primitives become URL literals (`'text'`, `33`, `NS.Enum'Member'`, `duration'P1D'`, raw GUIDs/dates); complex, collection, and entity arguments travel as parameter aliases (`p=@p` plus `?@p={json}` in the path). Actions: `parameters` becomes the POST body (`{}` when empty); enum numbers and literals are normalized to member names. `parameters` as a string → `parameters must be a JSON object, not a string.` |
| Unknown tool | `IsError`, text `Unknown tool '{name}'.` |
| Empty tool name at handler | `CallToolAsync` returns `Tool name is required.` before runtime. Empty name at `InvokeAsync` throws `ArgumentException` (not a tool result). |

---

## 4. Hosts, surfaces, and fixtures

### 4.1 Hosts

| Code | Project | Executor | Transport | `shutdown_server` |
|------|---------|----------|-----------|-------------------|
| **AspNetCore** | `Tests.AspNetCore` (OData 8) or `Tests.AspNetCore.Restier` (OData 7) | `InProcessODataExecutor` HTTP to `{prefix}/…` on TestServer | Streamable HTTP `POST {prefix}/mcp` | absent |
| **Tools** | `Tests.Tools` + `Tests.Core` live + `Tests.Integration` | `RemoteODataExecutor` named client `OData` | stdio (and optional Streamable HTTP) | present as extra tool |

Every mutating or query tool case that can run on both hosts **must** list both. Live Northwind/TripPin are Tools/Core (remote). Convention and Restier in-process are AspNetCore.

### 4.2 Surfaces

| Code | Where | Notes |
|------|-------|-------|
| **OData8** | `Tests.AspNetCore` convention `AddRouteComponents` | `CustomersController` seed `CustomerId=1 CompanyName=Contoso`. POST/PATCH require `Authorization`. GET/DELETE do not. Simple model: Customers, Orders, Products, OrderItems. Paging model: ClientCustomers (no PageSize) + ServerCustomers (PageSize). Rate-limit model: Customers, Products, `MostValuable()`. |
| **Restier** | `Tests.AspNetCore.Restier` `MapApiRoute` endpoint routing | `McpCustomer` `Id`+`CompanyName` seed Contoso/Fabrikam (and five-row paging seed). `McpProduct` `Id`+`Name` on prefix `shop` in multi-prefix tests. No `Authorization` on the current Restier APIs. |
| **Northwind** | `https://services.odata.org/V4/Northwind/Northwind.svc` | **Read.** Entity sets include Products (`ProductID` int), Customers (`CustomerID` string e.g. `ALFKI`), Orders, Order_Details (**composite** `OrderID,ProductID`), Categories, Employees, Suppliers, Shippers, Territories, Regions. Writes must `IsError`. |
| **TripPin** | `https://services.odata.org/TripPinRESTierService` (follow service-root redirect; session URL) | **Read/write.** People (`UserName` string e.g. `russellwhyte`), Airlines (`AirlineCode`), Airports (`IcaoCode`). Navigations: `Friends` (to-many), `Trips` (to-many). Unbound function `GetNearestAirport`. Unbound action `ResetDataSource`. Bound action `ShareTrip`. Prefer unique `UserName` per mutating test; do not depend on leftover state from other runs. |

### 4.3 Required extra fixtures (add only if missing; do not mock)

These are **test hosts**, still real OData:

| Fixture | Purpose |
|---------|---------|
| Convention controller that returns **403** for a specific key (e.g. `Customers(403)`) | Forbidden through tool |
| Convention controller that returns **405** for POST on a read-only set | Method not allowed |
| Convention controller that returns **409** on duplicate create | Conflict |
| Convention controller that returns **412** when `If-Match` fails / **428** when missing | Precondition |
| Convention controller that returns **413** when body exceeds a small limit | Payload too large at OData, not MCP `MaxRequestBodyBytes` |
| Convention controller that returns **415** for non-JSON content | Unsupported media (tool always sends `application/json`; this is for the HTTP twin and for any path that can change Content-Type) |
| Convention controller that returns **500** / **503** with optional `Retry-After` | Upstream failure |
| Guid-keyed entity set `Widgets` (`Edm.Guid` key) | `FormatKey` unquoted guid |
| Boolean-keyed or already-covered bool formatting | `FormatKey("true")` |
| Composite-keyed set if not using Northwind `Order_Details` | Composite identity |
| Binary/stream property on a type | Must be absent from named create schema and describe_type |
| CSDL Documentation / `Core.Description` on set, type, property, navigation, operation | describe/list/named titles |
| `InternalSecret` CLR property with OData `Ignore()` | Must never appear |
| Operations-only model (`MostValuable`, `GetStatus(code)`, `Reset`) | `odata_call` / list operations / no named CRUD |
| Wide model 200 sets | `MaxNamedTools`, `MaxResources`, completions |
| Auth-required OData CUD (already on `CustomersController`) | Unauthenticated create/update |
| Partitioned rate limiter: MCP / Customers / Products / function / second prefix | Combinatorial 429 |

### 4.4 Matrix legend used in every tool section

| Cell | Meaning |
|------|---------|
| A8 | AspNetCore host × OData 8 convention |
| R7 | AspNetCore host × Restier OData 7 |
| NW | Tools/Core remote × live Northwind (read) |
| TP | Tools/Core remote × live TripPin (read/write) |
| TH | Tools stdio host catalog (includes `shutdown_server`) |

A `•` in the tool matrix means that cell **must** have a dedicated passing implementation of the happy-path twin. A `–` means the operation does not exist on that API (e.g. Northwind write) **but a failure case still must run** (405/501/400 `IsError` with status).

---

## 5. Shared assertion helpers

Implement once, reuse; do not re-invent per test:

- Parse OData JSON (`value`, `@odata.count`, `@odata.nextLink`, keys, company/product names) the way `ODataFeedReader` already does.
- Twin compare: same entity ids / names / count / nextLink **presence** (nextLink URLs may differ between in-process absolute and relative; assert path and query `$skip`/`$skiptoken`, not full host).
- Tool success: `IsError == false`; `StructuredContent` JSON when the OData body is non-empty; `Text` contains the HTTP status.
- Tool OData failure: `IsError == true`; `Text` contains `status {code}`; if the HTTP twin sent `Retry-After`, tool text contains `Retry-After:`.
- MCP transport failure: HTTP status on `POST {prefix}/mcp` (401/429) **without** wrapping as a tool result.
- Wire capture (TestServer / `DelegatingHandler` that **forwards**, never stubs): assert query keys on the OData request are `$filter` not `filter`, and that omitted `top` is absent from the URI.
- Catalog: generic names in order listed in §2; no `$` in any `InputSchema`; `shutdown_server` only on Tools extra list.

JSON-RPC `tools/call` body (Streamable HTTP, stateless) — every protocol case uses a real call, not `{}`:

```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "method": "tools/call",
  "params": {
    "name": "odata_query",
    "arguments": { "entitySet": "Customers" }
  }
}
```

Initialize/session headers follow the MCP 2026-07-28 Streamable HTTP rules the SDK already implements. If the SDK requires `initialize` first, do that, then `tools/call`. Empty `{}` is only allowed as a **negative** content-type/malformed case.

---

## 6. `odata_list_entity_sets`

### Purpose

Lists entity sets declared in the OData model, including CSDL documentation when the metadata provides Documentation or `Core.Description` annotations. No arguments. Catalog-only (does not call OData HTTP). Returns JSON `{ entitySets: [ { name, entityType, keys, description? } ] }` (`description` is **absent**, never `null`, when the set has no docs) and text `Declared entity sets: {count}.` This tool and `odata_list_operations` are the only two that declare an MCP `outputSchema`; `structuredContent` conforms to it.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore in-process | • | • | – | – |
| Tools remote | – | – | • | • |

### Happy path

1. **ListEntitySets_OData8_ReturnsCustomersOrdersProductsOrderItems**  
   - **Setup:** Convention host, `TestModels.GetSimpleModel()`, `AddODataMcp()`.  
   - **Call:** `odata_list_entity_sets` with `null` args.  
   - **Twin:** GET `{prefix}/$metadata` and GET `{prefix}` service document.  
   - **Expect:** `IsError=false`. Structured JSON contains `Customers`, `Orders`, `Products`, `OrderItems`. Keys for Customer include `CustomerId`. Count matches service document entity-set count. Text matches `Declared entity sets: {n}.`

2. **ListEntitySets_Restier_ReturnsCustomersMatchingMetadata**  
   - **Setup:** Restier `McpCustomerApi` prefix `odata`.  
   - **Call:** `odata_list_entity_sets`.  
   - **Twin:** GET `odata/$metadata` contains `Customers` / `McpCustomer`.  
   - **Expect:** Structured JSON contains `Customers` and key `Id`. Does not contain `Products` (other API).

3. **ListEntitySets_Northwind_ContainsProductsCustomersOrderDetails**  
   - **Setup:** Tools/Core catalog from live `$metadata`.  
   - **Call:** `odata_list_entity_sets`.  
   - **Twin:** GET `{Northwind}/$metadata` EntityContainer.  
   - **Expect:** Names include `Products`, `Customers`, `Order_Details`, `Orders`, `Categories`, `Employees`. `Order_Details` keys include both `OrderID` and `ProductID`.

4. **ListEntitySets_TripPin_ContainsPeopleAirlinesAirports**  
   - **Setup:** Tools host `ToolsMcpHost.CreateAsync(TripPin)`.  
   - **Call:** `odata_list_entity_sets`.  
   - **Twin:** live `$metadata`.  
   - **Expect:** `People` (key `UserName`), `Airlines`, `Airports`.

5. **ListEntitySets_DocumentedCsdl_IncludesDescription**  
   - **Setup:** Catalog from documented CSDL used in Core tests.  
   - **Call:** `odata_list_entity_sets`.  
   - **Twin:** none (model-only, still real parser).  
   - **Expect:** description contains `People who travel.`

6. **ListEntitySets_ExcludeEntitySets_OmitsPeopleFromList**  
   - **Setup:** `ExcludeEntitySets = ["People"]`.  
   - **Call:** `odata_list_entity_sets`.  
   - **Expect:** no `People`. Generic tools still present. Named `list_people` absent.

7. **ListEntitySets_OperationsOnlyModel_ReturnsEmptyArray**  
   - **Setup:** operations-only EDM.  
   - **Call:** `odata_list_entity_sets`.  
   - **Expect:** `entitySets` empty array, text `Declared entity sets: 0.` Tool still succeeds.

8. **ListEntitySets_WideModel200_ReturnsAllDeclaredSetsNotJustNamed**  
   - **Setup:** 200 entity sets, `MaxNamedTools=150`, `MaxResources=50`.  
   - **Call:** `odata_list_entity_sets`.  
   - **Expect:** 200 names in the tool result (this tool is not capped by `MaxResources`; resources/list is). If the product later caps this list, the test fails and the spec must be updated — do not silently truncate.

9. **ListEntitySets_TwoPrefixes_IsolatedCatalogs**  
   - **Setup:** AspNetCore prefixes `odata` (Customers) and `shop`/`internal` (Products or minimal Customers).  
   - **Call:** list on each session.  
   - **Expect:** `odata` list does not include the other prefix’s exclusive sets.

10. **ListEntitySets_JsonRpcToolsCall_AspNetCoreStreamableHttp**  
    - **Setup:** A8 host.  
    - **Call:** POST `/odata/mcp` JSON-RPC `tools/call` name `odata_list_entity_sets` arguments `{}`.  
    - **Expect:** MCP 200, `isError` false, structured/text lists Customers.

### Misunderstood parameters

11. **ListEntitySets_DollarFilterArgument_IsIgnoredAndStillSucceeds**  
    - **Call:** `{ "$filter": "x", "entitySet": "Customers" }`.  
    - **Expect:** success; extra keys ignored; schema `additionalProperties:false` is catalog-only (runtime does not reject). Document whether SDK validation rejects this on `tools/call`; if the SDK strips unknown props, still assert runtime `InvokeAsync` ignores them.

12. **ListEntitySets_UsingQueryArgs_FilterTop_DoesNotQueryOData**  
    - **Call:** `{ "filter": "true", "top": 1 }`.  
    - **Expect:** still catalog list, no HTTP to `/Customers`. Wire capture: zero OData GETs.

13. **ListEntitySets_UsingGetArgs_EntitySetAndKey_Ignored**  
    - **Call:** `{ "entitySet": "Customers", "key": "1" }`.  
    - **Expect:** full list, not a single entity.

14. **ListEntitySets_NameArgumentMeantForDescribe_Ignored**  
    - **Call:** `{ "name": "Customer" }`.  
    - **Expect:** full list, not a type card.

### Malformed payloads

15. **ListEntitySets_NullArguments_Succeeds**  
16. **ListEntitySets_EmptyObject_Succeeds**  
17. **ListEntitySets_JsonRpc_EmptyBody_IsProtocolErrorNotToolSuccess** — POST `/odata/mcp` with `{}`. Expect not a successful `tools/call` result for this tool. Not 404.  
18. **ListEntitySets_JsonRpc_InvalidJson_IsProtocolError** — body `{`.  
19. **ListEntitySets_JsonRpc_WrongContentTypeTextPlain_RejectedOrUnsupported**  
20. **ListEntitySets_JsonRpc_BinaryBody_Rejected**  
21. **ListEntitySets_JsonRpc_HugeBodyOverKestrelLimit_413OnMcpHttp** — this is MCP HTTP 413, not OData 413.

### Server failures

This tool does not call OData. Server failures are catalog/session failures:

22. **ListEntitySets_SessionMissing_HandlerThrowsOrIsError** — handler without request services.  
23. **ListEntitySets_DoesNotSurfaceOData404** — even if `/Customers` would 404, this tool never hits it.

### Overwhelm / rate limit / size

24. **ListEntitySets_McpTransportRateLimit_SecondPost429** — A8 `RateLimitingPolicyName=mcp` permit 1. First JSON-RPC list succeeds or is accepted; second POST `/odata/mcp` is HTTP 429. Tool result is not produced.  
25. **ListEntitySets_ODataSetRateLimit_DoesNotAffectThisTool** — Customers budget 1 already spent via HTTP GET; `odata_list_entity_sets` still succeeds (no OData call).  
26. **ListEntitySets_MaxResponseBytesTiny_IsErrorSuggestingSelectTop** — set `MaxResponseBytes` below the JSON size of a Northwind list; expect `IsError` and text containing `select` and `top`.  
27. **ListEntitySets_ConcurrentTwentyCalls_AllSucceed** — A8 and NW.

### Cross-tool interactions

28. **ListEntitySets_ThenDescribeType_EachName_SucceedsOrIsExcluded** — for every name returned, `odata_describe_type` with that name succeeds.  
29. **ListEntitySets_ThenQueryFirstSet_SucceedsOnReadSurfaces**  
30. **ListEntitySets_AgreesWithResourcesList_SetNames** — every entity-set resource name (except `$metadata`) appears in the list; list may be a superset of resources when `MaxResources` truncates.  
31. **ListEntitySets_AspNetCore_ToolsListContainsThisTool_OmitsShutdown**  
32. **ListEntitySets_ToolsHost_ToolsListContainsThisTool_AndShutdown**

---

## 7. `odata_describe_type`

### Purpose

Describes declared properties, keys, navigations, **bound operations, and enums** for a type or entity set in the compact grammar of [TYPE-SHAPES.md](./TYPE-SHAPES.md). Argument `name` is an entity set or type name (short or full). `format` is `text` (default) or `json`; one representation per call, never both.

- **text:** `Type  (set: Set, key: K)` header, `// docs` only when CSDL has them, `Name: type` (required on create) vs `Name?: type` (optional), `// key` / `// key, store-generated`, `Name -> Type[]` navigations, `enum(A|B)` members inline with one `// filter enums as NS.Enum'{value}'` hint (one pattern per distinct enum the type uses, comma-separated), `string(n)` only when `MaxLength ≤ 16`, and an `operations` block listing **bound** operations only (`// writes` for actions, `// collection` for collection-bound). Returned as `content[0].text`; `structuredContent` is absent.
- **json:** `{ "Type": { "set", "key": [...], "description"?, "longDescription"?, "setDescription"?, "enumFilterLiterals"?: ["NS.Enum'{value}'"], "props": { "Name": "type!" }, "docs"?, "navs"?, "ops"? } }`. `!` marks required on create. Empty sections are omitted; there is never a `null` value, no `nullable`, no `namespace`, no `entityTypeDescription`.

The `resources/read` type card is the same JSON byte for byte. Unknown name → `Type or entity set '{name}' is not declared in the model.` Unknown format → `Unknown format '{value}'. Use text or json.`

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • | • | – | – |
| Tools | – | – | • | • |

### Happy path

33. **DescribeType_OData8_ByEntitySetCustomers_ContainsCustomerIdAndOrdersNav**  
    - **Call:** `{ "name": "Customers" }`.  
    - **Twin:** `$metadata` EntityType Customer.  
    - **Expect:** keys `CustomerId`; properties include `CompanyName`; navigations include `Orders`; `InternalSecret` absent if ignored.

34. **DescribeType_OData8_ByTypeNameCustomer_SameAsSet**  
    - **Call:** `{ "name": "Customer" }`.  
    - **Expect:** same keys/properties as case 33; identical text when the type has exactly one set. A type with no set renders `Type  (key: K)` with no `set:` part.

35. **DescribeType_OData8_ByFullName_IfNamespacePresent**  
    - **Call:** `{ "name": "{namespace}.Customer" }`.  
    - **Expect:** success.

36. **DescribeType_Restier_Customers_ContainsIdAndCompanyName**  
    - **Twin:** Restier `$metadata`.  
    - **Expect:** no `Orders` nav (Restier customer has none).

37. **DescribeType_Northwind_Products_ContainsProductIDAndCategoryNav**  
    - **Call:** `Products` and `Product`.  
    - **Expect:** key `ProductID`; nav `Category` and/or `Order_Details` / `Supplier` as declared.

38. **DescribeType_Northwind_OrderDetails_CompositeKeys**  
    - **Call:** `Order_Details`.  
    - **Expect:** keys `OrderID` and `ProductID`.

39. **DescribeType_TripPin_People_ContainsUserNameFriendsTrips**  
    - **Expect:** key `UserName`; navs `Friends`, `Trips`; CSDL docs if present.

40. **DescribeType_DocumentedCsdl_PropertyAndNavDescriptions**  
    - **Expect:** text is exactly the §1.1 rendering: header `// A person who travels.` then the long description and set docs on their own `//` lines, `UserName: string // key; Unique person name.` with `    // Used as the entity key.` beneath, `Friends -> Person[] // Other people this person knows.`. Northwind (no CSDL docs) renders **no** `//` doc lines and no `description` keys in json.

41. **DescribeType_BinaryStreamProperties_OmittedFromPropertiesArray**  
    - **Setup:** type with `Edm.Binary` / `Edm.Stream`.  
    - **Expect:** those names absent.

42. **DescribeType_JsonRpcToolsCall_OData8**  
    - **Call:** JSON-RPC `odata_describe_type` `{ "name": "Customers" }` on `/odata/mcp`.

### Misunderstood parameters

43. **DescribeType_MissingName_IsErrorMissingRequiredArgument** — text contains `name`.  
44. **DescribeType_NullName_IsError**  
45. **DescribeType_WhitespaceName_IsError**  
46. **DescribeType_UsesEntitySetKey_NotName_IsErrorMissingName** — `{ "entitySet": "Customers" }`.  
47. **DescribeType_UsesIdInsteadOfName_IsError** — `{ "id": "Customer" }`.  
48. **DescribeType_NameWithDollarMetadata_IsErrorNotDeclared** — `{ "name": "$metadata" }`.  
49. **DescribeType_ExtraFilterTop_IgnoredAndStillDescribes** — `{ "name": "Customers", "filter": "x", "top": 1 }`. No OData HTTP.  
50. **DescribeType_WrongCase_PeopleVsPeople** — case-insensitive match (`customers` vs `Customers`) succeeds.  
50.1. **DescribeType_UnknownFormat_IsError** — `{ "name": "People", "format": "garbage" }` → `isError`, text names `format`, the bad value, `text`, and `json`.  
50.2. **DescribeType_FormatCaseInsensitive_NullIsText** — `"format": "JSON"` returns `structuredContent`; `"format": null` behaves as `text`.  
50.3. **DescribeType_Enum_RendersMembersLiteralFlagsAndComputed** — enum fixture: `Color: enum(Red|Green)`, one `// filter enums as NS.Color'{value}', NS.Permissions'{value}'` header line, `Access?: enum(Read|Write) // flags, comma-separated`, `Id?: int // key, store-generated` for a `Core.Computed` key, `Code?: string(8)` for `MaxLength=8`, binary properties absent.  
50.4. **DescribeType_Operations_ListsBoundOnlyWithMarkers** — `operations` block lists bound functions and actions only, binding parameter omitted, `// writes` on actions, `// collection` on collection-bound, operations bound to a base type appear on derived types; unbound operations are absent. TripPin `People` lists `GetFavoriteAirline() -> Airline`, `GetFriendsTrips(userName: string) -> Trip[]`, `UpdateLastName(lastName: string) -> bool // writes`, `ShareTrip(userName: string, tripId: int) // writes` and not `GetNearestAirport` / `ResetDataSource`.

### Malformed payloads

51. **DescribeType_NameAsNumber_CoercedToRawTextThenLookedUp** — `{ "name": 1 }` → lookup `"1"` → not declared error.  
52. **DescribeType_NameAsObject_IsError** — `{ "name": { "x": 1 } }`.  
53. **DescribeType_NameAsArray_IsError**  
54. **DescribeType_JsonRpc_NoParams_ProtocolOrMissingName**  
55. **DescribeType_JsonRpc_InvalidJson**  
56. **DescribeType_UnicodeNameNotInModel_IsError** — `{ "name": "客戶" }`.  
57. **DescribeType_EmojiName_IsErrorNotDeclared**

### Server failures

Catalog-only; unknown type is MCP-side, not HTTP 404:

58. **DescribeType_UnknownTypeGhost_IsErrorNotDeclared_NoHttp404** — wire: no OData request. Text does **not** need `status 404`.  
59. **DescribeType_ExcludedEntitySet_IsErrorEvenIfInEdm** — `ExcludeEntitySets=["People"]` then name `People`: current code uses `ResolveIncludedSets` first then falls back to **entity types**, so a type named `Person` still describes; set name `People` with excluded set may still resolve via type if names differ. **Assert actual:** excluded **set** `People` — if a type `People` does not exist, error; if type `Person` is requested, success. Do not leak excluded set as `entitySet` field when the set is excluded.  
60. **DescribeType_DoesNotForwardAuthorizationBecauseNoHttp**

### Overwhelm / rate limit / size

61. **DescribeType_Mcp429OnSecondJsonRpc** — transport limiter.  
62. **DescribeType_ODataCustomers429_DoesNotAffect** — set limiter spent; describe still works.  
63. **DescribeType_WideModel_DescribeRows000_Succeeds**  
64. **DescribeType_MaxResponseBytesTiny_IsError**  
65. **DescribeType_ConcurrentDescribeAllNorthwindSets_AllSucceedOrNotDeclared**

### Cross-tool interactions

66. **DescribeType_ThenQueryThatSet_UsesAPropertyFromTheCardInSelect** — `select` of a described property succeeds.  
67. **DescribeType_ThenNavigate_UsesANavigationFromTheCard** — TripPin `Friends` or OData8 `Orders`.  
68. **DescribeType_AgreesWithResourceReadTypeCard** — `resources/read` `odata://odata/Customers` JSON **equals** `odata_describe_type` `format=json` byte for byte (same `key`, `props`, `navs`, `ops`).  
69. **DescribeType_IgnoredPropertyNeverAppears_InternalSecret**

---

## 7.1. `odata_describe_model`

### Purpose

The whole-service map, never EDMX. No required arguments. `detail=summary` (default) lists every included set as its describe header (`Type  (set: Set, key: K)`), at most one `// doc` line, and its navigation lines; no property lists; then an `operations` block of **unbound** operations. `detail=complete` renders every in-scope type in the full [TYPE-SHAPES.md](./TYPE-SHAPES.md) §1.1 grammar (one block per distinct type, blank-line separated), then the complex types those types use (`Name  (complex)` header), then unbound operations. `format=mermaid` is a relationship-only `erDiagram` (`A ||--o{ B : Nav`, `||--o|` for single targets, bare entity line when a type has no navigations) with no attribute compartments whatever the detail. `format=json` is `{ sets: { Set: { type, key, description?, navs? } }, operations? }` for summary and `{ types: { Type: <describe body> }, complexTypes?, operations? }` for complete. `sets` scopes summary, complete, and mermaid to the named included sets (case-insensitive); complex types follow the scoped entities; unbound operations are always listed.

Errors: unknown `detail` → `Unknown detail '{v}'. Use summary or complete.`; unknown `format` → `Unknown format '{v}'. Use text or json or mermaid.`; `sets` not an array of strings → `sets must be a JSON array of entity set names.`; unknown set → `Entity set '{name}' is not declared in the model.`; over `MaxResponseBytes` → `The model description is {n} bytes across {k} sets, over the {max} byte limit. Pass sets to scope it or use detail=summary.` (no silent trim, no CSDL fallback).

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • | • | – | – |
| Tools | – | – | • | • |

### Happy path

69.1. **DescribeModel_Default_SummaryText** — fixture: headers for every set in container order, `  // A buyer.` under the documented type, `  Orders -> Order[]`, `  Customer? -> Customer`, no property lines, `operations` block with `Ping() -> string` and `Reset(hard?: bool) // writes`; bound `Top` absent.  
69.2. **DescribeModel_Complete_TextIncludesTypesComplexTypesAndOperations** — every type block equals `odata_describe_type` text for that set, then `Address  (complex)` / `Geo  (complex)` blocks (transitive), then unbound operations.  
69.3. **DescribeModel_CompleteWithSets_ScopesTypesAndComplexTypes** — `sets: ["orders"]` (case-insensitive) dumps Order only, no complex types Order does not use, operations still listed.  
69.4. **DescribeModel_Mermaid_RelationshipsOnly** — exact `erDiagram` lines; contains no type tokens.  
69.5. **DescribeModel_SummaryJson_SetsAndOperations** — `sets` object in container order, each with `type`, `key`, `navs?`, `description?`; `operations` map; no `props`; no `null`.  
69.6. **DescribeModel_CompleteJson_TypesComplexTypesAndOperations** — `types.Customer.props.Name == "string!"`, `types.Customer.ops.Top`, `types.Order.enumFilterLiterals == ["Shop.Status'{value}'"]`, `complexTypes.Geo.props.Lat == "number!"`.  
69.7. **DescribeModel_Northwind_SummaryHasSetsAndNavigations** — live: `Customer  (set: Customers, key: CustomerID)` followed by `  Orders -> Order[]`; composite key header for `Order_Details`; no `operations` block.  
69.8. **DescribeModel_Northwind_CompleteHasProperties** — live: `  CompanyName?: string`, `  ProductID: int // key`.  
69.9. **DescribeModel_TripPin_SummaryEndsWithUnboundOperations** — live: starts with the People header and its three navigations; ends with `operations` / `GetPersonWithMostFriends() -> Person` / `GetNearestAirport(lat: number, lon: number) -> Airport` / `ResetDataSource() // writes`; `ShareTrip` and `GetFavoriteAirline` absent.  
69.10. **Catalog_GenericTools_IncludeDescribeModelAfterDescribeType** — eleven generics; `odata_describe_model` immediately after `odata_describe_type` and before `odata_query`; readOnly + idempotent; schema enumerates `detail`, `format`, `sets`; description says `Do not read $metadata`.

### Misunderstood parameters / malformed

69.11. **DescribeModel_BadArguments_AreErrors** — `detail=everything`, `format=xml`, `sets: ["Ghosts"]`, `sets: "Customers"` (string) each error with the messages above.

### Overwhelm / size

69.12. **DescribeModel_Oversize_IsErrorMentioningSetsAndSummary** — `MaxResponseBytes=40`, `detail=complete` → error text names the byte count, `3 sets`, `sets`, and `summary`; contains no `<edmx` and no `$metadata`.

### Baselines

`Baselines/TypeShapes/Current/northwind.describe_model.summary.txt`, `trippin.describe_model.summary.txt`, `trippin.describe_model.complete.txt` lock the live payloads.

---

## 8. `odata_query`

### Purpose

Queries an entity set. Parameter names do not include `$`; the executor adds `$filter`, `$select`, `$orderby`, `$expand`, `$top`, `$skip`, and `$count`. Required: `entitySet`. MCP must not add `top` when omitted.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • | • | – | – |
| Tools | – | – | • | • |

### Happy path

70. **OdataQuery_OData8_Customers_MatchesGetOdataCustomers**  
    - **Call:** `{ "entitySet": "Customers" }` no top.  
    - **Twin:** GET `/odata/Customers`.  
    - **Expect:** same company names. Wire URI has **no** `$top`.

71. **OdataQuery_OData8_Top1OrderbyCustomerId_Matches**  
    - **Call:** `top=1`, `orderby=CustomerId`.  
    - **Twin:** GET `/odata/Customers?$orderby=CustomerId&$top=1`.  
    - **Expect:** Contoso only. Wire has `$top=1` and `$orderby=CustomerId`, not `top=`.

72. **OdataQuery_OData8_FilterCompanyNameEqContoso_Matches**  
    - **Call:** `filter=CompanyName eq 'Contoso'`.  
    - **Twin:** `$filter=CompanyName eq 'Contoso'`.

73. **OdataQuery_OData8_SelectCompanyName_Matches**  
    - **Call:** `select=CompanyName`.  
    - **Expect:** payload has CompanyName; twin `$select=CompanyName`.

74. **OdataQuery_OData8_ExpandOrders_Matches**  
    - **Call:** `expand=Orders`.  
    - **Twin:** `$expand=Orders`.  
    - **Expect:** same expand presence (empty collection ok).

75. **OdataQuery_OData8_SkipWithoutTop_Passthrough**  
    - **Call:** `orderby=CustomerId`, `skip=0` without top.  
    - **Twin:** `$skip=0` without `$top`.  
    - **Expect:** wire has `$skip` and no `$top`.

76. **OdataQuery_OData8_CountTrue_ReturnsOdataCount**  
    - **Call:** `count=true`.  
    - **Twin:** `$count=true`.

77. **OdataQuery_PagingClient_FilterSkipTopOrderbySelect_MatchTwin**  
    - **Setup:** ClientCustomers five rows.  
    - **Call:** filter `CustomerId gt 1`, orderby `CustomerId`, skip 1, top 2, select `CompanyName`, count true.  
    - **Twin:** the `$`-prefixed equivalent.  
    - **Expect:** identical names and count.

78. **OdataQuery_PagingServer_OmitTop_ReturnsPageSizeAndNextLink**  
    - **Setup:** ServerCustomers PageSize=2.  
    - **Call:** entitySet `ServerCustomers`, orderby only.  
    - **Twin:** GET without `$top`.  
    - **Expect:** two rows + nextLink. MCP did not inject top.

79. **OdataQuery_PagingServer_SkipTop_SecondPageMatchesNextLinkPayload**  
    - **Call:** skip/top matching the next page.  
    - **Twin:** follow `@odata.nextLink`.  
    - **Expect:** same keys (nextLink URL may include `$skiptoken`; skip/top is an equivalent assertion when PageSize and skip align).

80. **OdataQuery_Restier_Customers_MatchesGet**  
81. **OdataQuery_Restier_FilterFabrikam_Matches**  
82. **OdataQuery_Restier_Top1OrderbyId_Matches**  
83. **OdataQuery_Restier_SkipAndTop_MatchHttp**  
84. **OdataQuery_Restier_OmitTop_ReturnsAllSeededRowsNoInjectedTop**  
85. **OdataQuery_Northwind_ProductsTop1_ReturnsProduct**  
86. **OdataQuery_Northwind_ProductsSkip1Top1OrderbyProductID_MatchHttp**  
87. **OdataQuery_Northwind_Skip0VsSkip1_PagesDiffer**  
88. **OdataQuery_Northwind_FilterProductIDEq1_MatchHttp**  
89. **OdataQuery_Northwind_SelectProductName_MatchHttp**  
90. **OdataQuery_Northwind_ExpandCategory_MatchHttp**  
91. **OdataQuery_Northwind_CountTrueTop0_ReturnsCount** — `top=0`, `count=true` if the service allows; else `top=1`+count.  
92. **OdataQuery_Northwind_CustomersStringFilter_ALFKI** — `filter=CustomerID eq 'ALFKI'`.  
93. **OdataQuery_Northwind_OmitTop_DoesNotAddTopOnWire** — capture Remote executor URI; no `$top` unless Northwind itself redirects with one (assert request, not response).  
94. **OdataQuery_TripPin_PeopleTop2_MatchHttp**  
95. **OdataQuery_TripPin_FilterUserNameEqRussellwhyte**  
96. **OdataQuery_TripPin_ExpandFriends_MatchHttp**  
97. **OdataQuery_TripPin_OrderbyUserNameSkipTopCount**  
98. **OdataQuery_JsonRpcToolsCall_OData8_Customers**  
99. **OdataQuery_JsonRpcToolsCall_Restier_Customers**  
100. **OdataQuery_NamedListCustomers_SamePageAsGeneric** — see also §16; this generic case asserts generic payload equals `list_customers` for skip=1 top=2.

### Misunderstood parameters

101. **OdataQuery_MissingEntitySet_IsErrorContainsEntitySet**  
102. **OdataQuery_WhitespaceEntitySet_IsError**  
103. **OdataQuery_NullEntitySet_IsError**  
104. **OdataQuery_EntityInsteadOfEntitySet_IsErrorMissingEntitySet** — `{ "entity": "Customers" }`.  
105. **OdataQuery_SetInsteadOfEntitySet_IsError** — `{ "set": "Customers" }`.  
106. **OdataQuery_DollarFilter_IsIgnoredSoUnfilteredResult** — `{ "entitySet": "Customers", "$filter": "CompanyName eq 'NoSuch'" }`. Twin unfiltered GET. Expect Contoso still present (filter not applied). Wire has no `$filter`.  
107. **OdataQuery_DollarTop_IsIgnoredNotInjectedAsTop** — `{ "entitySet":"Customers", "$top": 1 }` returns full set on ClientCustomers (5 rows), not 1.  
108. **OdataQuery_FilterWithDollarInValue_IsFine** — `filter=CompanyName eq '$top'`.  
109. **OdataQuery_UsingGetArgs_KeyWithoutBeingGet_KeyIgnored** — `{ "entitySet":"Customers", "key":"1" }` still lists, does not GET by key.  
110. **OdataQuery_SwappedKeyAndEntitySet_EntitySetIs1_IsError404FromOData** — `{ "entitySet":"1", "key":"Customers" }`.  
111. **OdataQuery_TopAsString_1_Succeeds** — `"top": "1"`.  
112. **OdataQuery_TopAsBoolTrue_OData400_IsErrorStatus400**  
113. **OdataQuery_TopAsArray_OData400_IsError**  
114. **OdataQuery_TopAsObject_OData400_IsError**  
115. **OdataQuery_SkipAsString**  
116. **OdataQuery_SkipAsBool_OData400**  
117. **OdataQuery_CountAsStringTrue**  
118. **OdataQuery_CountAsNumber1_ForwardedRaw_ODataMay400OrTreatTruthy** — assert twin `$count=1` if that is what is sent; document OData’s response.  
119. **OdataQuery_CountAsStringYes_OData400**  
120. **OdataQuery_FilterAsNumber_ForwardedRaw**  
121. **OdataQuery_FilterAsArray_OData400**  
122. **OdataQuery_UnknownPropertyFoo_Ignored** — still queries.  
123. **OdataQuery_BodyProperty_IgnoredOnQuery**  
124. **OdataQuery_NavigationProperty_IgnoredOnQuery**  
125. **OdataQuery_SelectAsArrayOfStrings_RawTextArray_OData400**

### Malformed payloads

126. **OdataQuery_EmptyFilterString_ForwardsEmptyDollarFilter** — may 400; `IsError` if OData 400.  
127. **OdataQuery_FilterUnicodeEmoji_CompanyNameEq** — `filter=CompanyName eq 'Contoso😀'` → empty or 400, not 500.  
128. **OdataQuery_FilterInjectionOr1Eq1_StillValidODataOr400** — `filter=1 eq 1`. If allowed, returns all rows (OData boolean), **not** a SQL injection. Twin the same string.  
129. **OdataQuery_FilterSqlDropTable_IsOData400OrEmptyNot500** — `filter=CompanyName eq 'x'; DROP TABLE Customers;--`.  
130. **OdataQuery_FilterUnclosedQuote_OData400**  
131. **OdataQuery_ExpandInjection_OData400** — `expand=Orders/$ref,NotANav`.  
132. **OdataQuery_EntitySetEmptyString_IsErrorRequired**  
133. **OdataQuery_EntitySetJsonNull_IsError**  
134. **OdataQuery_JsonRpc_ArgumentsNull_MissingEntitySet**  
135. **OdataQuery_JsonRpc_MalformedJson**  
136. **OdataQuery_JsonRpc_WrongContentTypeXml**  
137. **OdataQuery_JsonRpc_HugeArguments_McpOrGuard**

### Server failures

138. **OdataQuery_UnknownEntitySet_IsErrorStatus404OrNotSuccess** — `DoesNotExist`. Twin GET 404. Text contains the status.  
139. **OdataQuery_OData8_401IfQueryRequiresAuth** — if a set is `[Authorize]`; otherwise skip this cell only when the fixture has no authorized GET, and implement on an authorized-GET controller.  
140. **OdataQuery_OData8_403ForbiddenSet** — fixture. Status in text.  
141. **OdataQuery_405OnWrongMethodIsNotThisTool** — N/A for GET query; covered by create.  
142. **OdataQuery_OData429OnCustomers_IsErrorStatus429RetryAfter** — A8 partitioned limiter Customers=1; first query ok; second `IsError` contains `429` and `Retry-After` when the limiter sets it.  
143. **OdataQuery_OData500_IsErrorStatus500** — error controller.  
144. **OdataQuery_OData503_IsErrorStatus503RetryAfter**  
145. **OdataQuery_InvalidFilterSyntax_OData400** — `filter=this is not filter`.  
146. **OdataQuery_Northwind_WriteVerbNotUsed** — GET only.  
147. **OdataQuery_TripPin_UnknownSet_404**

### Overwhelm / rate limit / size

148. **OdataQuery_MaxFilterLengthExceeded_IsErrorNoHttp** — filter of 2049 chars. Text contains `filter exceeds the maximum length`. Wire: no request.  
149. **OdataQuery_MaxFilterLengthExact2048_IsAllowedAndSent**  
150. **OdataQuery_MaxExpandLengthExceeded_IsErrorNoHttp** — 513 chars.  
151. **OdataQuery_MaxSelectLengthExceeded_IsErrorNoHttp** — 1025 chars.  
152. **OdataQuery_MaxResponseBytesExceeded_IsErrorSuggestSelectTop** — tiny max vs Northwind Products unpaged.  
153. **OdataQuery_DoesNotClampTopToMaxTop** — `top=50` on a 5-row set returns 5, not a hidden 10. `top=1000` on Northwind is forwarded as `$top=1000` (service may cap; MCP must not rewrite to a smaller number).  
154. **OdataQuery_DeepExpandIfServerAllows_TripPinFriendsFriends** — `expand=Friends($expand=Friends)` if TripPin allows; else OData 400 `IsError`.  
155. **OdataQuery_WideExpandSelectOnNorthwind_WithinGuards**  
156. **OdataQuery_McpTransport429_JsonRpcSecondCall** — HTTP 429, not tool status 429.  
157. **OdataQuery_CustomersLimited_ProductsUnlimited_Independent** — spend Customers; Products query still works.  
158. **OdataQuery_FunctionLimited_QueryStillWorks** — spend `MostValuable`; Customers query works.  
159. **OdataQuery_SecondPrefixMcpLimited_FirstPrefixQueryWorks** — combinatorial.  
160. **OdataQuery_ConcurrentTenQueries_NorthwindProductsTop1**  
161. **OdataQuery_OperationsOnlyModel_QueryCustomers_404OrMissingSetError**

### Cross-tool interactions

162. **OdataQuery_ThenGetFirstKey_OData8**  
163. **OdataQuery_ThenNavigateOrders_OData8OrTripPinFriends**  
164. **OdataQuery_ThenCreateThenQueryFilter_SeesNewRow** — Restier/TripPin/OData8-with-auth.  
165. **OdataQuery_EqualsListCustomers_SameSkipTop**  
166. **OdataQuery_DoesNotEqualResourceRead** — `resources/read` type card is not the collection.

---

## 9. `odata_get`

### Purpose

Gets an entity by key. Required `entitySet` + `key`. Path `{entitySet}({FormatKey(key)})`. Optional `select` and `expand` are advertised in the schema and applied via `ReadQueryOptions` (same names as query); the runtime also honors any other query option it is handed.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • | • | – | – |
| Tools | – | – | • | • |

### Happy path

167. **OdataGet_OData8_Customer1_MatchesGetCustomers1** — key `"1"` unquoted on wire `Customers(1)`. Twin GET `/odata/Customers(1)` Contoso.  
168. **OdataGet_OData8_SelectCompanyName** — `select=CompanyName`. Twin `$select`.  
169. **OdataGet_OData8_ExpandOrders**  
170. **OdataGet_Restier_Customer1_Contoso** — key `"1"`, path `Customers(1)`.  
171. **OdataGet_Northwind_Product1_MatchHttp** — key `"1"` → `Products(1)`.  
172. **OdataGet_Northwind_CustomerALFKI_StringKeyQuoted** — key `ALFKI` → wire `Customers('ALFKI')`. Twin GET `Customers('ALFKI')`.  
173. **OdataGet_Northwind_CustomerAlreadyQuoted_NotDoubleQuoted** — key `'ALFKI'` → `Customers('ALFKI')` not `Customers('''ALFKI''')`.  
174. **OdataGet_Northwind_OrderDetails_CompositeKey** — key `OrderID=10248,ProductID=11` (use a real pair from a prior query). Twin GET `Order_Details(OrderID=10248,ProductID=11)`. **Expect wire path unquoted composite.** Fail if `FormatKey` wraps the whole key in quotes.  
175. **OdataGet_TripPin_PersonRussellwhyte** — key `russellwhyte` → `People('russellwhyte')`.  
176. **OdataGet_GuidKey_Unquoted** — Widgets fixture.  
177. **OdataGet_JsonRpcToolsCall_OData8_Customer1**  
178. **OdataGet_EqualsGetCustomerNamedTool** — same payload as `get_customer` key 1.

### Misunderstood parameters

179. **OdataGet_MissingKey_IsErrorContainsKey**  
180. **OdataGet_MissingEntitySet_IsError**  
181. **OdataGet_WhitespaceKey_IsError**  
182. **OdataGet_IdInsteadOfKey_IsErrorMissingKey** — `{ "entitySet":"Customers", "id":"1" }`.  
183. **OdataGet_EntityInsteadOfEntitySet_IsError**  
184. **OdataGet_Swapped_EntitySetIsKey_KeyIsSetName** — `{ "entitySet":"1", "key":"Customers" }`.  
185. **OdataGet_UsingQueryArgsWithoutKey_IsError** — `{ "entitySet":"Customers", "filter":"..." }`.  
186. **OdataGet_DollarKey_IgnoredMissingKey** — `{ "entitySet":"Customers", "$key":"1" }`.  
187. **OdataGet_KeyAsNumber_1_SucceedsNumeric** — JSON number `1` → `GetRawText()` `"1"` → unquoted.  
188. **OdataGet_KeyAsBool_true_UnquotedPath** — may 404.  
189. **OdataGet_KeyAsArray_IsErrorOr404**  
190. **OdataGet_KeyAsObject_CompositeAttempt** — `{ "key": { "OrderID": 10248, "ProductID": 11 } }` → raw JSON object string, quoted as a whole → wrong path → 404 `IsError`. Documents that composite must be a string until structured keys exist.  
191. **OdataGet_ExtraUnknownProps_Ignored**  
192. **OdataGet_FilterOnGet_IsForwardedAsQueryOption** — `{ entitySet, key, filter }` **does** copy `filter` onto GET by key. Twin `Customers(1)?$filter=...`. Assert that; do not strip it in tests.

### Malformed payloads

193. **OdataGet_EmptyKeyString_IsError**  
194. **OdataGet_KeyWithEmoji_QuotedSent_404Or400**  
195. **OdataGet_KeyOBrienApostrophe_Doubled** — `O'Brien` → `'O''Brien'`.  
196. **OdataGet_JsonRpc_Malformed**  
197. **OdataGet_JsonRpc_WrongContentType**  
198. **OdataGet_KeyNull_IsError**

### Server failures

199. **OdataGet_UnknownKey999_IsError404** — OData8, Restier, Northwind Products(99999), TripPin People('no-such'). Twin 404. Text contains `404`.  
200. **OdataGet_UnknownSet_404**  
201. **OdataGet_UnauthorizedGet_401** — authorized-GET fixture; without header `IsError` 401. With forwarded Authorization succeeds.  
202. **OdataGet_Forbidden_403**  
203. **OdataGet_MethodNotAllowed_405** — if GET entity disabled.  
204. **OdataGet_429OnSet_RetryAfter**  
205. **OdataGet_500_StatusInText**  
206. **OdataGet_Northwind_DoesNotCreate**

### Overwhelm / rate limit / size

207. **OdataGet_MaxExpandLengthExceeded_NoHttp**  
208. **OdataGet_MaxSelectLengthExceeded_NoHttp**  
209. **OdataGet_MaxResponseBytesTiny_IsError** — expand large TripPin person graph.  
210. **OdataGet_McpHttp429**  
211. **OdataGet_ConcurrentGets_NorthwindProducts1Through10**  
212. **OdataGet_DeepExpand_TripPinFriends**

### Cross-tool interactions

213. **OdataGet_AfterCreate_SeesNewEntity** — Restier/TripPin/OData8-auth.  
214. **OdataGet_AfterUpdate_SeesPatch**  
215. **OdataGet_AfterDelete_404**  
216. **OdataGet_ThenNavigate**  
217. **OdataGet_DoesNotEqualQueryTop1WithoutFilter** — get-by-key is not a list.

---

## 10. `odata_create`

### Purpose

Creates an entity from a JSON body. POST `{entitySet}` with `application/json`. Schema requires `entitySet`+`body`. Runtime: string or object `body`, or leftover properties. **Before HTTP**, when the set's type is declared and the body is a JSON object, the body is validated per §3.1 "Pre-HTTP entity validation": required-on-create (unless `ODataMcpCatalogOptions.EnforceRequiredOnCreate` is `false`), unknown properties on closed types, JSON kind, `null` on non-nullable, enum membership, `MaxLength`. The convention test model declares `CustomerId` as `Core.Computed` and the optional strings nullable, so `{"CompanyName":"X"}` is a complete Customer; Northwind requires `CustomerID`; Northwind `Products` requires `ProductID` and `Discontinued`.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • (auth) | • | – fail | – |
| Tools | – | – | • fail | • |

Northwind cells are **failure** happy-paths (read-only service): `IsError` with 4xx/405.

### Happy path

218. **OdataCreate_OData8_WithAuthorization_Returns201AndVisibleToGet**  
    - **Setup:** in-process with `Authorization` on the MCP HTTP context (or executor-with-auth).  
    - **Call:** `{ entitySet: Customers, body: "{\"CompanyName\":\"Fabrikam\"}" }`.  
    - **Twin:** POST `/odata/Customers` same JSON with Authorization.  
    - **Expect:** success 201/200; GET/query shows Fabrikam.

219. **OdataCreate_OData8_ObjectBodyNotString_SameAsString** — `body` as JSON object `{ "CompanyName": "ObjectCo" }`.

220. **OdataCreate_OData8_LeftoverPropertiesWithoutBody_PostsThem** — `{ entitySet, CompanyName: "Loose" }` no `body`.

221. **OdataCreate_Restier_CustomerNorthwind_VisibleToHttpFilter**  
    - **Call:** body `{ "Id": 3, "CompanyName": "Northwind" }` (or next id).  
    - **Twin:** POST `odata/Customers`.  
    - **Expect:** HTTP GET filter sees it (already partially covered; keep as create-tool-first).

222. **OdataCreate_TripPin_PersonUniqueUserName_ThenGetThenDelete**  
    - **Call:** POST People with unique `UserName`, `FirstName`, `LastName`, required TripPin fields (`Emails`, `AddressInfo` as the service requires).  
    - **Twin:** POST live People.  
    - **Expect:** get by key succeeds; delete cleans up.

223. **OdataCreate_JsonRpcToolsCall_Restier**  
224. **OdataCreate_EqualsCreateCustomerNamed_Restier** — named `create_customer` with property args vs generic body.

### Misunderstood parameters

225. **OdataCreate_MissingEntitySet_IsError**  
226. **OdataCreate_EntityInsteadOfEntitySet_IsError**  
227. **OdataCreate_IdInsteadOfEntitySet_IsError**  
228. **OdataCreate_UsingQueryArgs_FilterTop_AsOnlyExtras_PostsEmptyOrFilterAsProperty** — `{ entitySet: Customers, filter: "x", top: 1 }` without body: leftover serialization **includes `filter` and `top` in JSON body**. Expect OData 400 or ignored properties, **not** a query. Assert posted body contains those keys unless runtime later excludes query names on generic create (today `ReadBody` only strips `entitySet`/`key`). Lock current behavior: query names **are** posted on generic create without `body`.  
229. **OdataCreate_UsingGetArgs_KeyInCreate_KeyStrippedFromBody** — `{ entitySet, key, CompanyName }` without body: `key` excluded from JSON; CompanyName posted.  
230. **OdataCreate_BodyAsNumber_RawTextPosted** — likely 400.  
231. **OdataCreate_BodyAsArray_PostedArray_400**  
232. **OdataCreate_BodyAsBool_400**  
233. **OdataCreate_DollarBody_IgnoredMissingBodySynthesizedFromRest**  
234. **OdataCreate_SwappedBodyAndEntitySet** — entitySet is JSON, body is `"Customers"` → 404/400.

### Malformed payloads

235. **OdataCreate_EmptyBodyString_PostsEmpty_OData400Or415** — schema required but runtime posts `""`.  
236. **OdataCreate_BodyOpenBraceOnly_MalformedJson_OData400** — `body: "{"`.  
237. **OdataCreate_BodyNullJson_TreatedMissing_SynthesizesLeftovers**  
238. **OdataCreate_BodyLiteralNullString**  
239. **OdataCreate_InvalidJson_TrailingComma**  
240. **OdataCreate_BinaryGarbageBody**  
241. **OdataCreate_UnicodeCompanyName_SucceedsRestierAndTripPin** — `CompanyName: "北風 😀"`.  
242. **OdataCreate_MissingRequiredCompanyName_OData8_400** — with auth, empty CompanyName. Twin 400. Status in tool text.  
243. **OdataCreate_JsonRpc_Malformed**  
244. **OdataCreate_JsonRpc_WrongContentTypeOnMcp** — MCP 415/400, not OData.

### Server failures

245. **OdataCreate_OData8_WithoutAuthorization_IsError401** — tool `IsError`, text `401`. Twin POST without header 401. **In-process must forward Authorization when present** (case 246).  
246. **OdataCreate_OData8_AuthorizationForwardedFromMcpHttpContext_Succeeds** — JSON-RPC tools/call with `Authorization` on POST `/odata/mcp`; in-process OData POST must include the same header.  
247. **OdataCreate_OData8_Forbidden_403**  
248. **OdataCreate_UnknownSet_404**  
249. **OdataCreate_Northwind_Products_IsError4xxOr405** — read-only. Text has status.  
250. **OdataCreate_405OnReadOnlyController**  
251. **OdataCreate_409DuplicateKey_RestierOrFixture**  
252. **OdataCreate_412Precondition** — if fixture uses If-Match on POST (rare); else 428 on missing precondition for a picky controller.  
253. **OdataCreate_413ODataBodyTooLarge** — OData-layer limit smaller than MCP guard.  
254. **OdataCreate_415UnsupportedMedia** — only if a path can send non-JSON; tool always JSON. Twin with `text/plain` 415.  
255. **OdataCreate_428PreconditionRequired**  
256. **OdataCreate_429OnSet_RetryAfter**  
257. **OdataCreate_500**  
258. **OdataCreate_503**  
259. **OdataCreate_TripPin_MissingRequiredFields_400**

### Overwhelm / rate limit / size

260. **OdataCreate_MaxRequestBodyBytesExceeded_IsErrorNoHttp** — body length > 262144 (or configured 64). Text contains `request body exceeds the maximum size`.  
261. **OdataCreate_MaxRequestBodyBytesExact_Allowed**  
262. **OdataCreate_MaxResponseBytesTinyOn201Payload_IsErrorAfterODataSucceeds** — entity was created; tool still `IsError` on oversized response. Follow-up GET should see the row (side effect). Document this.  
263. **OdataCreate_McpHttp429**  
264. **OdataCreate_ConcurrentCreates_RestierUniqueIds**  
265. **OdataCreate_WideModel_CreateOnUnnamedSetViaGenericStillWorks** — set without named family.

### Cross-tool interactions

266. **OdataCreate_ThenGet_ThenUpdate_ThenDelete_Restier**  
267. **OdataCreate_ThenQueryFilter_SeesRow**  
268. **OdataCreate_ThenNamedGet**  
269. **OdataCreate_UnauthenticatedThenAuthenticated_OData8**  
270. **OdataCreate_DoesNotCallQuery** — wire is POST not GET.

---

## 11. `odata_update`

### Purpose

Updates an entity with PATCH `{entitySet}({key})`. Required `entitySet`, `key`, `body`. Idempotent hint true. **Before HTTP** the body gets the same validation as create except required-on-create: leftover query option names (`top`, `$filter`) on a closed type are `Unknown property` errors, `null` on a non-nullable property is `cannot be null`, and omitted fields are simply not sent.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • auth | • | – fail | – |
| Tools | – | – | • fail | • |

### Happy path

271. **OdataUpdate_OData8_WithAuthorization_PatchesCompanyName_MatchesTwin**  
    - **Call:** key `1`, body `{ "CompanyName": "Updated" }`.  
    - **Twin:** PATCH `/odata/Customers(1)` with Authorization.  
    - **Expect:** success; GET shows Updated.

272. **OdataUpdate_OData8_ObjectBody**  
273. **OdataUpdate_OData8_LeftoverPropertiesWithoutBodyKey** — `{ entitySet, key, CompanyName: "LoosePatch" }` — `ReadBody` serializes leftovers including nothing stripped except entitySet/key.  
274. **OdataUpdate_Restier_PatchCompanyName_VisibleToHttpGet**  
275. **OdataUpdate_TripPin_PatchFirstName_ThenGet** — People('russellwhyte') is shared; prefer a person created in-test.  
276. **OdataUpdate_JsonRpcToolsCall_OData8WithAuth**  
277. **OdataUpdate_EqualsUpdateCustomerNamed**  
278. **OdataUpdate_EmptySuccess204_TextContains204** — if service returns no body.

### Misunderstood parameters

279. **OdataUpdate_MissingKey_IsError**  
280. **OdataUpdate_MissingEntitySet_IsError**  
281. **OdataUpdate_MissingBodyAndNoLeftovers_PostsEmptyObjectOrEmpty** — `{ entitySet, key }` only: leftover dict empty `{}`. May 400.  
282. **OdataUpdate_IdInsteadOfKey_IsErrorMissingKey**  
283. **OdataUpdate_EntityInsteadOfEntitySet**  
284. **OdataUpdate_DollarFilterAsBody_IgnoredAsQueryNotSentOnPatch** — update does **not** pass query options (`UpdateAsync` queryOptions null). `$filter` leftover may land in body JSON.  
285. **OdataUpdate_UsingQueryArgs_TopOnUpdate_GoesIntoBodyIfNoBody**  
286. **OdataUpdate_UsingGetArgsOnly_NoBody**  
287. **OdataUpdate_SwappedKeyAndEntitySet**  
288. **OdataUpdate_KeyAsNumber**  
289. **OdataUpdate_BodyAsArray_400**  
290. **OdataUpdate_PutVsPatch_ToolAlwaysPatch** — wire method PATCH, never PUT.

### Malformed payloads

291. **OdataUpdate_EmptyBodyString**  
292. **OdataUpdate_MalformedJsonBrace**  
293. **OdataUpdate_NullBody**  
294. **OdataUpdate_UnicodePatch**  
295. **OdataUpdate_JsonRpc_Malformed**  
296. **OdataUpdate_JsonRpc_WrongContentType**

### Server failures

297. **OdataUpdate_OData8_WithoutAuthorization_401**  
298. **OdataUpdate_OData8_AuthorizationForwarded_Succeeds**  
299. **OdataUpdate_UnknownKey_404**  
300. **OdataUpdate_UnknownSet_404**  
301. **OdataUpdate_Northwind_IsError4xxOr405**  
302. **OdataUpdate_403**  
303. **OdataUpdate_405IfPatchDisabled**  
304. **OdataUpdate_409**  
305. **OdataUpdate_412IfMatchFailure** — fixture If-Match.  
306. **OdataUpdate_413**  
307. **OdataUpdate_415**  
308. **OdataUpdate_428**  
309. **OdataUpdate_429RetryAfter**  
310. **OdataUpdate_500**  
311. **OdataUpdate_503**  
312. **OdataUpdate_TripPin_PatchImmutableKey_400**

### Overwhelm / rate limit / size

313. **OdataUpdate_MaxRequestBodyBytesExceeded_NoHttp**  
314. **OdataUpdate_MaxResponseBytesTiny**  
315. **OdataUpdate_McpHttp429**  
316. **OdataUpdate_ConcurrentPatches_LastWriteWinsOr409** — Restier two patches same key.

### Cross-tool interactions

317. **OdataUpdate_AfterCreate_BeforeDelete**  
318. **OdataUpdate_ThenQueryFilterNewName**  
319. **OdataUpdate_IdempotentSecondPatchSameBody_Succeeds**  
320. **OdataUpdate_DoesNotDelete**

---

## 12. `odata_delete`

### Purpose

Deletes an entity by key. DELETE `{entitySet}({key})`. Destructive + idempotent hints. No body.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • | • | – fail | – |
| Tools | – | – | • fail | • |

Note: convention `CustomersController.Delete` does **not** currently require Authorization. Tests must not assume 401 on delete unless a fixture is added. Add **DeleteRequiresAuthCustomersController** for the 401-delete cell.

### Happy path

321. **OdataDelete_OData8_ExistingKey_204ThenGet404** — create-or-seed extra row if deleting Contoso would break other tests; prefer create-then-delete. Twin DELETE.  
322. **OdataDelete_Restier_CreatedRow_ThenGet404**  
323. **OdataDelete_TripPin_CreatedPerson_ThenGet404**  
324. **OdataDelete_EmptyBody204_TextContainsStatus**  
325. **OdataDelete_JsonRpcToolsCall_Restier**  
326. **OdataDelete_EqualsDeleteCustomerNamed**  
327. **OdataDelete_IdempotentSecondDelete_404IsError** — first success; second `IsError` 404. (Hint says idempotent; OData still 404s. Assert 404, do not fake success.)

### Misunderstood parameters

328. **OdataDelete_MissingKey_IsError**  
329. **OdataDelete_MissingEntitySet_IsError**  
330. **OdataDelete_IdInsteadOfKey**  
331. **OdataDelete_EntityInsteadOfEntitySet**  
332. **OdataDelete_Swapped**  
333. **OdataDelete_UsingQueryArgs_FilterIgnoredNotAQuery** — DELETE still issued to `Customers('filter')` if filter used as key mistakenly; with both entitySet+key, filter is **not** forwarded (DeleteAsync queryOptions null).  
334. **OdataDelete_UsingGetArgs_OkIfEntitySetAndKeyPresent**  
335. **OdataDelete_BodyOnDelete_Ignored**  
336. **OdataDelete_KeyAsNumber**  
337. **OdataDelete_DollarKey_MissingKeyError**

### Malformed payloads

338. **OdataDelete_EmptyKey**  
339. **OdataDelete_EmojiKey_404**  
340. **OdataDelete_JsonRpc_Malformed**  
341. **OdataDelete_JsonRpc_WrongContentType**  
342. **OdataDelete_ApostropheKey_FormatKeyDoubled**

### Server failures

343. **OdataDelete_UnknownKey999_404** — OData8, Restier, Northwind, TripPin.  
344. **OdataDelete_UnknownSet_404**  
345. **OdataDelete_Northwind_405Or4xx**  
346. **OdataDelete_Unauthorized_401** — auth-delete fixture.  
347. **OdataDelete_Forbidden_403**  
348. **OdataDelete_405**  
349. **OdataDelete_409ConflictIfConstrained** — delete customer with orders if the service rejects.  
350. **OdataDelete_412**  
351. **OdataDelete_428**  
352. **OdataDelete_429**  
353. **OdataDelete_500**  
354. **OdataDelete_503**  
355. **OdataDelete_413NotApplicableNoBody** — confirm no body guard.

### Overwhelm / rate limit / size

356. **OdataDelete_McpHttp429**  
357. **OdataDelete_SetRateLimit429**  
358. **OdataDelete_ConcurrentDeletesSameKey_OneSuccessOne404**  
359. **OdataDelete_MaxResponseBytesIrrelevantOn204**

### Cross-tool interactions

360. **OdataDelete_AfterCreateGet**  
361. **OdataDelete_ThenQueryDoesNotList**  
362. **OdataDelete_ThenNavigate_404**  
363. **OdataDelete_DoesNotPatch**

---

## 13. `odata_navigate`

### Purpose

Follows a navigation property from a key. GET `{entitySet}({key})/{navigation}`. Required `entitySet`, `key`, `navigation`. Query options (`filter`, `top`, `skip`, `count`, `select`, `orderby`, `expand`) are advertised in the schema and **are** applied (`NavigateAsync` → `ReadQueryOptions`).

There is **no** named `list_{set}_{nav}` tool. Navigation is this generic only.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • Orders | – no nav | – | – |
| Tools | – | – | • Product/Category | • Friends/Trips |

Restier `McpCustomer` has **no** navigations. Restier cases are missing-nav failures, not happy path.

### Happy path

364. **OdataNavigate_OData8_Customer1Orders_MatchesGetCustomers1Orders**  
    - **Twin:** GET `/odata/Customers(1)/Orders`.  
    - **Expect:** same payload (possibly empty array).

365. **OdataNavigate_OData8_OrdersToCustomer_ToOne** — if Orders seeded with CustomerId; else Northwind/TripPin.

366. **OdataNavigate_Northwind_Product1Category_ToOne_MatchHttp** — GET `Products(1)/Category`.

367. **OdataNavigate_Northwind_CustomerALFKIOrders_ToMany_MatchHttp**

368. **OdataNavigate_Northwind_Product1OrderDetails_ToMany_Page**

369. **OdataNavigate_TripPin_RussellwhyteFriends_ToMany_MatchHttp**

370. **OdataNavigate_TripPin_RussellwhyteTrips_ToMany_MatchHttp**

371. **OdataNavigate_TripPin_Friends_OmitTop_DoesNotInjectTop_ServerMayPage** — wire no `$top`.

372. **OdataNavigate_TripPin_Friends_SkipTopFilterOrderbySelect_MatchHttp** — `$`-prefixed twin on `People('russellwhyte')/Friends`.

373. **OdataNavigate_TripPin_Friends_CountTrue**

374. **OdataNavigate_PagingNavCollection_NextLinkWhenPageSizeSet** — convention fixture `Customers(1)/Orders` with PageSize.

375. **OdataNavigate_JsonRpcToolsCall_NorthwindCategory**

376. **OdataNavigate_ExpandOnNavigation_IfAllowed**

### Misunderstood parameters

377. **OdataNavigate_MissingNavigation_IsErrorContainsNavigation**  
378. **OdataNavigate_MissingKey_IsError**  
379. **OdataNavigate_MissingEntitySet_IsError**  
380. **OdataNavigate_NavInsteadOfNavigation_IsErrorMissingNavigation** — `{ ..., "nav": "Orders" }`.  
381. **OdataNavigate_NameInsteadOfNavigation** — `{ "name": "Orders" }`.  
382. **OdataNavigate_PropertyInsteadOfNavigation** — `{ "property": "Orders" }`.  
383. **OdataNavigate_UsingQueryArgsWithoutNavigation**  
384. **OdataNavigate_UsingGetArgsWithoutNavigation**  
385. **OdataNavigate_SwappedNavigationAndKey** — key `Orders`, navigation `1`.  
386. **OdataNavigate_DollarExpandAsNavigationName_404** — `navigation: "$expand"`.  
387. **OdataNavigate_FilterAsNavigation_404**  
388. **OdataNavigate_NavigationAsNumber**  
389. **OdataNavigate_EntityInsteadOfEntitySet**

### Malformed payloads

390. **OdataNavigate_EmptyNavigation**  
391. **OdataNavigate_WhitespaceNavigation**  
392. **OdataNavigate_EmojiNavigation_404**  
393. **OdataNavigate_InjectionNavigation_OrdersCommaHack_404Or400** — `navigation: "Orders/$count"`. Twin same path.  
394. **OdataNavigate_JsonRpc_Malformed**  
395. **OdataNavigate_UnicodeNavNotDeclared**

### Server failures

396. **OdataNavigate_WrongNameNotANav_IsError404or400** — OData8 `NotANav`; Restier `Orders` (undeclared); Northwind `Nope`. Twin same. Status in text.  
397. **OdataNavigate_KeyNotFound_404** — `Customers(999)/Orders`.  
398. **OdataNavigate_UnknownSet_404**  
399. **OdataNavigate_Restier_AnyNavigation_404or400** — no navs on McpCustomer.  
400. **OdataNavigate_401** — authorized nav fixture.  
401. **OdataNavigate_403**  
402. **OdataNavigate_405**  
403. **OdataNavigate_429OnCustomersPath**  
404. **OdataNavigate_500**  
405. **OdataNavigate_ToOneMissingRelated_204OrNullOr404** — assert twin parity.

### Overwhelm / rate limit / size

406. **OdataNavigate_MaxFilterLengthExceeded_NoHttp**  
407. **OdataNavigate_MaxExpandLengthExceeded_NoHttp**  
408. **OdataNavigate_MaxSelectLengthExceeded_NoHttp**  
409. **OdataNavigate_MaxResponseBytesTiny_Friends**  
410. **OdataNavigate_DeepExpandFriendsFriends**  
411. **OdataNavigate_McpHttp429**  
412. **OdataNavigate_ConcurrentFriendsAndTrips**  
413. **OdataNavigate_DoesNotAddDefaultTopOnToMany**

### Cross-tool interactions

414. **OdataNavigate_AfterQueryPeople_UsesUserNameKey**  
415. **OdataNavigate_ThenQueryIsNotTheSame** — navigate path ≠ entity set query.  
416. **OdataNavigate_PageNavSet_SkipTopEqualsTwin**  
417. **OdataNavigate_AfterCreateOrderForCustomer** — if writable.

---

## 14. `odata_list_operations`

### Purpose

Lists the **unbound** operations of the service as compact signatures. No arguments. JSON `{ operations: { "Name": "(arg: type, arg?: type) -> Return // writes; docs" } }` — functions first then actions, in declaration order; `// writes` marks actions; parameter and return types use the [TYPE-SHAPES.md](./TYPE-SHAPES.md) §1.3 tokens; `docs` only when CSDL has them. No `kind`, no `isBound`, no `parameters` array. When nothing unbound is declared the payload is `{}` (the section is omitted, never an empty array). Text `Declared operations: {count}.` Bound operations are **not** here; they are listed on `odata_describe_type` for their binding type and in `odata_describe_model` complete. Catalog-only (no OData HTTP).

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • rate-limit model / operations-only | • (may be empty) | – | – |
| Tools | – | – | • often empty | • GetNearestAirport, ShareTrip, ResetDataSource |

### Happy path

418. **ListOperations_OData8_RateLimitModel_ContainsMostValuableFunction** — `operations.MostValuable == "() -> int"`.  
419. **ListOperations_OData8_OperationsOnly_ContainsMostValuableGetStatusReset** — `operations.GetStatus == "(code?: string) -> string"`; `operations.Reset == "() // writes"`.  
420. **ListOperations_TripPin_UnboundOnly_ShareTripIsOnDescribeType** — payload is exactly `{"operations":{"GetPersonWithMostFriends":"() -> Person","GetNearestAirport":"(lat: number, lon: number) -> Airport","ResetDataSource":"() // writes"}}`; `ShareTrip` absent from the list and present in `odata_describe_type` `People`.  
420.1. **ListOperations_UnboundOnly_CompactSignatures** — fixture with a collection-bound `Top`: list carries `Ping` and `Reset` only; no `kind`, `isBound`.  
420.2. **ListOperations_None_EmptyObject** — model with no unbound operations → `{}` and `Declared operations: 0.`.  
421. **ListOperations_Northwind_EmptyOrDeclaredOnly** — do not invent ops.  
422. **ListOperations_RestierCustomerApi_EmptyArraySucceeds**  
423. **ListOperations_DocumentedOperation_IncludesCsdlDescription**  
424. **ListOperations_JsonRpcToolsCall_OperationsOnlyHost**  
425. **ListOperations_FewTablesNoOps_DeclaredOperations0**

### Misunderstood parameters

426. **ListOperations_ExtraEntitySet_IgnoredNoHttp**  
427. **ListOperations_FilterTop_Ignored**  
428. **ListOperations_NameArg_IgnoredDoesNotFilter** — still full list, not `odata_describe_type`.  
429. **ListOperations_UsingCallArgs_BodyName_Ignored**

### Malformed payloads

430. **ListOperations_NullArgs_Succeeds**  
431. **ListOperations_JsonRpc_Malformed**  
432. **ListOperations_JsonRpc_WrongContentType**  
433. **ListOperations_HugeExtraPayload_StillListsOrMcp413**

### Server failures

434. **ListOperations_DoesNotHitOData_SoNo404**  
435. **ListOperations_UnknownToolIfMisspelled_odata_list_operation** — singular; `Unknown tool`.

### Overwhelm / rate limit / size

436. **ListOperations_MaxResponseBytesTiny_IsError**  
437. **ListOperations_McpHttp429**  
438. **ListOperations_FunctionRateLimitSpent_StillLists** — listing does not call MostValuable.  
439. **ListOperations_WideModelNoOps_Count0**  
440. **ListOperations_Concurrent**

### Cross-tool interactions

441. **ListOperations_ThenCallEachUnboundFunction_Get** — operations-only `MostValuable`, `GetStatus` with `code`.  
442. **ListOperations_ThenCallEachUnboundAction_Post** — `Reset`.  
443. **ListOperations_ThenCallBound_ShareTrip_RequiresEntitySetKey** — `ShareTrip` is read from `odata_describe_type` `People` (not the list); calling it without `entitySet`/`key` errors.  
444. **ListOperations_AgreesWithMetadataFunctionsAndActions** — twin `$metadata`.  
445. **ListOperations_AspNetCore_NoShutdownInToolsList**

---

## 15. `odata_call`

### Purpose

Calls a declared function (GET) or action (POST) by `name`, with every argument in `parameters` (a JSON object keyed by the declared parameter names as listed on `odata_describe_type` for bound operations or `odata_list_operations` for unbound). Instance-bound: also `entitySet` and `key`; path `{entitySet}({key})/{name}`. Collection-bound: `entitySet` only; path `{entitySet}/{name}`. Unbound: neither; path `{name}` or `{name}(p=v,…)`. There is no `body` and no other top-level argument. Everything is validated against the EDM **before HTTP** (§3.1 "Function / action parameters"): unknown name → `Operation '{name}' is not declared in the model.`; wrong binding, unknown/missing/mistyped parameters, and stringified `parameters` each return `isError` with the declared signature and send nothing. Actions matched before functions, case-insensitive.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • MostValuable / GetStatus / Reset | – unless API declares ops | – | – |
| Tools | – | – | • fail unknown or empty | • GetNearestAirport, ResetDataSource, ShareTrip |

### Happy path

446. **OdataCall_OData8_UnboundFunctionMostValuable_GetMatchesHttp**  
    - **Call:** `{ "name": "MostValuable" }`.  
    - **Twin:** GET `/odata/MostValuable` (or `MostValuable()`).  
    - **Expect:** same int payload. Wire GET, no body.

447. **OdataCall_OData8_UnboundFunctionGetStatusWithCode_PathContainsCode**  
    - **Call:** `{ name: GetStatus, code: "open" }`.  
    - **Twin:** GET `GetStatus(code='open')` (string FormatKey).  
    - **Expect:** parity.

448. **OdataCall_OData8_UnboundActionReset_Post**  
    - **Call:** `{ name: Reset, body: "{}" }`.  
    - **Twin:** POST `/odata/Reset`.  
    - **Expect:** wire POST JSON.

449. **OdataCall_OData8_OperationsOnly_SameThreeCalls**

450. **OdataCall_TripPin_GetNearestAirport_LatLon_MatchHttp**  
    - **Call:** `{ name: GetNearestAirport, parameters: { lat: 33, lon: -118 } }`.  
    - **Twin:** GET `GetNearestAirport(lat=...,lon=...)`.  
    - **Expect:** airport payload.

451. **OdataCall_TripPin_ResetDataSource_Post_IsSuccessOrDocumentedError** — mutating live service; prefer a dedicated session URL. If the service forbids, `IsError` with status — still a real call.

452. **OdataCall_TripPin_BoundActionShareTrip_EntitySetPeopleKey** — `{ name: ShareTrip, entitySet: People, key: russellwhyte, parameters: { userName: "scottketchum", tripId: N } }`. Twin POST `People('russellwhyte')/ShareTrip` with body `{"userName":"scottketchum","tripId":N}`.

453. **OdataCall_TripPin_BoundFunctionIfDeclared_GetFavoriteAirline** — listed on `odata_describe_type` `People` (bound); call with `entitySet`+`key` and no `parameters`. GET `People('russellwhyte')/GetFavoriteAirline`.

454. **OdataCall_JsonRpcToolsCall_MostValuable**

455. **OdataCall_CaseInsensitiveName_mostvaluable**

### Misunderstood parameters

456. **OdataCall_MissingName_IsError**  
457. **OdataCall_OperationInsteadOfName_IsErrorMissingName**  
458. **OdataCall_FunctionInsteadOfName**  
459. **OdataCall_IdInsteadOfName**  
460. **OdataCall_BoundWithoutEntitySet_IsErrorMissingEntitySet** — ShareTrip without entitySet → `ShareTrip is bound to Person. Pass entitySet and key. Signature: ShareTrip(userName: string, tripId: int) // writes`; no HTTP.  
461. **OdataCall_BoundWithoutKey_IsErrorMissingKey** — same message; no HTTP.  
462. **OdataCall_UnboundWithEntitySetAndKey_IsError** — `MostValuable is unbound. Omit entitySet and key.`; no HTTP.  
463. **OdataCall_UsingQueryArgs_FilterTopLevel_IsError** — top-level `filter` → `Unexpected argument 'filter'. Put operation arguments in parameters as a JSON object.`; no HTTP.  
464. **OdataCall_UsingGetArgs_OnUnbound_IsError** — `entitySet`/`key` on an unbound call is the unbound error above.  
465. **OdataCall_DollarName_MissingName**  
466. **OdataCall_NameAsNumber**  
467. **OdataCall_ExtraUnknownParamOnFunction_IsErrorNoHttp** — `parameters: { foo: 1 }` → `Unknown parameter 'foo'. Declared: lat, lon.` (or `Declared: none.`); no HTTP.  
468. **OdataCall_StringParamUnquotedVsQuoted_FormatKeyQuotesStrings** — `parameters: { code: "open" }` → path `GetStatus(code='open')`.  
469. **OdataCall_ActionParametersObjectNotString** — `parameters: "{}"` (string) → `parameters must be a JSON object, not a string.`; `parameters: {}` → POST `{}`.  
469.1. **Call_InstanceBound_RequiresEntitySetAndKey** / **Call_CollectionBound_EntitySetOnly** — fixture: passing the binding parameter is `Unknown parameter 'person'. Declared: lastName.`; collection-bound `Top` with a `key` or without `entitySet` is `Top is collection-bound. Pass entitySet; omit key. Signature: …`; the valid call is GET `People/Top(count=2)`.  
469.2. **Call_ParameterValidation_FailsBeforeHttp** — missing non-nullable → `Missing parameter 'lat'. Signature: GetNearestAirport(lat: number) -> string`; wrong kind → `Parameter 'lat' must be a JSON number. Signature: …`; top-level argument → `Unexpected argument 'lat'. …`; `body` → `Unexpected argument 'body'. …`.  
469.3. **Call_LiteralForms_EnumDurationAndAlias** — enum number `1` → `NS.Color'Green'` on the URL and `"Green"` in an action body; `Edm.Duration` → `duration'PT5S'`; GUID raw; complex argument → `at=@at` with `?@at=<escaped json>` in the path; unknown member → `Parameter 'color' must be one of Red, Green; 'Blue' is not a member.`

### Malformed payloads

470. **OdataCall_EmptyName**  
471. **OdataCall_ParametersMalformedString_IsError** — `parameters: "{"` → `parameters must be a JSON object, not a string.`  
472. **OdataCall_BodyArgument_IsErrorNoHttp** — any `body` argument → `Unexpected argument 'body'. …`; nothing sent.  
473. **OdataCall_JsonRpc_Malformed**  
474. **OdataCall_UnicodeOperationName_NotDeclaredError**  
475. **OdataCall_EmojiName_NotDeclared**

### Server failures

476. **OdataCall_UnknownOperationGhost_IsErrorNotDeclared_NoHttp** — text `Operation 'Ghost' is not declared in the model.`  
477. **OdataCall_Northwind_AnyNameNotInModel_NotDeclared**  
478. **OdataCall_DeclaredButOData404** — typo path if binding wrong.  
479. **OdataCall_MissingFunctionParam_OData400** — GetStatus without `code`: `code` is **nullable** in the convention model, so it is omitted from the path and the service answers (twin GET `GetStatus`). **OdataCall_MissingFunctionParam_FailsBeforeHttp** — TripPin `GetNearestAirport` without `lon` (non-nullable) → `Missing parameter 'lon'. Signature: GetNearestAirport(lat: number, lon: number) -> Airport`; no HTTP.  
480. **OdataCall_401**  
481. **OdataCall_403**  
482. **OdataCall_405PostOnFunctionOrGetOnAction** — if someone used wrong verb internally, runtime picks GET vs POST from kind; assert function never POST.  
483. **OdataCall_409**  
484. **OdataCall_412**  
485. **OdataCall_413Body**  
486. **OdataCall_415**  
487. **OdataCall_428**  
488. **OdataCall_429FunctionBudget_MostValuableSecondCall** — function limiter 1; second call `IsError` 429 Retry-After. Customers query still works.  
489. **OdataCall_500**  
490. **OdataCall_503**  
491. **OdataCall_RestierCustomerApi_UnknownOp_NotDeclared**

### Overwhelm / rate limit / size

492. **OdataCall_HugeUnknownParameterOnAction_NoHttp** — a 262,145-byte value under an undeclared name is `Unknown parameter 'payload'. Declared: none.` with no HTTP; `MaxRequestBodyBytes` still guards the serialized `parameters` of declared actions.  
493. **OdataCall_MaxResponseBytesTiny**  
494. **OdataCall_McpHttp429VsFunction429_AreDifferentLayers** — MCP permit 1 vs function permit 1. First layer HTTP 429 on `/mcp`; function layer is tool `IsError` 429.  
495. **OdataCall_ConcurrentMostValuable_Until429**  
496. **OdataCall_OperationsOnly_CallWhenNoSets**  
497. **OdataCall_WideModel_NoOps_Ghost**

### Cross-tool interactions

498. **OdataCall_AfterListOperations_EveryUnboundFunctionOnce_OData8OpsOnly**  
499. **OdataCall_ShareTrip_ThenGetPerson**  
500. **OdataCall_Reset_ThenQueryPeopleStillWorks_TripPin** — careful with live reset; isolate session.  
501. **OdataCall_DoesNotUseOdataQueryPath** — wire path is operation, not entity set.

---

## 16. Named `list_{set}`

### Purpose

Entity-set-bound query. Name `list_{ToSnakeCase(set.Name)}` (e.g. `list_customers`, `list_products`, `list_people`, `list_order_details`, `list_client_customers`). Binds `entitySet` from the catalog; arguments are only query options. Description reminds: query parameter names do not include `$`. Same runtime as `odata_query` after injecting `entitySet`.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • `list_customers` | • `list_customers` | – | – |
| Tools | – | – | • pin `IncludeEntitySets=["Products","Customers"]` | • `list_people` |

When `MaxNamedTools` is too small for a family, **no** `list_*` for that set; use generic `odata_query` (negative catalog cases below).

### Happy path

502. **ListCustomers_OData8_MatchesOdataQueryCustomers_NoTopInjected**  
503. **ListCustomers_OData8_SkipTopOrderby_MatchesGenericAndHttp**  
504. **ListCustomers_OData8_FilterSelectExpandCount**  
505. **ListCustomers_Restier_MatchesGenericAndHttp**  
506. **ListCustomers_Restier_Skip1Top2_EqualsOdataQuery** — already sketched in RestierPagingTests; keep as named-first.  
507. **ListClientCustomers_Paging_OmitTop_AllFive**  
508. **ListServerCustomers_Paging_NextLinkWithoutInjectedTop**  
509. **ListProducts_Northwind_Top1_MatchHttpAndGeneric**  
510. **ListPeople_TripPin_ExpandFriends_MatchGeneric**  
511. **ListOrderDetails_Northwind_IfFamilyFitsCap** — snake `list_order_details`.  
512. **ListCustomers_JsonRpcToolsCall_Restier**  
513. **ListCustomers_ToolsListContains_WhenCapAllows**  
514. **ListCustomers_DescriptionContainsNoDollarReminder**

### Misunderstood parameters

515. **ListCustomers_PassingEntitySet_OverriddenByCatalogBinding** — `{ entitySet: "Products", top: 1 }` still queries **Customers** because InvokeNamedAsync overwrites `entitySet`. **Lock this.** Do not query Products.  
516. **ListCustomers_PassingKey_IgnoredNotAGet**  
517. **ListCustomers_DollarFilter_IgnoredUnfiltered**  
518. **ListCustomers_UsingGetArgsOnlyKey_ReturnsCollection**  
519. **ListCustomers_TopAsArray_400**  
520. **ListCustomers_CountAsString**  
521. **ListCustomers_UnknownProp_Ignored**  
522. **ListProducts_CalledAsList_Product_UnknownTool** — wrong snake.

### Malformed payloads

523. **ListCustomers_EmptyArgs_EqualsUnpagedQuery**  
524. **ListCustomers_MalformedFilter**  
525. **ListCustomers_EmojiFilter**  
526. **ListCustomers_JsonRpc_Malformed**  
527. **ListCustomers_InjectionFilter**

### Server failures

528. **ListCustomers_OData429_StatusAndRetryAfter**  
529. **ListCustomers_401IfSetAuthorized**  
530. **ListCustomers_404IfRenamedSetNotPossible** — tool still bound; OData could 404 if route missing.  
531. **ListGhost_UnknownTool** — `list_ghost` not in catalog.  
532. **ListCustomers_500**  
533. **ListProducts_Northwind_WriteNotInvolved**

### Overwhelm / rate limit / size

534. **ListCustomers_MaxFilterLengthExceeded**  
535. **ListCustomers_MaxExpandLengthExceeded**  
536. **ListCustomers_MaxSelectLengthExceeded**  
537. **ListCustomers_MaxResponseBytesTiny**  
538. **ListCustomers_McpHttp429**  
539. **ListCustomers_VsListProducts_IndependentSetLimits**  
540. **ListRows000_WideModel_OnlyIfNamedFamilyEmitted**  
541. **ListCustomers_DoesNotAddDefaultTop**  
542. **ListCustomers_Concurrent**

### Cross-tool interactions

543. **ListCustomers_ThenGetCustomer_SameRow**  
544. **ListCustomers_ThenOdataNavigate**  
545. **ListCustomers_FamilyComplete_GetCreateUpdateDeletePresentWhenFlagsTrue**  
546. **ListCustomers_AbsentWhenCapTooSmallForFamily** — `MaxNamedTools = 10` (generics already 10) → remaining 0 → **no** named tools at all.  
547. **ListCustomers_AbsentWhenRemaining4AndFamily5** — remaining 4 cannot fit 5; **no** partial `list_customers` without get/create/update/delete.  
548. **ListCustomers_PresentWhenIncludeCreateFalseReducesFamilyToFit** — family size 4 (list/get/update/delete) fits remaining 4.  
549. **ListZzzLastAlphabetical_NotEmittedWhenCapFilledByIncludeEntitySetsFirst**

---

## 17. Named `get_{type}`

### Purpose

Gets a `{Type.Name}` by key. Name `get_{ToSnakeCase(type.Name)}` (`get_customer`, `get_product`, `get_person`, `get_order_detail` if type is `Order_Detail`/`OrderDetail`). Required `key` only. Runtime injects `entitySet`.

### Matrix

Same cells as §16. Restier type `McpCustomer` → `get_mcp_customer` (**not** `get_customer`). Convention type `Customer` → `get_customer`. Northwind `Product` → `get_product`. TripPin `Person` → `get_person`. **Tests must use the actual advertised name from `tools/list`**, and also include a case that fails if someone assumes `get_customer` on Restier.

### Happy path

550. **GetCustomer_OData8_Key1_MatchesOdataGetAndHttp**  
551. **GetMcpCustomer_Restier_Key1_MatchesOdataGet** — name from catalog (`get_mcp_customer`).  
552. **GetProduct_Northwind_Key1_Matches**  
553. **GetPerson_TripPin_Russellwhyte_Matches**  
554. **GetCustomer_SelectExpand_Forwarded**  
555. **GetCustomer_JsonRpcToolsCall**  
556. **GetCustomer_NumericKeyUnquotedOnWire**  
557. **GetPerson_StringKeyQuotedOnWire**

### Misunderstood parameters

558. **GetCustomer_MissingKey_IsError**  
559. **GetCustomer_IdInsteadOfKey_IsError**  
560. **GetCustomer_EntitySetPassed_OverwrittenToBoundSet** — `{ entitySet: "Products", key: "1" }` still GET **Customers(1)**, not Products. Lock.  
561. **GetCustomer_UsingListArgs_FilterTop_WithoutKey_IsError**  
562. **GetCustomer_UsingListArgs_WithKey_FilterForwardedOnGet**  
563. **GetCustomer_DollarKey_MissingKey**  
564. **GetCustomer_KeyAsObject**  
565. **GetCustomer_SwappedIfSomeonePassesName**  
566. **GetCustomers_PluralWrongName_UnknownTool** — `get_customers`.  
567. **GetCustomer_OnRestier_UnknownToolIfTypeIsMcpCustomer**

### Malformed payloads

568. **GetCustomer_EmptyKey**  
569. **GetCustomer_EmojiKey_404**  
570. **GetCustomer_JsonRpc_Malformed**  
571. **GetPerson_OBrienKey_Apostrophe**

### Server failures

572. **GetCustomer_UnknownKey_404**  
573. **GetCustomer_401**  
574. **GetCustomer_403**  
575. **GetCustomer_429**  
576. **GetCustomer_500**  
577. **GetGhost_UnknownTool**  
578. **GetProduct_Northwind_99999_404**

### Overwhelm / rate limit / size

579. **GetCustomer_MaxExpandLengthExceeded**  
580. **GetCustomer_MaxResponseBytesTiny**  
581. **GetCustomer_McpHttp429**  
582. **GetCustomer_Concurrent**  
583. **GetPerson_DeepExpandFriends**

### Cross-tool interactions

584. **GetCustomer_AfterCreateCustomer**  
585. **GetCustomer_AfterUpdate**  
586. **GetCustomer_AfterDelete_404**  
587. **GetCustomer_ThenNavigate**  
588. **GetCustomer_FamilyNeverPartial**

---

## 18. Named `create_{type}`

### Purpose

Creates a `{Type}`. Schema is a JSON object of declared non-binary properties (**not** a `body` string) with `required` = required-on-create ([TYPE-SHAPES.md](./TYPE-SHAPES.md) §3): Northwind `create_customer` requires `CustomerID` only (live V4 Northwind declares `CompanyName` nullable); TripPin `create_person` requires `UserName`, `FirstName`, `Gender`, `FavoriteFeature`, `Features`. Enum properties carry `enum` member names; nullable properties carry `["type","null"]` (and `null` in `enum`); collections are `array` + `items`; complex types are `object`; `maxLength` only when `≤ 16`; no `description` unless CSDL has one (never the property name). Description: `Creates a {Type}.` + CSDL summary. Runtime: if `body` absent, serializes leftover properties excluding query option names, `key`, and `entitySet`. POST bound set.

`IncludeCreate=false` omits this tool; generic `odata_create` remains.

### Matrix

Writable cells: OData8 (auth), Restier, TripPin. Northwind: do not advertise success; if named create exists, calling it must `IsError`.

Restier name: `create_mcp_customer`. Convention: `create_customer`. TripPin: `create_person`. Northwind: `create_product` when family emitted.

### Happy path

589. **CreateCustomer_OData8_PropertyArgs_WithAuth_201** — `{ CompanyName: "NamedCo" }` no body. Twin POST.  
590. **CreateCustomer_OData8_ExplicitBodyString_AlsoWorks** — if `body` present, used as-is (`InvokeNamedAsync` only synthesizes when `body` missing).  
591. **CreateMcpCustomer_Restier_PropertyArgs_VisibleToHttp**  
592. **CreatePerson_TripPin_RequiredFields_ThenDelete**  
593. **CreateCustomer_JsonRpcToolsCall_Restier**  
594. **CreateCustomer_SchemaContainsCompanyName_NotDollarFilter_NotInternalSecret_NotBinary**  
595. **CreateCustomer_EqualsGenericOdataCreate**

### Misunderstood parameters

596. **CreateCustomer_UsingGenericEntitySetArg_Overwritten** — still creates on bound set.  
597. **CreateCustomer_UsingListArgs_FilterTop_ExcludedFromBody** — query option names are stripped in **named** create synthesizer (`QueryOptionNames`). Assert posted JSON does **not** contain `filter`/`top`. (Contrast generic create leftover behavior in case 228.)  
598. **CreateCustomer_UsingGetArgs_KeyExcludedFromBody**  
599. **CreateCustomer_IdInsteadOfCompanyName_May400**  
600. **CreateCustomer_BodyObjectVsProperties**  
601. **CreateCustomer_UnknownPropertyInArgs_PostedAndODataMayIgnoreOr400**  
602. **CreateCustomers_Plural_UnknownTool**

### Malformed payloads

603. **CreateCustomer_EmptyArgs_PostsEmptyObject_400**  
604. **CreateCustomer_MalformedCompanyNameOnlyBrace** — if passed via body string.  
605. **CreateCustomer_UnicodeName**  
606. **CreateCustomer_JsonRpc_Malformed**  
607. **CreateCustomer_NullPropertyValues**

### Server failures

608. **CreateCustomer_OData8_NoAuth_401**  
609. **CreateCustomer_OData8_AuthForwardedFromJsonRpc**  
610. **CreateCustomer_MissingCompanyName_400**  
611. **CreateProduct_Northwind_IsError**  
612. **CreateCustomer_409Duplicate**  
613. **CreateCustomer_403**  
614. **CreateCustomer_405**  
615. **CreateCustomer_413OData**  
616. **CreateCustomer_429**  
617. **CreateCustomer_500**  
618. **CreateGhost_UnknownTool**

### Overwhelm / rate limit / size

619. **CreateCustomer_MaxRequestBodyBytesExceeded** — huge property string.  
620. **CreateCustomer_MaxResponseBytesTiny**  
621. **CreateCustomer_McpHttp429**  
622. **CreateCustomer_ConcurrentUniqueNames**  
623. **CreateCustomer_OmittedWhenIncludeCreateFalse_UnknownTool_GenericCreateStillWorks**

### Cross-tool interactions

624. **CreateCustomer_ThenGetCustomer_ThenDeleteCustomer**  
625. **CreateCustomer_ThenListCustomersFilter**  
626. **CreateCustomer_ThenOdataQuery**  
627. **CreateCustomer_FamilyCapInteraction_See547**

---

## 19. Named `update_{type}`

### Purpose

PATCH a `{Type}`. Schema is `key` (string, first) plus the same declared property map as `create_*`; `required: ["key"]` only; no `body`. Non-nullable types do not include `null`. Description: `PATCH a {Type}. Send only fields to change; omit to keep. Do not send JSON null for required properties.` + CSDL summary. Runtime still accepts an explicit `body` string and otherwise serializes the leftover properties (everything but `entitySet` and `key`). PATCH bound set. `IncludeUpdate=false` omits tool.

Names: `update_customer`, `update_mcp_customer`, `update_person`, `update_product`.

### Happy path

628. **UpdateCustomer_OData8_Auth_PatchCompanyName_MatchTwin**  
629. **UpdateMcpCustomer_Restier_Patch_MatchHttp**  
630. **UpdatePerson_TripPin_PatchFirstName_OnCreatedPerson**  
631. **UpdateCustomer_JsonRpcToolsCall**  
632. **UpdateCustomer_ObjectBody**  
633. **UpdateCustomer_EqualsOdataUpdate**  
634. **UpdateCustomer_204EmptyBodyText**

### Misunderstood parameters

635. **UpdateCustomer_MissingKey_IsError**  
636. **UpdateCustomer_MissingBody_SynthesizesLeftoversOrEmpty** — schema requires body; runtime `ReadBody` synthesizes. `{ key: "1", CompanyName: "X" }` without body POSTs leftover properties as JSON (query names not stripped in UpdateAsync ReadBody — **only entitySet/key stripped**). Named update does **not** use the create synthesizer. Lock: `filter` leftover would be in JSON.  
637. **UpdateCustomer_IdInsteadOfKey**  
638. **UpdateCustomer_EntitySetOverwritten** — cannot retarget Products.  
639. **UpdateCustomer_UsingListArgsWithoutKey**  
640. **UpdateCustomer_UsingCreatePropertyArgsWithoutBodyOrKey**  
641. **UpdateCustomer_DollarKey**  
642. **UpdateCustomers_UnknownTool**

### Malformed payloads

643. **UpdateCustomer_MalformedBody**  
644. **UpdateCustomer_EmptyBodyString**  
645. **UpdateCustomer_Unicode**  
646. **UpdateCustomer_JsonRpc_Malformed**  
647. **UpdateCustomer_NullBody**

### Server failures

648. **UpdateCustomer_OData8_NoAuth_401**  
649. **UpdateCustomer_AuthForwarded**  
650. **UpdateCustomer_UnknownKey_404**  
651. **UpdateProduct_Northwind_IsError**  
652. **UpdateCustomer_403**  
653. **UpdateCustomer_405**  
654. **UpdateCustomer_409**  
655. **UpdateCustomer_412IfMatch**  
656. **UpdateCustomer_428**  
657. **UpdateCustomer_413**  
658. **UpdateCustomer_429**  
659. **UpdateCustomer_500**  
660. **UpdateGhost_UnknownTool**

### Overwhelm / rate limit / size

661. **UpdateCustomer_MaxRequestBodyBytesExceeded**  
662. **UpdateCustomer_MaxResponseBytesTiny**  
663. **UpdateCustomer_McpHttp429**  
664. **UpdateCustomer_Concurrent**  
665. **UpdateCustomer_OmittedWhenIncludeUpdateFalse_GenericUpdateStillWorks**

### Cross-tool interactions

666. **UpdateCustomer_AfterCreate_BeforeDelete**  
667. **UpdateCustomer_ThenGetCustomer**  
668. **UpdateCustomer_ThenListCustomers**  
669. **UpdateCustomer_IdempotentRepeat**

---

## 20. Named `delete_{type}`

### Purpose

Deletes a `{Type}` by key. Schema `{ key }` required. `IncludeDelete=false` omits tool. Destructive hint true.

Names: `delete_customer`, `delete_mcp_customer`, `delete_person`, `delete_product`.

### Happy path

670. **DeleteCustomer_OData8_CreatedRow_204ThenGet404**  
671. **DeleteMcpCustomer_Restier_CreatedRow**  
672. **DeletePerson_TripPin_CreatedPerson**  
673. **DeleteCustomer_JsonRpcToolsCall**  
674. **DeleteCustomer_EqualsOdataDelete**  
675. **DeleteCustomer_TextContainsStatusOn204**

### Misunderstood parameters

676. **DeleteCustomer_MissingKey_IsError**  
677. **DeleteCustomer_IdInsteadOfKey**  
678. **DeleteCustomer_EntitySetOverwrittenCannotDeleteProducts** — `{ entitySet: Products, key: 1 }` deletes **Customers(1)** if key 1 exists — **dangerous lock**. Test on a host where Customer 1 must not be destroyed: use a disposable created customer and pass `entitySet: Products` with that customer key; assert Products(1) still exists and the customer is gone.  
679. **DeleteCustomer_UsingListArgsWithoutKey**  
680. **DeleteCustomer_BodyIgnored**  
681. **DeleteCustomer_DollarKey**  
682. **DeleteCustomers_UnknownTool**  
683. **DeleteCustomer_KeyAsNumber**

### Malformed payloads

684. **DeleteCustomer_EmptyKey**  
685. **DeleteCustomer_EmojiKey_404**  
686. **DeleteCustomer_JsonRpc_Malformed**  
687. **DeletePerson_ApostropheKey**

### Server failures

688. **DeleteCustomer_UnknownKey_404**  
689. **DeleteProduct_Northwind_IsError**  
690. **DeleteCustomer_401AuthFixture**  
691. **DeleteCustomer_403**  
692. **DeleteCustomer_405**  
693. **DeleteCustomer_409Constraint**  
694. **DeleteCustomer_412**  
695. **DeleteCustomer_429**  
696. **DeleteCustomer_500**  
697. **DeleteGhost_UnknownTool**  
698. **DeleteCustomer_SecondCall_404**

### Overwhelm / rate limit / size

699. **DeleteCustomer_McpHttp429**  
700. **DeleteCustomer_Set429**  
701. **DeleteCustomer_ConcurrentSameKey**  
702. **DeleteCustomer_OmittedWhenIncludeDeleteFalse_GenericDeleteStillWorks**  
703. **DeleteCustomer_MaxResponseBytesOn204Unaffected**

### Cross-tool interactions

704. **DeleteCustomer_AfterCreateGetUpdate**  
705. **DeleteCustomer_ThenListCustomers_Absent**  
706. **DeleteCustomer_ThenNavigate_404**  
707. **DeleteCustomer_DoesNotCallResetDataSource**

---

## 21. `shutdown_server` (Tools host only)

### Purpose

Local-only control-plane tool. Acknowledges shutdown and cancels host lifetime after `delay_seconds` (0–10, default **2**). Optional `reason`. Returns JSON `{ "message": "Server shutdown initiated. Reason: {reason}. Shutting down in {n} second(s)." }` Default reason `User requested shutdown`. Not in AspNetCore catalogs. `destructiveHint: true`, `readOnlyHint: false`, `idempotentHint: false`, `openWorldHint: false`.

### Matrix

| Host \ Surface | OData8 | Restier | Northwind | TripPin |
|----------------|--------|---------|-----------|---------|
| AspNetCore | • **absent** | • **absent** | – | – |
| Tools | – | – | • present | • present |

### Happy path

708. **ShutdownServer_ToolsHost_ToolsListContains** — Northwind `ToolsMcpHost` extra tool; catalog.Tools itself does **not** contain it; `CreateShutdownTool` does.  
709. **ShutdownServer_DelayZero_CancelsImmediately_ReturnsJsonAck** — reason `test`. CTS cancelled. Message includes reason and `0 second(s)`.  
710. **ShutdownServer_DelayTwoDefault_WhenOmitted_Uses2** — omit `delay_seconds`; ack says 2; cancel happens after ~2s (wait up to 3s).  
711. **ShutdownServer_DelayOne_CancelsAfterOneSecond**  
712. **ShutdownServer_ReasonUnicode_JsonEncoded** — reason with quotes and emoji; payload is valid JSON (uses `JsonEncodedText`).  
713. **ShutdownServer_StdioRoundTrip_ToolsCallThenProcessExits** — real stdio host against Northwind; JSON-RPC call; process exits 0.  
714. **ShutdownServer_JsonRpcOnToolsHttpIfEnabled**

### Misunderstood parameters

715. **ShutdownServer_DelaySecondsVsDelay_s** — `{ "delay": 0 }` ignores unknown; default 2 applies (not 0). Assert not immediate cancel if we need to distinguish — use a spy CTS and wait 200ms: must **not** be cancelled yet if default 2.  
716. **ShutdownServer_Delay_secondsSnakeVsCamel** — schema is `delay_seconds`. `delaySeconds` is unknown; default 2.  
717. **ShutdownServer_ReasonAsNumber_Stringified**  
718. **ShutdownServer_ExtraEntitySet_Ignored**  
719. **ShutdownServer_UsingOdataQueryArgs_Ignored**

### Malformed payloads

720. **ShutdownServer_DelayNegative_ThrowsOrIsError** — `InvokeAsync` throws `ArgumentOutOfRangeException` for <0 or >10. Handler currently lets it throw (not mapped to `IsError` in `HandleShutdownAsync`). **Protocol test:** JSON-RPC should surface an error, not hang.  
721. **ShutdownServer_Delay11_OutOfRange**  
722. **ShutdownServer_DelayAsString2_Parsed** — string `"0"` parses.  
723. **ShutdownServer_DelayAsStringNo_Error**  
724. **ShutdownServer_DelayAsBool_ParseFail**  
725. **ShutdownServer_DelayAsArray**  
726. **ShutdownServer_EmptyReason_UsesDefaultUserRequestedShutdown**  
727. **ShutdownServer_NullReason_Default**

### Server failures

728. **ShutdownServer_AspNetCore_UnknownTool** — Invoke `shutdown_server` on A8 runtime: `Unknown tool 'shutdown_server'.`  
729. **ShutdownServer_AspNetCore_ToolsListOmits** — OData8 and Restier.  
730. **ShutdownServer_JsonRpcUnknownOnAspNetCoreMcp**  
731. **ShutdownServer_DoesNotCallOData** — no HTTP to Northwind on invoke.

### Overwhelm / rate limit / size

732. **ShutdownServer_NotAffectedByOData429**  
733. **ShutdownServer_RepeatedCallAfterCancel_SecondMayNoOpOrThrow** — document CTS already cancelled.  
734. **ShutdownServer_DelayMax10_Allowed**  
735. **ShutdownServer_ConcurrentTwoCalls_BothAck** — do not crash.

### Cross-tool interactions

736. **ShutdownServer_AfterOdataQuery_StillAcksThenDies** — query first, then shutdown.  
737. **ShutdownServer_DoesNotRemoveCatalogToolsMidCall**  
738. **ShutdownServer_TryCommandNorthwind_DoesNotRequireShutdown** — `odata-mcp try` exit 0 without calling shutdown.

---

## 22. MCP protocol handlers (not tools)

Short section so these are not forgotten. Primary focus remains tools; every generic tool still needs a JSON-RPC `tools/call` case in its own section (already listed).

### 22.1 `tools/list`

739. **ToolsList_OData8_GenericOrderThenNamed_NoShutdown** — order from §2; named after; no `shutdown_server`; no `$` in schemas; hints: query readOnly, delete destructive, update idempotent, create neither readOnly nor destructive.  
740. **ToolsList_Restier_ContainsListCustomers_OmitsShutdown**  
741. **ToolsList_ToolsHostNorthwind_ContainsGenericsPlusShutdownAtEnd**  
742. **ToolsList_MaxNamedTools15_CountLeq15_NoPartialFamily**  
743. **ToolsList_IncludeCreateFalse_NoCreateStar_StillListGet**  
744. **ToolsList_IncludeUpdateDeleteFalse_NoThoseNames**  
745. **ToolsList_ExcludeEntitySetsPeople_NoListPeople**  
746. **ToolsList_IncludeEntitySetsProductsFirst_NamedProductsFamilyBeforeAlphabetical**  
747. **ToolsList_OperationsOnly_NoNamedCrud_HasOdataCall**  
748. **ToolsList_JsonRpc_StreamableHttp_OData8** — method `tools/list`, not `{}`.  
749. **ToolsList_PaginationCursor_IfSdkSupports_NextCursorNullWhenUnpaged** — current handler returns full list, no cursor. Assert `nextCursor` absent/null. If SDK later pages, add a follow-up case; do not invent pagination in the product to pass a guess.  
750. **ToolsList_OpenWorldHintTrueOnOdataTools_FalseOnShutdown**  
751. **ToolsList_TwoPrefixes_DifferentNamedSets**

### 22.2 `tools/call` protocol

752. **ToolsCall_UnknownToolName_IsErrorUnknownTool** — JSON-RPC name `not_a_tool`.  
753. **ToolsCall_EmptyName_ToolNameRequired**  
754. **ToolsCall_MissingParams_Error**  
755. **ToolsCall_InitializeThenListThenCallQuery_OData8** — full session, not only empty POST.  
756. **ToolsCall_WrongJsonRpcVersion**  
757. **ToolsCall_NotificationsDoNotBreakStateless**  
758. **ToolsCall_EachGenericOnce_OData8_JsonRpc** — `odata_list_entity_sets`, `odata_describe_type`, `odata_query`, `odata_get`, `odata_create` (auth), `odata_update` (auth), `odata_delete`, `odata_navigate`, `odata_list_operations`, `odata_call`. Ten methods, not one loop hidden in a helper without assertions.  
759. **ToolsCall_EachGenericOnce_Restier_JsonRpc** — skip create/update auth; Restier open. Navigate expects error. Call expects not-declared unless ops exist.  
760. **ToolsCall_EachGenericOnce_ToolsStdio_Northwind** — writes `IsError`.  
761. **ToolsCall_EachGenericOnce_ToolsStdio_TripPin** — writes on unique entities.

### 22.3 `resources/list`

762. **ResourcesList_OData8_MetadataFirstThenSets_OdataScheme** — URIs `odata://odata/$metadata`, `odata://odata/Customers`. Mime XML vs JSON.  
763. **ResourcesList_ToolsHost_RouteRemote** — `odata://remote/$metadata`.  
764. **ResourcesList_MaxResources50_Wide200_CountLeq50IncludingMetadata**  
765. **ResourcesList_ExcludeEntitySets_Omits**  
766. **ResourcesList_IsTypeCardsNotCollections** — reading is §22.4; listing descriptions mention sets not row data.  
767. **ResourcesList_JsonRpc**  
768. **ResourcesList_DeterministicOrder_MetadataThenIncludeListThenAlpha**

### 22.4 `resources/read`

769. **ResourcesRead_Metadata_ReturnsCsdlXml** — mime `application/xml`; contains EntityContainer. Twin GET `$metadata` (content may differ in whitespace; assert entity set names).  
770. **ResourcesRead_EntitySet_ReturnsTypeCardNotFeed** — `odata://odata/Customers` JSON has `keys`/`properties`/`navigations`, **not** `@odata.context` feed of Contoso. **Forbidden:** returning the collection.  
771. **ResourcesRead_UnknownUri_EmptyOrError_NotOdataQuery**  
772. **ResourcesRead_HttpUrlRejectedOrEmpty** — `https://…/Customers` is not fetched (PROTOCOL: clients must not be told to fetch https for in-app routes).  
773. **ResourcesRead_EmptyUri_EmptyContents**  
774. **ResourcesRead_JsonRpc**  
775. **ResourcesRead_TypeCardOmitsIgnoredAndBinary**  
776. **ResourcesRead_DoesNotExecuteQuery** — wire: no GET `/Customers` collection.

### 22.5 `resources/templates/list`

777. **ResourceTemplates_ContainsEntitySetAndEntityByKey** — templates `odata://{route}/{entitySet}` and `odata://{route}/{entitySet}({key})`.  
778. **ResourceTemplates_AspNetCoreUsesPrefix_ToolsUsesRemote**  
779. **ResourceTemplates_JsonRpc**  
780. **ResourceTemplates_NoHttpsScheme**

### 22.6 `completion/complete`

781. **Complete_EntitySet_EmptyPrefix_DeclaredSetsCapped** — `MaxCompletionValues` default 50; wide 200 returns 50.  
782. **Complete_EntitySet_PrefixProd_NorthwindProducts**  
783. **Complete_EntitySet_UnknownPrefix_Empty**  
784. **Complete_ArgumentKey_NotEntitySet_EmptyValues** — `{key}` completions not implemented; empty, not invented keys.  
785. **Complete_ArgumentNavigation_Empty** — not implemented.  
786. **Complete_ArgumentName_Empty**  
787. **Complete_DoesNotInventSets**  
788. **Complete_JsonRpc**  
789. **Complete_ExcludeEntitySets_StillCompletesDeclaredContainerSets** — current `CompleteEntitySetNames` uses **all container sets**, not `ResolveIncludedSets`. **Lock or fail:** excluded `People` may still complete. If that is undesirable, the test should fail the product (preferred: completions honor exclude). Implementers: assert exclude is honored; if today’s code leaks excluded names, that is a product bug this case is meant to catch.

### 22.7 Transport / MapMcp

790. **MapMcp_IsInternal_PostPrefixMcpWorksWithoutAppCallingMapMcp**  
791. **PostOdataMcp_ToolsCallQuery_NotEmptyObjectAsOnlyHttpTest**  
792. **GetOdataMcp_Not404** — may be 405; not missing.  
793. **PostWrongPrefixMcp_404** — `/nope/mcp`.  
794. **OpenMcp_WithoutToken_Not401**  
795. **RequireAuthorization_WithoutToken_401OnMcpHttp**  
796. **RequireAuthorization_MalformedJwt_401**  
797. **RequireAuthorization_ValidJwt_ToolsCallSucceeds** — not merely “not 401”; actually `tools/call` `odata_query`.  
798. **RateLimitedMcp_SecondPost429_ODataGetStill200** — MCP layer ≠ OData layer.  
799. **ContentTypeMissingOnMcp_Rejected**  
800. **HugeMcpBody_413Http**

---

## 23. Combinatorial / matrix

These are not attached to a single tool. Each cell is a named test.

### 23.1 Rate-limit: MCP × Customers × Products × function × second prefix MCP

Budgets (example): MCP `/odata/mcp` = 1, Customers = 1, Products = 2, `MostValuable` = 1, `/shop/mcp` = 1, shop Products unlimited unless specified.

801. **Rate_ODataBudgetsIndependent_HttpTwins** — GET Customers ok then 429; GET Products twice ok third 429; MostValuable ok then 429; other path unlimited.  
802. **Rate_McpQueryCustomers_ConsumesCustomersNotJustMcp** — in-process tool query hits `/odata/Customers` so **Customers** budget applies to the **inner** HTTP. First `odata_query` Customers ok; second tool `IsError` 429. Meanwhile MCP POST count may still be 1 if using InvokeAsync (no MCP HTTP). Split:  
803. **Rate_JsonRpcMcpPermit1_SecondToolsCallHttp429_InnerODataNotInvoked**  
804. **Rate_QueryProductsTwice_Third429_CustomersStillOk**  
805. **Rate_CallMostValuableTwice_Second429_QueryCustomersOk**  
806. **Rate_SpendCustomers_CallFunctionStillOk_QueryProductsStillOk**  
807. **Rate_McpAndCustomersBoth1_JsonRpcQueryCustomers_MayHitEitherLayer** — assert: first call succeeds; second is **either** HTTP 429 on `/mcp` **or** tool 429; never a silent 200 with empty body. Prefer separate tests 803 vs 802 so layers stay distinguishable.  
808. **Rate_RetryAfterPresentOnOData429_InToolText**  
809. **Rate_RetryAfterPresentOnMcpHttp429_HeaderOnHttpResponse** — not inside a tool result.  
810. **Rate_SecondPrefixShopMcp_IndependentOfOdataMcp** — exhaust `/odata/mcp`; `/shop/mcp` tools/call still works.  
811. **Rate_SecondPrefixODataShopProducts_IndependentOfOdataCustomers**  
812. **Rate_Restier_SameIndependence_CustomersVsProductsPrefixes**  
813. **Rate_NamedListCustomers_SameBudgetAsOdataQueryCustomers**  
814. **Rate_OdataNavigateOnCustomersPath_ConsumesCustomersBudget**  
815. **Rate_OdataGetProducts_ConsumesProductsBudget**  
816. **Rate_OdataCallFunction_DoesNotConsumeCustomers**  
817. **Rate_ListEntitySets_ConsumesNeitherSetBudget**  
818. **Rate_DescribeType_ConsumesNeither**  
819. **Rate_ListOperations_ConsumesNeither**  
820. **Rate_ConcurrentBurst_ProductsPermit2_ExactlyTwoSucceed**

### 23.2 Prefix include / exclude

821. **Prefix_AddODataMcp_AllDiscoveredPrefixesGetMcp** — `odata` and `internal`/`shop`.  
822. **Prefix_IncludePrefixesOdataOnly_ShopHasNoMcp_ShopODataStillWorks** — GET `/shop/Products` 200; POST `/shop/mcp` 404.  
823. **Prefix_ExcludeRoutesInternal_NoInternalMcp**  
824. **Prefix_IncludeAndExclude_ExcludeWinsAfterInclude**  
825. **Prefix_EmptyIncludeMeansAll**  
826. **Prefix_Restier_IncludeOdataOnly**  
827. **Prefix_JsonRpcOnIncluded_ToolsCallQuery**  
828. **Prefix_AddRouteExplicit_WhenDiscoveryBlind**  
829. **Prefix_DiscoveryWinsOverExplicitSamePrefix**  
830. **Prefix_RootEmptyPrefix_MapsSlashMcp** — if a route prefix is `""`.

### 23.3 Auth required vs open

831. **Auth_OpenMcp_ToolsCallQueryWithoutToken_200**  
832. **Auth_RequiredMcp_NoToken_401**  
833. **Auth_RequiredMcp_BadToken_401**  
834. **Auth_RequiredMcp_GoodToken_ToolsCallQuery_200**  
835. **Auth_RequiredMcp_GoodToken_CreateForwardsToOData_OData8**  
836. **Auth_OpenMcp_CreateWithoutToken_ODataStill401OnCustomersController** — MCP open ≠ OData open. Tool `IsError` 401.  
837. **Auth_OpenMcp_GetWithoutToken_200** — GET customers is anonymous.  
838. **Auth_ToolsHost_AuthTokenOnHttpClient_TripPinIfNeeded**  
839. **Auth_Restier_OpenCreate_No401**  
840. **Auth_Mcp401_IsNotToolIsError** — no `CallToolResult`; HTTP 401.

### 23.4 Named-tool cap

841. **Cap_Default150_IncludesGenerics** — Northwind tools.Count ≤ 150; generics 10; named families whole.  
842. **Cap_MaxNamedTools10_OnlyGenerics**  
843. **Cap_MaxNamedTools14_NoFamilyOf5** — remaining 4; zero named.  
844. **Cap_MaxNamedTools15_ExactlyOneFamilyOf5**  
845. **Cap_IncludeCreateFalse_Family4_FitsRemaining4**  
846. **Cap_IncludeEntitySetsProducts_FirstFamilyIsProducts** — even if `Alphabetical_list_of_products` sorts first.  
847. **Cap_TwoHundredSets_DoesNotEmitThousandTools**  
848. **Cap_NeverListWithoutGet**  
849. **Cap_GenericAlwaysPresentEvenWhenCap0ClampedToGenerics** — remaining `Max(0, cap - 10)`; if cap < 10, still **all generics** (code does not drop generics). Assert `MaxNamedTools=3` still lists 10 generics.  
850. **Cap_NamedCreateSchemaDeclaredPropertiesOnly**

### 23.5 Two prefixes isolated catalogs

851. **Iso_OdataQueryCustomersOnOdataPrefix_CannotSeeShopProductsInCatalog**  
852. **Iso_OdataQueryProductsOnOdataPrefix_404** — Products only on shop.  
853. **Iso_ShopQueryProducts_200_ShopQueryCustomers_404**  
854. **Iso_DescribeType_DoesNotLeakOtherPrefixTypes**  
855. **Iso_ResourcesUrisUseOwnPrefix** — `odata://odata/…` vs `odata://shop/…`  
856. **Iso_Completions_OdataPrefixDoesNotCompleteShopOnlySets**  
857. **Iso_NamedToolsSameNameDifferentSessions** — both may have `list_customers` only if both models have Customers; shop products host has `list_products` not `list_customers`.  
858. **Iso_RateLimitOnePrefix_DoesNotStarveTheOther**  
859. **Iso_RestierMultiPrefix_JsonRpcEachMcp**  
860. **Iso_UseODataMcp_MapsPrefixesWithoutAppCallingMapMcp**

### 23.6 Flags, ignore, documentation, snake_case

861. **Flags_IncludeCreateFalse_NamedCreateUnknown_GenericCreateWorks**  
862. **Flags_IncludeUpdateFalse_NamedUpdateUnknown_GenericUpdateWorks**  
863. **Flags_IncludeDeleteFalse_NamedDeleteUnknown_GenericDeleteWorks**  
864. **Flags_AllWriteFlagsFalse_FamilyIsListAndGetOnly_CapUses2**  
865. **Ignore_InternalSecret_AbsentFromDescribeNamedCreateResourceCard**  
866. **Docs_CsdlFlowsToNamedToolDescriptionAndTitleIfShort**  
867. **Snake_OrderItems_ListOrder_items** — `list_order_items`, `get_order_item` (type `OrderItem`).  
868. **Snake_Order_Details_ListOrder_details**

### 23.7 Host process isolation

869. **Proc_AspNetCoreTests_DoNotLoadOData7ControllerType**  
870. **Proc_RestierTests_DoNotLoadOData8ControllerType**  
871. **Proc_CoreTests_DoNotReferenceAspNetCoreOData**

---

## 24. Gaps vs current tests

Current suites over-use `odata_query` and catalog-name assertions. The following are **missing or only half-done** on real hosts (in-process TestServer, Restier, live Northwind/TripPin). RecordingODataExecutor tests in `ODataToolRuntimeTests` do **not** close these gaps.

### 24.1 Protocol

- `OpenMcpHostTests`, `AuthenticatedMcpHostTests`, `RateLimitedMcpHostTests`, `RestierCustomerMcpTests.Restier_McpEndpoint_IsNotMissing` POST **empty `{}`** to `{prefix}/mcp` and only assert not 404 / 401 / 429. **No JSON-RPC `tools/call`.**  
- No `tools/call` for each generic on Streamable HTTP.  
- No stdio JSON-RPC `tools/call` in `Tests.Tools` (host tests check catalog names + `ShutdownServerTool` delay 0 + `try` command).  
- `resources/read` type-card-vs-feed is not asserted on AspNetCore/Restier HTTP.  
- Completions honor-exclude is untested (and likely wrong).  
- Valid JWT tests do not actually invoke `odata_query`.

### 24.2 Named CRUD

- Named `list_customers` skip/top vs generic exists on **Restier paging** and is the rare named-tool runtime hit. Convention paging tests call **generic** `odata_query` only.  
- **No** `get_customer` / `get_mcp_customer` / `get_product` / `get_person` on real OData.  
- **No** `create_customer` / `create_mcp_customer` / `create_person` on real OData (named create schema is catalog-only in `NamedToolCapTests`).  
- **No** `update_*` named on real OData.  
- **No** `delete_*` named on real OData.  
- Restier type name `McpCustomer` ⇒ tool `get_mcp_customer` is easy to get wrong; untested.  
- `InvokeNamedAsync` entitySet overwrite (calling `list_customers` with `entitySet: Products`) untested on a real executor.  
- Query-option stripping on **named** create vs **generic** create leftover posting untested on real POST bodies.

### 24.3 `odata_navigate`

- Core recording test builds `People('a')/Friends` only.  
- In-process failure test: missing property `NotANav` error.  
- **No** successful navigate on OData8 `Customers(1)/Orders`, Northwind `Products(1)/Category` or `Customers('ALFKI')/Orders`, TripPin `Friends`/`Trips`.  
- **No** skip/top/count/nextLink on a navigation collection.  
- **No** to-one vs to-many twin.

### 24.4 `odata_update` / `odata_delete`

- In-process: update without auth is error; update **success** uses `ExecutorWithAuthorization` **directly**, not `odata_update` with forwarded MCP context.  
- Delete missing key error; **no** create-then-delete success on convention/Restier/TripPin via the tool.  
- Restier tests create via tool and via HTTP, but **never update or delete** via MCP.  
- Live TripPin write path untested (no Core live create/update/delete).  
- Northwind write failure via update/delete untested.

### 24.5 `odata_call` / `odata_list_operations`

- Operations-only host lists names; **does not call** `MostValuable` / `GetStatus` / `Reset` on a real OData endpoint (rate-limit tests call `odata_call` MostValuable for 429, not payload twin).  
- TripPin `GetNearestAirport`, `ShareTrip`, `ResetDataSource` untested.  
- Bound vs unbound missing entitySet/key untested on a real bound action.  
- Restier list-operations empty success untested.

### 24.6 `odata_create`

- Restier `Restier_McpCreate_VisibleToHttpGet` exists (good).  
- OData8 create success is executor-direct, not tool + Authorization forwarding through `HttpContext`.  
- TripPin create untested.  
- Object-body vs string-body vs leftover properties untested on real POST.  
- `MaxRequestBodyBytes` untested on hosts.

### 24.7 `odata_get`

- Restier and convention in-process get Contoso exist.  
- Northwind get-by-key (int and **string** `ALFKI`) untested.  
- Composite `Order_Details` untested (and will expose FormatKey quoting).  
- Guid keys untested.  
- Get + select/expand untested.

### 24.8 Passthrough paging

- Convention + Restier paging for **generic query** is strong (omit top, skip+top, count, nextLink, named list vs generic on Restier).  
- **Navigate** paging missing.  
- Northwind omit-top wire assertion missing (live tests always send `top=1` except skip+top pair).  
- TripPin paging untested.

### 24.9 Failures / overwhelm

- Status **404** covered for some gets/queries.  
- **401** create/update without auth on convention (tool), but not JSON-RPC header forwarding.  
- **403, 405, 409, 412, 413, 415, 428, 500, 503** not asserted through tool text.  
- OData **429** + Retry-After: Core recording test + in-process Customers/Products/function limiters exist; MCP HTTP 429 exists separately. Combinatorial MCP×set×function×second prefix is only partially written (`ODataServiceRateLimitTests` / Restier equivalent).  
- `MaxFilterLength` / `MaxExpandLength` / `MaxSelectLength` not hit on hosts.  
- Deep expand, concurrent calls, emoji/injection filters: absent.

### 24.10 Live Tools host

- `ToolsMcpHost_Northwind_IncludesQueryAndShutdown` checks **names**, then asserts catalog does **not** contain `shutdown_server` (true — it is extra). Does not call `odata_query` through the stdio server.  
- No TripPin Tools host mutation test.  
- `shutdown_server` delay default 2, out-of-range, JSON-RPC, AspNetCore absence via tools/call: only delay 0 unit test.

### 24.11 Catalog vs TOOL-SURFACE.md drift (do not test the drift as if it were product)

- Named navigate `list_{set}_{nav}` is documented in TOOL-SURFACE.md §3 and **not implemented**. Tests must not require it.  
- `odata_call` / `odata_list_operations` **are** implemented despite TOOL-SURFACE.md “Phase 2+” `odata_call_function` / `odata_call_action`. Tests use the implemented names.

### 24.12 What is already in good shape (do not redo blindly)

- Generic name order and `$`-less `filter` schema (`ODataMcpCatalogToolTests`).  
- Named family cap / no split (`NamedToolCapTests`).  
- Restier vs HTTP twin for query/filter/get/list-sets/describe/create.  
- Convention paging passthrough for `odata_query`.  
- Discovery, include prefixes, two prefixes, operations-only list, wide catalog caps.  
- Process isolation Restier vs OData 8 controller types.  
- MCP transport 401/429 vs OData 429 (as HTTP, not as full tools/call).

Implementers should add **new test classes per tool** (`OdataNavigateHostTests`, `NamedCrudRestierTests`, `NamedCrudConventionTests`, `TripPinMutationTests`, `JsonRpcToolsCallTests`, `OdataCallHostTests`) rather than stuffing more `odata_query` methods into paging classes.

---

## 25. Case index (counts)

| Section | Case numbers | Count |
|---------|--------------|-------|
| `odata_list_entity_sets` | 1–32 | 32 |
| `odata_describe_type` | 33–69 | 37 |
| `odata_query` | 70–166 | 97 |
| `odata_get` | 167–217 | 51 |
| `odata_create` | 218–270 | 53 |
| `odata_update` | 271–320 | 50 |
| `odata_delete` | 321–363 | 43 |
| `odata_navigate` | 364–417 | 54 |
| `odata_list_operations` | 418–445 | 28 |
| `odata_call` | 446–501 | 56 |
| Named `list_{set}` | 502–549 | 48 |
| Named `get_{type}` | 550–588 | 39 |
| Named `create_{type}` | 589–627 | 39 |
| Named `update_{type}` | 628–669 | 42 |
| Named `delete_{type}` | 670–707 | 38 |
| `shutdown_server` | 708–738 | 31 |
| Protocol handlers | 739–800 | 62 |
| Combinatorial / matrix | 801–871 | 71 |
| **Total named cases** | **1–871** | **871** |

Multi-host/multi-surface bullets inside a case are additional implementations (often 2–4 test methods per case). Expect well over a thousand `[TestMethod]`s once A8, R7, NW, and TP cells are expanded. That is the point: `odata_query` is one tenth of the surface, not the product.
