# Inventory — Concept vs deletion

**Rule:** concepts are sacred. Every type below may be deleted and rewritten. This table tells agents what *job* to keep and what *not* to port.

Legend: **REWRITE** = job survives under new types · **DELETE** = do not port · **HOST** = job moves to a host package.

---

## Core — Models (`src/Microsoft.OData.Mcp.Core/Models`)

These types **are** the projection. Do not create a second `Edm/` namespace. Edit in place so they stay aligned with `IEdmModel` (declared properties only, keys, navs, operations, documentation).

| Type | Fate | Job |
|------|------|-----|
| `EdmModel` | KEEP / EVOLVE | Root schema — same role as `IEdmModel` |
| `EdmEntityType` | KEEP / EVOLVE | Declared structural + nav properties, keys |
| `EdmComplexType` | KEEP / EVOLVE | Declared properties |
| `EdmEntityContainer` | KEEP / EVOLVE | Container |
| `EdmEntitySet` | KEEP / EVOLVE | Entity sets |
| `EdmSingleton` | KEEP / EVOLVE | Singletons |
| `EdmProperty` | KEEP / EVOLVE | Declared structural properties + docs |
| `EdmNavigationProperty` | KEEP / EVOLVE | Declared navigations |
| `EdmNavigationPropertyBinding` | KEEP / EVOLVE | Bindings |
| `EdmReferentialConstraint` | KEEP / EVOLVE | FKs (describe only) |
| `EdmFunction` / `EdmAction` | EVOLVE | Must actually be parsed this time |
| `EdmFunctionImport` / `EdmActionImport` | EVOLVE | Imports |
| `EdmParameter` | EVOLVE | Operation params |
| `EdmPrimitiveType` | DELETE if still unreferenced | Use EDM type names on `EdmProperty.Type` |

---

## Core — Parsing

| Type | Fate | Job |
|------|------|-----|
| `ICsdlMetadataParser` | EVOLVE | Keep `ParseFromString` / `ParseFromStream` / `ParseFromFile` |
| `CsdlParser` | EVOLVE | Same methods; fill functions/actions/docs; declared members only |

---

## Core — Tools (current)

| Type | Fate | Job |
|------|------|-----|
| `IMcpToolFactory` / `McpToolFactory` | REWRITE | Becomes one catalog builder (tools **and** resources) |
| `McpToolDefinition` / `McpToolContext` / `McpToolResult` | REWRITE | SDK 2 tool registration + handler |
| `McpToolGenerationOptions` | REWRITE | Cap, include/exclude, verbs — not a mode enum war |
| `McpToolOperationType` / `McpToolExample*` | REWRITE or fold | Verb + examples |
| `ODataMcpTools` | DELETE class; **keep generic semantics** | Generic CRUD/query |
| `DynamicODataMcpTools` | DELETE class; **keep discover/describe semantics** | Discovery tools |
| `WithODataTools()` assembly scan | DELETE | Dual pipeline |

---

## Core — Legacy

| Type | Fate |
|------|------|
| `Legacy/McpTool` and `Legacy/Generators/*` | DELETE |
| Naming-convention idea (snake_case default) | Keep snake_case only unless a host option is proven |
| Property-level exclusion | REWRITE into builder (binary/stream + explicit excludes) |

---

## Core — Routing (custom REST)

| Type | Fate |
|------|------|
| `McpCommand`, `McpEndpointRegistry`, `McpRouteEntry`, `McpRouteMatcher`, `SpanRouteParser`, `ODataRouteOptionsResolver`, `IMcpEndpointRegistry` | DELETE (wrong protocol) |
| Fast path matching | Not needed; SDK owns HTTP |

---

## Core — Configuration

| Type | Fate |
|------|------|
| `ODataServiceConfiguration` BaseUrl, MetadataPath, RequestTimeout, Authentication Type/Bearer/ApiKey/Basic | REWRITE into a small options type |
| `ODataAuthenticationType`, `BasicAuthenticationCredentials` | REWRITE |
| `CachingConfiguration` and children | DELETE until used |
| `SecurityConfiguration` and children (headers, IP, rate limit, data protection, input validation) | DELETE |
| `MonitoringConfiguration` and children | DELETE |
| `NetworkConfiguration` and children | DELETE |
| `FeatureFlagsConfiguration` | DELETE |
| `McpServerInfo` / `BuildInfo` | Fold into SDK `serverInfo` |
| `McpServerConfiguration` as a kitchen sink | DELETE |
| `ODataMcpOptions` dead properties | DELETE; keep ExcludeRoutes, include/exclude sets, max named tools |

---

## Core — Other

| Type | Fate |
|------|------|
| `JsonConstants` | REWRITE as source-gen + shared options |
| `AddODataMcpCore` / `AddODataHttpClient` | REWRITE (no Authentication project ref) |
| `DynamicModelRefreshService` | DELETE stub; refresh is a later phase |
| `ODataMcpOptions` in Core | Move AspNetCore-only flags to AspNetCore |

---

## Tools host

| Type | Fate |
|------|------|
| `StartCommand` stdio + metadata fetch + SDK server | REWRITE against SDK 2 + AOT + one catalog |
| `shutdown_server` | **KEEP as spec** (new implementation) |
| `AddCommand` Claude `/mcp add` wizard | REWRITE if still useful after SDK 2 |
| `ODataMcpRootCommand` / `Program` | REWRITE |
| `DynamicToolGeneratorService` | DELETE (re-logger) |
| README `--port` / `test` claims | Implement `test`; HTTP optional |

---

## AspNetCore

| Type | Fate |
|------|------|
| `AddODataMcp()` | REWRITE — all routes |
| `.WithMcp()` | **NEW** — one route |
| Internal `MapMcp("{prefix}/mcp")` | USE SDK |
| `ODataMcpMiddleware` custom REST | DELETE |
| `ODataMcpRouteConvention` 501s | DELETE |
| `AddMcp` no-op | DELETE |
| `DEPRECATE_ServiceCollectionExtensions` | DELETE |
| `UseODataMcp` | DELETE (convention + `MapControllers` is enough) |
| Health checks that lie | DELETE |
| `IEdmModel` adapter | **NEW** |
| In-app `IOdataExecutor` | **NEW** |

---

## Authentication

| Type | Fate |
|------|------|
| Outbound Bearer/API key/Basic on HttpClient | REWRITE in Core executor / Tools host |
| `TokenValidationService` | HOST — optional AspNetCore inbound |
| `ITokenDelegationService` | DELETE until implemented |
| Scope matrices, cert stores, token exchange graphs | DELETE until a path needs them |
| `ClaimsPrincipalExtensions` | Keep if inbound JWT ships |

---

## Tests and docs

| Asset | Fate |
|-------|------|
| `CsdlParserTests`, factory characterization, binary exclusion | Port ideas onto new types; hit **live** Northwind/TripPin |
| AspNetCore tests expecting 200 on REST `/mcp` | DELETE; replace with SDK client + `MapControllers` |
| `TestHttpMessageHandler` | DELETE — violates no-mocks |
| Generated API docs for deleted types | Regenerated from new public surface only |

---

## Do not port

Custom REST protocol, dual tool registration, `shutdown_server` on HTTP MCP, `IOdataSchema`, EdmLib in Core, floating `0.*-*`, config empires, 501 conventions, Legacy generators, `queryCustomers`.
