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
| `UseODataMcp` | KEEP — pipeline activation after OData routes are mapped |
| Health checks that lie | DELETE |
| `IEdmModel` adapter | **NEW** |
| In-app `IOdataExecutor` | **NEW** |
| `AddODataProtectedResource()` | **NEW — landed.** One line that publishes RFC 9728 protected resource metadata for every discovered OData prefix and annotates the `401` challenge with `resource_metadata`. Independent of `AddODataMcp`. See [AUTHENTICATION.md](./AUTHENTICATION.md) §Zero-config protected resource. |
| `Authentication/ODataProtectedResourceOptions` | **NEW — landed.** Authorization servers, scopes, display metadata, optional explicit prefixes, `AnnotateChallenges`, and a `Validate()` the startup filter calls. |
| `Authentication/ODataProtectedResourceMiddleware` | **NEW — landed.** Serves both well-known forms anonymously, 405s other verbs, passes an unknown prefix through, and rewrites the `Bearer` challenge on a `401` beneath a covered prefix. Lazy, locked, cached prefix discovery via `ODataMcpRouteDiscovery`. |
| `Authentication/ODataProtectedResourceStartupFilter` | **NEW — landed.** Validates at startup and puts the middleware at the front of the pipeline, which is what keeps the document readable without a token. |
| `Authentication/ODataProtectedResourceJsonContext` | **NEW — landed.** Source-generated writer for the SDK `ProtectedResourceMetadata`; no reflection JSON in this package. |
| `Constants/ProtectedResourceConstants` | **NEW — landed.** `WellKnownPath`, `BearerScheme`, `ResourceMetadataParameter`, `ScopeParameter`, and the served content type / cache header. Deliberately duplicated rather than referencing `Microsoft.OData.Mcp.Authentication`. |

---

## Authentication

| Type | Fate |
|------|------|
| `Microsoft.OData.Mcp.Authentication.Outbound`: `ODataMcpAuthConstants`, `OAuthChallenge`, `WwwAuthenticateParser`, `ProtectedResourceMetadataClient`, `AuthorizationServerMetadata` (+ `AuthorizationServerMetadataClient`), `OAuthDiscovery` (+ `OAuthDiscoveryResult`), `OutboundDiscoveryHttp`, `OutboundDiscoveryException`, `OAuthErrorPayload`, `DeviceAuthorizationResponse`, `TokenEndpointResponse`, `OutboundOAuthJsonContext`, `OutboundAuthLogRedactor`, `LatchkeyTokenCache`, `OutboundOAuthOptions`, `OutboundGrantKind` (+ `OutboundGrantKindParser`), `OutboundConsentRequest`, `OAuthConsentRequiredException`, `TokenEndpointClient`, `OAuthTokenException`, `DeviceCodeGrant`, `RefreshTokenGrant`, `GrantSelector`, `ScopeResolver`, `OutboundOAuthClient` (+ `OutboundTokenResult`), `ODataOutboundAuthHandler`, `LoopbackAuthorizationCallback`, `AuthorizationCodePkceGrant`, `ClientCredentialsGrant`, `DynamicClientRegistrar` (+ `DynamicClientRegistrationRequest`) | **KEEP — landed.** Discovery, grants, cache, and the outbound handler on the named `"OData"` client. See [AUTHENTICATION.md](./AUTHENTICATION.md). |
| Core `ODataExecuteResult.WwwAuthenticate` (raw challenge strings) | **KEEP — landed.** No OAuth parsing in Core; `RemoteODataExecutor` does not retry. |
| Tools: `OutboundOptionsBinder`, `StdioConsentPresenter`, `ToolsMcpSessionHolder` | **KEEP — landed.** CLI flag binding onto `OutboundOAuthOptions`, stderr/browser consent presenter, and the pre-`Build()` session holder `ToolsMcpHost.CreateAsync` assigns after metadata parse. |
| `Microsoft.OData.Mcp.Tests.Shared.Authentication` (local authorization server + protected-resource fixtures, no OData package), `Microsoft.OData.Mcp.Tests.Authentication` (outbound unit tests + secured rich convention API, OData 8 only) | **KEEP — landed.** See [TESTING.md](./TESTING.md) §8. |
| `Microsoft.OData.Mcp.Tests.Authentication.Restier` (secured Restier API, OData 7 only) | **KEEP — landed.** In `Microsoft.OData.Mcp.slnx`; linked `RestierToolTestBase`-derived suites per [TESTING.md](./TESTING.md) §8.2. |
| `TokenValidationService`, `ITokenValidationService`, `McpAuthenticationOptions`, `JwtBearerOptions` (ours) | DELETE |
| `ITokenDelegationService`, `DelegatedToken`, `TokenForwardingStrategy`, `TokenDelegationOptions`, `TokenExchangeOptions` | DELETE |
| `ClientCredentials`, `ClientCertificate`, `CertificateSource`, `ClientAuthenticationMethod` | DELETE |
| `AuthorizationMetadata`, `UserContext`, `EntityScopeRequirements`, `ScopeAuthorizationOptions`, `ScopeEnforcementBehavior`, `TargetServiceOptions`, `RetryPolicyOptions`, `BackoffStrategy` | DELETE |
| `ClaimsPrincipalExtensions` | DELETE |

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
