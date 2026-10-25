# Actions as first-class MCP tools

**Status:** Draft (input for implementation)  
**Date:** 2026-10-24  
**Applies to:** Core catalog + runtime; `Microsoft.OData.Mcp.AspNetCore` Streamable HTTP (`{prefix}/mcp`); Tools host for remote one-shot calls  
**Host example:** Restier `[McpTask]` on a bound action; convention `HasMcpTask()` on `ActionConfiguration`

Companion docs: [TOOL-SURFACE.md](./TOOL-SURFACE.md), [PROTOCOL.md](./PROTOCOL.md), [IN-PROCESS-ODATA-EXECUTION.md](./IN-PROCESS-ODATA-EXECUTION.md), [OPTIMIZATION.md](./OPTIMIZATION.md), [TYPE-SHAPES.md](./TYPE-SHAPES.md), [AUTHENTICATION.md](./AUTHENTICATION.md).

The catalog already emits named `action_*` / `function_*` tools (PR 1, §4) plus generic `odata_call`. This document keeps that catalog contract and replaces the old “hold `tools/call` open and stream `IAsyncEnumerable`” plan with **MCP Tasks + URL elicitation** for a small set of annotated actions.

---

## 1. Scenario

An operator-present bound action starts an out-of-band browser hop and returns a ticket with a URL. Example: Restier `Authorize` on `IntegrationConnection`:

```
POST /v1/IntegrationConnections({id})/Authorize
```

The API starts Connected Accounts, returns `{ status: Started, connectUri, expiresIn, message }`, and holds an in-memory session until the provider redirects to the app callback (same process as MCP). The browser is the user-agent. Tokens never pass through the MCP client.

Two remaining gaps:

1. **Catalog.** Named `action_integrationconnection_authorize` exists; assistants can still find the action on `odata_describe_type` and invoke `odata_call`. Both paths must behave the same.
2. **Completion.** `tools/call` today finishes when the inner OData body is the Started ticket. The callback later writes Succeeded to the **browser**, not to the MCP client.

MCP 2026-07-28 Streamable HTTP is stateless, so the SDK cannot send a live `elicitation/create` JSON-RPC request (`ElicitAsync` throws). URL elicitation uses **MRTR** ([SEP-2322](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/mrtr/mrtr.html)): throw `InputRequiredException` with `InputRequest.ForElicitation` (`mode: url`). The client fulfills that and retries `tools/call` with `inputResponses` + `requestState`. Protocol Tasks (`io.modelcontextprotocol/tasks`) are optional: when the client also opted in, the same elicitation is nested as task `inputRequests` and the waiter can outlive the original POST.

If the client does **not** advertise URL elicitation, `tools/call` returns the Started ticket (`connectUri` + `message`) and is done. The operator opens the URL, finishes in the browser, and **tells the assistant they are done**.

This document does **not** cover background jobs, queue drain, or any work that continues after the operator has left the browser hop. Those stay on the host’s own dispatcher. The C# SDK does not implement SEP-2575 (`notifications/tasks`); task-capable clients poll `tasks/get`. That is enough for a minutes-long consent wait on the Tasks path. It is not a completion signal for unattended work, and it is not used when the client omitted tasks.

---

## 2. What the catalog does today

`ODataMcpCatalog.BuildTools` emits the eleven generic tools, then unbound `function_*` / `action_*` (alphabetical within each), then `BuildNamedFamily` for each included entity set (`list_*` / `get_*` / `create_*` / `update_*` / `delete_*`) followed by that set’s instance-bound then collection-bound named operations. `IncludeActions` / `IncludeFunctions` default `true`. Overloads: first declaration only. `odata_call` remains.

`ODataToolRuntime.InvokeNamedAsync` dispatches named operations through `CallNamedOperationAsync` → `CallOperationAsync` (synthesizes `name`, baked `entitySet`, `key`, remaining keys as `parameters`). CRUD prefix dispatch is unchanged. Unbound named tools do not require `EntitySetName`. The descriptor carries `EntitySetName`, `OperationKind`, `OperationName`, and `BindingParameterType`.

Bound operations remain on `odata_describe_type`. Unbound operations remain on `odata_list_operations`. Those are discovery for the generic, not a substitute for the named tools.

[TOOL-SURFACE.md](./TOOL-SURFACE.md) §3 documents the CRUD family and the `action_*` / `function_*` rows. The line “Do not register `odata_call_function` / `odata_call_action`” forbids splitting the **generic** into two generics.

`odata_call` stays, the same way `odata_get` stays next to `get_product`. Execution is one-shot unless §5 applies.

---

## 3. What happens today for Authorize

1. Claude lists tools. Named tools include entity-set CRUD and `action_integrationconnection_authorize`. The assistant can call the named tool. The result is still the Started ticket as a completed `tools/call`.
2. `action_integrationconnection_authorize` (or `odata_call` `Authorize`) POSTs the Restier action through `InProcessODataExecutor`. The executor `await`s the inner pipeline, then seeks the inner `Response.Body` and returns it. The action returns one JSON ticket (`status: Started`, `connectUri`, `expiresIn`, `message`) and the HTTP call ends.
3. Microsoft.OData.Mcp wraps that JSON as the `tools/call` **result**. The JSON-RPC request is done. There is no `IProgress<ProgressNotificationValue>` and no streaming execute path.
4. The operator may finish Google. Auth0 hits the API callback. The API completes the in-memory session.
5. The browser receives `{ status: Succeeded, … }`. **That HTTP client is the browser, not the MCP session.** The assistant never gets the final ticket.

A second `GET …/events` SSE URL would not help. Claude’s tool call will not subscribe to it. `WithHttpTransport(options => options.Stateless = true)` stays. MCP 2026-07-28 has no protocol session id; every POST stands alone. The durable handle is a **task id**, not a held request stream. [PROTOCOL.md](./PROTOCOL.md) §6 “No HTTP+SSE” still forbids a custom `MapGet` / `/mcp/events` route.

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

Functions are the same overlooked pass. Same patterns with prefix `function_`: `function_getnearestairport`, `function_person_getfavoriteairline`. Functions are GET and `readOnlyHint: true`. They are never MCP Tasks (§5.2).

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

## 5. Annotated actions as MCP Tasks

### 5.1 Cycle

When the client opted into `io.modelcontextprotocol/tasks` **and** the resolved action carries `Microsoft.OData.Mcp.V1.Task`, `tools/call` returns a task handle immediately. The inner OData POST still runs once (Started ticket). URL elicitation asks the operator to open `connectUri`. Completing elicitation means the link was opened or refused, not that the provider grant finished. The task goes back to `working` and waits on the host completion source. The callback completes that source. `tasks/get` then returns `completed` with the Succeeded or Failed ticket as the ordinary `CallToolResult`.

```
Assistant                      MCP (stateless HTTP)                 API process
    |                                  |                                  |
    |  tools/call                      |                                  |
    |  action_integrationconnection_authorize
    |  (_meta.extensions.tasks)        |                                  |
    |--------------------------------->|  CreateTask (working)            |
    |  resultType=task                 |<---------------------------------|
    |<---------------------------------|                                  |
    |  tasks/get                       |  in-process Authorize            |
    |--------------------------------->|  Started + connectUri            |
    |  status=input_required           |  ElicitAsync mode=url            |
    |  elicitation/create url=connectUri
    |<---------------------------------|                                  |
    |  (Claude: open this URL)         |                                  |
    |  tasks/update inputResponses     |                                  |
    |--------------------------------->|  elicitation accepted            |
    |  tasks/get status=working        |  wait on host TCS                |
    |  (browser: provider picker)      |         GET callback             |
    |                                  |         TCS completes            |
    |  tasks/get status=completed      |                                  |
    |  result = Succeeded ticket       |                                  |
    |<---------------------------------|                                  |
```

Hard rules:

1. **Official MCP only.** No second SSE URL, no `/mcp/events`, no custom JSON-RPC. Clients poll `tasks/get`. Do not implement SEP-2575 (`notifications/tasks`); the C# SDK does not ship it.
2. **Return a task only when the request opted in.** `_meta["io.modelcontextprotocol/clientCapabilities"].extensions["io.modelcontextprotocol/tasks"]`. If that key is absent: one-shot Started ticket, return, stop. No `ElicitAsync`, no waiter, no `tasks/*`, no progress. Missing `taskId` / `status` / `createdAt` / `lastUpdatedAt` / `ttlMs` on a task result is a tool error on Claude Code (`Unsupported result type 'task'`).
3. **Inner OData stays in-process.** [IN-PROCESS-ODATA-EXECUTION.md](./IN-PROCESS-ODATA-EXECUTION.md). The first execute is still one-shot `IODataExecutor.ExecuteAsync`. The wait after elicitation is a host `IMcpAnnotatedActionWaiter`, not a held OData HTTP body.
4. **Elicitation accept is not completion.** `tasks/update` for `elicitation/create` means the operator opened or declined the URL. Decline / `Esc` → terminal Failed ticket (`completed` + `isError: true`) or `cancelled`. Accept → `working` and wait.
5. **Timeout = session expiry.** Honor `expiresIn` on the Started ticket when present; otherwise `ODataMcpCatalogOptions.MaxOperationWait` (default 10 minutes). `tasks/cancel` is cooperative: cancel the waiter’s token.
6. **Domain errors stay `completed`.** A Failed authorization ticket is `CallToolResult` with `isError: true` inside a `completed` task. Reserve task status `failed` for protocol errors (SEP-2663).

Named `action_*` and `odata_call` of the **same** annotated action share this path. CRUD and functions never do.

### 5.2 Who gets wrapped

SDK `IMcpServerBuilder.WithTasks(store)` wraps **every** `[McpServerTool]`. OData tools are catalog handlers, not `[McpServerTool]`, and wrapping `odata_query` / `list_*` / `get_*` as tasks would poll for every read. Do **not** call SDK `WithTasks` from app code.

OData MCP wraps a `tools/call` as a task only when **all** of these hold:

| Gate | Rule |
|------|------|
| Client | This request includes the tasks extension. |
| Host | `ODataMcpServerBuilder.WithTasks` ran at `AddODataMcp` (store registered). |
| Kind | The resolved tool is an **action** (`OperationKind = Action`), named or via `odata_call`. |
| Annotation | The EDM action has `Microsoft.OData.Mcp.V1.Task` = true. |

Functions, entity-set CRUD, generics (`odata_query`, `odata_get`, …), and unannotated actions stay one-shot even when the client opted into tasks.

CSDL has no Tasks concept. Detection is the vocabulary term on the **action** already in the model (`IEdmAction.Namespace` + `Name`). Hosts stamp that object; they do not invent a `Target` path or a `Default` schema. `$metadata` must export it so Tools, remote clients, and other OData stacks can see the same annotation.

OASIS CSDL XML 4.01 §14.2.2: nest the annotation in the model element (the Action lives in whichever schema the builder already put it in). TripPin and Microsoft Graph do this for operations — types and actions share a schema (`Trippin`, `microsoft.graph`); bound actions have no `ActionImport`; unbound functions/actions are imported on the container. Restier matches TripPin for operations (declared in the entity schema) and Northwind for the container (`Default` holds only the entity container).

```xml
<edmx:Reference Uri="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml">
  <edmx:Include Namespace="Microsoft.OData.Mcp.V1" Alias="Mcp" />
</edmx:Reference>
…
<Schema Namespace="Trippin">
  <Action Name="ShareTrip" IsBound="true">
    <Parameter Name="personInstance" Type="Trippin.Person" />
    <Annotation Term="Mcp.Task" Bool="true" />
  </Action>
</Schema>
```

Out-of-line targeting is also legal. The `Target` is the qualified operation name, optionally with overload parentheses (OASIS Example 42). A slash after a structured type names a property or navigation:

```xml
<Annotations Target="Trippin.ShareTrip(Trippin.Person)">
  <Annotation Term="Mcp.Task" Bool="true" />
</Annotations>
```

Other OASIS Example 42 forms: `Trippin.ShareTrip`, `Trippin.ShareTrip()`, `Trippin.Container/ResetDataSource` (the unbound **import**).

Store `IsTask` on Core `EdmAction` (bool, default `false`). AspNetCore `EdmModelAdapter` copies the term from `IEdmModel`. Named tool description appends one sentence when `IsTask`: `Runs as an MCP task when the client opts in; open any returned URL to finish.`

Hosts stamp the term; they do not wrap Restier `IModelBuilder` and they do not reference Restier from `Microsoft.OData.Mcp.AspNetCore`.

| Host | Stamp |
|------|--------|
| Convention ModelBuilder | `action.HasMcpTask()` (optional `elicitUrlProperties`). Chain `HasMcpElicitUrl("connectUri")` to stamp `ElicitUrlProperties` as its own term. `GetEdmModel` references the vocabulary and writes the terms. |
| Restier API / OData controller | `[McpTask]` on the CLR method (Core). Pair `[McpElicitUrl("connectUri")]` to stamp `ElicitUrlProperties`. `UseODataMcp` stamps the finished `EdmModel` (`McpTaskAttributeAnnotations.Apply`) by matching method name and bound/unbound shape. Functions are not tasked. |

Canonical CSDL lives in Core (`Microsoft.OData.Mcp.V1.xml`). `CsdlParser` reads it from remote `$metadata`. AspNetCore `McpTaskAnnotations.EnsureReferenced` adds `edmx:Reference` + Include alias `Mcp` so GET `{prefix}/$metadata` and the MCP `odata://{prefix}/$metadata` resource carry the same document.

Optional term `Microsoft.OData.Mcp.V1.ElicitUrlProperties` (collection of strings) names JSON properties on the action result that are URLs to elicit. Default when omitted: `connectUri`, `url`, `authorizationUrl`, `verificationUri`. Hosts stamp it with `[McpElicitUrl("connectUri")]` / `HasMcpElicitUrl("connectUri")`. `[McpTask("connectUri")]` / `HasMcpTask("connectUri")` remain a convenience that stamps both terms. Same URL-only rule as [AUTHENTICATION.md](./AUTHENTICATION.md): `ElicitationId` is a new GUID; never tokens, `connect_code`, or vault ids in the elicitation payload or message.

Unannotated `Authorize`, or an annotated action called by a client that did not advertise URL elicitation, is one-shot: Started ticket with `connectUri`. The operator tells the assistant when they have finished. Do not wait in-process for that client. A client that advertised `elicitation.url` gets MRTR URL elicitation even without the Tasks extension.

### 5.3 Where the task store lives

`IMcpTaskStore` is an MCP protocol store: `taskId`, status, TTL, `inputRequests`, terminal `CallToolResult`. It is **not** a column on `IntegrationConnection` and **not** a table of grant rows.

| Store | Owns |
|-------|------|
| `IMcpTaskStore` | Ephemeral task handles (minutes). Singleton under stateless HTTP so `tasks/get` on a later POST finds the id. `InMemoryMcpTaskStore` for a single process. A host that scales out supplies a shared implementation (SDK: same instance or external storage; `CreateTaskAsync` must be visible to the next `GetTaskAsync` before return). |
| Host session (e.g. Connected Accounts TCS) | The operator-wait that the callback completes. Already in-memory; same single-process rule. |
| `IntegrationConnection` | Domain: `DateAuthorized`, `ConnectionHealthTypeId`, `SecretsProtected`, `SettingsJson`. |

Do not add `McpTaskId` (or a serialized store) to `IntegrationConnections`. A connection outlives many Authorize attempts. Tasks expire. Unbound annotated actions have no connection row. Assistants must not poll the entity as the completion channel.

SEP-2663: register the store as a **singleton**. Stateless Streamable HTTP creates a fresh server per POST; a scoped store looks empty on `tasks/get`.

### 5.4 Builder: `AddODataMcp`, not `UseODataMcp`

`AddODataMcp` is where `services.AddMcpServer()` runs (`IMcpServerBuilder`). Task method handlers, capability advertisement, and the annotated-action call filter have to be registered there. `UseODataMcp` only maps `{prefix}/mcp` via internal `MapMcp`. It cannot call SDK builder methods after `WebApplication.Build()`.

```csharp
builder.Services.AddODataMcp(options =>
{
    options.Catalog.InstructionsPreface = "…";
}, mcp =>
{
    mcp.WithTasks(new InMemoryMcpTaskStore());
    mcp.WaitForAnnotatedAction<HostAuthorizeWaiter>();
});

app.MapRestier(/* … */);
app.UseODataMcp();
```

Keep today’s overloads. Add an optional `Action<ODataMcpServerBuilder> configureServer` (null = no Tasks). `AddODataMcp` still returns `IServiceCollection`.

`ODataMcpServerBuilder` (AspNetCore):

| Member | Role |
|--------|------|
| `IMcpServerBuilder Server` | The SDK builder already used for `WithHttpTransport` + `WithODataCatalogHandlers`. Hosts may attach logging filters. They must **not** call SDK `WithTasks` on it. |
| `WithTasks(IMcpTaskStore store)` | Our method. Registers `store` as singleton `IMcpTaskStore`. Advertises `io.modelcontextprotocol/tasks` on server capabilities. Wires `tasks/get`, `tasks/update`, `tasks/cancel` from `ModelContextProtocol.Extensions.Tasks`. Installs OData’s annotated-action alternate `tools/call` path (§5.5). PackageReference `ModelContextProtocol.Extensions.Tasks` on the AspNetCore host. |
| `WaitForAnnotatedAction<TWaiter>()` where `TWaiter : IMcpAnnotatedActionWaiter` | Registers the host waiter as singleton. Required for Succeeded after elicitation. Without it, the task completes with the Started payload after elicitation (URL was shown; grant still finishes only in the browser). |

`UseODataMcp()` stays parameterless. Do not add `UseODataMcp(mcp => mcp.WithTasks(…))`; that callback would run too late.

Tools host: named action tools still generate; Tasks wrapping is AspNetCore-only (in-process callback). Remote `odata-mcp` stays one-shot.

### 5.5 Runtime (annotated action, client opted in)

1. Resolve the tool. If any §5.2 gate fails, `CallToolAsync` as today.
2. `CreateTaskAsync` on the store **before** the JSON-RPC result. Return `CreateTaskResult`: new `taskId`, `status: working`, `createdAt` / `lastUpdatedAt` UTC, `ttlMs` from `MaxOperationWait` (or ticket `expiresIn` once known), `pollIntervalMs` default 1000, optional `statusMessage` from the action description.
3. Run the inner `CallOperationAsync` on a background scope (new DI scope; `McpHttpContextAccessor.Start` / `End` as today). First result is the Started JSON.
4. If the JSON has an elicit URL property, `server.ElicitAsync` with `mode: url`, that URL, message = ticket `message` when present. Inside a task scope the SDK surfaces this as `input_required` / `inputRequests` instead of a server-initiated JSON-RPC request.
5. After accept: `IMcpAnnotatedActionWaiter.WaitAsync`. After decline: complete with Failed ticket / `isError`. After timeout or `tasks/cancel`: cancel the waiter token; complete with Failed or `cancelled`.
6. Waiter returns the terminal `CallToolResult` (Succeeded / Failed ticket). `SetCompletedAsync`. No waiter: `SetCompletedAsync` with the Started payload.

```csharp
public interface IMcpAnnotatedActionWaiter
{
    /// <param name="context">Action name, binding key, one-shot result JSON, task id.</param>
    /// <param name="cancellationToken">Timeout, <c>tasks/cancel</c>, and process shutdown.</param>
    /// <returns>The terminal tool result, or <see langword="null"/> to keep the Started payload.</returns>
    Task<CallToolResult?> WaitAsync(McpAnnotatedActionContext context, CancellationToken cancellationToken);
}
```

`IODataExecutor` stays one-shot. Do not add `IAsyncEnumerable` / `StreamResults` / `IMcpOperationStream`. Do not hold the original `tools/call` POST.

### 5.6 What the AspNetCore host must not do

- Call SDK `WithTasks` (wraps every attributed tool; OData tools are not those).
- Wrap unannotated actions, functions, or CRUD.
- Open `HttpClient` to `{prefix}/mcp`.
- Flip `Stateless = false`.
- Persist `IMcpTaskStore` or `taskId` on domain entity tables.
- Put tokens or vault ids in elicitation Content, `statusMessage`, or tool `structuredContent`.
- Require the assistant to `GET` the binding entity as the completion signal.
- Elicit, wait, or poll when the client omitted URL elicitation. That client gets Started and the operator says they are done.

---

## 6. Host checklist once Tasks wrap an annotated action

1. Keep the action returning a single ticket JSON. First HTTP response is still Started + `connectUri`. Do not change it to `IAsyncEnumerable`.
2. Stamp `Microsoft.OData.Mcp.V1.Task` with `[McpTask]` (Restier / controllers) or `HasMcpTask()` (convention). Stamp `ElicitUrlProperties` with `[McpElicitUrl("connectUri")]` or `HasMcpElicitUrl("connectUri")` when the default list is too wide. Confirm GET `{prefix}/$metadata` contains the `edmx:Reference`, `Term="Mcp.Task"`, and the nested `ElicitUrlProperties` collection on that action.
3. `AddODataMcp(..., mcp => { mcp.WithTasks(new InMemoryMcpTaskStore()); mcp.WaitForAnnotatedAction<HostAuthorizeWaiter>(); })`.
4. Waiter: given the Started ticket, wait on the host completion source (same token as session `expiresIn`). Return Succeeded (`message`: the grant completed) or Failed.
5. Single-process: in-memory store + in-memory session. Refuse scale-out while both are in-memory.

No HTML bootstrap. The browser is only the OAuth user-agent. `action_integrationconnection_authorize` and `odata_call` `Authorize` both hit §5.5.

Claude Code: Tasks are public beta (v2.1.304+, protocol 2026-07-28, interactive CLI / Agent SDK / GitHub Actions). URL elicitation is interactive-terminal only; a non-interactive run that hits `input_required` asks the server to cancel.

---

## 7. Implementation order

One builder, one invoke path. Tests first in each PR (MSTest v3, Breakdance, FluentAssertions, no mocks). Update [TOOL-SURFACE.md](./TOOL-SURFACE.md) §3, [OPTIMIZATION.md](./OPTIMIZATION.md) §1, [TOOL-TEST-MANIFEST.md](./TOOL-TEST-MANIFEST.md), [PROTOCOL.md](./PROTOCOL.md) §5 and §11, and [ARCHITECTURE.md](./ARCHITECTURE.md) §4.1 in the same PRs as the code they describe.

### PR 1 — Named `action_*` / `function_*` tools (catalog + one-shot invoke) — done (§4)

**Files:** `ODataMcpCatalog.cs`, `ODataMcpCatalogOptions.cs`, `ODataToolDescriptor.cs`, `ODataMcpCatalogConstants.cs`, `ODataToolRuntime.cs` (`InvokeNamedAsync`), `ODataMcpInstructions.cs`, `NamedOperationToolTests`, TripPin/Restier host tests.

**Behavior:** Generate and dispatch named tools. Execution is one-shot. Annotated Tasks are PR 2.

**Tests:** TripPin names in §4.1; `tools/list` order; `odata_call` still works; no `action_*` collision with CRUD; `IncludeActions=false`; `ExcludeEntitySets`; paging still includes generics.

### PR 2 — Tasks infrastructure + annotation gate — done

**Files:** Core `EdmAction.IsTask`, `McpTaskAttribute`, embedded `Microsoft.OData.Mcp.V1.xml`; AspNetCore `HasMcpTask`, `McpTaskAttributeAnnotations`, `EdmModelAdapter` copy; `ODataMcpServerBuilder`; `AddODataMcp` `configureServer`; `WithTasks(IMcpTaskStore)` calling SDK `WithTasks` with `ExecutionModeSelector` Optional only for annotated actions; `ODataMcpHandlers` catalog `CallToolHandler` plus `AfterInvoke`; `ODataMcpCatalogOptions.MaxOperationWait`; Restier fixture `[McpTask]` on `Share`.

**Behavior:** Opt-in client + annotated action → `CreateTaskResult`. Unannotated / no opt-in → one-shot. `tasks/get` finds the id on a later POST (singleton store). CRUD never becomes a task. GET `$metadata` exports `edmx:Reference` + `Mcp.Task` so external clients parse the same term.

**Tests:** Restier `[McpTask]` on `Share` (`action_mcpcustomer_share` is a task; `Reset` is not); convention `HasMcpTask`; HTTP `$metadata` and session CSDL contain `McpVocabulary.ReferenceUri` and `Term="Mcp.Task"`. JSON-RPC Streamable HTTP: first `tools/call` result has `resultType=task`; `tasks/get` is `working` then `completed` with the inner OData JSON when no waiter is registered. Same action without the term stays one-shot even if the client sends the extension. `list_*` / `odata_query` stay one-shot. Missing URL elicitation + annotated action is one-shot Started with no elicitation and no waiter. Missing Tasks + URL elicitation is MRTR `InputRequiredResult`, not a task handle. `UseODataMcp` does not grow a `WithTasks` overload.

### PR 3 — URL elicitation inside the task + host waiter — done

**Files:** Core `McpElicitUrlAttribute`; AspNetCore `HasMcpElicitUrl` / `McpElicitUrlTermConfiguration`, `ODataMcpAnnotatedActionTasks` (`AfterInvokeAsync`, `FindElicitUrl`, AUTH-12), `IMcpAnnotatedActionWaiter`, `McpAnnotatedActionContext`, `WaitForAnnotatedAction<T>`; Restier fixture `Authorize` + `[McpElicitUrl("connectUri")]` + `McpAuthorizationTicket` + `RestierAuthorizeWaiter`; `RestierMcpElicitationTests`.

**Behavior:** `tasks/get` → `input_required` with `elicitation/create`. Accept → waiter → terminal ticket. Decline → Failed. `tasks/cancel` / timeout cancel the waiter token.

**Tests:** Fixture yields connectUri; client capability includes URL elicitation; `tasks/get` carries `inputRequests`; after `tasks/update` accept the waiter’s Succeeded JSON is the completed `CallToolResult`. Decline does not call the waiter. Tokens absent from elicitation params.

---

## 8. Acceptance

Catalog:

- `tools/list` includes `action_integrationconnection_authorize` when the model declares bound `Authorize` on `IntegrationConnection`.
- TripPin lists `action_resetdatasource` and `action_person_sharetrip`. `function_getnearestairport` is present when functions are included.
- Unbound named tools appear after the generics and before any bound `action_*` / `function_*` (and therefore before the per-set CRUD families).
- `odata_call` `Authorize` / `ShareTrip` / `ResetDataSource` still succeed.
- `IncludeActions=false` removes named action tools only.

Tasks (in-process, annotated):

- Client with tasks extension + annotated `Authorize`: `tools/call` returns `resultType=task`; `tasks/get` shows `input_required` with URL elicitation for `connectUri` before the operator finishes the picker.
- After callback (waiter) the completed task result is the Succeeded/Failed ticket, not the Started ticket.
- Client with URL elicitation and no Tasks extension: `tools/call` returns MRTR `InputRequiredResult` (`elicitation/create` `mode: url`). Retry with `inputResponses` completes the waiter. No `CreateTaskResult`.
- Client without URL elicitation, or action without the term: one-shot Started ticket. Zero elicitation, zero waiter. The operator tells the assistant when they are done.
- `odata_query` / `get_*` / unannotated `ShareTrip` never create tasks.
- Network: original `tools/call` POST ends after `CreateTaskResult`. Later POSTs are `tasks/get` / `tasks/update` / `tasks/cancel`. Zero extra `text/event-stream` routes. `Stateless` remains `true`.
- `tasks/cancel` and wait timeout cancel the waiter. Domain Failed tickets are `completed` + `isError`.

---

## 9. Out of scope

| Item | Why |
|------|------|
| SEP-2575 `notifications/tasks` | Not in the C# SDK. Poll `tasks/get`. |
| SDK `WithTasks` auto-wrap of all tools | Would task CRUD. OData tools are handlers, not `[McpServerTool]`. |
| `IMcpTaskStore` / `taskId` on `IntegrationConnection` | Protocol store vs domain row. Tasks expire; connections do not. |
| Holding `tools/call` open / `IAsyncEnumerable` / `StreamResults` | Replaced by Tasks. |
| Elicitation / waiter on clients that omit URL elicitation | Started ticket; the human tells the assistant they are done. |
| `subscriptions/listen` | Change notifications, not this wait. |
| OData `respond-async` / 202 + monitor URL | A second HTTP resource. |
| Named tools for operation overloads past the first | `odata_call` remains. |
| Tools-host (remote) Tasks | No in-process callback. Named tools still generate. |
| Flipping Streamable HTTP to stateful | Store singleton is enough. |
