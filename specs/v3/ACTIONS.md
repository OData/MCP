# Actions as first-class MCP tools

**Status:** Draft (input for implementation)  
**Date:** 2026-10-20  
**Applies to:** Core catalog + runtime; `Microsoft.OData.Mcp.AspNetCore` Streamable HTTP (`{prefix}/mcp`); Tools host for remote one-shot calls  
**Host example:** PodOverlord Restier `Authorize` on `IntegrationConnection`

Companion docs: [TOOL-SURFACE.md](./TOOL-SURFACE.md), [PROTOCOL.md](./PROTOCOL.md), [IN-PROCESS-ODATA-EXECUTION.md](./IN-PROCESS-ODATA-EXECUTION.md), [OPTIMIZATION.md](./OPTIMIZATION.md), [TYPE-SHAPES.md](./TYPE-SHAPES.md), [AUTHENTICATION.md](./AUTHENTICATION.md).

The catalog builds a named tool per entity-set CRUD verb and a generic `odata_call` for operations. It never emits a named tool per action. That is unfinished catalog work: Task 7 of [EXECUTION-PLAN.md](./EXECUTION-PLAN.md) implemented named CRUD; operations were later given a generic invoke and never got the same named pass. This document finishes that pass, then adds the long-running `tools/call` behavior the Authorize scenario needs.

---

## 1. Scenario

PodOverlord tracks podcast social metrics. It never posts. To pull YouTube stats, an operator must grant a YouTube Brand Account. That grant is a Restier **bound action** on `IntegrationConnection`:

```
POST /v1/IntegrationConnections({id})/Authorize
```

The API starts an Auth0 Connected Accounts browser hop (Google Brand Account picker) and holds an in-memory session until Auth0 redirects to:

```
GET /auth0/connected-accounts/callback?connect_code=…&state=…
```

MCP and that callback run **in the same process**. The session is already there. The assistant opened the picker because the tool told it to. When the callback completes, the MCP client still does not see `Succeeded`.

Two gaps:

1. **Catalog.** `Authorize` is not a named tool. The assistant finds it on `odata_describe_type` and invokes it through generic `odata_call`.
2. **Runtime.** Every tool, including `odata_call`, waits for a complete inner OData HTTP body and returns that body as the `tools/call` result. There is no progress channel. A Started ticket with `connectUri` is either the final result (HTTP ended too early) or invisible until the wait finishes (too late to open the picker).

Named tools without streaming still leave the operator-wait broken. Streaming only on `odata_call` still leaves agents on the generic.

---

## 2. What the catalog does today

`ODataMcpCatalog.BuildTools` emits the eleven generic tools, then unbound `function_*` / `action_*` (alphabetical within each), then `BuildNamedFamily` for each included entity set (`list_*` / `get_*` / `create_*` / `update_*` / `delete_*`) followed by that set’s instance-bound then collection-bound named operations. `IncludeActions` / `IncludeFunctions` default `true`. Overloads: first declaration only. `odata_call` remains.

`ODataToolRuntime.InvokeNamedAsync` dispatches named operations through `CallNamedOperationAsync` → `CallOperationAsync` (synthesizes `name`, baked `entitySet`, `key`, remaining keys as `parameters`). CRUD prefix dispatch is unchanged. Unbound named tools do not require `EntitySetName`. The descriptor carries `EntitySetName`, `OperationKind`, `OperationName`, and `BindingParameterType`.

Bound operations remain on `odata_describe_type`. Unbound operations remain on `odata_list_operations`. Those are discovery for the generic, not a substitute for the named tools.

[TOOL-SURFACE.md](./TOOL-SURFACE.md) §3 documents the CRUD family and the `action_*` / `function_*` rows. The line “Do not register `odata_call_function` / `odata_call_action`” forbids splitting the **generic** into two generics.

`odata_call` stays, the same way `odata_get` stays next to `get_product`. Execution is still one-shot: the inner OData HTTP body is the `tools/call` result. Long-running wait is §5.

---

## 3. What happens today for Authorize

1. Claude lists tools. Named tools include entity-set CRUD and `action_integrationconnection_authorize`. The assistant can call the named tool. The result is still the Started ticket as a completed `tools/call`.
2. `action_integrationconnection_authorize` (or `odata_call` `Authorize`) POSTs the Restier action through `InProcessODataExecutor`. The executor `await`s the inner pipeline, then seeks the inner `Response.Body` and returns it. The action returns one JSON ticket (`status: Started`, `connectUri`, `expiresIn`, `message`) and the HTTP call ends.
3. Microsoft.OData.Mcp wraps that JSON as the `tools/call` **result**. The JSON-RPC request is done. There is no `IProgress<ProgressNotificationValue>` and no streaming execute path.
4. The operator may finish Google. Auth0 hits the API callback. The API completes the in-memory session.
5. The browser receives `{ status: Succeeded, … }`. **That HTTP client is the browser, not the MCP session.** The assistant never gets the final ticket.

A second `GET …/events` SSE URL would not help. Claude’s tool call will not subscribe to it. Streamable HTTP already has a **request-scoped** SSE channel for this `tools/call`. Use that.

`WithHttpTransport(options => options.Stateless = true)` stays. MCP 2026-07-28 has no protocol session id. Stateless means every POST stands alone. The server may still answer one `tools/call` with an SSE stream that carries `notifications/progress` and then the JSON-RPC result. [PROTOCOL.md](./PROTOCOL.md) §6 “No HTTP+SSE” forbids a custom `MapGet` / `/mcp/events` route, not the official per-request response stream.

---

## 4. Named action tools

### 4.1 Names

Prefix `action_`, then each EDM short name through `ToToolName` (lowercase as declared). `IntegrationConnection` is `integrationconnection`, not `integration_connection`. Use `type.Name` / `set.Name` / `action.Name` (never the namespace). The prefix is the point: it never collides with `list_` / `get_` / `create_` / `update_` / `delete_` / `odata_`, it groups every action in `tools/list`, and `InvokeNamedAsync` already dispatches on a prefix.

Named CRUD tools use the same rule (`get_integrationconnection`, `list_orderdetails` vs `list_order_details`). `ToToolName` lowercases the declared short name; it does not insert separators.

| Binding | Pattern | Example |
|---------|---------|---------|
| Unbound | `action_{action}` | `action_resetdatasource` (TripPin `ResetDataSource`) |
| Instance-bound | `action_{singular_type}_{action}` | `action_integrationconnection_authorize`, `action_person_sharetrip` |
| Collection-bound | `action_{set}_{action}` | `action_products_discount` |

`{singular_type}` is the entity type short name, lowercased (`IntegrationConnection` → `integrationconnection`). Instance-bound actions take a `key`. `{set}` is the entity set short name, lowercased. Collection-bound actions take no `key`.

Functions are the same overlooked pass. Same patterns with prefix `function_`: `function_getnearestairport`, `function_person_getfavoriteairline`. Functions are GET and `readOnlyHint: true`. They do not stream.

Title: CSDL `Description` when present, otherwise `"{Action} a {Type}"` / `"{Action}"`. Description: CSDL docs plus one operational sentence (instance-bound: `POST {Set}({key})/{Action}.`; collection-bound: `POST {Set}/{Action}.`; unbound: `POST {Action}.`). Functions say GET.

### 4.2 Why this shape

Verb-noun names (`authorize_integrationconnection`) look like CRUD, collide with `get_*` the moment an action is named `Get…`, and scatter actions through the list. Putting the entity before the action (`action_integrationconnection_authorize`) keeps every action on one type adjacent.

The binding parameter is never in the tool name. It becomes `key` (instance) or is implied by the set baked into the descriptor.

### 4.3 Collisions and overloads

The `action_` / `function_` prefixes make CRUD collisions structurally impossible.

Two included sets of the same type: emit one instance-bound tool per set, using that set’s lowercased short name in place of the type stem so the set is in the name (`action_vipcustomers_approve` vs `action_customers_approve`). Bake `EntitySetName` on each.

Overloads of the same action name on one binding type: one named tool for the first overload in declaration order. The rest remain on `odata_call` until a later pass.

`IncludeEntitySets` / `ExcludeEntitySets` apply to bound tools the same way they apply to CRUD. Unbound tools always generate.

`ODataMcpCatalogOptions.IncludeActions` and `IncludeFunctions` default `true`, parallel to `IncludeCreate` / `IncludeUpdate` / `IncludeDelete`. Off means named operation tools are omitted; `odata_call` remains.

### 4.4 Input schema

Flatten like `create_*`. Do not nest arguments under `parameters` — that wrapper is the generic `odata_call` contract.

| Binding | Properties | Required |
|---------|------------|----------|
| Unbound | declared parameters | non-nullable parameters |
| Instance-bound | `key` (named key schema for the type) + declared non-binding parameters | `key` + non-nullable parameters |
| Collection-bound | declared non-binding parameters | non-nullable parameters |

When the tool is bound to exactly one included set, bake `EntitySetName` on the descriptor. The caller does not pass `entitySet`. After the per-set emission in §4.3, that is every bound tool.

`additionalProperties: false`. Enum / primitive mapping reuses `BuildEnumSchema` / `MapJsonSchemaType`. Binary/stream parameters are omitted. Complex and collection parameters are JSON objects / arrays with a short description naming the type for `odata_describe_type`.

### 4.5 Hints and output

| Kind | `readOnlyHint` | `idempotentHint` | `destructiveHint` |
|------|----------------|------------------|-------------------|
| Function | `true` | `true` | `false` |
| Action | `false` | `false` | `true` |

`openWorldHint: true`.

`outputSchema`: when the EDM return type is a complex type with exposed properties, emit that object schema. Primitive returns use `{ "value": … }`. No return type → no `outputSchema`.

### 4.6 Placement, descriptor, dispatch

`BuildTools` order:

1. Generic tools (unchanged, including `odata_list_operations` and `odata_call`).
2. Unbound `function_*` then `action_*`, each alphabetical.
3. For each included entity set, in `OrderSetsForNamedTools` order: the CRUD family, then instance-bound `action_*` / `function_*` on that set, then collection-bound on that set.

Unbound operations apply to the whole service (`ResetDataSource`, `GetNearestAirport`). They are few and should sit on the first `tools/list` page, immediately after the generics. Bound tools stay next to that set’s CRUD family. Putting unbound last buries them behind every entity-set family.

Extend `ODataToolDescriptor`:

| Property | Role |
|----------|------|
| `EntitySetName` | Already present. Bound tools. |
| `OperationKind` | `None` (CRUD/generic), `Action`, `Function`. |
| `OperationName` | EDM short name (`Authorize`). |
| `BindingParameterType` | EDM type name, or null if unbound. |

`InvokeNamedAsync`: `action_` / `function_` synthesize `odata_call` arguments (`name`, `entitySet`, `key`, `parameters` from the remaining keys) and call `CallOperationAsync`. CRUD prefix dispatch is unchanged. Unbound named tools do not require `EntitySetName`.

### 4.7 Instructions

`ODataMcpInstructions.Default` already says prefer named tools when listed. Named action and function tools become part of that set. Keep the `$` rule, `odata_describe_model`, `odata_call` payload rule, and `$metadata` rule. Bound operations remain on the type shape; unbound remain on `odata_list_operations` — those are discovery for the generic, not a substitute for the named tools.

Update [TOOL-SURFACE.md](./TOOL-SURFACE.md) §3 in the same PR as the generator: add the `action_*` / `function_*` rows. Do not treat the current CRUD-only table as the ceiling.

---

## 5. Long-running actions

### 5.1 Cycle (one MCP request, no second SSE)

Keep `tools/call` **open** for the life of the in-memory session. Emit the Started ticket **before** the wait so the client can open `connectUri`. When the callback completes the session, finish the same `tools/call`.

```
Assistant                         MCP Streamable HTTP                    API process
    |                                    |                                    |
    |  tools/call                        |                                    |
    |  action_integrationconnection_authorize
    |  (_meta.progressToken)             |                                    |
    |----------------------------------->|  start in-process Authorize        |
    |                                    |  (IAsyncEnumerable or coordinator) |
    |                                    |         start session              |
    |                                    |         yield Started+connectUri   |
    |  notifications/progress            |<-----------------------------------|
    |  (Started ticket; open connectUri) |                                    |
    |<-----------------------------------|                                    |
    |  [optional] elicitation/create     |                                    |
    |  mode=url, Url=connectUri          |                                    |
    |<-----------------------------------|                                    |
    |  (browser: Auth0 + Google picker)  |                                    |
    |                                    |         GET callback complete      |
    |                                    |         session TCS completes      |
    |                                    |         yield Succeeded            |
    |  tools/call result                 |<-----------------------------------|
    |  (Succeeded ticket + next-step msg)|                                    |
    |<-----------------------------------|                                    |
```

Hard rules:

1. **Official MCP only.** Progress and result travel on the existing Streamable HTTP (or stdio) session for this `tools/call`. Do not `MapGet` a second `text/event-stream`. Do not invent `/mcp/events`. Do not use MCP Tasks ([PROTOCOL.md](./PROTOCOL.md) §11 stays deferred).
2. **Inner work stays in-process.** Same as [IN-PROCESS-ODATA-EXECUTION.md](./IN-PROCESS-ODATA-EXECUTION.md): no `HttpClient` to `{prefix}/mcp` or to an events URL on the same app. The wait is on the host’s session object.
3. **Client abort cancels the action.** `CancellationToken` from `tools/call` must cancel the Restier wait. Closing the request SSE stream is the Streamable HTTP cancel signal (MCP 2026-07-28).
4. **Timeout = session expiry.** If the operator never finishes, the stream ends with a Failed ticket, not a hung tool. Honor the host session’s `expiresIn` when present; otherwise `ODataMcpCatalogOptions.MaxOperationWait` (default 10 minutes) cancels the token.

Named `action_*` and `odata_call` share the stream mapping. Streaming is not generic-only.

### 5.2 How an action becomes a stream

CSDL has no `IAsyncEnumerable`. Detection is two-sided.

**Catalog (advertised to the client):**

- Vocabulary term `Microsoft.OData.Mcp.V1.StreamResults` (boolean) on the action, **or**
- AspNetCore adapter sets the same flag when the CLR method returns `IAsyncEnumerable<T>` (or `IAsyncEnumerable<T?>`) whose `T` is the EDM return type or the collection element type.

Store `StreamResults` on `EdmAction` (bool, default `false`). Named tool description appends `Stays open until the last item; watch progress for URLs to open.` when true. The runtime still streams `odata_call` when the resolved action has the flag.

**Runtime (in-process):**

Do not wait for a buffered inner HTTP body. `InProcessODataExecutor.ExecuteAsync` as written (`await pipeline(inner)` then seek `Response.Body`) cannot emit progress during a wait: the pipeline has not returned.

Use an in-process enumerator that runs with `McpHttpContextAccessor.Start` / `End` so Restier and `User` see a normal request, and **yields CLR items as they are produced**, serialized to JSON by Core (`System.Text.Json`), not by the OData formatter.

| Source | Behavior |
|--------|----------|
| CLR method is `IAsyncEnumerable<T>` | AspNetCore resolves and invokes it (binding entity + non-binding parameters + `CancellationToken`). `await foreach` each item. |
| Host registers `IMcpOperationStream` for that action name | Same `await foreach`. Escape hatch when Restier cannot expose the enumerator. |
| Neither | One-shot `ExecuteAsync` as today. `StreamResults` on the catalog is ignored at runtime (remote Tools host; CSDL-only flag). |

Do not add a second OData HTTP call. Do not require the OData JSON formatter to flush a `{"value":[…]}` array; current Restier/OData buffers collections and would emit Started only after Succeeded already exists.

Remote Tools host: always one-shot. Named tools still exist. Streaming is an AspNetCore in-process feature.

### 5.3 Item → MCP mapping

| Stream item | MCP |
|-------------|-----|
| Every item except the last | `notifications/progress` when the client sent `_meta.progressToken`. `ProgressNotificationValue.Message` is the compact JSON of the item (must include `connectUri` when present). `Progress` is 1-based item index; `Total` omitted unless the host knows it. |
| Last item | `tools/call` **result** (`structuredContent` = the item, short text = `message` if present). |
| Exception / cancel | Failed tool result (`isError: true`); do not leave the call open. |
| Timeout | Failed tool result whose structured content is a Failed ticket when the host produced one; otherwise text `The operation timed out.` |

First item is `Started` (must include `connectUri` for Authorize). Last item is `Succeeded` or `Failed`.

`notifications/progress` requires a client `progressToken` (MCP 2026-07-28). `notifications/message` requires `_meta` `logLevel`. Neither is guaranteed.

**URI fallback:** when a non-final item has a URI-looking property (`connectUri`, `url`, `authorizationUrl`, `verificationUri`) and the client advertised URL elicitation, send `elicitation/create` `mode: url` with that URL (same shape as [AUTHENTICATION.md](./AUTHENTICATION.md) mid-session consent: URL + `ElicitationId`, never tokens). The tool **keeps waiting** on the host TCS while the elicitation is open; completing or cancelling the elicitation does not complete the action. AspNetCore must grow this elicitation path if it is Tools-only today.

Do not make URL elicitation the only channel. Progress is enough when the client sends a token. Elicitation is how a URI reaches a human when progress is absent.

Do not require the assistant to poll `GET IntegrationConnections(id)` as the completion signal.

### 5.4 What the AspNetCore host must not do

- Open `HttpClient` to `{prefix}/mcp` or to an events URL on the same app.
- Return `text/event-stream` from the Restier action itself as a second protocol. Restier yields CLR objects; **MCP** multiplexes them onto the session already open for `tools/call`.
- Flip `Stateless = false` to make progress work.
- Buffer the enumerator into a list and then emit progress. Started must leave the process before the wait begins.

### 5.5 Progress plumbing in Core

`ODataMcpHandlers.CallToolAsync` already receives `RequestContext<CallToolRequestParams>`. Today it only reads `Name` and `Arguments`.

- Read `request.Params.ProgressToken`.
- When present, pass `IProgress<ProgressNotificationValue>` into `ODataToolRuntime.InvokeAsync` (new optional parameter; default no-op).
- The handler reports via `request.Server.NotifyProgressAsync` (SDK 2.2 `McpSession.NotifyProgressAsync`).
- `CallOperationAsync` (and the named `action_` / `function_` dispatch into it) is the only invoke path that reports. CRUD does not.

`IODataExecutor` stays one-shot. Streaming is a separate in-process invoker, not a second execute method on every host.

---

## 6. Host (PodOverlord) once MCP streams

1. Change `Authorize` to `IAsyncEnumerable<ConnectionAuthorizationTicket>` (same complex type as today).
2. Yield `Started` with `connectUri` (Auth0 `/authorize` for the first-party Regular Web app; callback is `GET /auth0/connected-accounts/callback`) **before** waiting.
3. Wait on `TaskCompletionSource` on the in-memory Connected Accounts session, observing `CancellationToken`.
4. Callback `CompleteAsync` sets the TCS.
5. Yield `Succeeded` (`message`: YouTube is authorized; next step is YouTube sync / query metrics) or `Failed`.

No second SSE. No HTML bootstrap. The browser is only the OAuth user-agent. The AspNetCore adapter sees `IAsyncEnumerable<>` and sets `StreamResults` on the EDM action. The catalog emits `action_integrationconnection_authorize`.

---

## 7. Implementation order

One builder, one invoke path. Tests first in each PR (MSTest v3, Breakdance, FluentAssertions, no mocks). Update [TOOL-SURFACE.md](./TOOL-SURFACE.md) §3, [OPTIMIZATION.md](./OPTIMIZATION.md) §1, [TOOL-TEST-MANIFEST.md](./TOOL-TEST-MANIFEST.md), and [PROTOCOL.md](./PROTOCOL.md) §5 in the same PRs as the code they describe.

### PR 1 — Named `action_*` / `function_*` tools (catalog + one-shot invoke) — done (§4)

**Files:** `ODataMcpCatalog.cs`, `ODataMcpCatalogOptions.cs`, `ODataToolDescriptor.cs`, `ODataMcpCatalogConstants.cs`, `ODataToolRuntime.cs` (`InvokeNamedAsync`), `ODataMcpInstructions.cs`, new `NamedOperationToolTests`, TripPin/Restier host tests for `action_person_sharetrip` / `action_resetdatasource` / `function_getnearestairport`.

**Behavior:** Generate and dispatch named tools. Execution is still one-shot `CallOperationAsync` → `IODataExecutor`. `action_integrationconnection_authorize` still returns Started as the result until PR 2.

**Tests:** TripPin unbound action and bound action have the names in §4.1; `tools/list` order is generics, then unbound `function_*`/`action_*`, then per-set CRUD and bound tools; `odata_call` still works; no `action_*` name collides with CRUD; `IncludeActions=false` omits named actions; bound tools respect `ExcludeEntitySets`; `tools/list` pages still include generics.

### PR 2 — Streamed in-process actions

**Files:** `EdmAction` (`StreamResults`), AspNetCore `EdmModelAdapter` (detect `IAsyncEnumerable`), new `IMcpOperationStream` + default CLR enumerator, `ODataMcpHandlers` progress wiring, `ODataToolRuntime.CallOperationAsync` stream loop, `ODataMcpCatalogOptions.MaxOperationWait`, handler tests that assert a progress notification is sent **before** the enumerator’s second item is awaited.

**Behavior:** `tools/call` stays open. First item → progress. Last item → result. Cancel and timeout covered.

**Tests:** A Restier (OData 7) fixture action that yields Started, delays, yields Succeeded. JSON-RPC Streamable HTTP: progress event on the response stream contains `connectUri`; final `CallToolResult.structuredContent` is Succeeded. Client disconnect cancels the delay. `action_*` and `odata_call` both stream. A one-shot action is unchanged.

### PR 3 — URL elicitation fallback for URI tickets

**Files:** AspNetCore elicitation path (mirror Tools [AUTHENTICATION.md](./AUTHENTICATION.md) `mode: url`), stream loop calls it for URI properties on non-final items when the client advertised the capability.

**Behavior:** Operator gets an open-this-URL prompt even without `progressToken`. Wait still ends on the host TCS.

Depends on PR 2. Keep it in the plan because 2026-07-28 makes progress optional.

---

## 8. Acceptance

Catalog:

- `tools/list` includes `action_integrationconnection_authorize` when the model declares bound `Authorize` on `IntegrationConnection`.
- TripPin lists `action_resetdatasource` and `action_person_sharetrip`. `function_getnearestairport` is present when functions are included.
- Unbound named tools appear after the generics and before any bound `action_*` / `function_*` (and therefore before the per-set CRUD families).
- `odata_call` `Authorize` / `ShareTrip` / `ResetDataSource` still succeed.
- `IncludeActions=false` removes named action tools only.

Streaming (in-process):

- After `tools/call` `action_integrationconnection_authorize` (or `odata_call` `Authorize`), the client receives a progress (or URL elicitation) containing `connectUri` **before** the operator finishes the picker.
- The same `tools/call` does not return until callback completion, client abort, or timeout.
- The result payload is the Succeeded/Failed ticket, not the Started ticket.
- Network trace shows **one** MCP Streamable HTTP POST for that call; zero extra `text/event-stream` routes; `Stateless` remains `true`.
- `CancellationToken` from the MCP request cancels the Restier wait.

---

## 9. Out of scope

| Item | Why |
|------|------|
| MCP Tasks / `tasks/get` | [PROTOCOL.md](./PROTOCOL.md) §11. A handle-and-poll loop is a different product. |
| `subscriptions/listen` | Change notifications, not request-scoped progress. |
| OData `respond-async` / 202 + monitor URL | A second HTTP resource. The wait is in-process. |
| Streaming through the OData JSON formatter | Current Restier/OData buffers collections. Revisit if a formatter flushes `IAsyncEnumerable` items. |
| Named tools for operation overloads past the first | `odata_call` remains. |
| Remote (Tools host) streaming | No in-process enumerator. Named tools still generate. |
| Flipping Streamable HTTP to stateful | Not required for per-request progress. |
