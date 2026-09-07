# OData 7 / 8 Compatibility — Endpoint Routing

**Status:** Living (authoritative)  
**Revised:** 2026-09-06  
**Applies to:** `Microsoft.OData.Mcp.AspNetCore`

---

## 1. Problem

Core already avoids OData-library lock-in: it ships a custom EDM and CSDL parser so Tools never takes `Microsoft.OData.Edm` or `Microsoft.OData.Core`.

The ASP.NET Core host made the same class of mistake if it `PackageReference`s **Microsoft.AspNetCore.OData 8.\***. That forces OData 8 into every process that calls `AddODataMcp()`. Restier.AspNetCore (and any OData 7 app) loads `Microsoft.AspNet.OData.ODataController`. Those two controller types cannot exist in one process.

That is a **host discovery** problem, not an `IEdmModel` problem, and not a reason to tell Restier to “move to 8.”

---

## 2. What is actually incompatible

| Surface | OData 7 (Restier today) | OData 8 |
|---------|-------------------------|---------|
| Package ID | `Microsoft.AspNetCore.OData` 7.\* | Same ID, 8.\* |
| Public namespace | `Microsoft.AspNet.OData` | `Microsoft.AspNetCore.OData` |
| Controller | `Microsoft.AspNet.OData.ODataController` | `Microsoft.AspNetCore.OData.Routing.Controllers.ODataController` |
| Options type | `Microsoft.AspNet.OData.ODataOptions` | `Microsoft.AspNetCore.OData.ODataOptions` |
| Model registration | `MapODataRoute` / Restier `MapApiRoute` | `ODataOptions.AddRouteComponents` / `RouteComponents` |

A NuGet range `[7, 9)` on **Microsoft.AspNetCore.OData** is not sufficient. Restore picks one major version; the types the other major version needs are not there.

---

## 3. What is *not* incompatible

**`Microsoft.OData.Edm.IEdmModel` is shared.** Both AspNetCore.OData 7.x and 8.x depend on EdmLib 7.x (`Microsoft.OData.Edm` / `Microsoft.OData.Core` in the `[7.x, 8.0)` band). The adapter maps **that** interface into Core’s model.

`GetEdmVersion`, schema elements, entity sets, navigations, functions, actions, and vocabulary annotations used by the adapter are EdmLib APIs. They do **not** justify an OData 7 vs 8 product split.

`IOdataExecutor` for the in-app host is HTTP under the route prefix. It does not need OData 7 or 8 types.

---

## 4. Why `ODataOptions` is not required

`ODataOptions.RouteComponents` is OData **8**’s private registry of `(prefix, IEdmModel)`. MCP only needs those two facts. OData 7 never had that dictionary.

Both stacks already write OData services onto **ASP.NET Core endpoint routing**:

| Stack | How the prefix appears | Where `IEdmModel` lives |
|-------|------------------------|-------------------------|
| **OData 8** | Conventional endpoints in `EndpointDataSource` | Endpoint metadata: `IODataRoutingMetadata.Prefix` and `.Model` |
| **OData 7** | `IEndpointRouteBuilder.MapODataRoute(routeName, routePrefix, model)` | Per-route container keyed by route name |
| **Restier (OData 7)** | `MapRestier` → `MapApiRoute` → `MapODataServiceRoute` | Same per-route container. Pattern: `{routePrefix}/{**ODataEndpointPath_{routeName}}` via `MapDynamicControllerRoute` |

Restier Breakdance already supports `useEndpointRouting: true`. Endpoint routing is the common table. `ODataOptions` is not.

---

## 5. Host rules

1. **`Microsoft.OData.Mcp.AspNetCore` must not `PackageReference` `Microsoft.AspNetCore.OData`.** Not 7, not 8, not a range. That package ID is the lock-in.
2. **EdmLib is allowed only in the adapter** (`Microsoft.OData.Edm` in the 7.x band) to copy **declared** `IEdmModel` members into Core. Do not take `Microsoft.OData.Core` payload APIs.
3. **Discover services from `EndpointDataSource`**, after routing is built (startup filter / hosted service that runs once endpoints exist). Do not read `IOptions<ODataOptions>`.
4. **Do not compile against `IODataRoutingMetadata`, `IPerRouteContainer`, `ODataRoute`, or `ODataController`.** Those types live in version-specific assemblies. Duck-type: a metadata object with a string prefix and an `IEdmModel`; a catch-all route template `{prefix}/{**ODataEndpointPath_*}`; a per-route container resolved from DI by name if present. If duck-typing a member requires an OData 7/8 type, stop and take prefix + model from an explicit registration API instead.
5. **One MCP endpoint per discovered prefix:** internal `MapMcp("{prefix}/mcp")`. Developers still never call `MapMcp`.
6. **In-process execution** remains HTTP to `{prefix}/…` on the same app (loopback / TestServer handler). Preserve `Authorization`. No OData 7/8 types on that path.

If duck-typed discovery cannot see a Restier or OData 7 model without taking their types, the host must accept an explicit `(prefix, IEdmModel)` registration. Discovery from the routing table is the default when metadata is present; explicit registration is the escape hatch. Both are version-agnostic.

---

## 6. Public API (unchanged intent)

| Call | Meaning |
|------|---------|
| `AddODataMcp()` | Register MCP services for **every** OData prefix discovered on the endpoint data source (and any explicitly registered). |
| `UseODataMcp()` | Map official MCP at `{prefix}/mcp` after OData routes exist. |
| Prefix opt-in / exclude | `IncludePrefixes` / `ExcludeRoutes` on host options. Works for OData 7 and 8. |
| Mutual exclusion | All-routes vs opt-in list still throw if both are used. |

Fluent `ODataOptions.WithMcp()` after `AddRouteComponents` is **OData 8 syntactic sugar**. It must **not** live in a package that `PackageReference`s OData 8 if Restier apps reference that same package. Version-neutral opt-in is include/exclude prefixes. An OData 8-only `WithMcp()` helper is allowed only as an optional extra that Restier apps never load.

XML docs must state:

- `AddODataMcp()` registers services. `UseODataMcp()` discovers OData routes from ASP.NET Core endpoint routing (OData 7 `MapODataRoute` / Restier `MapApiRoute` and OData 8 conventional endpoints) and maps MCP.
- Prefer `AddODataMcp()` + `UseODataMcp()` unless a prefix must stay hidden; then use include/exclude prefixes.
- Do not reference `Microsoft.AspNetCore.OData` from this package.

---

## 7. What the host does *not* do

- Re-parse `$metadata` when the app already has `IEdmModel`.
- Re-evaluate `$filter`.
- Load both OData 7 and OData 8 hosting assemblies.
- Require Restier (or any app) to move to OData 8.

---

## 8. Package graph (AspNetCore)

```
Microsoft.OData.Mcp.AspNetCore
  → Core
  → ModelContextProtocol.AspNetCore 2.x
  → Microsoft.OData.Edm 7.x     (adapter only; IEdmModel)
  → Microsoft.AspNetCore.App    (EndpointDataSource, routing, HTTP)
  ✗ Microsoft.AspNetCore.OData  (neither 7 nor 8)

App with OData 8     → Microsoft.AspNetCore.OData 8.*  (app’s choice)
App with Restier     → Microsoft.AspNetCore.OData 7.*  (app’s choice)
```

The app brings 7 or 8. The host reads the routing table and `IEdmModel`. MCP and Core stay on our types.
